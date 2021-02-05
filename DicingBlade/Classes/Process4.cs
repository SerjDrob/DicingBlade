
//#nullable enable

using Microsoft.VisualStudio.Workspace;
using netDxf;
using netDxf.Entities;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace DicingBlade.Classes
{
    [AddINotifyPropertyChangedInterface]
    internal class Process4 : IMessager, IDisposable
    {
        public Process4(IMachine machine, Wafer2D wafer, Blade blade, ITechnology technology, Diagram[] proc) // В конструкторе происходит загрузка технологических параметров
        {

            _machine = machine ?? throw new ProcessException("Не выбрана установка для процесса"); ;
            _wafer = wafer ?? throw new ProcessException("Не выбрана подложка для процесса");
            _blade = blade ?? throw new ProcessException("Не выбран диск для процесса");
            _feedSpeed = technology.FeedSpeed;
            //_machine.OnSensorStateChanged += Machine_OnAirWanished;
            //_machine.OnSensorStateChanged += Machine_OnCoolWaterWanished;
            //_machine.OnSensorStateChanged += Machine_OnSpinWaterWanished;
            //_machine.OnSensorStateChanged += Machine_OnVacuumWanished;
            _machine.OnSensorStateChanged     += _machine_OnSensorStateChanged;
            _machine.OnAxisMotionStateChanged += _machine_OnAxisMotionStateChanged;
            _machine.OnSpindleStateChanging   += _machine_OnSpindleStateChanging;

            _machine.SwitchOnValve(Valves.ChuckVacuum);
            Task.Delay(500).Wait();
            if (!_machineVacuumSensor)
            {
                throw new ProcessException("Отсутствует вакуум на столике. Возможно не установлена рамка или неисправна вакуумная система");
            }
            if (!_spindleWorking)
            {
                //  throw new ProcessException("Не включен шпиндель");
            }
            
            _baseProcess = proc;
            CancelProcess = false;            

            _checkCut.Set(technology.StartControlNum, technology.ControlPeriod);
            
            ProcessStatus = Status.StartLearning;

            _workingCondition = new(true);
            var InspectLeaf = new Leaf(TakeThePhotoAsync).SetBlock(_cutInspectCondition);
            var nextSideLeaf = new Leaf(GetFunc(Diagram.GoNextDirection)).SetBlock(_sideDoneCondition);

            _workingSequence
                .Hire(new Leaf(GetFunc(Diagram.GoNextCutXy)))
                .Hire(new Leaf(GetFunc(Diagram.GoNextDepthZ)))
                .Hire(new Leaf(GetFunc(Diagram.CuttingX), InspectLeaf))
                .Hire(new Leaf(IncrementLine))
                .Hire(new Leaf(GetFunc(Diagram.GoTransferingHeightZ)))
                .Hire(nextSideLeaf);

            _learningSequence = new();
            _learningCondition = new(true);

            var rotationSelector = new Selector();
            var rotationLeaf = new Leaf(IncrementDir,new Leaf(GetFunc(Status.MovingNextDir), new Leaf(GetFunc(Status.StartLearning))))
                                        .SetBlock(_learningCondition);

            _learningSequence                
                .Hire(new Leaf(GetFunc(Status.Learning), rotationLeaf))
                .SetBlock(_learningCondition);

            var workingTicker = new Ticker(_workingSequence, _workingCondition);
            _rootSequence
                .Hire(new Leaf(GetFunc(Status.StartLearning)))
                .Hire(_learningSequence)
                .Hire(workingTicker);

            rotationLeaf.CheckMyCondition      += SetConditionsStates;
            _learningSequence.CheckMyCondition += SetConditionsStates;
            nextSideLeaf.CheckMyCondition      += SetConditionsStates;
            rotationSelector.CheckMyCondition  += SetConditionsStates;
            InspectLeaf.CheckMyCondition       += SetConditionsStates;
            workingTicker.CheckMyCondition     += SetConditionsStates;

            _rootSequence.DoWork();

        }

        private void SetConditionsStates()
        {
            _learningCondition.SetState(_learningNextDir);
            _cutInspectCondition.SetState(_checkCut.addToCurrentCut());
            _workingCondition.SetState(!(SideCounter == _wafer.SidesCount));
            _sideDoneCondition.SetState(SideDone);
        }
        private async Task IncrementDir()
        {
            if (_currentDirection < _wafer.SidesCount - 1)
            {
                _wafer.SetSide(++_currentDirection);
                if (_wafer.SidesCount-1 == _wafer.CurrentSide)
                {
                    _learningNextDir = false;
                }
            }
            else
            {
                _wafer.SetSide(_currentDirection = 0);
            }
        }

        private async Task IncrementLine()
        {
            if (CurrentLine != _wafer.CurrentLinesCount)
            {
                CurrentLine++;
            }
        }
        #region NewFields
        private int _currentDirection;
        private double _zRatio = 1;
        #endregion

        private bool _learningNextDir          = true;

        private Condition _bladeInWaferCond    = new();
        private Condition _learningCondition   = new();
        private Sequence _workingSequence      = new();
        private Selector _learningSelector     = new();
        private Selector _workingSelector      = new();
        private Condition _workingCondition    = new();
        private Condition _cutInspectCondition = new();
        private Condition _sideDoneCondition   = new(false);
        private Sequence _rootSequence         = new();
        private Sequence _learningSequence;
        private TempWafer2D _tempWafer2D;
        private Func<Task> GetFunc(Diagram element)
        {
            var x = new double();
            var y = new double();
            var z = new double();
            var u = new double();
            var xCurLineEnd = _wafer[CurrentLine].End.X;//_wafer.GetCurrentLine(CurrentLine).end.X;

            return element switch
            {
                Diagram.GoWaferStartX => new Func<Task>(async () =>
                {
                    var xCurLineEnd = _wafer[CurrentLine].End.X;
                    if (BladeInWafer) ;
                    _machine.SetVelocity(Velocity.Service);
                    x = _machine.TranslateSpecCoor(Place.BladeChuckCenter, -xCurLineEnd, 0);
                    double xGap = _blade.XGap(_wafer.Thickness);                    
                    await _machine.MoveAxInPosAsync(Ax.X, x + xGap);
                })
                ,


                Diagram.GoWaferEndX => new Func<Task>(
                     async () =>
                     {
                         var xCurLineEnd = _wafer[CurrentLine].End.X;
                         _machine.SetVelocity(Velocity.Service);
                         x = _machine.TranslateSpecCoor(Place.BladeChuckCenter, -xCurLineEnd, 0);
                         await _machine.MoveAxInPosAsync(Ax.X, x);
                     })
                ,

                Diagram.GoNextDepthZ => new Func<Task>(
                    async () =>
                    {
                        _machine.SetVelocity(Velocity.Service);                        
                        z = _machine.TranslateSpecCoor(Place.ZBladeTouch, _wafer[_zRatio], Ax.Z);
                        await _machine.MoveAxInPosAsync(Ax.Z, z);
                    })
                ,

                Diagram.CuttingX => new Func<Task>(
                    async () =>
                    {
                        _machine.SwitchOnValve(Valves.Coolant);
                        await Task.Delay(300).ConfigureAwait(false);
                        _machine.SetAxFeedSpeed(Ax.X, _feedSpeed);
                        IsCutting = true;

                        xCurLineEnd = _wafer[CurrentLine].End.X;
                        x = _machine.TranslateSpecCoor(Place.BladeChuckCenter, -xCurLineEnd, 0);

                        BladeTracingEvent(true);
                        await _machine.MoveAxInPosAsync(Ax.X, x);
                        BladeTracingEvent(false);
                        IsCutting = false;


                        _machine.SwitchOffValve(Valves.Coolant);

                        //if (!_wafer.CurrentCutIncrement(CurrentLine))
                        //{
                        //    NextLine();
                        //}

                    })
                ,


                Diagram.GoCameraPointXyz => new Func<Task>(
                    async () =>
                    {
                        _machine.SetVelocity(Velocity.Service);
                        z = _machine.TranslateSpecCoor(Place.ZBladeTouch, _wafer.Thickness + _bladeTransferGapZ, Ax.Z);
                        await _machine.MoveAxInPosAsync(Ax.Z, z);

                        x = _machine.GetGeometry(Place.CameraChuckCenter, Ax.X);
                        y = _wafer[CurrentLine != 0 ? CurrentLine - 1 : 0].Start.Y - _machine.GetFeature(MFeatures.CameraBladeOffset);
                        y = _machine.TranslateSpecCoor(Place.BladeChuckCenter, -y, Ax.Y);
                        await _machine.MoveGpInPosAsync(Groups.XY, new double[] { x, y }, true);
                        await _machine.MoveAxesInPlaceAsync(Place.ZFocus);
                    })
                ,

                Diagram.GoOnWaferRightX => new Func<Task>(
                    async () =>
                    {
                        if (BladeInWafer) ;
                        _machine.SetVelocity(Velocity.Service);
                        x = _wafer.GetNearestCut(_yActual - _machine.GetGeometry(Place.CameraChuckCenter, Ax.Y)).End.X;
                        await _machine.MoveAxInPosAsync(Ax.X, x);
                    })
                ,

                Diagram.GoOnWaferLeftX => new Func<Task>(
                    async () =>
                    {
                        if (BladeInWafer) ;
                        _machine.SetVelocity(Velocity.Service);
                        x = _wafer.GetNearestCut(_yActual - _machine.GetGeometry(Place.CameraChuckCenter, Ax.Y)).Start.X;
                        await _machine.MoveAxInPosAsync(Ax.X, x);
                    })
                ,

                Diagram.GoWaferCenterXy => new Func<Task>(
                    async () =>
                    {
                        if (BladeInWafer) ;
                        _machine.SetVelocity(Velocity.Service);
                        await _machine.MoveGpInPlaceAsync(Groups.XY, Place.CameraChuckCenter);
                    })
                ,

                Diagram.GoNextCutY => new Func<Task>(
                    async () =>
                    {
                        if (BladeInWafer) ;
                        _machine.SetVelocity(Velocity.Service);
                        y = _machine.TranslateSpecCoor(Place.BladeChuckCenter, _wafer[CurrentLine].Start.Y, Ax.Y);
                        await _machine.MoveAxInPosAsync(Ax.Y, y);
                    })
                ,

                Diagram.GoNextCutXy => new Func<Task>(
                    async () =>
                    {
                        // if (BladeInWafer) ;
                        _machine.SetVelocity(Velocity.Service);
                        x = _wafer[CurrentLine].Start.X;
                        y = _wafer[CurrentLine].Start.Y;
                        var arr = _machine.TranslateActualCoors(Place.BladeChuckCenter, new (Ax, double)[] { (Ax.X, -x), (Ax.Y, -y) });
                        var xy = new double[] { arr.GetVal(Ax.X), arr.GetVal(Ax.Y) };
                        await _machine.MoveGpInPosAsync(Groups.XY, xy, true);
                    })
                ,

                Diagram.GoTransferingHeightZ => new Func<Task>(
                    async () =>
                    {
                        _machine.SetVelocity(Velocity.Service);
                        await _machine.MoveAxInPosAsync(Ax.Z, _machine.GetFeature(MFeatures.ZBladeTouch) - _wafer.Thickness - _bladeTransferGapZ);
                    })
                ,

                Diagram.GoDockHeightZ => new Func<Task>(
                    async () =>
                    {
                        _machine.SetVelocity(Velocity.Service);
                        await _machine.MoveAxInPosAsync(Ax.Z, 1);
                    })
                ,

                Diagram.GoNextDirection => new Func<Task>(
                    async () =>
                    {
                        _machine.SetVelocity(Velocity.Service);
                        await MoveNextDirAsync();
                        SideDone = false;
                        CurrentLine = 0;
                        //SideCounter++;                       
                    })
                ,

                Diagram.GoCameraPointLearningXyz => new Func<Task>(
                    async () =>
                    {
                        _machine.SetVelocity(Velocity.Service);
                        await _machine.MoveAxInPosAsync(Ax.Z, _machine.GetFeature(MFeatures.ZBladeTouch) - _wafer.Thickness - _bladeTransferGapZ);
                        y = _wafer.GetNearestCut(0).Start.Y;
                        var arr = _machine.TranslateActualCoors(Place.CameraChuckCenter, new (Ax, double)[] { (Ax.X, 0), (Ax.Y, -y) });
                        var point = new double[] { arr.GetVal(Ax.X), arr.GetVal(Ax.Y) };
                        await _machine.MoveGpInPosAsync(Groups.XY, point);
                        await _machine.MoveAxesInPlaceAsync(Place.ZFocus);

                    })
                ,

                _ => new Func<Task>(
                         async () => { })

            };
        }
        private Func<Task> GetFunc(Status element)
        {
            return element switch
            {
                Status.StartLearning => new Func<Task>(
                    async () =>
                    {
                        _machine.SetVelocity(Velocity.Service);
                        await _machine.MoveAxInPosAsync(Ax.Z, _machine.GetFeature(MFeatures.ZBladeTouch) - _wafer.Thickness - _bladeTransferGapZ);
                        var y = _wafer.GetNearestCut(0).Start.Y;
                        var arr = _machine.TranslateActualCoors(Place.CameraChuckCenter, new (Ax, double)[] { (Ax.X, 0), (Ax.Y, -y) });

                        //_machine.MoveAxInPosAsync(Ax.Y, arr.GetVal(Ax.Y));
                        //_machine.MoveAxInPosAsync(Ax.X, arr.GetVal(Ax.X));
                        var point = new double[] { arr.GetVal(Ax.X), arr.GetVal(Ax.Y) };
                        await _machine.MoveGpInPosAsync(Groups.XY, point);
                        await _machine.MoveAxesInPlaceAsync(Place.ZFocus);

                        ProcessStatus = Status.Learning;
                    })
                ,

                Status.Learning => new Func<Task>(
                    async () =>
                    {
                        var y = _machine.TranslateActualCoors(Place.CameraChuckCenter, Ax.Y);
                        _wafer.TeachSideShift(y);
                        _wafer.TeachSideAngle(_uActual);
                        //_learningNextDir = _wafer.NextDir();
                    })
                ,

                Status.MovingNextDir => new Func<Task>(
                    async () =>
                    {
                        _machine.SetVelocity(Velocity.Service);
                        await MoveNextDirAsync(false);
                    }),

                Status.Working => new Func<Task>(
                    async () =>
                    {
                        PauseProcess = true;
                        if (PauseProcess) await PauseScenarioAsync();
                        CutWidthMarkerVisibility = Visibility.Visible;
                        ChangeScreensEvent?.Invoke(true);
                        ProcessStatus = Status.Correcting;
                        _machine.StartVideoCapture(0);
                    })
                ,

                Status.Correcting => new Func<Task>(
                    async () =>
                    {
                        var result = MessageBox.Show($"Сместить следующие резы на {CutOffset} мм?", "", MessageBoxButton.OKCancel);
                        if (result == MessageBoxResult.OK) _wafer.AddToSideShift(CutOffset);
                        ChangeScreensEvent?.Invoke(false);
                        ProcessStatus = Status.Working;
                        CutWidthMarkerVisibility = Visibility.Hidden;
                        CutOffset = 0;
                        PauseProcess = false;
                        _machine.FreezeVideoCapture();
                    })
                ,

                _ =>
                     async () => { }
            };
        }

        public async Task AlignWafer()
        {
            if (_learningCondition.State)
            {
                if (!_tempWafer2D.FirstPointSet)
                {
                    _tempWafer2D.Point1 = new double[] { _xActual, _yActual };
                    _tempWafer2D.FirstPointSet = true;
                    ThrowMessage("Выберете второй ориентир для выравнивания и нажмите _",1);
                }
                else
                {
                    _tempWafer2D.Point2 = new double[] { _xActual, _yActual };
                    _machine.SetVelocity(Velocity.Service);
                    var angle = _tempWafer2D.GetAngle();
                    await _machine.MoveAxInPosAsync(Ax.U, _uActual - angle);
                    var rotation = new RotateTransform(-angle);
                    rotation.CenterX = _machine.GetGeometry(Place.CameraChuckCenter, Ax.X);
                    rotation.CenterY = _machine.GetGeometry(Place.CameraChuckCenter, Ax.Y);
                    var point = rotation.Transform(new System.Windows.Point(_tempWafer2D.Point2[0], _tempWafer2D.Point2[1]));
                    await _machine.MoveAxInPosAsync(Ax.Y, point.Y);
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
                    ThrowMessage("Пластине кранты!",0);
                    break;
                case Status.Correcting:
                    break;
                case Status.Done:
                    break;
                default:
                    break;
            }
        }
        public static int procCount = 0;
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
                default:
                    break;
            }
        }
        private void _machine_OnAxisMotionStateChanged(Ax axis, double position, bool nLmt, bool pLmt, bool motionDone)
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
                default:
                    break;
            }
        }

        private double _xActual;
        private double _yActual;
        private double _zActual;
        private double _uActual;
        private bool _machineVacuumSensor;
        private bool _spindleWorking;
        
        private readonly Wafer2D _wafer;
        private readonly IMachine _machine;
        private readonly Blade _blade;
        private CheckCutControl _checkCut;
        public ChangeScreens ChangeScreensEvent;
        public event Action<bool> BladeTracingEvent;
        
        public Visibility CutWidthMarkerVisibility { get; set; } = Visibility.Hidden;
        public Status ProcessStatus { get; private set; }

      
        public double CutOffset { get; set; } = 0;

        private double _bladeTransferGapZ /*{ get; set; }*/ = 1;
        private bool IsCutting { get; set; } = false;
       

        private bool _pauseProcess;
        public bool PauseProcess
        {
            get => _pauseProcess;
            set
            {
                _pauseProcess = value;
                if (_pauseToken != null)
                {
                    if (value)
                    {
                        _pauseToken.Pause();
                    }
                    else _pauseToken.Resume();
                }
            }
        }
        private bool _cancelProcess;
        public bool CancelProcess
        {
            set
            {
                _cancelProcess = value;
                if (_cancellationToken != null)
                {
                    if (value)
                    {
                        _cancellationToken.Cancel();
                    }
                }
            }
        }

        private PauseTokenSource _pauseToken;

        private Diagram[] _baseProcess;

        private CancellationTokenSource _cancellationToken;
        public bool SideDone { get; private set; } = false;
        public int SideCounter { get; private set; } = 0;
        private bool BladeInWafer => _zActual > _machine.GetGeometry(Place.ZBladeTouch, 2) - _wafer.Thickness - _bladeTransferGapZ;

        public int CurrentLine { get; private set; }
        //private double RotationSpeed { get; set; }
        private double _feedSpeed;
       
        public GetRotation GetRotationEvent;

        public event Action<string,int> ThrowMessage;

        public bool Rotation { get; set; } = false;

        public async Task PauseScenarioAsync()
        {
            await _machine.WaitUntilAxisStopAsync(Ax.X);
            //Machine.EmgStop();
            await ProcElementDispatcherAsync(Diagram.GoCameraPointXyz);
        }
        
        private void NextLine()
        {
            CurrentLine++;
            //if (CurrentLine < _wafer.DirectionLinesCount - 1) CurrentLine++;
            //else if (CurrentLine == _wafer.DirectionLinesCount - 1)
            //{
            //    SideDone = true;
            //    SideCounter++;
            //}
        }
        private async Task MoveNextDirAsync(bool next = true)
        {
            //if (!next || !_wafer.NextDir(true))
            {

                double angle = _wafer.CurrentSideAngle;
                double time = 0;
                var deltaAngle =  _wafer.CurrentSideAngle - _wafer.PrevSideAngle;
                if (_wafer.CurrentSideActualAngle == _wafer.CurrentSideAngle)
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
                GetRotationEvent(deltaAngle, time);
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
                    y = _wafer[CurrentLine != 0 ? CurrentLine - 1 : 0].Start.Y - _machine.GetFeature(MFeatures.CameraBladeOffset);
                    y = _machine.TranslateSpecCoor(Place.BladeChuckCenter, -y, Ax.Y);
                    await _machine.MoveGpInPosAsync(Groups.XY, new double[] { x, y }, true);


                    await _machine.MoveAxInPosAsync(Ax.Z, _machine.GetFeature(MFeatures.CameraFocus));
                    break;
                
                case Diagram.GoTransferingHeightZ:
                    _machine.SetVelocity(Velocity.Service);
                    await _machine.MoveAxInPosAsync(Ax.Z, _machine.GetFeature(MFeatures.ZBladeTouch) - _wafer.Thickness - _bladeTransferGapZ);
                    break;
                
                default:
                    break;
            }
        }
        private async Task TakeThePhotoAsync()
        {
            _machine.StartVideoCapture(0);
            await ProcElementDispatcherAsync(Diagram.GoTransferingHeightZ);
            //await ProcElementDispatcherAsync(Diagram.GoCameraPointXyz);
            await Task.Run(GetFunc(Diagram.GoCameraPointXyz));
            _machine.SwitchOnValve(Valves.Blowing);
            await Task.Delay(100).ConfigureAwait(false);
            _machine.FreezeVideoCapture();
            _machine.SwitchOffValve(Valves.Blowing);
        }
        public async Task StartPauseProc()
        {
            await _rootSequence.DoWork();            
        }
        private void Machine_OnVacuumWanished(Sensors sensor, bool state)
        {
            if (sensor == Sensors.ChuckVacuum & !state)
            {
                switch (ProcessStatus)
                {
                    case Status.StartLearning:
                        ThrowMessage?.Invoke("Вакуум исчез!!!",0);
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
                    default:
                        break;
                }
                if (IsCutting) { }
            }
        }


        public void Dispose()
        {
            _cancellationToken?.Dispose();
        }
        ~Process4()
        {
            Dispose();
        }
    }
}


