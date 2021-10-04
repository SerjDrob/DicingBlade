
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
using DicingBlade.ViewModels;

namespace DicingBlade.Classes
{
    [AddINotifyPropertyChangedInterface]
    internal class Process4 : IMessager, IDisposable
    {
        public Process4(IMachine machine, Wafer2D wafer, Blade blade, ITechnology technology, Diagram[] proc) 
        {
            blade.Thickness = 0.11;
            blade.Diameter = 55.6;

            _machine = machine ?? throw new ProcessException("Не выбрана установка для процесса"); ;
            _wafer = wafer ?? throw new ProcessException("Не выбрана подложка для процесса");
            _blade = blade ?? throw new ProcessException("Не выбран диск для процесса");
            _technology = technology;
            RefresfTechnology(_technology);            
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
               // throw new ProcessException("Отсутствует вакуум на столике. Возможно не установлена рамка или неисправна вакуумная система");
            }
            if (!_spindleWorking)
            {
                //  throw new ProcessException("Не включен шпиндель");
            }
           
            _checkCut.Set(technology.StartControlNum, technology.ControlPeriod);
            
            ProcessStatus = Status.StartLearning;

            var singleCutCondition = new Condition(true);
            var singleCutSequence = new Sequence()
                .Hire(new Leaf(GetFunc(Diagram.GoTransferingHeightZ)))
                .Hire(new Leaf(GetFunc(Diagram.GoCurPositionCutXy)))
                .Hire(new Leaf(GetFunc(Diagram.GoNextDepthZ)))
                .Hire(new Leaf(GetFunc(Diagram.CuttingX))) 
                .Hire(new Leaf(GetFunc(Diagram.GoCameraPointXyz)))
                .Hire(new Leaf(async ()=> { singleCutCondition.SetState(false); }));

            var singleCutTicker = new Ticker(singleCutSequence, singleCutCondition);
            _singleCutLeaf = new Leaf(async () => { singleCutCondition.SetState(true); }, singleCutTicker);
            _singleCutLeaf.SetPauseToken(new PauseTokenSource());
            _workingCondition = new(true);
            var inspectLeaf = new Leaf(TakeThePhotoAsync);//.SetBlock(_cutInspectCondition);
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

            Sequence learningSequence = new();
            _learningCondition = new(true);           

            var rotationSelector = new Selector();
            var rotationLeaf = new Leaf(IncrementDir,new Leaf(GetFunc(Status.MovingNextDir), new Leaf(GetFunc(Status.StartLearning))))
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

            rotationLeaf.CheckMyCondition      += SetConditionsStates;
            learningSequence.CheckMyCondition += SetConditionsStates;
            nextSideLeaf.CheckMyCondition      += SetConditionsStates;
            rotationSelector.CheckMyCondition  += SetConditionsStates;
            inspectLeaf.CheckMyCondition       += SetConditionsStates;
            workingTicker.CheckMyCondition     += SetConditionsStates;
            inspectSelector.CheckMyCondition   += SetConditionsStates;

            _inspectX = _machine.GetGeometry(Place.CameraChuckCenter, Ax.X);
            _learningDone = false;
            _rootSequence.SetPauseToken(_pauseProcTokenSource);
            _rootSequence.DoWork();
        }

        public void RefresfTechnology(ITechnology technology)
        {
            _feedSpeed = technology.FeedSpeed;
            _undercut = technology.UnterCut;
        }
        private async Task EndProcess()
        {
            await _machine.MoveGpInPlaceAsync(Groups.XY, Place.Loading);
            ProcessStatus = Status.Done;
            ThrowMessage("Всё!",0);
        }
        private ITechnology _technology;
        private async Task SetProcessStatus()
        {
            ProcessStatus = Status.Working;
            OnProcessStatusChanged("Работа");
        }
        private void SetConditionsStates()
        {
            _rotateLearningCondition.SetState(_learningNextDir);
            _learningCondition.SetState(!_learningDone);
            _cutInspectCondition.SetState(_checkCut.Check & !_correctSelCondition.State);
            _workingCondition.SetState(ProcessStatus != Status.Ending);
            _sideDoneCondition.SetState(SideDone);
        }
        private async Task IncrementDir()
        {
            if (CurrentDirection < _wafer.SidesCount - 1)
            {
                _wafer.SetSide(++CurrentDirection);
                if (_wafer.SidesCount - 1 == _wafer.CurrentSide)
                {
                    _learningNextDir = false;
                }
            }
            else
            {
                _wafer.SetSide(CurrentDirection = 0);
            }
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
                if (_wafer.CurrentSide!=0)
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
        #region NewFields
        public int CurrentDirection { get; private set; }
        private double _zRatio = 0;
        #endregion
        private PauseTokenSource _pauseProcTokenSource = new();
        private bool _learningNextDir              = true;
        private Condition _learningCondition       = new();
        private Sequence _workingSequence          = new();
        private Condition _workingCondition        = new();
        private Condition _cutInspectCondition     = new();
        private Condition _sideDoneCondition       = new(false);
        private Sequence _rootSequence             = new();
        private Condition _correctSelCondition     = new();
        private Condition _rotateLearningCondition = new(true);
        private Leaf _singleCutLeaf;
        private TempWafer2D _tempWafer2D;
        private List<Task> _localTasks             = new();

        private async Task AwaitTaskAsync(Task task)
        {
            _localTasks.Add(task);
            await task.ConfigureAwait(false);
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
                    await AwaitTaskAsync(_machine.MoveAxInPosAsync(Ax.X, x + xGap));
                })
                ,


                Diagram.GoWaferEndX => new Func<Task>(
                     async () =>
                     {
                         var xCurLineEnd = _wafer[CurrentLine].End.X;
                         _machine.SetVelocity(Velocity.Service);
                         x = _machine.TranslateSpecCoor(Place.BladeChuckCenter, -xCurLineEnd, 0);
                         await AwaitTaskAsync(_machine.MoveAxInPosAsync(Ax.X, x));
                     })
                ,

                Diagram.GoNextDepthZ => new Func<Task>(
                    async () =>
                    {
                        _machine.SetVelocity(Velocity.Service);                        
                        z = _machine.TranslateSpecCoor(Place.ZBladeTouch, _wafer[_zRatio] + _undercut, Ax.Z);
                        await AwaitTaskAsync(_machine.MoveAxInPosAsync(Ax.Z, z));
                    })
                ,

                Diagram.CuttingX => new Func<Task>(
                    async () =>
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
                        _checkCut.addToCurrentCut();
                        _machine.SwitchOffValve(Valves.Coolant);     
                    })
                ,


                Diagram.GoCameraPointXyz => new Func<Task>(
                    async () =>
                    {
                        _machine.SetVelocity(Velocity.Service);
                        z = _machine.TranslateSpecCoor(Place.ZBladeTouch, _wafer.Thickness + _bladeTransferGapZ, Ax.Z);
                        await AwaitTaskAsync(_machine.MoveAxInPosAsync(Ax.Z, z));

                        // x = _machine.GetGeometry(Place.CameraChuckCenter, Ax.X);
                        // y = _wafer[CurrentLine != 0 ? CurrentLine : 0].Start.Y - _wafer.CurrentShift;// - _machine.GetFeature(MFeatures.CameraBladeOffset);
                        y = - _machine.TranslateSpecCoor(Place.BladeChuckCenter, _yActual, Ax.Y);
                        y = _machine.TranslateSpecCoor(Place.CameraChuckCenter, -y, Ax.Y);
                        await AwaitTaskAsync(_machine.MoveGpInPosAsync(Groups.XY, new double[] { _inspectX, y }, true));
                        await AwaitTaskAsync(_machine.MoveAxesInPlaceAsync(Place.ZFocus));
                    })
                ,

                Diagram.GoOnWaferRightX => new Func<Task>(
                    async () =>
                    {
                        if (BladeInWafer) ;
                        _machine.SetVelocity(Velocity.Service);
                        x = _wafer.GetNearestCut(_yActual - _machine.GetGeometry(Place.CameraChuckCenter, Ax.Y)).End.X;
                        await AwaitTaskAsync(_machine.MoveAxInPosAsync(Ax.X, x));
                    })
                ,

                Diagram.GoOnWaferLeftX => new Func<Task>(
                    async () =>
                    {
                        if (BladeInWafer) ;
                        _machine.SetVelocity(Velocity.Service);
                        x = _wafer.GetNearestCut(_yActual - _machine.GetGeometry(Place.CameraChuckCenter, Ax.Y)).Start.X;
                        await AwaitTaskAsync(_machine.MoveAxInPosAsync(Ax.X, x));
                    })
                ,

                Diagram.GoWaferCenterXy => new Func<Task>(
                    async () =>
                    {
                        if (BladeInWafer) ;
                        _machine.SetVelocity(Velocity.Service);
                        await AwaitTaskAsync(_machine.MoveGpInPlaceAsync(Groups.XY, Place.CameraChuckCenter));
                    })
                ,

                Diagram.GoNextCutY => new Func<Task>(
                    async () =>
                    {
                        if (BladeInWafer) ;
                        _machine.SetVelocity(Velocity.Service);
                        y = _machine.TranslateSpecCoor(Place.BladeChuckCenter, _wafer[CurrentLine].Start.Y, Ax.Y);
                        await AwaitTaskAsync(_machine.MoveAxInPosAsync(Ax.Y, y));
                    })
                ,

                Diagram.GoNextCutXy => new Func<Task>(
                    async () =>
                    {
                        // if (BladeInWafer) ;
                        _machine.SetVelocity(Velocity.Service);
                        x = _wafer[CurrentLine].Start.X;
                        x = x + Math.Sign(x) * _blade.XGap(_wafer.Thickness);
                        y = _wafer[CurrentLine].Start.Y - _wafer.CurrentShift;
                        var arr = _machine.TranslateActualCoors(Place.BladeChuckCenter, new (Ax, double)[] { (Ax.X, -x), (Ax.Y, -y) });
                        var xy = new double[] { arr.GetVal(Ax.X), arr.GetVal(Ax.Y) };
                        await AwaitTaskAsync(_machine.MoveGpInPosAsync(Groups.XY, xy, true));
                    })
                ,

                Diagram.GoCurPositionCutXy => new Func<Task>(
                   async () =>
                   {
                        // if (BladeInWafer) ;
                       _machine.SetVelocity(Velocity.Service);
                       x = _wafer[CurrentLine].Start.X;
                       x = x + Math.Sign(x) * _blade.XGap(_wafer.Thickness);
                       y = - _machine.TranslateSpecCoor(Place.CameraChuckCenter, _yActual, Ax.Y);
                       //y = _wafer[CurrentLine].Start.Y - _wafer.CurrentShift;
                       var arr = _machine.TranslateActualCoors(Place.BladeChuckCenter, new (Ax, double)[] { (Ax.X, -x), (Ax.Y, -y) });
                       var xy = new double[] { arr.GetVal(Ax.X), arr.GetVal(Ax.Y) };
                       await AwaitTaskAsync(_machine.MoveGpInPosAsync(Groups.XY, xy, true));
                   })
                ,

                Diagram.GoTransferingHeightZ => new Func<Task>(
                    async () =>
                    {
                        _machine.SetVelocity(Velocity.Service);
                        await AwaitTaskAsync(_machine.MoveAxInPosAsync(Ax.Z, _machine.GetFeature(MFeatures.ZBladeTouch) - _wafer.Thickness - _bladeTransferGapZ));
                    })
                ,

                Diagram.GoDockHeightZ => new Func<Task>(
                    async () =>
                    {
                        _machine.SetVelocity(Velocity.Service);
                        await AwaitTaskAsync(_machine.MoveAxInPosAsync(Ax.Z, 1));
                    })
                ,

                Diagram.GoNextDirection => new Func<Task>(
                    async () =>
                    {
                        _machine.SetVelocity(Velocity.Service);
                        await AwaitTaskAsync(MoveNextDirAsync());

                        _procParamsEventArgs.currentShift = _wafer.CurrentShift;
                        _procParamsEventArgs.currentSideAngle = _wafer.CurrentSideAngle;
                        OnProcParamsChanged(this, _procParamsEventArgs);

                        SideDone = false;
                        CurrentLine = 0;
                        //SideCounter++;                       
                    })
                ,

                Diagram.GoCameraPointLearningXyz => new Func<Task>(
                    async () =>
                    {
                        _machine.SetVelocity(Velocity.Service);
                        await AwaitTaskAsync(_machine.MoveAxInPosAsync(Ax.Z, _machine.GetFeature(MFeatures.ZBladeTouch) - _wafer.Thickness - _bladeTransferGapZ));
                        y = _wafer.GetNearestCut(0).Start.Y;
                        var arr = _machine.TranslateActualCoors(Place.CameraChuckCenter, new (Ax, double)[] { (Ax.X, 0), (Ax.Y, -y) });
                        var point = new double[] { arr.GetVal(Ax.X), arr.GetVal(Ax.Y) };
                        await AwaitTaskAsync(_machine.MoveGpInPosAsync(Groups.XY, point));
                        await AwaitTaskAsync(_machine.MoveAxesInPlaceAsync(Place.ZFocus));

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
                        await AwaitTaskAsync(_machine.MoveAxInPosAsync(Ax.Z, _machine.GetFeature(MFeatures.ZBladeTouch) - _wafer.Thickness - _bladeTransferGapZ));
                        var y = _wafer.GetNearestCut(0).Start.Y;
                        var arr = _machine.TranslateActualCoors(Place.CameraChuckCenter, new (Ax, double)[] { (Ax.X, 0), (Ax.Y, -y) });
                        //_machine.MoveAxInPosAsync(Ax.Y, arr.GetVal(Ax.Y));
                        //_machine.MoveAxInPosAsync(Ax.X, arr.GetVal(Ax.X));
                        var point = new double[] { arr.GetVal(Ax.X), arr.GetVal(Ax.Y) };  
                        await AwaitTaskAsync(_machine.MoveGpInPosAsync(Groups.XY, point));
                        await AwaitTaskAsync(_machine.MoveAxesInPlaceAsync(Place.ZFocus));
                        ProcessStatus = Status.Learning;
                    })
                ,

                Status.Learning => new Func<Task>(
                    async () =>
                    {                        
                        var y = _machine.TranslateActualCoors(Place.CameraChuckCenter, Ax.Y);
                        _wafer.TeachSideShift(y);
                        
                        _procParamsEventArgs.currentShift = _wafer.CurrentShift;
                        OnProcParamsChanged(this, _procParamsEventArgs);
                        
                        _wafer.TeachSideAngle(_uActual);
                        if (!_learningNextDir)
                        {
                            _learningDone = true;
                        }
                    })
                ,

                Status.MovingNextDir => new Func<Task>(
                    async () =>
                    {
                        _machine.SetVelocity(Velocity.Service);
                        await AwaitTaskAsync(MoveNextDirAsync(false));

                        _procParamsEventArgs.currentShift = _wafer.CurrentShift;
                        _procParamsEventArgs.currentSideAngle = _wafer.CurrentSideAngle;
                        OnProcParamsChanged(this, _procParamsEventArgs);
                    }),

                Status.Working => new Func<Task>(
                    async () =>
                    {
                        //PauseProcess = true;
                        //if (PauseProcess) AwaitTaskAsync(PauseScenarioAsync());
                        CutWidthMarkerVisibility = Visibility.Visible;
                        ChangeScreensEvent?.Invoke(true);
                        ProcessStatus = Status.Correcting;
                        _machine.StartVideoCapture(0);
                    })
                ,

                Status.Correcting => new Func<Task>(
                    async () =>
                    {
                        var result = MessageBox.Show($"Сместить следующие резы на {CutOffset:N3} мм?", "", MessageBoxButton.OKCancel);
                        if (result == MessageBoxResult.OK) _wafer.AddToSideShift(CutOffset);
                        ChangeScreensEvent?.Invoke(false);
                        ProcessStatus = Status.Working;
                        CutWidthMarkerVisibility = Visibility.Hidden;
                        CutOffset = 0;
                        //PauseProcess = false;
                        _machine.FreezeVideoCapture();
                    })
                ,

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
                    _tempWafer2D.Point1 = new double[] { _xActual, _yActual };
                    _tempWafer2D.FirstPointSet = true;
                    ThrowMessage("Выберете второй ориентир для выравнивания и нажмите _",1);
                }
                else
                {
                    _tempWafer2D.Point2 = new double[] { _xActual, _yActual };
                    _machine.SetVelocity(Velocity.Service);
                    var angle = _tempWafer2D.GetAngle();
                    await AwaitTaskAsync(_machine.MoveAxInPosAsync(Ax.U, _uActual - angle));
                    var rotation = new RotateTransform(-angle);
                    rotation.CenterX = _machine.GetGeometry(Place.CameraChuckCenter, Ax.X);
                    rotation.CenterY = _machine.GetGeometry(Place.CameraChuckCenter, Ax.Y);
                    var point = rotation.Transform(new System.Windows.Point(_tempWafer2D.Point2[0], _tempWafer2D.Point2[1]));
                    await AwaitTaskAsync(_machine.MoveAxInPosAsync(Ax.Y, point.Y));
                    _tempWafer2D.FirstPointSet = false;
                    ThrowMessage("", 1);
                }
            }
        }
        private void _machine_OnSpindleStateChanging(object? obj, SpindleEventArgs e )
        {
            _spindleWorking = e.OnFreq;
            switch (ProcessStatus)
            {
                case Status.None:
                    break;
                case Status.StartLearning:
                    break;
                case Status.Learning:
                    break;
                case Status.Working when IsCutting & !_spindleWorking:
                    EmergencyScript();
                    break;
                case Status.Correcting:
                    break;
                case Status.Done:
                    break;
                default:
                    break;
            }
        }
        
        private void _machine_OnSensorStateChanged(Sensors sensor, bool state)
        {
            if (!state)
            {
                switch (ProcessStatus)
                {
                    case Status.None:
                        break;
                    case Status.StartLearning:
                        break;
                    case Status.Learning:
                        break;
                    case Status.Working:
                        if (IsCutting)
                        { 
                            if (!_pauseProcTokenSource.IsPaused)
                            {
                                //EmergencyScript();
                                //_pauseProcTokenSource.Pause();
                                //ThrowMessage?.Invoke($"Аварийная остановка.{_machine.GetSensorName(sensor)}",0);
                            }
                        }
                        break;
                    case Status.Correcting:
                        break;
                    case Status.Done:
                        break;
                    case Status.MovingNextDir:
                        break;
                    case Status.Ending:
                        break;
                    case Status.Pause:
                        break;
                }
            }

            if (state)
            {
                _offSensors ^= sensor;
            }
            else
            {
                _offSensors |= sensor;
            }

        }

        private Sensors _offSensors = 0;
        private void _machine_OnAxisMotionStateChanged(Ax axis, double position, bool nLmt, bool pLmt, bool motionDone, bool motionStart)
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
        public void TeachDiskShift()
        {
            Properties.Settings.Default.DiskShift = _lastCutY - _yActual;
            Properties.Settings.Default.Save();
        }
        public async Task DoSingleCut() => await AwaitTaskAsync(_singleCutLeaf.DoWork());

        private double _xActual;
        private double _yActual;
        private double _zActual;
        private double _uActual;
        private bool _machineVacuumSensor;
        private bool _spindleWorking;
        private double _inspectX;
        private double _lastCutY;
        
        private readonly Wafer2D _wafer;
        private readonly IMachine _machine;
        private readonly Blade _blade;
        private CheckCutControl _checkCut;
        public ChangeScreens ChangeScreensEvent;
        public event Action<bool> BladeTracingEvent;
        public event Action<string> OnProcessStatusChanged;
        public event Action OnControlPointAppeared;


        public Visibility CutWidthMarkerVisibility { get; set; } = Visibility.Hidden;
        public Status ProcessStatus { get; private set; }       
        public double CutOffset { get; set; } = 0;

        private double _bladeTransferGapZ /*{ get; set; }*/ = 3;
        private bool IsCutting { get; set; } = false;   
        public bool SideDone { get; private set; } = false;
        private bool BladeInWafer => _zActual > _machine.GetGeometry(Place.ZBladeTouch,Ax.Z) - _wafer.Thickness - _bladeTransferGapZ;
        public int CurrentLine { get; private set; }
        
        private double _feedSpeed;
        private double _undercut;
        public GetRotation GetRotationEvent;
        private bool _learningDone;
        private ProcParams _procParamsEventArgs;
        public event Action<string,int> ThrowMessage;
        public bool Rotation { get; set; } = false;
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
            await Task.Run(GetFunc(Diagram.GoCameraPointXyz));
            _machine.SwitchOnValve(Valves.Blowing);
            await Task.Delay(100).ConfigureAwait(false);
            _machine.FreezeVideoCapture();
            _machine.SwitchOffValve(Valves.Blowing);
            OnControlPointAppeared();
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
            await Task.Run(()=>
            {
                while (ProcessStatus == Status.Correcting) ;                
            });
            _inspectX = _xActual;
            OnControlPointAppeared();
        }
        private void GetPermissionBySensors(Sensors sensors)
        {
            string s = default;
            foreach (var sensor in Enum.GetValues(typeof(Sensors)))
            {
                if (sensors.HasFlag((Sensors)sensor))
                {
                    s += _machine.GetSensorName((Sensors)sensor) + "\n";
                }
            }

            if (s != default)
            {
                throw new ProcessException($"Отсутствует:\n{s}");
            }
        }
        public async Task StartPauseProc()
        {
            

            //GetPermissionBySensors(_offSensors);
            if (!(Status.Working|Status.Correcting|Status.Pause).HasFlag(ProcessStatus))
            { 
                _machine.StartVideoCapture(0);
                _rootSequence.DoWork();
            }
            else
            {
                if (ProcessStatus == Status.Pause)
                {
                    _pauseProcTokenSource.Resume();
                    ProcessStatus = Status.Working;
                    return;
                }
                if (_correctSelCondition.State)
                { 
                    var result = MessageBox.Show($"Сместить следующие резы на {CutOffset} мм?", "", MessageBoxButton.OKCancel);
                    if (result == MessageBoxResult.OK)
                    {
                        _wafer.AddToSideShift(CutOffset);
                    }
                    var nearestNum =
                        _wafer.GetNearestNum(_yActual - _machine.GetGeometry(Place.CameraChuckCenter, Ax.Y));
                    if(CurrentLine != nearestNum)
                    {
                        result = MessageBox.Show($"Изменить номер реза на {nearestNum}?", "", MessageBoxButton.OKCancel);
                        if (result == MessageBoxResult.OK)
                        {
                            CurrentLine=nearestNum-1;
                        }
                    }
                    ChangeScreensEvent?.Invoke(false);                    
                    CutWidthMarkerVisibility = Visibility.Hidden;
                    CutOffset = 0;
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

        public void EmergencyScript()
        {
            _pauseProcTokenSource.Pause();
            //ProcessStatus = Status.Pause;
            _machine.Stop(Ax.X);
            _machine.MoveAxInPosAsync(Ax.Z, 0);
            _machine.StopSpindle();
        }
        public void SubstrateChanged(object obj, SettingsChangedEventArgs eventArgs)
        {
            if (eventArgs.Settings is IWafer & (int)(ProcessStatus & (Status.Working | Status.Correcting | Status.MovingNextDir | Status.Ending)) == 0)
            {
                var wf = (IWafer)eventArgs.Settings;
                _wafer.SetChanges(wf.IndexH, wf.IndexW, wf.Thickness, new Rectangle2D(wf.Height, wf.Width));
            }
        }
        public void Dispose()
        {
            //_cancellationToken?.Dispose();
        }
        ~Process4()
        {            
            Dispose();
        }

        public event Action<object, ProcParams> OnProcParamsChanged;
    }
}


