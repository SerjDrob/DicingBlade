
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
    //public delegate void GetRotation(double angle, double time);
    //public delegate void ChangeScreens(bool regime);

    //internal enum Diagram
    //{
    //    GoWaferStartX,
    //    GoWaferEndX,
    //    GoNextDepthZ,
    //    CuttingX,
    //    GoCameraPointXyz,
    //    GoOnWaferRightX,
    //    GoOnWaferLeftX,
    //    GoWaferCenterXy,
    //    GoNextCutY,
    //    GoNextCutXy,
    //    GoTransferingHeightZ,
    //    GoDockHeightZ,
    //    GoNextDirection,
    //    GoCameraPointLearningXyz
    //}

    //internal enum Status
    //{
    //    StartLearning,
    //    Learning,
    //    Working,
    //    Correcting,
    //    Done
    //}
    /// <summary>
    /// Структура параметров процесса
    /// </summary>
    //internal struct TempWafer2D
    //{
    //    //public bool Round;
    //    //public double XIndex;
    //    //public double XShift;
    //    //public double YIndex;
    //    //public double YShift;
    //    //public double XAngle;
    //    //public double YAngle;
    //    public bool FirstPointSet;
    //    public Vector2 Point1;
    //    public Vector2 Point2;
    //    public double GetAngle()
    //    {
    //        return Math.Atan2(Point2.Y - Point1.Y, Point2.X - Point1.X);
    //    }
    //}
    //struct CheckCutControl
    //{
    //    int startCut;
    //    int checkInterval;
    //    int currentCut;
    //    public bool addToCurrentCut()
    //    {
    //        int res = 0;
    //        currentCut++;
    //        if (currentCut >= startCut)
    //        {
    //            Math.DivRem(currentCut - startCut, checkInterval, out res);
    //            if (res == 0) return true;
    //            else return false;
    //        }
    //        else return false;
    //    }
    //    public void Reset()
    //    {
    //        currentCut = 0;
    //    }
    //    public void Set(int start, int interval)
    //    {
    //        currentCut = 0;
    //        checkInterval = interval;
    //        startCut = start;
    //    }
    //}
    //public delegate void SetPause(bool pause);
    [AddINotifyPropertyChangedInterface]
    internal class Process2:IMessager, IDisposable
    {
        public Process2(IMachine machine, Wafer wafer, Blade blade, ITechnology technology, Diagram[] proc) // В конструкторе происходит загрузка технологических параметров
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
                throw new ProcessException("Не включен шпиндель");
            }
            Traces = new ObservableCollection<TracePath>();
            TracesView = new WaferView();
            _baseProcess = proc;
            CancelProcess = false;
           // FeedSpeed = PropContainer.Technology.FeedSpeed;
            
            _checkCut.Set(technology.StartControlNum, technology.ControlPeriod);
            _machine.GoThereAsync(Place.CameraChuckCenter).Wait();
            ProcessStatus = Status.StartLearning;
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
        public TracePath TracingLine { get; set; }
        public WaferView TracesView { get; set; }
        public ObservableCollection<TracePath> Traces { get; set; }
        public double CutWidth { get; set; } = 0.05;
        public double CutOffset { get; set; } = 0;
        private List<TracePath> _traces;
        private double _bladeTransferGapZ /*{ get; set; }*/ = 1;
        private bool IsCutting { get; set; } = false;
        private bool InProcess { get; set; } = false;
        public bool CutsRotate { get; set; } = true;
        //private bool procToken = true;
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
                await _machine.MoveAxInPosAsync(Ax.U,angle);
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
        public async Task ToTeachVideoScale()
        {
            //TeachVScaleMarkersVisibility = Visibility.Hidden;
            //ProcessMessage = "Подведите ориентир к одному из визиров и нажмите *";
            //await WaitForConfirmationAsync();
            //var y = _machine.Y.ActualPosition;
            //ProcessMessage = "Подведите ориентир ко второму визиру и нажмите *";
            //await WaitForConfirmationAsync();
            //_machine.CameraScale = _machine.TeachMarkersRatio / Math.Abs(y - _machine.Y.ActualPosition);
            //ProcessMessage = "";
            //TeachVScaleMarkersVisibility = Visibility.Hidden;
        }
        public async Task ToTeachChipSizeAsync()
        {
            if (MessageBox.Show("Обучить размер кристалла?", "Обучение", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
            {
                ProcessMessage = "Подведите ориентир к перекрестию и нажмите *";
                await WaitForConfirmationAsync();
                var y = _yActual;
                ProcessMessage = "Подведите следующий ориентир к перекрестию и нажмите *";
                await WaitForConfirmationAsync();
                var size = Math.Round(Math.Abs(y - _yActual), 3);
                ProcessMessage = "";
                if (MessageBox.Show($"\rНовый размер кристалла {size} мм.\n Запомнить?", "Обучение", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                {
                    PropContainer.WaferTemp.IndexH = size;//currentIndex?
                }
            }
        }
        private async Task WaitForConfirmationAsync()
        {
            UserConfirmation = false;
            await Task.Run(() =>
            {
                while (!UserConfirmation)
                {
                    Task.Delay(1).Wait();
                }
            });
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
                    z = _machine.TranslateSpecCoor(Place.ZBladeTouch, _wafer.GetCurrentCutZ(CurrentLine),Ax.Z);
                    await _machine.MoveAxInPosAsync(Ax.Z, z);
                    break;
                case Diagram.CuttingX:
                    _machine.SwitchOnValve(Valves.Coolant);
                    await Task.Delay(300).ConfigureAwait(false);                   
                    _machine.SetAxFeedSpeed(Ax.X, _feedSpeed);
                    IsCutting = true;                   
                   
                    x = _machine.TranslateSpecCoor(Place.BladeChuckCenter, - xCurLineEnd, 0);
                    //var traceX = _xActual;
                    //var traceY = _xActual;
                    //var angle = _uActual;
                    ////Thread tracingThread = new Thread(new ThreadStart(() =>
                    ////{
                    ////    do
                    ////    {
                    ////        TracingLine = new TracePath(traceY, traceX, _xActual, angle);
                    ////        await Task.Delay(1).ConfigureAwait(false);
                    ////    } while (IsCutting);
                    ////}));
                    //Task.Run(() =>
                    //{
                    //    do
                    //    {
                    //        TracingLine = new TracePath(traceY, traceX, _xActual, angle);
                    //        Task.Delay(1).Wait();
                    //    } while (IsCutting);
                    //}
                    //);
                    //tracingThread.Start();
                    BladeTracingEvent(true);
                    await _machine.MoveAxInPosAsync(Ax.X, x);
                    BladeTracingEvent(false);
                    IsCutting = false;


                    

                    //RotateTransform rotateTransform = new RotateTransform(
                    //    -_wafer.GetCurrentDiretionAngle,
                    //    _machine.BladeChuckCenter.X,
                    //    _machine.BladeChuckCenter.Y
                    //    );

                    //var point1 = rotateTransform.Transform(new System.Windows.Point(traceX, traceY + _wafer.GetCurrentDirectionIndexShift));
                    //var point2 = rotateTransform.Transform(new System.Windows.Point(_machine.X.ActualPosition, traceY + _wafer.GetCurrentDirectionIndexShift));
                    //point1 = new TranslateTransform(-_machine.BladeChuckCenter.X, -_machine.BladeChuckCenter.Y).Transform(point1);
                    //point2 = new TranslateTransform(-_machine.BladeChuckCenter.X, -_machine.BladeChuckCenter.Y).Transform(point2);


                    //TracesView.RawLines.Add(new Line(
                    //    new Vector2(point1.X, point1.Y),
                    //    new Vector2(point2.X, point2.Y)
                    //    ));
                    //TracesView.RawLines = new ObservableCollection<Line>(TracesView.RawLines);
                    
                    
                    //TracingLine = null;


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
                    y = _machine.TranslateSpecCoor(Place.BladeChuckCenter,-y,Ax.Y);
                    await _machine.MoveGpInPosAsync(Groups.XY, new double[] { x, y }, true);


                    await _machine.MoveAxInPosAsync(Ax.Z, _machine.GetFeature(MFeatures.CameraFocus));
                    break;
                case Diagram.GoOnWaferRightX:
                    if (BladeInWafer) break;
                    _machine.SetVelocity(Velocity.Service);
                    x = _wafer.GetNearestCut(_yActual - _machine.GetGeometry(Place.CameraChuckCenter,Ax.Y)).EndPoint.X;
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
                    var arr = _machine.TranslateActualCoors(Place.BladeChuckCenter, new (Ax,double)[] {(Ax.X, -x),(Ax.Y, -y) });                    
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
                    arr = _machine.TranslateActualCoors(Place.CameraChuckCenter, new (Ax,double)[] { (Ax.X, 0), (Ax.Y,-y) });
                    var point = new double[] { arr.GetVal(Ax.X), arr.GetVal(Ax.Y) };
                    await _machine.MoveGpInPosAsync(Groups.XY, point);                    
                    await _machine.MoveAxInPosAsync( Ax.Z, /*Machine.CameraFocus*/3.5);
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
            switch (ProcessStatus)
            {
                case Status.StartLearning:
                    await ProcElementDispatcherAsync(Diagram.GoCameraPointLearningXyz);
                    ProcessStatus = Status.Learning;
                    break;
                case Status.Learning:
                    var y = _machine.TranslateActualCoors(Place.CameraChuckCenter,Ax.Y);
                    _wafer.AddToCurrentDirectionIndexShift = y - _wafer.GetNearestCut(y).StartPoint.Y;
                    _wafer.SetCurrentDirectionAngle = _uActual;
                    if (_wafer.NextDir())
                    {
                        _machine.SetVelocity(Velocity.Service);
                        await MoveNextDirAsync(false);
                        await ProcElementDispatcherAsync(Diagram.GoCameraPointLearningXyz);
                    }
                    else
                    {
                        ProcessStatus = Status.Working;

                        _traces = new List<TracePath>();

                        DoProcessAsync(_baseProcess);
                    }
                    break;
                case Status.Working:
                    PauseProcess = true;
                    if (PauseProcess) await PauseScenarioAsync();
                    CutWidthMarkerVisibility = Visibility.Visible;
                    ChangeScreensEvent?.Invoke(true);
                    ProcessStatus = Status.Correcting;
                    _machine.StartVideoCapture(0);
                    break;
                case Status.Correcting:
                    var result = MessageBox.Show($"Сместить следующие резы на {CutOffset} мм?", "", MessageBoxButton.OKCancel);
                    if (result == MessageBoxResult.OK) _wafer.AddToCurrentDirectionIndexShift = CutOffset;
                    ChangeScreensEvent?.Invoke(false);
                    ProcessStatus = Status.Working;
                    CutWidthMarkerVisibility = Visibility.Hidden;
                    CutOffset = 0;
                    PauseProcess = false;
                    _machine.FreezeVideoCapture();
                    break;
                default:
                    break;

            }
        }
        private void Machine_OnVacuumWanished(Sensors sensor, bool state)
        {
            if(sensor == Sensors.ChuckVacuum & !state)
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
        ~Process2()
        {
            Dispose();
        }
    }
}
