
//#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using netDxf;
using Microsoft.VisualStudio.Workspace;
using PropertyChanged;
using netDxf.Entities;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;

namespace DicingBlade.Classes
{
    public delegate void GetRotation(double angle, double time);
    public delegate void ChangeScreens(bool regime);

    internal enum Diagram
    {
        GoWaferStartX,
        GoWaferEndX,
        GoNextDepthZ,
        CuttingX,
        GoCameraPointXyz,
        GoOnWaferRightX,
        GoOnWaferLeftX,
        GoWaferCenterXy,
        GoNextCutY,
        GoNextCutXy,
        GoTransferingHeightZ,
        GoDockHeightZ,
        GoNextDirection,
        GoCameraPointLearningXyz,
        GoCurPositionCutXy
    }
    [Flags]
    internal enum Status
    {
        None = 1,
        StartLearning = 2,
        Learning = 4,
        Working = 8,
        Correcting = 16,
        Done = 32,
        MovingNextDir = 64,
        Ending = 128,
        Pause = 256
    }
    /// <summary>
    /// Структура параметров процесса
    /// </summary>
    internal struct TempWafer2D
    {
        //public bool Round;
        //public double XIndex;
        //public double XShift;
        //public double YIndex;
        //public double YShift;
        //public double XAngle;
        //public double YAngle;
        public bool FirstPointSet;
        public double[] Point1;
        public double[] Point2;
        public double GetAngle()
        {
            var tan = (Point2[1] - Point1[1]) / (Point2[0] - Point1[0]);
            var sign = Math.Sign(tan);
            var angle = Math.Atan(Math.Abs(tan))*180/Math.PI;
            return sign * angle;
        }
    }
    struct CheckCutControl
    {
        int startCut;
        int checkInterval;
        int currentCut;
        public bool Check;
        public void addToCurrentCut()
        {
            int res = 0;
            currentCut++;
            if (currentCut >= startCut)
            {
                Math.DivRem(currentCut - startCut, checkInterval, out res);
                Check = res == 0;
            }
            else
            {
                Check = false;
            }
        }
        public void Reset()
        {
            currentCut = 0;
        }
        public void Set(int start, int interval)
        {
            currentCut = 0;
            checkInterval = interval;
            startCut = start;
        }
    }
    //public delegate void SetPause(bool pause);
    [AddINotifyPropertyChangedInterface]
    internal class Process
    {
        public string ProcessMessage { get; set; } = "";
        public bool UserConfirmation { get; set; } = false;
        private readonly Wafer _wafer;
        private readonly Machine _machine;
        private readonly Blade _blade;
        private CheckCutControl _checkCut;
        public ChangeScreens ChangeScreensEvent;
        public Visibility TeachVScaleMarkersVisibility { get; set; } = Visibility.Hidden;
        public Visibility CutWidthMarkerVisibility { get; set; } = Visibility.Hidden;
        public Status ProcessStatus { get; set; }
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
        private bool BladeInWafer => _machine.Z.ActualPosition > _machine.ZBladeTouch - _wafer.Thickness - _bladeTransferGapZ;

        public int CurrentLine { get; private set; }
        //private double RotationSpeed { get; set; }
        private double FeedSpeed { get; set; }
        //private bool Aligned { get; set; }
        //private double OffsetAngle { get; set; }
        public GetRotation GetRotationEvent;
        public bool Rotation { get; set; } = false;
        public Process(Machine machine, Wafer wafer, Blade blade, ITechnology technology, Diagram[] proc) // В конструкторе происходит загрузка технологических параметров
        {
            _machine = machine;
            _wafer = wafer;
            _blade = blade;
            Traces = new ObservableCollection<TracePath>();
            TracesView = new WaferView();
            _baseProcess = proc;
            CancelProcess = false;
            FeedSpeed = PropContainer.Technology.FeedSpeed;
            _machine.OnAirWanished += Machine_OnAirWanished;
            _machine.OnCoolWaterWanished += Machine_OnCoolWaterWanished;
            _machine.OnSpinWaterWanished += Machine_OnSpinWaterWanished;
            _machine.OnVacuumWanished += Machine_OnVacuumWanished;
            _checkCut.Set(1, 2);
        }
        public async Task PauseScenarioAsync()
        {
            await _machine.X.WaitUntilStopAsync();
            //Machine.EmgStop();
            await ProcElementDispatcherAsync(Diagram.GoCameraPointXyz);
        }
        public async Task DoProcessAsync(Diagram[] diagrams)
        {
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
                    time = Math.Abs(angle - _machine.U.ActualPosition) / _machine.U.GetVelocity();
                }
                else
                {
                    angle = _wafer.GetCurrentDiretionActualAngle;
                    time = Math.Abs(_wafer.GetCurrentDiretionActualAngle - _machine.U.ActualPosition) / _machine.U.GetVelocity();
                }
                Rotation = true;
                GetRotationEvent(deltaAngle, time);
                await _machine.U.MoveAxisInPosAsync(angle);
                Rotation = false;
            }

        }
        private async Task MovePrevDirAsync()
        {
            if (_wafer.PrevDir())
            {
                await _machine.U.MoveAxisInPosAsync(_wafer.GetCurrentDiretionActualAngle);
            }
        }
        private void PrevLine()
        {
            if (CurrentLine > 0) CurrentLine--;
        }
        public async Task ToTeachVideoScale()
        {
            TeachVScaleMarkersVisibility = Visibility.Hidden;
            ProcessMessage = "Подведите ориентир к одному из визиров и нажмите *";
            await WaitForConfirmationAsync();
            var y = _machine.Y.ActualPosition;
            ProcessMessage = "Подведите ориентир ко второму визиру и нажмите *";
            await WaitForConfirmationAsync();
            _machine.CameraScale = _machine.TeachMarkersRatio / Math.Abs(y - _machine.Y.ActualPosition);
            ProcessMessage = "";
            TeachVScaleMarkersVisibility = Visibility.Hidden;
        }
        public async Task ToTeachChipSizeAsync()
        {
            if (MessageBox.Show("Обучить размер кристалла?", "Обучение", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
            {
                ProcessMessage = "Подведите ориентир к перекрестию и нажмите *";
                await WaitForConfirmationAsync();
                var y = _machine.Y.ActualPosition;
                ProcessMessage = "Подведите следующий ориентир к перекрестию и нажмите *";
                await WaitForConfirmationAsync();
                var size = Math.Round(Math.Abs(y - _machine.Y.ActualPosition), 3);
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
                    Thread.Sleep(1);
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
            switch (element)
            {
                case Diagram.GoWaferStartX:
                    if (BladeInWafer) break;
                    _machine.SetVelocity(Velocity.Service);
                    target = _machine.CtoBSystemCoors(_wafer.GetCurrentLine(CurrentLine).start);
                    double xGap = _blade.XGap(_wafer.Thickness);
                    await _machine.X.MoveAxisInPosAsync(target.X + xGap);
                    break;
                case Diagram.GoWaferEndX:
                    if (BladeInWafer) break;
                    _machine.SetVelocity(Velocity.Service);
                    await _machine.X.MoveAxisInPosAsync(_wafer.GetCurrentLine(CurrentLine).end.X + _machine.BladeChuckCenter.X);
                    break;
                case Diagram.GoNextDepthZ:
                    _machine.SetVelocity(Velocity.Service);
                    if (_wafer.CurrentCutIsDone(CurrentLine)) break;
                    await _machine.Z.MoveAxisInPosAsync(_machine.ZBladeTouch - _wafer.GetCurrentCutZ(CurrentLine));
                    break;
                case Diagram.CuttingX:
                    _machine.SwitchOnCoolantWater = true;
                    _machine.X.SetVelocity(FeedSpeed);
                    IsCutting = true;

                    target = _machine.CtoBSystemCoors(_wafer.GetCurrentLine(CurrentLine).end);
                    var traceX = _machine.X.ActualPosition;
                    var traceY = _machine.Y.ActualPosition;
                    var angle = _machine.U.ActualPosition;
                    Thread tracingThread = new Thread(new ThreadStart(() =>
                    {
                        do
                        {
                            TracingLine = new TracePath(traceY, traceX, _machine.X.ActualPosition, angle);
                            Thread.Sleep(1);
                        } while (IsCutting);
                    }));
                    tracingThread.Start();
                    await _machine.X.MoveAxisInPosAsync(target.X);
                    IsCutting = false;


                    //traces.Add(TracingLine);

                    RotateTransform rotateTransform = new RotateTransform(
                        -_wafer.GetCurrentDiretionAngle,
                        _machine.BladeChuckCenter.X,
                        _machine.BladeChuckCenter.Y
                        );

                    var point1 = rotateTransform.Transform(new System.Windows.Point(traceX, traceY + _wafer.GetCurrentDirectionIndexShift));
                    var point2 = rotateTransform.Transform(new System.Windows.Point(_machine.X.ActualPosition, traceY + _wafer.GetCurrentDirectionIndexShift));
                    point1 = new TranslateTransform(-_machine.BladeChuckCenter.X, -_machine.BladeChuckCenter.Y).Transform(point1);
                    point2 = new TranslateTransform(-_machine.BladeChuckCenter.X, -_machine.BladeChuckCenter.Y).Transform(point2);


                    TracesView.RawLines.Add(new Line2D(
                        new System.Windows.Point(point1.X, point1.Y),
                        new System.Windows.Point(point2.X, point2.Y)
                        ));
                    TracesView.RawLines = new ObservableCollection<Line2D>(TracesView.RawLines);
                    TracingLine = null;
                    _machine.SwitchOnCoolantWater = false;

                    if (!_wafer.CurrentCutIncrement(CurrentLine))
                    {
                        NextLine();
                    }
                    _checkCut.addToCurrentCut();
                    if (_checkCut.Check) await TakeThePhotoAsync();
                    break;
                case Diagram.GoCameraPointXyz:
                    _machine.SetVelocity(Velocity.Service);
                    await _machine.Z.MoveAxisInPosAsync(_machine.ZBladeTouch - _wafer.Thickness - _bladeTransferGapZ);
                    await _machine.MoveInPosXyAsync(new Vector2(
                        _machine.CameraChuckCenter.X,
                        _machine.CtoBSystemCoors(_wafer.GetCurrentLine(CurrentLine != 0 ? CurrentLine - 1 : 0).start).Y - _machine.CameraBladeOffset
                        ));
                    await _machine.Z.MoveAxisInPosAsync(_machine.CameraFocus);
                    break;
                case Diagram.GoOnWaferRightX:
                    if (BladeInWafer) break;
                    _machine.SetVelocity(Velocity.Service);
                    await _machine.X.MoveAxisInPosAsync(_wafer.GetNearestCut(_machine.Y.ActualPosition - _machine.CameraChuckCenter.Y).EndPoint.X);
                    break;
                case Diagram.GoOnWaferLeftX:
                    if (BladeInWafer) break;
                    _machine.SetVelocity(Velocity.Service);
                    await _machine.X.MoveAxisInPosAsync(_wafer.GetNearestCut(_machine.Y.ActualPosition - _machine.CameraChuckCenter.Y).StartPoint.X);
                    break;
                case Diagram.GoWaferCenterXy:
                    if (BladeInWafer) break;
                    _machine.SetVelocity(Velocity.Service);

                    await _machine.MoveInPosXyAsync(_machine.CameraChuckCenter);
                    break;
                case Diagram.GoNextCutY:
                    if (BladeInWafer) break;
                    _machine.SetVelocity(Velocity.Service);
                    await _machine.Y.MoveAxisInPosAsync(_machine.CtoBSystemCoors(_wafer.GetCurrentLine(CurrentLine).start).Y);
                    break;
                case Diagram.GoNextCutXy:
                    // if (BladeInWafer) break;
                    _machine.SetVelocity(Velocity.Service);
                    target = _machine.CtoBSystemCoors(_wafer.GetCurrentLine(CurrentLine).start);
                    target.X -= _blade.XGap(_wafer.Thickness);
                    await _machine.MoveInPosXyAsync(target);
                    break;
                case Diagram.GoTransferingHeightZ:
                    _machine.SetVelocity(Velocity.Service);
                    await _machine.Z.MoveAxisInPosAsync(_machine.ZBladeTouch - _wafer.Thickness - _bladeTransferGapZ);
                    break;
                case Diagram.GoDockHeightZ:
                    _machine.SetVelocity(Velocity.Service);
                    await _machine.Z.MoveAxisInPosAsync(1);
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
                    await _machine.Z.MoveAxisInPosAsync(_machine.ZBladeTouch - _wafer.Thickness - _bladeTransferGapZ);

                    var y = _wafer.GetNearestCut(0).StartPoint.Y;
                    var point = _machine.CtoCSystemCoors(new Vector2(0, _wafer.GetNearestCut(0).StartPoint.Y));

                    await _machine.MoveInPosXyAsync(point);
                    await _machine.Z.MoveAxisInPosAsync(/*Machine.CameraFocus*/3.5);
                    break;
                default:
                    break;
            }
        }
        private async Task TakeThePhotoAsync()
        {
            _machine.GetSnapShot = false;
            await ProcElementDispatcherAsync(Diagram.GoTransferingHeightZ);
            await ProcElementDispatcherAsync(Diagram.GoCameraPointXyz);
            _machine.SwitchOnBlowing = true;
            Thread.Sleep(100);
            _machine.GetSnapShot = true;
            _machine.SwitchOnBlowing = false;
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
                    _wafer.AddToCurrentDirectionIndexShift = _machine.CoSystemCurrentCoors.Y - _wafer.GetNearestCut(_machine.CoSystemCurrentCoors.Y).StartPoint.Y;
                    _wafer.SetCurrentDirectionAngle = _machine.U.ActualPosition;
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
                    ChangeScreensEvent(true);
                    ProcessStatus = Status.Correcting;
                    _machine.GetSnapShot = false;
                    break;
                case Status.Correcting:
                    var result = MessageBox.Show($"Сместить следующие резы на {CutOffset} мм?", "", MessageBoxButton.OKCancel);
                    if (result == MessageBoxResult.OK) _wafer.AddToCurrentDirectionIndexShift = CutOffset;
                    ChangeScreensEvent(false);
                    ProcessStatus = Status.Working;
                    CutWidthMarkerVisibility = Visibility.Hidden;
                    CutOffset = 0;
                    PauseProcess = false;
                    _machine.GetSnapShot = true;
                    break;
                default:
                    break;

            }
        }
        private void Machine_OnVacuumWanished(/*DIEventArgs eventArgs*/)
        {
            if (IsCutting) { }
            //throw new NotImplementedException();
        }
        private void Machine_OnSpinWaterWanished(/*DIEventArgs eventArgs*/)
        {
            if (IsCutting) { }
            //throw new NotImplementedException();
        }
        private void Machine_OnCoolWaterWanished(/*DIEventArgs eventArgs*/)
        {
            if (IsCutting) { }
            //throw new NotImplementedException();
        }
        private void Machine_OnAirWanished(/*DIEventArgs eventArgs*/)
        {
            if (IsCutting) { }
            //throw new NotImplementedException();
        }
    }
}
