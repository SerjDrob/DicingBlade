
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
    internal class Process3 : IMessager, IDisposable
    {
        public Process3(IMachine machine, Wafer wafer, Blade blade, ITechnology technology, Diagram[] proc) // В конструкторе происходит загрузка технологических параметров
        {

            _machine = machine ?? throw new ProcessException("Не выбрана установка для процесса"); ;
            _wafer = wafer ?? throw new ProcessException("Не выбрана подложка для процесса");
            _blade = blade ?? throw new ProcessException("Не выбран диск для процесса");
            _feedSpeed = technology.FeedSpeed;
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
            {
                throw new ProcessException("Отсутствует вакуум на столике. Возможно не установлена рамка или неисправна вакуумная система");
            }
            if (!_spindleWorking)
            {
              //  throw new ProcessException("Не включен шпиндель");
            }
            //Traces = new ObservableCollection<TracePath>();
            //TracesView = new WaferView();
            _baseProcess = proc;
            CancelProcess = false;
            // FeedSpeed = PropContainer.Technology.FeedSpeed;

            _checkCut.Set(technology.StartControlNum, technology.ControlPeriod);
            //_machine.GoThereAsync(Place.CameraChuckCenter).Wait();
            ProcessStatus = Status.StartLearning;


           
            _workingCondition = new(true);
            var InspectLeaf = new Leaf(TakeThePhotoAsync).SetBlock(_cutInspectCondition);
            var nextSideLeaf = new Leaf(GetFunc(Diagram.GoNextDirection)).SetBlock(_sideDoneCondition);

            _workingSequence
                .Hire(new Leaf(GetFunc(Diagram.GoNextCutXy)))
                .Hire(new Leaf(GetFunc(Diagram.GoNextDepthZ)))
                .Hire(new Leaf(GetFunc(Diagram.CuttingX), InspectLeaf))
                .Hire(new Leaf(GetFunc(Diagram.GoTransferingHeightZ)))
                .Hire(nextSideLeaf);
            
            _learningSequence = new();            
            _learningCondition = new(true);
            
            var rotationSelector = new Selector();
            var rotationLeaf = new Leaf(GetFunc(Status.MovingNextDir)).SetBlock(_learningCondition);

            _learningSequence
                .Hire(new Leaf(GetFunc(Status.StartLearning)))
                .Hire(new Leaf(GetFunc(Status.Learning), rotationLeaf))
                .SetBlock(_learningCondition);

            var workingTicker = new Ticker(_workingSequence, _workingCondition);
            _rootSequence
                .Hire(_learningSequence)
                .Hire(workingTicker);

            rotationLeaf.CheckMyCondition      += SetConditiosStates;
            _learningSequence.CheckMyCondition += SetConditiosStates;
            nextSideLeaf.CheckMyCondition      += SetConditiosStates;
            rotationSelector.CheckMyCondition  += SetConditiosStates;
            InspectLeaf.CheckMyCondition       += SetConditiosStates;
            workingTicker.CheckMyCondition     += SetConditiosStates;

            _rootSequence.DoWork();

        }

        private void SetConditiosStates()
        {
            _learningCondition.SetState(_learningNextDir);
            _cutInspectCondition.SetState(_checkCut.Check);
            _workingCondition.SetState(!(SideCounter == _wafer.DirectionsCount));
            _sideDoneCondition.SetState(SideDone);

        }

        private bool _learningNextDir = true;
        private Condition _bladeInWaferCond = new();
        private Condition _learningCondition = new();
        private Sequence _workingSequence = new();
        private Selector _learningSelector = new();
        private Selector _workingSelector = new();
        private Condition _workingCondition = new();
        private Condition _cutInspectCondition = new();
        private Condition _sideDoneCondition = new(false);
        private Sequence _rootSequence = new();
        private Sequence _learningSequence;
        private TempWafer2D _tempWafer2D;
        private Func<Task> GetFunc(Diagram element)
        {
            var x = new double();
            var y = new double();
            var z = new double();
            var u = new double();
            var xCurLineEnd = _wafer.GetCurrentLine(CurrentLine).end.X;

            return element switch
            {
                Diagram.GoWaferStartX => new Func<Task>( async () =>
                      {
                          xCurLineEnd = _wafer.GetCurrentLine(CurrentLine).end.X;
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
                         xCurLineEnd = _wafer.GetCurrentLine(CurrentLine).end.X;                         
                         _machine.SetVelocity(Velocity.Service);
                         x = _machine.TranslateSpecCoor(Place.BladeChuckCenter, -xCurLineEnd, 0);
                         await _machine.MoveAxInPosAsync(Ax.X, x);
                     })
                ,

                Diagram.GoNextDepthZ => new Func<Task>(
                    async () =>
                   {
                       _machine.SetVelocity(Velocity.Service);
                      // if (_wafer.CurrentCutIsDone(CurrentLine)) ;
                       z = _machine.TranslateSpecCoor(Place.ZBladeTouch, _wafer.GetCurrentCutZ(CurrentLine), Ax.Z);
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

                       xCurLineEnd = _wafer.GetCurrentLine(CurrentLine).end.X;
                       x = _machine.TranslateSpecCoor(Place.BladeChuckCenter, -xCurLineEnd, 0);

                       BladeTracingEvent(true);
                       await _machine.MoveAxInPosAsync(Ax.X, x);
                       BladeTracingEvent(false);
                       IsCutting = false;


                       _machine.SwitchOffValve(Valves.Coolant);

                       if (!_wafer.CurrentCutIncrement(CurrentLine))
                       {
                           NextLine();
                       }
                      
                   })
                ,


                Diagram.GoCameraPointXyz => new Func<Task>(
                    async () =>
                   {
                       _machine.SetVelocity(Velocity.Service);
                       z = _machine.TranslateSpecCoor(Place.ZBladeTouch, _wafer.Thickness + _bladeTransferGapZ, Ax.Z);
                       await _machine.MoveAxInPosAsync(Ax.Z, z);

                       x = _machine.GetGeometry(Place.CameraChuckCenter, Ax.X);
                       y = _wafer.GetCurrentLine(CurrentLine != 0 ? CurrentLine - 1 : 0).start.Y - _machine.GetFeature(MFeatures.CameraBladeOffset);
                       y = _machine.TranslateSpecCoor(Place.BladeChuckCenter, -y, Ax.Y);
                       await _machine.MoveGpInPosAsync(Groups.XY, new double[] { x, y }, true);
                       await _machine.MoveAxInPosAsync(Ax.Z, _machine.GetFeature(MFeatures.CameraFocus));
                   })
                ,

                Diagram.GoOnWaferRightX => new Func<Task>(
                    async () =>
                   {
                       if (BladeInWafer) ;
                       _machine.SetVelocity(Velocity.Service);
                       x = _wafer.GetNearestCut(_yActual - _machine.GetGeometry(Place.CameraChuckCenter, Ax.Y)).EndPoint.X;
                       await _machine.MoveAxInPosAsync(Ax.X, x);
                   })
                ,

                Diagram.GoOnWaferLeftX => new Func<Task>(
                    async () =>
                   {
                       if (BladeInWafer) ;
                       _machine.SetVelocity(Velocity.Service);
                       x = _wafer.GetNearestCut(_yActual - _machine.GetGeometry(Place.CameraChuckCenter, Ax.Y)).StartPoint.X;
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
                       y = _machine.TranslateSpecCoor(Place.BladeChuckCenter, _wafer.GetCurrentLine(CurrentLine).start.Y, Ax.Y);
                       await _machine.MoveAxInPosAsync(Ax.Y, y);
                   })
                ,

                Diagram.GoNextCutXy => new Func<Task>(
                    async () =>
                   {
                        // if (BladeInWafer) ;
                       _machine.SetVelocity(Velocity.Service);
                       x = _wafer.GetCurrentLine(CurrentLine).start.X;
                       y = _wafer.GetCurrentLine(CurrentLine).start.Y;
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
                       y = _wafer.GetNearestCut(0).StartPoint.Y;
                       var arr = _machine.TranslateActualCoors(Place.CameraChuckCenter, new (Ax, double)[] { (Ax.X, 0), (Ax.Y, -y) });
                       var point = new double[] { arr.GetVal(Ax.X), arr.GetVal(Ax.Y) };
                       await _machine.MoveGpInPosAsync(Groups.XY, point);
                       await _machine.MoveAxesInPlaceAsync(Place.ZFocus);
                      
                   })
                ,

                _ => new Func<Task>(
                         async() => { })

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
                       var y = _wafer.GetNearestCut(0).StartPoint.Y;
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
                       _wafer.AddToCurrentDirectionIndexShift = y - _wafer.GetNearestCut(y).StartPoint.Y;
                       _wafer.SetCurrentDirectionAngle = _uActual;
                       _learningNextDir = _wafer.NextDir();
                   })
                ,

                Status.MovingNextDir => new Func<Task>(
                    async () =>
                    {
                       // if(_learningNextDir = _wafer.NextDir())
                        {
                            _machine.SetVelocity(Velocity.Service);
                            await MoveNextDirAsync(false);
                        }                        
                        //await ProcElementDispatcherAsync(Diagram.GoCameraPointLearningXyz);
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
                       if (result == MessageBoxResult.OK) _wafer.AddToCurrentDirectionIndexShift = CutOffset;
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

        private double _xActual;
        private double _yActual;
        private double _zActual;
        private double _uActual;
        private bool _machineVacuumSensor;
        private bool _spindleWorking;
        public string ProcessMessage { get; set; } = "";
        public bool UserConfirmation { get; set; } = false;
        private readonly Wafer _wafer;
        private readonly IMachine _machine;
        private readonly Blade _blade;
        private CheckCutControl _checkCut;
        public ChangeScreens ChangeScreensEvent;
        public event Action<bool> BladeTracingEvent;
        public Visibility TeachVScaleMarkersVisibility { get; set; } = Visibility.Hidden;
        public Visibility CutWidthMarkerVisibility { get; set; } = Visibility.Hidden;
        public Status ProcessStatus { get; private set; }
       
        public WaferView TracesView { get; set; }
        
        public double CutWidth { get; set; } = 0.05;
        public double CutOffset { get; set; } = 0;
        
        private double _bladeTransferGapZ /*{ get; set; }*/ = 1;
        private bool IsCutting { get; set; } = false;
        private bool InProcess { get; set; } = false;
        public bool CutsRotate { get; set; } = true;
        
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
        //private bool Aligned { get; set; }
        //private double OffsetAngle { get; set; }
        public GetRotation GetRotationEvent;

        public event Action<string,int> ThrowMessage;

        public bool Rotation { get; set; } = false;

        public async Task PauseScenarioAsync()
        {
            await _machine.WaitUntilAxisStopAsync(Ax.X);
            //Machine.EmgStop();
            await ProcElementDispatcherAsync(Diagram.GoCameraPointXyz);
        }
        public async Task DoProcessAsync(Diagram[] diagrams)
        {
            procCount++;
            if (!InProcess)
            {
                PauseProcess = false;
                _pauseToken = new PauseTokenSource();
                _cancellationToken = new CancellationTokenSource();
                InProcess = true;
                while (InProcess)
                {
                    foreach (var item in diagrams)
                    {
                        if (_cancellationToken.IsCancellationRequested)
                        {
                            InProcess = false;
                            _cancellationToken.Dispose();
                            CancelProcess = false;
                            break;
                        }
                        await _pauseToken.Token.WaitWhilePausedAsync();
                        await ProcElementDispatcherAsync(item);
                    }
                }
                ProcessStatus = Status.Done;
                _wafer.ResetWafer();
                await _machine.GoThereAsync(Place.Loading);
            }
        }
        private void NextLine()
        {
            if (CurrentLine < _wafer.DirectionLinesCount - 1) CurrentLine++;
            else if (CurrentLine == _wafer.DirectionLinesCount - 1)
            {
                SideDone = true;
                SideCounter++;
            }
        }
        private async Task MoveNextDirAsync(bool next = true)
        {
            if (!next || !_wafer.NextDir(true))
            {

                double angle = 0;
                double time = 0;
                var deltaAngle = _wafer.GetCurrentDiretionAngle - _wafer.GetPrevDiretionAngle;
                if (_wafer.GetCurrentDiretionActualAngle == _wafer.GetCurrentDiretionAngle)
                {
                    angle = _wafer.GetPrevDiretionActualAngle - _wafer.GetPrevDiretionAngle + _wafer.GetCurrentDiretionAngle;
                    time = Math.Abs(angle - _uActual) / _machine.GetAxisSetVelocity(Ax.U);
                }
                else
                {
                    angle = _wafer.GetCurrentDiretionActualAngle;
                    time = Math.Abs(_wafer.GetCurrentDiretionActualAngle - _uActual) / _machine.GetAxisSetVelocity(Ax.U);
                }
                Rotation = true;
                GetRotationEvent(deltaAngle, time);
                await _machine.MoveAxInPosAsync(Ax.U, angle);
                Rotation = false;
            }

        }
        private async Task MovePrevDirAsync()
        {
            if (_wafer.PrevDir())
            {
                await _machine.MoveAxInPosAsync(Ax.U, _wafer.GetCurrentDiretionActualAngle);
            }
        }
        private void PrevLine()
        {
            if (CurrentLine > 0) CurrentLine--;
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

            var xCurLineEnd = _wafer.GetCurrentLine(CurrentLine).end.X;
            switch (element)
            {
                case Diagram.GoWaferStartX:
                    if (BladeInWafer) break;
                    _machine.SetVelocity(Velocity.Service);
                    x = _machine.TranslateSpecCoor(Place.BladeChuckCenter, -xCurLineEnd, 0);
                    double xGap = _blade.XGap(_wafer.Thickness);
                    await _machine.MoveAxInPosAsync(Ax.X, x + xGap);
                    break;
                case Diagram.GoWaferEndX:
                    if (BladeInWafer) break;
                    _machine.SetVelocity(Velocity.Service);
                    x = _machine.TranslateSpecCoor(Place.BladeChuckCenter, -xCurLineEnd, 0);
                    await _machine.MoveAxInPosAsync(Ax.X, x);
                    break;
                case Diagram.GoNextDepthZ:
                    _machine.SetVelocity(Velocity.Service);
                    if (_wafer.CurrentCutIsDone(CurrentLine)) break;
                    z = _machine.TranslateSpecCoor(Place.ZBladeTouch, _wafer.GetCurrentCutZ(CurrentLine), Ax.Z);
                    await _machine.MoveAxInPosAsync(Ax.Z, z);
                    break;
                case Diagram.CuttingX:
                    _machine.SwitchOnValve(Valves.Coolant);
                    await Task.Delay(300).ConfigureAwait(false);
                    _machine.SetAxFeedSpeed(Ax.X, _feedSpeed);
                    IsCutting = true;

                    x = _machine.TranslateSpecCoor(Place.BladeChuckCenter, -xCurLineEnd, 0);
                    
                    BladeTracingEvent(true);
                    await _machine.MoveAxInPosAsync(Ax.X, x);
                    BladeTracingEvent(false);
                    IsCutting = false;


                    _machine.SwitchOffValve(Valves.Coolant);

                    if (!_wafer.CurrentCutIncrement(CurrentLine))
                    {
                        NextLine();
                    }
                    _checkCut.addToCurrentCut();
                    if (_checkCut.Check) await TakeThePhotoAsync();
                    break;

                case Diagram.GoCameraPointXyz:
                    _machine.SetVelocity(Velocity.Service);
                    z = _machine.TranslateSpecCoor(Place.ZBladeTouch, _wafer.Thickness + _bladeTransferGapZ, Ax.Z);
                    await _machine.MoveAxInPosAsync(Ax.Z, z);

                    x = _machine.GetGeometry(Place.CameraChuckCenter, Ax.X);
                    y = _wafer.GetCurrentLine(CurrentLine != 0 ? CurrentLine - 1 : 0).start.Y - _machine.GetFeature(MFeatures.CameraBladeOffset);
                    y = _machine.TranslateSpecCoor(Place.BladeChuckCenter, -y, Ax.Y);
                    await _machine.MoveGpInPosAsync(Groups.XY, new double[] { x, y }, true);


                    await _machine.MoveAxInPosAsync(Ax.Z, _machine.GetFeature(MFeatures.CameraFocus));
                    break;
                case Diagram.GoOnWaferRightX:
                    if (BladeInWafer) break;
                    _machine.SetVelocity(Velocity.Service);
                    x = _wafer.GetNearestCut(_yActual - _machine.GetGeometry(Place.CameraChuckCenter, Ax.Y)).EndPoint.X;
                    await _machine.MoveAxInPosAsync(Ax.X, x);
                    break;
                case Diagram.GoOnWaferLeftX:
                    if (BladeInWafer) break;
                    _machine.SetVelocity(Velocity.Service);
                    x = _wafer.GetNearestCut(_yActual - _machine.GetGeometry(Place.CameraChuckCenter, Ax.Y)).StartPoint.X;
                    await _machine.MoveAxInPosAsync(Ax.X, x);
                    break;
                case Diagram.GoWaferCenterXy:
                    if (BladeInWafer) break;
                    _machine.SetVelocity(Velocity.Service);
                    await _machine.MoveGpInPlaceAsync(Groups.XY, Place.CameraChuckCenter);
                    break;
                case Diagram.GoNextCutY:
                    if (BladeInWafer) break;
                    _machine.SetVelocity(Velocity.Service);
                    y = _machine.TranslateSpecCoor(Place.BladeChuckCenter, _wafer.GetCurrentLine(CurrentLine).start.Y, Ax.Y);
                    await _machine.MoveAxInPosAsync(Ax.Y, y);
                    break;
                case Diagram.GoNextCutXy:
                    // if (BladeInWafer) break;
                    _machine.SetVelocity(Velocity.Service);
                    x = _wafer.GetCurrentLine(CurrentLine).start.X;
                    y = _wafer.GetCurrentLine(CurrentLine).start.Y;
                    var arr = _machine.TranslateActualCoors(Place.BladeChuckCenter, new (Ax, double)[] { (Ax.X, -x), (Ax.Y, -y) });
                    var xy = new double[] { arr.GetVal(Ax.X), arr.GetVal(Ax.Y) };
                    await _machine.MoveGpInPosAsync(Groups.XY, xy, true);
                    break;
                case Diagram.GoTransferingHeightZ:
                    _machine.SetVelocity(Velocity.Service);
                    await _machine.MoveAxInPosAsync(Ax.Z, _machine.GetFeature(MFeatures.ZBladeTouch) - _wafer.Thickness - _bladeTransferGapZ);
                    break;
                case Diagram.GoDockHeightZ:
                    _machine.SetVelocity(Velocity.Service);
                    await _machine.MoveAxInPosAsync(Ax.Z, 1);
                    break;
                case Diagram.GoNextDirection:
                    if (InProcess & SideDone /*| ProcessStatus == Status.Learning*/) //if the blade isn't in the wafer
                    {
                        _machine.SetVelocity(Velocity.Service);
                        //CutsRotate = true;
                        await MoveNextDirAsync();
                        //CutsRotate = false;
                        SideDone = false;
                        CurrentLine = 0;
                        SideCounter++;
                        if (SideCounter == _wafer.DirectionsCount)
                        {
                            InProcess = false;
                        }
                    }
                    break;
                case Diagram.GoCameraPointLearningXyz:
                    _machine.SetVelocity(Velocity.Service);
                    await _machine.MoveAxInPosAsync(Ax.Z, _machine.GetFeature(MFeatures.ZBladeTouch) - _wafer.Thickness - _bladeTransferGapZ);
                    y = _wafer.GetNearestCut(0).StartPoint.Y;
                    arr = _machine.TranslateActualCoors(Place.CameraChuckCenter, new (Ax, double)[] { (Ax.X, 0), (Ax.Y, -y) });
                    var point = new double[] { arr.GetVal(Ax.X), arr.GetVal(Ax.Y) };
                    await _machine.MoveGpInPosAsync(Groups.XY, point);
                    await _machine.MoveAxInPosAsync(Ax.Z, /*Machine.CameraFocus*/3.5);
                    break;
                default:
                    break;
            }
        }
        private async Task TakeThePhotoAsync()
        {
            _machine.StartVideoCapture(0);
            await ProcElementDispatcherAsync(Diagram.GoTransferingHeightZ);
            await ProcElementDispatcherAsync(Diagram.GoCameraPointXyz);
            _machine.SwitchOnValve(Valves.Blowing);
            await Task.Delay(100).ConfigureAwait(false);
            _machine.FreezeVideoCapture();
            _machine.SwitchOffValve(Valves.Blowing);
        }
        public async Task StartPauseProc()
        {
            await _rootSequence.DoWork();
            //switch (ProcessStatus)
            //{
            //    case Status.StartLearning:
            //        await ProcElementDispatcherAsync(Diagram.GoCameraPointLearningXyz);
            //        ProcessStatus = Status.Learning;
            //        break;
            //    case Status.Learning:
            //        var y = _machine.TranslateActualCoors(Place.CameraChuckCenter, Ax.Y);
            //        _wafer.AddToCurrentDirectionIndexShift = y - _wafer.GetNearestCut(y).StartPoint.Y;
            //        _wafer.SetCurrentDirectionAngle = _uActual;
            //        if (_wafer.NextDir())
            //        {
            //            _machine.SetVelocity(Velocity.Service);
            //            await MoveNextDirAsync(false);
            //            await ProcElementDispatcherAsync(Diagram.GoCameraPointLearningXyz);
            //        }
            //        else
            //        {
            //            ProcessStatus = Status.Working;

            //            _traces = new List<TracePath>();

            //            DoProcessAsync(_baseProcess);
            //        }
            //        break;
            //    case Status.Working:
            //        PauseProcess = true;
            //        if (PauseProcess) await PauseScenarioAsync();
            //        CutWidthMarkerVisibility = Visibility.Visible;
            //        ChangeScreensEvent?.Invoke(true);
            //        ProcessStatus = Status.Correcting;
            //        _machine.StartVideoCapture(0);
            //        break;
            //    case Status.Correcting:
            //        var result = MessageBox.Show($"Сместить следующие резы на {CutOffset} мм?", "", MessageBoxButton.OKCancel);
            //        if (result == MessageBoxResult.OK) _wafer.AddToCurrentDirectionIndexShift = CutOffset;
            //        ChangeScreensEvent?.Invoke(false);
            //        ProcessStatus = Status.Working;
            //        CutWidthMarkerVisibility = Visibility.Hidden;
            //        CutOffset = 0;
            //        PauseProcess = false;
            //        _machine.FreezeVideoCapture();
            //        break;
            //    default:
            //        break;

            //}
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
        ~Process3()
        {
            Dispose();
        }
    }
}

