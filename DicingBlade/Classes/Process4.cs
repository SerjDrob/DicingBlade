//#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using DicingBlade.Properties;
using Microsoft.VisualStudio.Workspace;
using netDxf;
using PropertyChanged;

namespace DicingBlade.Classes
{
    [AddINotifyPropertyChangedInterface]
    internal class Process4 : IMessager, IDisposable
    {
        public static int ProcCount;
        private readonly Blade _blade;
        private readonly IMachine _machine;

        private readonly Wafer2D _wafer;

/*
        private Diagram[] _baseProcess;
*/
        private Condition _bladeInWaferCond = new();
        private readonly double _bladeTransferGapZ = 2;
        private CancellationTokenSource _cancellationToken;
        private PauseTokenSource _pauseToken;
        private bool _cancelProcess;
        private CheckCutControl _checkCut;

        private readonly Condition _correctSelCondition = new();
        private readonly Condition _cutInspectCondition = new();
        private Condition _inspectSelCondition = new();
        private readonly Condition _learningCondition = new();
        private readonly Sequence _rootSequence = new();
        private readonly Condition _rotateLearningCondition = new(true);
        private readonly Condition _sideDoneCondition = new();
        private readonly Leaf _singleCutLeaf;
        private Selector _learningSelector = new();
        private readonly Condition _workingCondition = new();
        private Selector _workingSelector = new();
        private readonly Sequence _workingSequence = new();

        private double _feedSpeed;
        private double _inspectX;
        private double _lastCutY;
        private bool _learningDone;
        private bool _learningNextDir = true;
        private readonly List<Task> _localTasks = new();
        private bool _machineVacuumSensor;
        private bool _pauseProcess;
        private ProcParams _procParamsEventArgs;
        private bool _spindleWorking;
        private TempWafer2D _tempWafer2D;
        private double _uActual;
        private double _undercut;

        private double _xActual;
        private double _yActual;
        private double _zActual;
        public ChangeScreens ChangeScreensEvent;
        public GetRotation GetRotationEvent;
        public event Action<bool> BladeTracingEvent;
        public event Action<string> OnProcessStatusChanged;
        public event Action OnControlPointAppeared;
        public event Action<string, int> ThrowMessage;
        public Process4(IMachine machine, Wafer2D wafer, Blade blade, ITechnology technology,
            Diagram[] proc) // В конструкторе происходит загрузка технологических параметров
        {
            blade.Thickness = 0.11;
            blade.Diameter = 55.6;

            _machine = machine ?? throw new ProcessException("Не выбрана установка для процесса");
            _wafer = wafer ?? throw new ProcessException("Не выбрана подложка для процесса");
            _blade = blade ?? throw new ProcessException("Не выбран диск для процесса");
            RefreshTechnology(technology);
            //_machine.OnSensorStateChanged += Machine_OnAirWanished;
            //_machine.OnSensorStateChanged += Machine_OnCoolWaterWanished;
            //_machine.OnSensorStateChanged += Machine_OnSpinWaterWanished;
            //_machine.OnSensorStateChanged += Machine_OnVacuumWanished;
            _machine.OnSensorStateChanged += _machine_OnSensorStateChanged;
            _machine.OnAxisMotionStateChanged += _machine_OnAxisMotionStateChanged;
            _machine.OnSpindleStateChanging += _machine_OnSpindleStateChanging;

            _machine.SwitchOnValve(Valves.ChuckVacuum);
            Task.Delay(500).Wait();
            if (!_machineVacuumSensor)
                throw new ProcessException(
                    "Отсутствует вакуум на столике. Возможно не установлена рамка или неисправна вакуумная система");

            if (!_spindleWorking)
            {
                //  throw new ProcessException("Не включен шпиндель");
            }

            CancelProcess = false;

            _checkCut.Set(technology.StartControlNum, technology.ControlPeriod);

            ProcessStatus = Status.StartLearning;

            var singleCutCondition = new Condition(true);
            var singleCutSequence = new Sequence()
                .Hire(new Leaf(GetFunc(Diagram.GoTransferingHeightZ)))
                .Hire(new Leaf(GetFunc(Diagram.GoCurPositionCutXy)))
                .Hire(new Leaf(GetFunc(Diagram.GoNextDepthZ)))
                .Hire(new Leaf(GetFunc(Diagram.CuttingX)))
                //.Hire(new Leaf(GetFunc(Diagram.GoTransferingHeightZ)))
                .Hire(new Leaf(GetFunc(Diagram.GoCameraPointXyz)))
                .Hire(new Leaf(async () => { singleCutCondition.SetState(false); }));

            var singleCutTicker = new Ticker(singleCutSequence, singleCutCondition);
            _singleCutLeaf = new Leaf(async () => { singleCutCondition.SetState(true); }, singleCutTicker);

            _workingCondition = new Condition(true);
            var inspectLeaf = new Leaf(TakeThePhotoAsync); //.SetBlock(_cutInspectCondition);
            var nextSideLeaf = new Leaf(GetFunc(Diagram.GoNextDirection)).SetBlock(_sideDoneCondition);
            var inspectSelector = new Selector()
                .Hire(inspectLeaf).SetBlock(_cutInspectCondition)
                .Hire(new Leaf(CorrectionAsync)).SetBlock(_correctSelCondition);

            _workingSequence
                .Hire(new Leaf(SetProcessStatus))
                .Hire(new Leaf(GetFunc(Diagram.GoNextCutXy)))
                .Hire(new Leaf(GetFunc(Diagram.GoNextDepthZ)))
                .Hire(new Leaf(GetFunc(Diagram.CuttingX), inspectSelector))
                .Hire(new Leaf(IncrementLine))
                .Hire(new Leaf(GetFunc(Diagram.GoTransferingHeightZ)))
                .Hire(nextSideLeaf);

            var learningSequence = new Sequence();
            _learningCondition = new Condition(true);

            var rotationSelector = new Selector();
            var rotationLeaf = new Leaf(IncrementDir,
                    new Leaf(GetFunc(Status.MovingNextDir), new Leaf(GetFunc(Status.StartLearning))))
                .SetBlock(_rotateLearningCondition);

            learningSequence
                .Hire(new Leaf(GetFunc(Status.Learning), rotationLeaf))
                .SetBlock(_learningCondition);

            var workingTicker = new Ticker(_workingSequence, _workingCondition);
            _rootSequence
                .Hire(new Leaf(GetFunc(Status.StartLearning)))
                .Hire(learningSequence)
                .Hire(workingTicker)
                .Hire(new Leaf(EndProcess));

            rotationLeaf.CheckMyCondition += SetConditionsStates;
            learningSequence.CheckMyCondition += SetConditionsStates;
            nextSideLeaf.CheckMyCondition += SetConditionsStates;
            rotationSelector.CheckMyCondition += SetConditionsStates;
            inspectLeaf.CheckMyCondition += SetConditionsStates;
            workingTicker.CheckMyCondition += SetConditionsStates;
            inspectSelector.CheckMyCondition += SetConditionsStates;

            _inspectX = _machine.GetGeometry(Place.CameraChuckCenter, Ax.X);
            _learningDone = false;
            _rootSequence.DoWork();
            ProcCount++;
        }


        public Visibility CutWidthMarkerVisibility { get; set; } = Visibility.Hidden;
        public Status ProcessStatus { get; private set; }
        public double CutOffset { get; set; }
        private bool IsCutting { get; set; }

        public bool PauseProcess
        {
            get => _pauseProcess;
            set
            {
                _pauseProcess = value;
                if (_pauseToken != null)
                {
                    if (value)
                        _pauseToken.Pause();
                    else _pauseToken.Resume();
                }
            }
        }

        public bool CancelProcess
        {
            set
            {
                _cancelProcess = value;
                if (_cancellationToken != null)
                    if (value)
                        _cancellationToken.Cancel();
            }
        }

        public bool SideDone { get; private set; }

        private bool BladeInWafer =>
            _zActual > _machine.GetGeometry(Place.ZBladeTouch, Ax.Z) - _wafer.Thickness - _bladeTransferGapZ;

        public int CurrentLine { get; private set; }
        public bool Rotation { get; set; }

        public void Dispose()
        {
            _cancellationToken?.Dispose();
        }

        public void RefreshTechnology(ITechnology technology)
        {
            _feedSpeed = technology.FeedSpeed;
            _undercut = technology.UnterCut;
        }

        private async Task EndProcess()
        {
            await _machine.MoveGpInPlaceAsync(Groups.Xy, Place.Loading);
            ProcessStatus = Status.Done;
            ThrowMessage?.Invoke("Всё!", 0);
        }

        private async Task SetProcessStatus()
        {
            ProcessStatus = Status.Working;
            OnProcessStatusChanged?.Invoke("Работа");
        }

        private void SetConditionsStates()
        {
            _rotateLearningCondition.SetState(_learningNextDir);
            _learningCondition.SetState(!_learningDone);
            _cutInspectCondition.SetState(_checkCut.Check & !_correctSelCondition.State);
            _workingCondition.SetState(ProcessStatus != Status.Ending);
            _sideDoneCondition.SetState(SideDone);
        }

        public void SubstrateChanged(object substrate)
        {
            if (substrate is IWafer2 & (int)(ProcessStatus & (Status.Working | Status.Correcting | Status.MovingNextDir | Status.Ending))==0)
            {
                var wf = (IWafer) substrate;
                _wafer.SetChanges(wf.IndexH,wf.IndexW,wf.Thickness,new Rectangle2D(wf.Width,wf.Height));   
            }
        }
        private async Task IncrementDir()
        {
            if (CurrentDirection < _wafer.SidesCount - 1)
            {
                _wafer.SetSide(++CurrentDirection);
                if (_wafer.SidesCount - 1 == _wafer.CurrentSide) _learningNextDir = false;
            }
            else
            {
                _wafer.SetSide(CurrentDirection = 0);
            }

            _procParamsEventArgs.CurrentSide = CurrentDirection;
            OnProcParamsChanged?.Invoke(this,_procParamsEventArgs);
        }

        private async Task IncrementLine()
        {
            if (CurrentLine != _wafer.CurrentLinesCount)
            {
                CurrentLine++;
            }
            else
            {
                CurrentLine = 0;
                if (_wafer.CurrentSide != 0)
                {
                    SideDone = true;
                    _wafer.SetSide(_wafer.CurrentSide - 1);
                }
                else
                {
                    ProcessStatus = Status.Ending;
                }
            }
        }

        private async Task AwaitTaskAsync(Task task)
        {
            _localTasks.Add(task);
            await task.ConfigureAwait(true);
        }

        public async Task WaitProcDoneAsync()
        {
            await Task.WhenAll(_localTasks);
        }

        private Func<Task> GetFunc(Diagram element)
        {
            var x = new double();
            var y = new double();
            var z = new double();
            var u = new double();
            var xCurLineEnd = _wafer[CurrentLine].End.X; //_wafer.GetCurrentLine(CurrentLine).end.X;

            return element switch
            {
                Diagram.GoWaferStartX => async () =>
                {
                    var xCurLineEnd = _wafer[CurrentLine].End.X;
                    if (BladeInWafer) ;
                    _machine.SetVelocity(Velocity.Service);
                    x = _machine.TranslateSpecCoor(Place.BladeChuckCenter, -xCurLineEnd, 0);
                    var xGap = _blade.XGap(_wafer.Thickness);
                    await AwaitTaskAsync(_machine.MoveAxInPosAsync(Ax.X, x + xGap));
                },


                Diagram.GoWaferEndX => async () =>
                {
                    var xCurLineEnd = _wafer[CurrentLine].End.X;
                    _machine.SetVelocity(Velocity.Service);
                    x = _machine.TranslateSpecCoor(Place.BladeChuckCenter, -xCurLineEnd, 0);
                    await AwaitTaskAsync(_machine.MoveAxInPosAsync(Ax.X, x));
                },

                Diagram.GoNextDepthZ => async () =>
                {
                    _machine.SetVelocity(Velocity.Service);
                    z = _machine.TranslateSpecCoor(Place.ZBladeTouch, _wafer[_zRatio] + _undercut, Ax.Z);
                    await AwaitTaskAsync(_machine.MoveAxInPosAsync(Ax.Z, z));
                },

                Diagram.CuttingX => async () =>
                {
                    _machine.SwitchOnValve(Valves.Coolant);
                    await AwaitTaskAsync(Task.Delay(300));
                    _machine.SetAxFeedSpeed(Ax.X, _feedSpeed);
                    IsCutting = true;

                    xCurLineEnd = _wafer[CurrentLine].End.X;
                    x = _machine.TranslateSpecCoor(Place.BladeChuckCenter, -xCurLineEnd, 0);

                    BladeTracingEvent(true);
                    await AwaitTaskAsync(_machine.MoveAxInPosAsync(Ax.X, x));
                    BladeTracingEvent(false);
                    IsCutting = false;
                    _lastCutY = _yActual;
                    _checkCut.AddToCurrentCut();
                    _machine.SwitchOffValve(Valves.Coolant);
                },


                Diagram.GoCameraPointXyz => async () =>
                {
                    _machine.SetVelocity(Velocity.Service);
                    z = _machine.TranslateSpecCoor(Place.ZBladeTouch, _wafer.Thickness + _bladeTransferGapZ, Ax.Z);
                    await AwaitTaskAsync(_machine.MoveAxInPosAsync(Ax.Z, z));

                    // x = _machine.GetGeometry(Place.CameraChuckCenter, Ax.X);
                    // y = _wafer[CurrentLine != 0 ? CurrentLine : 0].Start.Y - _wafer.CurrentShift;// - _machine.GetFeature(MFeatures.CameraBladeOffset);
                    y = -_machine.TranslateSpecCoor(Place.BladeChuckCenter, _yActual, Ax.Y);
                    y = _machine.TranslateSpecCoor(Place.CameraChuckCenter, -y, Ax.Y);
                    await AwaitTaskAsync(_machine.MoveGpInPosAsync(Groups.Xy, new[] {_inspectX, y}, true));
                    await AwaitTaskAsync(_machine.MoveAxesInPlaceAsync(Place.ZFocus));
                },

                Diagram.GoOnWaferRightX => async () =>
                {
                    if (BladeInWafer) ;
                    _machine.SetVelocity(Velocity.Service);
                    x = _wafer.GetNearestCut(_yActual - _machine.GetGeometry(Place.CameraChuckCenter, Ax.Y)).End.X;
                    await AwaitTaskAsync(_machine.MoveAxInPosAsync(Ax.X, x));
                },

                Diagram.GoOnWaferLeftX => async () =>
                {
                    if (BladeInWafer) ;
                    _machine.SetVelocity(Velocity.Service);
                    x = _wafer.GetNearestCut(_yActual - _machine.GetGeometry(Place.CameraChuckCenter, Ax.Y)).Start
                        .X;
                    await AwaitTaskAsync(_machine.MoveAxInPosAsync(Ax.X, x));
                },

                Diagram.GoWaferCenterXy => async () =>
                {
                    if (BladeInWafer) ;
                    _machine.SetVelocity(Velocity.Service);
                    await AwaitTaskAsync(_machine.MoveGpInPlaceAsync(Groups.Xy, Place.CameraChuckCenter));
                },

                Diagram.GoNextCutY => async () =>
                {
                    if (BladeInWafer) ;
                    _machine.SetVelocity(Velocity.Service);
                    y = _machine.TranslateSpecCoor(Place.BladeChuckCenter, _wafer[CurrentLine].Start.Y, Ax.Y);
                    await AwaitTaskAsync(_machine.MoveAxInPosAsync(Ax.Y, y));
                },

                Diagram.GoNextCutXy => async () =>
                {
                    // if (BladeInWafer) ;
                    _machine.SetVelocity(Velocity.Service);
                    x = _wafer[CurrentLine].Start.X;
                    x = x + Math.Sign(x) * _blade.XGap(_wafer.Thickness);
                    y = _wafer[CurrentLine].Start.Y - _wafer.CurrentShift;
                    var arr = _machine.TranslateActualCoors(Place.BladeChuckCenter,
                        new[] {(Ax.X, -x), (Ax.Y, -y)});
                    var xy = new[] {arr.GetVal(Ax.X), arr.GetVal(Ax.Y)};
                    await AwaitTaskAsync(_machine.MoveGpInPosAsync(Groups.Xy, xy, true));
                },

                Diagram.GoCurPositionCutXy => async () =>
                {
                    // if (BladeInWafer) ;
                    _machine.SetVelocity(Velocity.Service);
                    x = _wafer[CurrentLine].Start.X;
                    x = x + Math.Sign(x) * _blade.XGap(_wafer.Thickness);
                    y = -_machine.TranslateSpecCoor(Place.CameraChuckCenter, _yActual, Ax.Y);
                    //y = _wafer[CurrentLine].Start.Y - _wafer.CurrentShift;
                    var arr = _machine.TranslateActualCoors(Place.BladeChuckCenter,
                        new[] {(Ax.X, -x), (Ax.Y, -y)});
                    var xy = new[] {arr.GetVal(Ax.X), arr.GetVal(Ax.Y)};
                    await AwaitTaskAsync(_machine.MoveGpInPosAsync(Groups.Xy, xy, true));
                },

                Diagram.GoTransferingHeightZ => async () =>
                {
                    _machine.SetVelocity(Velocity.Service);
                    await AwaitTaskAsync(_machine.MoveAxInPosAsync(Ax.Z,
                        _machine.GetFeature(MFeatures.ZBladeTouch) - _wafer.Thickness - _bladeTransferGapZ));
                },

                Diagram.GoDockHeightZ => async () =>
                {
                    _machine.SetVelocity(Velocity.Service);
                    await AwaitTaskAsync(_machine.MoveAxInPosAsync(Ax.Z, 1));
                },

                Diagram.GoNextDirection => async () =>
                {
                    _machine.SetVelocity(Velocity.Service);
                    await AwaitTaskAsync(MoveNextDirAsync());

                    _procParamsEventArgs.CurrentShift = _wafer.CurrentShift;
                    OnProcParamsChanged?.Invoke(this, _procParamsEventArgs);

                    SideDone = false;
                    CurrentLine = 0;
                },

                Diagram.GoCameraPointLearningXyz => async () =>
                {
                    _machine.SetVelocity(Velocity.Service);
                    await AwaitTaskAsync(_machine.MoveAxInPosAsync(Ax.Z,
                        _machine.GetFeature(MFeatures.ZBladeTouch) - _wafer.Thickness - _bladeTransferGapZ));
                    y = _wafer.GetNearestCut(0).Start.Y;
                    var arr = _machine.TranslateActualCoors(Place.CameraChuckCenter,
                        new (Ax, double)[] {(Ax.X, 0), (Ax.Y, -y)});
                    var point = new[] {arr.GetVal(Ax.X), arr.GetVal(Ax.Y)};
                    await AwaitTaskAsync(_machine.MoveGpInPosAsync(Groups.Xy, point));
                    await AwaitTaskAsync(_machine.MoveAxesInPlaceAsync(Place.ZFocus));
                },

                _ => async () => { }
            };
        }

        private Func<Task> GetFunc(Status element)
        {
            return element switch
            {
                Status.StartLearning => async () =>
                {
                    _machine.SetVelocity(Velocity.Service);
                    await AwaitTaskAsync(_machine.MoveAxInPosAsync(Ax.Z,
                        _machine.GetFeature(MFeatures.ZBladeTouch) - _wafer.Thickness - _bladeTransferGapZ));
                    var y = _wafer.GetNearestCut(0).Start.Y;
                    var arr = _machine.TranslateActualCoors(Place.CameraChuckCenter,
                        new (Ax, double)[] {(Ax.X, 0), (Ax.Y, -y)});
                    //_machine.MoveAxInPosAsync(Ax.Y, arr.GetVal(Ax.Y));
                    //_machine.MoveAxInPosAsync(Ax.X, arr.GetVal(Ax.X));
                    var point = new[] {arr.GetVal(Ax.X), arr.GetVal(Ax.Y)};
                    await AwaitTaskAsync(_machine.MoveGpInPosAsync(Groups.Xy, point));
                    await AwaitTaskAsync(_machine.MoveAxesInPlaceAsync(Place.ZFocus));
                    ProcessStatus = Status.Learning;
                },

                Status.Learning => async () =>
                {
                    var y = _machine.TranslateActualCoors(Place.CameraChuckCenter, Ax.Y);
                    _wafer.TeachSideShift(y);

                    _procParamsEventArgs.CurrentShift = _wafer.CurrentShift;
                    OnProcParamsChanged?.Invoke(this, _procParamsEventArgs);

                    _wafer.TeachSideAngle(_uActual);
                    if (!_learningNextDir) _learningDone = true;
                },

                Status.MovingNextDir => async () =>
                {
                    _machine.SetVelocity(Velocity.Service);
                    await AwaitTaskAsync(MoveNextDirAsync(false));

                    _procParamsEventArgs.CurrentShift = _wafer.CurrentShift;
                    OnProcParamsChanged?.Invoke(this, _procParamsEventArgs);
                },

                Status.Working => async () =>
                {
                    PauseProcess = true;
                    if (PauseProcess) AwaitTaskAsync(PauseScenarioAsync());
                    CutWidthMarkerVisibility = Visibility.Visible;
                    ChangeScreensEvent?.Invoke(true);
                    ProcessStatus = Status.Correcting;
                    _machine.StartVideoCapture(0);
                },

                Status.Correcting => async () =>
                {
                    var result = MessageBox.Show($"Сместить следующие резы на {CutOffset:N3} мм?", "",
                        MessageBoxButton.OKCancel);
                    if (result == MessageBoxResult.OK) _wafer.AddToSideShift(CutOffset);
                    ChangeScreensEvent?.Invoke(false);
                    ProcessStatus = Status.Working;
                    CutWidthMarkerVisibility = Visibility.Hidden;
                    CutOffset = 0;
                    PauseProcess = false;
                    _machine.FreezeVideoCapture();
                },

                _ =>
                    async () => { }
            };
        }

        public async Task AlignWafer()
        {
            //if (_learningCondition.State)
            {
                if (!_tempWafer2D.FirstPointSet)
                {
                    _tempWafer2D.Point1 = new[] {_xActual, _yActual};
                    _tempWafer2D.FirstPointSet = true;
                    ThrowMessage?.Invoke("Выберете второй ориентир для выравнивания и нажмите _", 1);
                }
                else
                {
                    _tempWafer2D.Point2 = new[] {_xActual, _yActual};
                    _machine.SetVelocity(Velocity.Service);
                    var angle = _tempWafer2D.GetAngle();
                    await AwaitTaskAsync(_machine.MoveAxInPosAsync(Ax.U, _uActual - angle));
                    var rotation = new RotateTransform(-angle);
                    rotation.CenterX = _machine.GetGeometry(Place.CameraChuckCenter, Ax.X);
                    rotation.CenterY = _machine.GetGeometry(Place.CameraChuckCenter, Ax.Y);
                    var point = rotation.Transform(new Point(_tempWafer2D.Point2[0],
                        _tempWafer2D.Point2[1]));
                    await AwaitTaskAsync(_machine.MoveAxInPosAsync(Ax.Y, point.Y));
                    _tempWafer2D.FirstPointSet = false;
                    ThrowMessage("", 1);
                }
            }
        }

        private void _machine_OnSpindleStateChanging(int frequency, double current, bool working)
        {
            _spindleWorking = working;
            switch (ProcessStatus)
            {
                case Status.None:
                    break;
                case Status.StartLearning:
                    break;
                case Status.Learning:
                    break;
                case Status.Working when IsCutting & !_spindleWorking:
                    // ThrowMessage("Пластине кранты!",0);
                    break;
                case Status.Correcting:
                    break;
                case Status.Done:
                    break;
            }
        }

        private void _machine_OnSensorStateChanged(Sensors sensor, bool state)
        {
            switch (sensor)
            {
                case Sensors.ChuckVacuum:
                    _machineVacuumSensor = state;
                    break;
                case Sensors.Air:
                    break;
                case Sensors.Coolant:
                    break;
                case Sensors.SpindleCoolant:
                    break;
            }
        }

        private void _machine_OnAxisMotionStateChanged(Ax axis, double position, bool nLmt, bool pLmt, bool motionDone,
            bool motionStart)
        {
            switch (axis)
            {
                case Ax.X:
                    _xActual = position;
                    break;
                case Ax.Y:
                    _yActual = position;
                    break;
                case Ax.Z:
                    _zActual = position;
                    break;
                case Ax.U:
                    _uActual = position;
                    break;
                case Ax.All:
                    break;
            }
        }

        public void TeachDiskShift()
        {
            Settings.Default.DiskShift = _lastCutY - _yActual;
            Settings.Default.Save();
        }

        public async Task DoSingleCut()
        {
            await AwaitTaskAsync(_singleCutLeaf.DoWork());
        }
        
        public async Task PauseScenarioAsync()
        {
            await AwaitTaskAsync(_machine.WaitUntilAxisStopAsync(Ax.X));
            //Machine.EmgStop();
            await AwaitTaskAsync(ProcElementDispatcherAsync(Diagram.GoCameraPointXyz));
        }

        private async Task MoveNextDirAsync(bool next = true)
        {
            //if (!next || !_wafer.NextDir(true))
            {
                var angle = new double();
                double time = 0;
                var deltaAngle = _wafer.CurrentSideAngle - _wafer.PrevSideAngle;
                if (Math.Abs(_wafer.CurrentSideActualAngle - _wafer.CurrentSideAngle) < 0.001)
                {
                    angle = _wafer.PrevSideActualAngle - _wafer.PrevSideAngle + _wafer.CurrentSideAngle;
                    time = Math.Abs(angle - _uActual) / _machine.GetAxisSetVelocity(Ax.U);
                }
                else
                {
                    angle = _wafer.CurrentSideActualAngle;
                    time = Math.Abs(_wafer.CurrentSideActualAngle - _uActual) / _machine.GetAxisSetVelocity(Ax.U);
                }

                Rotation = true;
                GetRotationEvent?.Invoke(deltaAngle, time);
                await _machine.MoveAxInPosAsync(Ax.U, angle);
                Rotation = false;
            }
        }

        private async Task ProcElementDispatcherAsync(Diagram element)
        {
            #region MyRegion

            // проверка перед каждым действием. асинхронные действия await()!!!
            // паузы, корректировки.
            //if(pauseToken.Equals(default)) await pauseToken.Token.WaitWhilePausedAsync();

            #endregion

            Vector2 target;
            var x = new double();
            var y = new double();
            var z = new double();
            var u = new double();

            var xCurLineEnd = _wafer[CurrentLine].End.X;
            switch (element)
            {
                case Diagram.GoCameraPointXyz:
                    _machine.SetVelocity(Velocity.Service);
                    z = _machine.TranslateSpecCoor(Place.ZBladeTouch, _wafer.Thickness + _bladeTransferGapZ, Ax.Z);
                    await _machine.MoveAxInPosAsync(Ax.Z, z);

                    x = _machine.GetGeometry(Place.CameraChuckCenter, Ax.X);
                    y = _wafer[CurrentLine != 0 ? CurrentLine - 1 : 0].Start.Y -
                        _machine.GetFeature(MFeatures.CameraBladeOffset);
                    y = _machine.TranslateSpecCoor(Place.BladeChuckCenter, -y, Ax.Y);
                    await _machine.MoveGpInPosAsync(Groups.Xy, new[] {x, y}, true);


                    await _machine.MoveAxInPosAsync(Ax.Z, _machine.GetFeature(MFeatures.CameraFocus));
                    break;

                case Diagram.GoTransferingHeightZ:
                    _machine.SetVelocity(Velocity.Service);
                    await _machine.MoveAxInPosAsync(Ax.Z,
                        _machine.GetFeature(MFeatures.ZBladeTouch) - _wafer.Thickness - _bladeTransferGapZ);
                    break;
            }
        }

        private async Task TakeThePhotoAsync()
        {
            _machine.StartVideoCapture(0);
            await ProcElementDispatcherAsync(Diagram.GoTransferingHeightZ);
            await Task.Run(GetFunc(Diagram.GoCameraPointXyz));
            _machine.SwitchOnValve(Valves.Blowing);
            await Task.Delay(100).ConfigureAwait(false);
            _machine.FreezeVideoCapture();
            _machine.SwitchOffValve(Valves.Blowing);
            OnControlPointAppeared?.Invoke();
        }

        private async Task CorrectionAsync()
        {
            await ProcElementDispatcherAsync(Diagram.GoTransferingHeightZ);
            await Task.Run(GetFunc(Diagram.GoCameraPointXyz));
            _machine.SwitchOnValve(Valves.Blowing);
            await Task.Delay(100).ConfigureAwait(false);
            _machine.SwitchOffValve(Valves.Blowing);

            CutWidthMarkerVisibility = Visibility.Visible;
            ChangeScreensEvent?.Invoke(true);
            ProcessStatus = Status.Correcting;
            _machine.StartVideoCapture(0);
            await Task.Run(() =>
            {
                while (ProcessStatus == Status.Correcting) ;
            });
            _inspectX = _xActual;
            OnControlPointAppeared();
        }

        public async Task StartPauseProc()
        {
            if ((ProcessStatus != Status.Working) & (ProcessStatus != Status.Correcting))
            {
                /*await*/
                _rootSequence.DoWork();
            }
            else
            {
                if (_correctSelCondition.State)
                {
                    var result = MessageBox.Show($"Сместить следующие резы на {CutOffset} мм?", "",
                        MessageBoxButton.OKCancel);
                    if (result == MessageBoxResult.OK) _wafer.AddToSideShift(CutOffset);
                    ChangeScreensEvent?.Invoke(false);
                    CutWidthMarkerVisibility = Visibility.Hidden;
                    CutOffset = 0;
                    PauseProcess = false;
                    _machine.FreezeVideoCapture();
                    _correctSelCondition.SetState(false);
                    OnProcessStatusChanged?.Invoke("Работа");
                    ProcessStatus = Status.Working;
                }
                else
                {
                    _correctSelCondition.SetState(true);
                    OnProcessStatusChanged?.Invoke("Пауза");
                    ProcessStatus = Status.Correcting;
                }
            }
        }

        private void Machine_OnVacuumWanished(Sensors sensor, bool state)
        {
            if ((sensor == Sensors.ChuckVacuum) & !state)
            {
                switch (ProcessStatus)
                {
                    case Status.StartLearning:
                        ThrowMessage?.Invoke("Вакуум исчез!!!", 0);
                        ProcessStatus = Status.None;
                        break;
                    case Status.Learning:
                        break;
                    case Status.Working:
                        break;
                    case Status.Correcting:
                        break;
                    case Status.Done:
                        break;
                }

                if (IsCutting)
                {
                }
            }
        }

        ~Process4()
        {
            Dispose();
        }

        public event Action<object, ProcParams> OnProcParamsChanged;

        #region NewFields

        public int CurrentDirection { get; private set; }
        private readonly double _zRatio = 0;

        #endregion
    }
}