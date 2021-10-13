#define Proc5
using Advantech.Motion;
using DicingBlade.Classes;
using DicingBlade.Classes.BehaviourTrees;
using DicingBlade.Classes.Test;
using DicingBlade.Properties;
using DicingBlade.Views;
using netDxf;
using netDxf.Entities;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Point = System.Windows.Point;

namespace DicingBlade.ViewModels
{
    [AddINotifyPropertyChangedInterface]
    internal class MainViewModel : IMainViewModel
    {
        private readonly ExceptionsAgregator _exceptionsAgregator;
        private readonly IMachine _machine;
        private double _cameraScale;
        private bool _homeDone;
        private ITechnology _technology;

        private IComSensor _flowMeter;

        private Task _tracingTask;
        private CancellationToken _tracingTaskCancellationToken;

        private CancellationTokenSource _tracingTaskCancellationTokenSource;
        private WatchSettingsService _settingsService;

        [Obsolete("Only for design data", true)]
        public MainViewModel()
            : this(null, null)
        {
            if ((bool)DesignerProperties.IsInDesignModeProperty.GetMetadata(typeof(DependencyObject)).DefaultValue)
            {
                throw new Exception("Use only for design mode");
            }
        }

        public MainViewModel(ExceptionsAgregator exceptionsAgregator, IMachine machine)
        {
            _exceptionsAgregator = exceptionsAgregator;

            Test = false;
            Cols = new[] { 0, 1 };
            Rows = new[] { 2, 1 };

            OpenFileCmd = new Command(args => OpenFile());
            ChangeCmd = new Command(args => Change());
            KeyDownCmd = new Command(args => KeyDownAsync(args));
            KeyUpCmd = new Command(args => KeyUp(args));
            WaferSettingsCmd = new Command(args => WaferSettings());
            MachineSettingsCmd = new Command(args => MachineSettings());
            TechnologySettingsCmd = new Command(args => TechnologySettings());
            ToTeachChipSizeCmd = new Command(args => ToTeachChipSizeAsync());
            ToTeachVideoScaleCmd = new Command(args => ToTeachVideoScaleAsync());
            ToTeachCutShiftCmd = new Command(args => ToTeachCutShift());
            TestCmd = new Command(args => Func(args));
            ClickOnImageCmd = new Command(args => ClickOnImage(args));
            LeftClickOnWaferCmd = new Command(args => LeftClickOnWafer(args));
            RightClickOnWaferCmd = new Command(args => RightClickOnWafer(args));
            CoolantValveOnCmd = new Command(args => CoolantValveOn());

            Bi = new BitmapImage();

            _exceptionsAgregator.SetShowMethod(s => { MessageBox.Show(s); });
            _exceptionsAgregator.SetShowMethod(s => { ProcessMessage = s; });
            _cameraScale = Settings.Default.CameraScale;

            try
            {
                _technology = StatMethods.DeSerializeObjectJson<Technology>(Settings.Default.TechnologyLastFile);

                _machine = machine;

                _machine.ConfigureAxes(new (Ax, double)[]
                {
                    (Ax.X, 0),
                    (Ax.U, 0),
                    (Ax.Z, 0),
                    (Ax.Y, 12.8)
                });

                _machine.ConfigureAxesGroups(new Dictionary<Groups, Ax[]>
                {
                    {Groups.XY, new[] {Ax.X, Ax.Y}}
                });

                _machine.ConfigureValves(new Dictionary<Valves, (Ax, Do)>
                {
                    {Valves.Blowing, (Ax.Z, Do.Out6)},
                    {Valves.ChuckVacuum, (Ax.Z, Do.Out4)},
                    {Valves.Coolant, (Ax.U, Do.Out4)},
                    {Valves.SpindleContact, (Ax.U, Do.Out5)}
                });

                _machine.SwitchOffValve(Valves.Blowing);
                _machine.SwitchOffValve(Valves.ChuckVacuum);
                _machine.SwitchOffValve(Valves.Coolant);
                _machine.SwitchOffValve(Valves.SpindleContact);

                _machine.ConfigureSensors(new Dictionary<Sensors, (Ax, Di, Boolean, string)>
                {
                    {Sensors.Air, (Ax.Z, Di.In1, false, "Воздух")},
                    {Sensors.ChuckVacuum, (Ax.X, Di.In2, false, "Вакуум")},
                    {Sensors.Coolant, (Ax.U, Di.In2, false, "СОЖ")},
                    {Sensors.SpindleCoolant, (Ax.Y, Di.In2, false, "Охлаждение шпинделя")}
                });



                ImplementMachineSettings();
                _machine.StartVideoCapture(0);
                _machine.OnVideoSourceBmpChanged += _machine_OnVideoSourceBmpChanged;
                _machine.OnAxisMotionStateChanged += _machine_OnAxisMotionStateChanged;
                _machine.OnSensorStateChanged += _machine_OnAxisSensorStateChanged;
                _machine.OnValveStateChanged += _machine_OnAxisValveStateChanged;
                _machine.OnSpindleStateChanging += _machine_OnSpindleStateChanging;
            }
            catch (MotionException ex)
            {
                MessageBox.Show(ex.Message, ex.StackTrace);
            }


            BaseProcess = new[]
            {
                Diagram.GoNextCutXy,
                Diagram.GoNextDepthZ,
                Diagram.CuttingX,
                Diagram.GoTransferingHeightZ,
                Diagram.GoNextDirection
            };
            _settingsService = new();
            _settingsService.OnSettingsChangedEvent += _settingsService_OnSettingsChangedEvent;
            AjustWaferTechnology();
            _flowMeter = new FlowMeter("COM9");
            _flowMeter.GetData += _flowMeter_GetData;
        }

        private void CoolantValveOn()
        {
            CoolantValveView ^= true;
        }

        public double Flow { get; set; }
        private void _flowMeter_GetData(decimal obj)
        {
            Flow = (double)obj;
        }

        private void _settingsService_OnSettingsChangedEvent(object sender, SettingsChangedEventArgs eventArgs)
        {

            if (eventArgs.Settings is IWafer)
            {
                var wf = (IWafer)eventArgs.Settings;
                var substrate = new Substrate2D(wf.IndexH, wf.IndexW, wf.Thickness, new Rectangle2D(wf.Height, wf.Width));
                if (Process is null)
                {
                    Substrate = substrate;
                }
                else
                {
                    substrate.SetSide(Process.CurrentDirection);
                }

                var wfViewFactory = new WaferViewFactory(substrate);
                ResetView ^= true;
                WaferView = new();
                WaferView.SetView(wfViewFactory);
            }
        }

        private Process5 Process5 { get; set; }

        public Velocity VelocityRegime { get; set; } = Velocity.Fast;
        public ObservableCollection<TraceLine> TracesCollectionView { get; set; } = new();
        public double XTrace { get; set; }
        public double YTrace { get; set; }
        public double XTraceEnd { get; set; }
        public BitmapImage Bi { get; set; }
        public double XView { get; set; }
        public double YView { get; set; }
        public double ZView { get; set; }
        public double UView { get; set; }
        public bool XpLmtView { get; set; }
        public bool YpLmtView { get; set; }
        public bool ZpLmtView { get; set; }
        public bool UpLmtView { get; set; }
        public bool XnLmtView { get; set; }
        public bool YnLmtView { get; set; }
        public bool ZnLmtView { get; set; }
        public bool UnLmtView { get; set; }
        public bool XMotDoneView { get; set; }
        public bool YMotDoneView { get; set; }
        public bool ZMotDoneView { get; set; }
        public bool UMotDoneView { get; set; }
        public bool ChuckVacuumValveView { get; set; }
        public bool CoolantValveView { get; set; }
        public bool BlowingValveView { get; set; }
        public bool ChuckVacuumSensorView { get; set; }
        public bool CoolantSensorView { get; set; }
        public bool TestBool { get; set; } = true;
        public bool AirSensorView { get; set; }
        public double CoolantRateView { get; set; }
        public bool SpindleCoolantSensorView { get; set; }
        public double BCCenterXView { get; set; }
        public double BCCenterYView { get; set; }
        public double CCCenterXView { get; set; }
        public double CCCenterYView { get; set; }
        public double ZBladeTouchView { get; set; }
        public int SpindleFreqView { get; set; }
        public double SpindleCurrentView { get; set; }
        public double WaferCurrentShiftView { get; set; }
        public ObservableCollection<TraceLine> ControlPointsView { get; set; } = new();
        public bool ResetView { get; private set; }
        public bool SpindleOnFreq { get; private set; }
        public bool SpindleAccelarating { get; private set; }
        public bool SpindleDeccelarating { get; private set; }
        public bool SpindleStops { get; private set; }
        public double PointX { get; set; }
        public double PointY { get; set; }
        public double CutWidthView { get; set; } = 0.05;
        public double RealCutWidthView { get; set; } = 0.1;
        public Process4 Process { get; set; }
        public Wafer Wafer { get; set; }
        public Wafer2D Substrate { get; private set; }
        public WaferView WaferView { get; set; }
        public ObservableCollection<TracePath> Traces { get; set; }
        public double WvAngle { get; set; }
        public bool WvRotate { get; set; }
        public double RotatingTime { get; set; } = 1;
        public Map Condition { get; set; }
        public double Thickness { get; set; } = 1;
        public bool Test { get; set; }
        public int[] Rows { get; set; }

        public int[] Cols { get; set; }

        private Diagram[] BaseProcess { get; }
        public ICommand OpenFileCmd { get; }
        public ICommand RotateCmd { get; }
        public ICommand ChangeCmd { get; }
        public ICommand KeyDownCmd { get; }
        public ICommand KeyUpCmd { get; }
        public ICommand WaferSettingsCmd { get; }
        public ICommand MachineSettingsCmd { get; }
        public ICommand TechnologySettingsCmd { get; }
        public ICommand ToTeachChipSizeCmd { get; }
        public ICommand ToTeachVideoScaleCmd { get; }
        public ICommand TestCmd { get; }
        public ICommand ClickOnImageCmd { get; }
        public ICommand LeftClickOnWaferCmd { get; }
        public ICommand RightClickOnWaferCmd { get; }
        public ICommand ToTeachCutShiftCmd { get; }
        public ICommand CoolantValveOnCmd { get; }

        public Visibility TeachVScaleMarkersVisibility { get; private set; } = Visibility.Hidden;
        public string ProcessMessage { get; private set; }
        public string ProcessStatus { get; private set; }
        public bool UserConfirmation { get; private set; }
        public double TeachMarkersRatio { get; } = 2;
        public bool MachineIsStillView { get; private set; }

        private async Task ToTeachCutShift()
        {
            if (MessageBox.Show("Обучить смещение реза от ТВ?", "Обучение", MessageBoxButton.OKCancel) ==
                MessageBoxResult.OK)
            {
                ProcessMessage = "Совместите горизонтальный визир с центром последнего реза и нажмите *";
                await WaitForConfirmationAsync();
                Process?.TeachDiskShift();
                _machine.ConfigureGeometry(new Dictionary<Place, (Ax, double)[]>
                {
                    {
                        Place.BladeChuckCenter,
                        new[]
                        {
                            (Ax.X, Settings.Default.XDisk),
                            (Ax.Y, Settings.Default.YObjective + Settings.Default.DiskShift)
                        }
                    }
                });
                ProcessMessage = "";
            }
        }

        private void _machine_OnSpindleStateChanging(object? obj, SpindleEventArgs e)
        {
            SpindleFreqView = e.Rpm;
            SpindleCurrentView = e.Current;
            SpindleOnFreq = e.OnFreq;
            SpindleAccelarating = e.Accelerating;
            SpindleDeccelarating = e.Deccelarating;
            SpindleStops = e.Stop;
        }

        private void _machine_OnVideoSourceBmpChanged(BitmapImage bitmapImage)
        {
            Bi = bitmapImage;
            //var tmp = Bi;
            //if (tmp?.StreamSource != null)
            //{
            //    await tmp.StreamSource.DisposeAsync().ConfigureAwait(false);
            //}
        }

        private void _machine_OnAxisValveStateChanged(Valves valve, bool state)
        {
            switch (valve)
            {
                case Valves.Blowing:
                    BlowingValveView = state;
                    break;
                case Valves.Coolant:
                    CoolantValveView = state;
                    break;
                case Valves.ChuckVacuum:
                    ChuckVacuumValveView = state;
                    break;
            }
        }

        private void _machine_OnAxisSensorStateChanged(Sensors sensor, bool state)
        {
            switch (sensor)
            {
                case Sensors.ChuckVacuum:
                    ChuckVacuumSensorView = state;
                    break;
                case Sensors.Air:
                    AirSensorView = state;
                    break;
                case Sensors.Coolant:
                    CoolantSensorView = state;
                    break;
                case Sensors.SpindleCoolant:
                    SpindleCoolantSensorView = state;
                    break;
            }
        }

        private void _machine_OnAxisMotionStateChanged(Ax axis, double position, bool nLmt, bool pLmt, bool motionDone,
            bool motionStart)
        {
            position = Math.Round(position, 3);
            switch (axis)
            {
                case Ax.X:
                    XView = position;
                    XpLmtView = pLmt;
                    XnLmtView = nLmt;
                    XMotDoneView = motionStart ? true : motionDone ? false : XMotDoneView;
                    break;
                case Ax.Y:
                    YView = position;
                    YpLmtView = pLmt;
                    YnLmtView = nLmt;
                    YMotDoneView = motionStart ? true : motionDone ? false : YMotDoneView;
                    break;
                case Ax.Z:
                    ZView = position;
                    ZpLmtView = pLmt;
                    ZnLmtView = nLmt;
                    ZMotDoneView = motionStart ? true : motionDone ? false : ZMotDoneView;
                    break;
                case Ax.U:
                    UView = position;
                    UpLmtView = pLmt;
                    UnLmtView = nLmt;
                    UMotDoneView = motionStart ? true : motionDone ? false : UMotDoneView;
                    break;
            }

            MachineIsStillView = XMotDoneView & YMotDoneView & ZMotDoneView & UMotDoneView;
        }

        private async Task ClickOnImage(object o)
        {
            var point = (Point)o;
            PointX = XView - point.X * _cameraScale;
            PointY = YView + point.Y * _cameraScale;
            _machine.SetVelocity(Velocity.Service);
            _machine.MoveAxInPosAsync(Ax.X, PointX);
            _machine.MoveAxInPosAsync(Ax.Y, PointY, true);
        }

        private async Task LeftClickOnWafer(object o)
        {
            var points = (Point[])o;

            if (WaferView.ShapeSize[0] > WaferView.ShapeSize[1])
            {
                PointX = points[0].X * 1.4 * WaferView.ShapeSize[0];
                PointY = points[0].Y * 1.4 * WaferView.ShapeSize[0];
            }
            else
            {
                PointX = points[1].X * 1.4 * WaferView.ShapeSize[1];
                PointY = points[1].Y * 1.4 * WaferView.ShapeSize[1];
            }

            PointX = _machine.TranslateSpecCoor(Place.CameraChuckCenter, -PointX, Ax.X);
            PointY = _machine.TranslateSpecCoor(Place.CameraChuckCenter, -PointY, Ax.Y);

            _machine.SetVelocity(Velocity.Service);
            _machine.MoveAxInPosAsync(Ax.X, PointX);
            _machine.MoveAxInPosAsync(Ax.Y, PointY);
        }

        private async Task RightClickOnWafer(object o)
        {
            var points = (Point[])o;

            if (WaferView.ShapeSize[0] > WaferView.ShapeSize[1])
            {
                PointX = points[0].X * 1.4 * WaferView.ShapeSize[0];
                PointY = points[0].Y * 1.4 * WaferView.ShapeSize[0];
            }
            else
            {
                PointX = points[1].X * 1.4 * WaferView.ShapeSize[1];
                PointY = points[1].Y * 1.4 * WaferView.ShapeSize[1];
            }

            PointX = _machine.TranslateSpecCoor(Place.CameraChuckCenter, -PointX, Ax.X);
            PointY = _machine.TranslateSpecCoor(Place.CameraChuckCenter, -Substrate.GetNearestY(PointY), Ax.Y);

            _machine.SetVelocity(Velocity.Service);
            _machine.MoveAxInPosAsync(Ax.X, PointX);
            _machine.MoveAxInPosAsync(Ax.Y, PointY);
        }

        private void SetRotation(double angle, double time)
        {
            WvAngle = angle;
            RotatingTime = time;
            WvRotate ^= true;
        }

        private void Machine_OnAirWanished(DiEventArgs eventArgs)
        {
            throw new NotImplementedException();
        }

        private void Func(object args)
        {
        }

        private void GetWvAngle(bool changing)
        {
        }

        private async Task ToTeachChipSizeAsync()
        {
            var i = Substrate.CurrentSide;

            if (MessageBox.Show("Обучить размер кристалла?", "Обучение", MessageBoxButton.OKCancel) ==
                MessageBoxResult.OK)
            {
                ProcessMessage = "Подведите ориентир к перекрестию и нажмите *";
                await WaitForConfirmationAsync();
                var y = YView;
                ProcessMessage = "Подведите следующий ориентир к перекрестию и нажмите *";
                await WaitForConfirmationAsync();
                var size = Math.Round(Math.Abs(y - YView), 3);
                ProcessMessage = "";

                if (MessageBox.Show($"\rНовый размер кристалла {size} мм.\n Запомнить?", "Обучение",
                    MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                {
                    var tempwafer = await StatMethods
                        .DeSerializeObjectJsonAsync<TempWafer>(Settings.Default.WaferLastFile).ConfigureAwait(false);
                    //PropContainer.WaferTemp.CurrentSide = Substrate.CurrentSide;
                    //PropContainer.WaferTemp.SetCurrentIndex(size);//currentIndex?
                    Substrate.SetCurrentIndex(size);
                    tempwafer.CurrentSide = Substrate.CurrentSide;
                    tempwafer.SetCurrentIndex(size);
                    //new TempWafer(PropContainer.WaferTemp).SerializeObjectJson(Settings.Default.WaferLastFile);
                    await tempwafer.SerializeObjectJsonAsync(Settings.Default.WaferLastFile).ConfigureAwait(false);
                    Wafer = new Wafer(new Vector2(0, 0), tempwafer.Thickness,
                        (0, tempwafer.Height, tempwafer.Width, tempwafer.IndexH),
                        (90, tempwafer.Width, tempwafer.Height, tempwafer.IndexW));
                    WaferView = Wafer.MakeWaferView();
                    //AjustWaferTechnology(Substrate.CurrentSide);
                }
            }
        }

        private async Task KeyDownAsync(object args)
        {
            var key = (KeyEventArgs)args;

            //test key
            if (key.Key == Key.Tab)
            {

            }

            if (key.Key == Key.Multiply)
            {
                UserConfirmation = true;
            }
            if (key.Key == Key.Q)
            {
                if (_machine.GetValveState(Valves.ChuckVacuum))
                    _machine.SwitchOffValve(Valves.ChuckVacuum);
                else
                    _machine.SwitchOnValve(Valves.ChuckVacuum);
            }

            if (key.Key == Key.W)
            {
                if (_machine.GetValveState(Valves.Coolant))
                    _machine.SwitchOffValve(Valves.Coolant);
                else
                    _machine.SwitchOnValve(Valves.Coolant);
            }

            if (key.Key == Key.R)
            {
                if (_machine.GetValveState(Valves.Blowing))
                    _machine.SwitchOffValve(Valves.Blowing);
                else
                    _machine.SwitchOnValve(Valves.Blowing);
            }

            if (key.Key == Key.D) _machine.GoWhile(Ax.U, AxDir.Pos);

            if (key.Key == Key.S) _machine.GoWhile(Ax.U, AxDir.Neg);
            if (key.Key == Key.F2)
            {
                if (SpindleAccelarating | SpindleOnFreq)
                    _machine.StopSpindle();
                else
                    try
                    {
                        _machine.SetSpindleFreq(_technology.SpindleFreq);
                        // TODO await
                        Task.Delay(100).Wait();
                        _machine.StartSpindle(Sensors.Air, Sensors.SpindleCoolant);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Запуск шпинделя");
                    }
            }

            if (key.Key == Key.F3)
            {
            }

            if (key.Key == Key.T) Change();
            if (key.Key == Key.Divide)
            {
#if Proc5
                if (_homeDone)
                {
                    if (Process5 is null)
                    {
                        var blade = new Blade();
                        blade.Diameter = 55.6;
                        blade.Thickness = 0.11;
                        Substrate.ResetWafer();
                        Process5 = new Process5(_machine, Substrate, blade, _technology);
                        Process5.GetRotationEvent += SetRotation;
                        Process5.ChangeScreensEvent += ChangeScreensRegime;
                        Process5.BladeTracingEvent += Process_BladeTracingEvent;
                        Process5.OnProcessStatusChanged += Process_OnProcessStatusChanged;
                        Process5.OnProcParamsChanged += Process_OnProcParamsChanged;
                        Process5.OnControlPointAppeared += Process_OnControlPointAppeared;
                        Process5.OnProcStatusChanged += Process5_OnProcStatusChanged;
                    }
                    else
                    {
                        Process5.StartPauseProc();
                    } 
                }
                else
                {
                    MessageBox.Show("Необходимо обнулить координаты. Нажмите клавишу Home");
                }
#else
                if (_homeDone)
                {
                    if (Process is null)
                    {
                        try
                        {
                            Process = new Process4(_machine, Substrate /*Wafer*/, new Blade(), _technology,
                                BaseProcess);
                            _exceptionsAgregator.RegisterMessager(Process);

                            Process.GetRotationEvent += SetRotation;
                            Process.ChangeScreensEvent += ChangeScreensRegime;
                            Process.BladeTracingEvent += Process_BladeTracingEvent;
                            Process.OnProcessStatusChanged += Process_OnProcessStatusChanged;
                            Process.OnProcParamsChanged += Process_OnProcParamsChanged;
                            Process.OnControlPointAppeared += Process_OnControlPointAppeared;
                            _settingsService.OnSettingsChangedEvent += Process.SubstrateChanged;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message);
                            Process = null;
                        }
                    }
                    else
                    {
                        if (Process.ProcessStatus == Status.Done)
                        {
                            await Process.WaitProcDoneAsync();
                            Process = null;
                            Substrate = null;
                            ResetWaferView();
                            AjustWaferTechnology();
                        }
                        else
                        {
                            try
                            {
                                await Process.StartPauseProc();
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"{ex.Message}\n{ex.StackTrace}");
                            }
                        }
                    }
                }
                else
                {
                    MessageBox.Show("Обнулись!");
                } 
#endif
            }

            if (key.Key == Key.A)
            {
                if (VelocityRegime == Velocity.Step)
                {
                    var velocity = VelocityRegime;
                    _machine.SetVelocity(Velocity.Service);
                    var y = YView - 0.03;
                    await _machine.MoveAxInPosAsync(Ax.Y, y + Substrate.CurrentIndex, true);
                    await Task.Delay(300);
                    await _machine.MoveAxInPosAsync(Ax.Y, y + 0.03 + Substrate.CurrentIndex, true);
                    _machine.SetVelocity(velocity);
                }
                else
                {
                    _machine.GoWhile(Ax.Y, AxDir.Pos);
                }
            }

            if (key.Key == Key.Z)
            {
                if (VelocityRegime == Velocity.Step)
                {
                    var velocity = VelocityRegime;
                    _machine.SetVelocity(Velocity.Service);
                    var y = YView - 0.03;
                    await _machine.MoveAxInPosAsync(Ax.Y, y - Substrate.CurrentIndex, true);
                    await Task.Delay(300);
                    await _machine.MoveAxInPosAsync(Ax.Y, y + 0.03 - Substrate.CurrentIndex, true);
                    _machine.SetVelocity(velocity);
                }
                else
                {
                    _machine.GoWhile(Ax.Y, AxDir.Neg);
                }
            }

            if (key.Key == Key.X) _machine.GoWhile(Ax.X, AxDir.Neg);
            if (key.Key == Key.C) _machine.GoWhile(Ax.X, AxDir.Pos);
            if (key.Key == Key.S) _machine.GoWhile(Ax.U, AxDir.Pos);
            if (key.Key == Key.D) _machine.GoWhile(Ax.U, AxDir.Neg);
            if (key.Key == Key.V) _machine.GoWhile(Ax.Z, AxDir.Pos);
            if (key.Key == Key.B) _machine.GoWhile(Ax.Z, AxDir.Neg);
            if (key.Key == Key.J)
            {
                var velocity = VelocityRegime != Velocity.Step ? VelocityRegime : Velocity.Slow;
                _machine.SetVelocity(Velocity.Service);
                var x = _machine.TranslateSpecCoor(Place.CameraChuckCenter, -Substrate.CurrentSideLength / 2, Ax.X);
                await _machine.MoveAxInPosAsync(Ax.X, x);
                _machine.SetVelocity(velocity);
            }

            if (key.Key == Key.K)
            {
                var velocity = VelocityRegime != Velocity.Step ? VelocityRegime : Velocity.Slow;
                _machine.SetVelocity(Velocity.Service);
                var x = _machine.TranslateSpecCoor(Place.CameraChuckCenter, 0, Ax.X);
                await _machine.MoveAxInPosAsync(Ax.X, x);
                _machine.SetVelocity(velocity);
            }

            if (key.Key == Key.L)
            {
                var velocity = VelocityRegime != Velocity.Step ? VelocityRegime : Velocity.Slow;
                _machine.SetVelocity(Velocity.Service);
                var x = _machine.TranslateSpecCoor(Place.CameraChuckCenter, Substrate.CurrentSideLength / 2, Ax.X);
                await _machine.MoveAxInPosAsync(Ax.X, x);
                _machine.SetVelocity(velocity);
            }

            if (key.Key == Key.I)
                if (MessageBox.Show("Завершить процесс?", "Процесс", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                {

                    await Process?.WaitProcDoneAsync();
                    Process = null;
                    Substrate = null;
                    ResetWaferView();
                    AjustWaferTechnology();
                }

            if (key.Key == Key.Home)
                try
                {
                    var task = Task.Factory.StartNew(() => _machine.GoThereAsync(Place.Home));
                    await task;
                    _homeDone = true;
                    if (Process != null) Process = null;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, ex.StackTrace);
                }

            if (key.Key == Key.OemMinus) await Process?.AlignWafer();
            if (key.Key == Key.Subtract) VelocityRegime = Velocity.Step;
            if (key.Key == Key.Add)
            {
                if (VelocityRegime == Velocity.Slow)
                {
                    VelocityRegime = Velocity.Fast;
                    _machine.SetVelocity(Velocity.Fast);
                }
                else
                {
                    VelocityRegime = Velocity.Slow;
                    _machine.SetVelocity(Velocity.Slow);
                }
            }

            if (key.Key == Key.G)
            {
                _machine?.GoThereAsync(Place.Loading);
            }

            if (Keyboard.IsKeyDown(Key.RightCtrl) && Keyboard.IsKeyDown(Key.Oem6))
            {
                await _machine?.GoThereAsync(Place.CameraChuckCenter);
            }

            if (Keyboard.IsKeyDown(Key.RightCtrl) && Keyboard.IsKeyDown(Key.Oem4))
            {
                await _machine?.GoThereAsync(Place.BladeChuckCenter);
            }

            if (key.Key == Key.N)
            {
                if (Process.ProcessStatus == Status.Correcting)
                {
                    Process.CutOffset += 0.001;
                }
            }

            if (key.Key == Key.M)
            {
                if (Process.ProcessStatus == Status.Correcting)
                { Process.CutOffset -= 0.001; }
            }
            if (key.Key == Key.F)
            {
                if (Process is not null)
                {
                    if (MessageBox.Show("Сделать одиночный рез?", "Резка", MessageBoxButton.OKCancel) ==
                        MessageBoxResult.OK)
                    {
                        await Process.DoSingleCut();
                    }
                }
            }

            if (key.Key == Key.F12)
            {
#if Proc5
                if (Process5 is not null)
                {
                    Process5?.EmergencyScript();
                    Process5.WaitProcDoneAsync().Wait();
                    Process5 = null;
                    Substrate = null;
                    ResetWaferView();
                    AjustWaferTechnology();
                    //MessageBox.Show("Процесс экстренно прерван оператором.", "Процесс", MessageBoxButton.OK, MessageBoxImage.Exclamation);

                }
#else
                Process?.EmergencyScript();
                MessageBox.Show("Процесс экстренно прерван оператором");
                await Process.WaitProcDoneAsync();
                Process = null;
#endif
            }
            //key.Handled = true;
        }

        private void Process5_OnProcStatusChanged(object sender, Process5.Stat e)
        {
            switch (e)
            {
                case Process5.Stat.Cancelled:
                    break;
                case Process5.Stat.End:
                    Process5.WaitProcDoneAsync().ContinueWith
                        (async t =>
                        {
                            Process5 = null;
                            Substrate = null;
                            ResetWaferView();
                            AjustWaferTechnology();
                            await _machine.GoThereAsync(Place.Loading);
                            MessageBox.Show("Процесс завершён.", "Процесс", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                        });
                    
                    break;
                default:
                    break;
            }
        }

        private void ResetWaferView()
        {
            WvAngle = default;
            ResetView ^= true;
        }
        private void Process_OnControlPointAppeared()
        {
            var rotateTransform = new RotateTransform(-Substrate.CurrentSideAngle);

            var point = new TranslateTransform(-CCCenterXView, -CCCenterYView).Transform(new Point(XView, YView));
            var point1 = rotateTransform.Transform(new Point(point.X - 1, point.Y + WaferCurrentShiftView));
            var point2 = rotateTransform.Transform(new Point(point.X + 1, point.Y + WaferCurrentShiftView));
            List<TraceLine> temp = new(ControlPointsView);
            temp.ForEach(br => br.Brush = Brushes.Blue);
            temp.Add(new TraceLine()
            { XStart = point1.X, XEnd = point2.X, YStart = point1.Y, YEnd = point2.Y, Brush = Brushes.OrangeRed });
            ControlPointsView = new ObservableCollection<TraceLine>(temp);
        }

        private void Process_OnProcParamsChanged(object arg1, ProcParams procParamsEventArgs)
        {
            WaferCurrentShiftView = procParamsEventArgs.currentShift;
            WaferCurrentSideAngle = procParamsEventArgs.currentSideAngle;
        }

        public double WaferCurrentSideAngle { get; set; }

        private void Process_OnProcessStatusChanged(string status)
        {
            ProcessStatus = status;
        }

        private async void Process_BladeTracingEvent(bool trace)
        {
            if (trace)
            {
                _tracingTaskCancellationTokenSource = new CancellationTokenSource();
                _tracingTaskCancellationToken = _tracingTaskCancellationTokenSource.Token;
                _tracingTask = new Task(() => BladeTracingTaskAsync(), _tracingTaskCancellationToken);

                _tracingTask.Start();
            }
            else
            {
                _tracingTaskCancellationTokenSource.Cancel();

                var rotateTransform = new RotateTransform(

                    //-Substrate.CurrentSideAngle,
                    -WaferCurrentSideAngle,
                    BCCenterXView,
                    BCCenterYView
                );

                var point1 = rotateTransform.Transform(new Point(XTrace, YTrace + WaferCurrentShiftView));
                var point2 = rotateTransform.Transform(new Point(XTraceEnd, YTrace + WaferCurrentShiftView));
                point1 = new TranslateTransform(-BCCenterXView, -BCCenterYView).Transform(point1);
                point2 = new TranslateTransform(-BCCenterXView, -BCCenterYView).Transform(point2);

                TracesCollectionView.Add(new TraceLine
                {
                    XStart = point1.X,
                    XEnd = point2.X,
                    YStart = point1.Y,
                    YEnd = point2.Y
                });

                TracesCollectionView = new ObservableCollection<TraceLine>(TracesCollectionView);
                XTrace = new double();
                YTrace = new double();
                XTraceEnd = new double();

                try
                {
                    await _tracingTask;
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    _tracingTaskCancellationTokenSource.Dispose();
                    _tracingTask?.Dispose();
                }
            }
        }

        private async Task BladeTracingTaskAsync()
        {
            XTrace = XView;
            YTrace = YView;

            while (!_tracingTaskCancellationToken.IsCancellationRequested)
            {
                XTraceEnd = XView;
                Task.Delay(100).Wait();
            }

            _tracingTaskCancellationToken.ThrowIfCancellationRequested();
        }

        private void KeyUp(object args)
        {
            var key = (KeyEventArgs)args;
            if (((key.Key == Key.A) | (key.Key == Key.Z)) & (VelocityRegime != Velocity.Step)) _machine.Stop(Ax.Y);
            if ((key.Key == Key.X) | (key.Key == Key.C)) _machine.Stop(Ax.X);
            if ((key.Key == Key.S) | (key.Key == Key.D)) _machine.Stop(Ax.U);
            if ((key.Key == Key.V) | (key.Key == Key.B)) _machine.Stop(Ax.Z);
        }

        private void WaferSettings()
        {
            var waferSettingsView = new WaferSettingsView
            {
                DataContext = new WaferSettingsViewModel(_settingsService)
            };

            waferSettingsView.ShowDialog();

            // AjustWaferTechnology();
        }

        private void MachineSettings()
        {
            new MachineSettingsView
            {
                DataContext = new MachineSettingsViewModel(XView, YView, ZView)
            }.ShowDialog();

            Settings.Default.Save();

            //Machine.RefreshSettings();
            try
            {
                ImplementMachineSettings();
            }
            catch (MotionException ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void ImplementMachineSettings()
        {
            var xpar = new MotionDeviceConfigs
            {
                maxAcc = 180,
                maxDec = 180,
                maxVel = 50,
                axDirLogic = (int)DirLogic.DIR_ACT_HIGH,
                plsOutMde = (int)PulseOutMode.OUT_DIR,
                reset = (int)HomeReset.HOME_RESET_EN,
                acc = Settings.Default.XAcc,
                dec = Settings.Default.XDec,
                ppu = Settings.Default.XPPU,
                homeVelLow = Settings.Default.XVelLow,
                homeVelHigh = Settings.Default.XVelService
            };
            var ypar = new MotionDeviceConfigs
            {
                maxAcc = 180,
                maxDec = 180,
                maxVel = 50,
                plsOutMde = (int)PulseOutMode.OUT_DIR_ALL_NEG,
                axDirLogic = (int)DirLogic.DIR_ACT_HIGH,
                reset = (int)HomeReset.HOME_RESET_EN,
                acc = Settings.Default.YAcc,
                dec = Settings.Default.YDec,
                ppu = Settings.Default.YPPU,
                plsInMde = (int)PulseInMode.AB_4X,
                homeVelLow = Settings.Default.YVelLow,
                homeVelHigh = Settings.Default.YVelService
            };
            var zpar = new MotionDeviceConfigs
            {
                maxAcc = 180,
                maxDec = 180,
                maxVel = 50,
                axDirLogic = (int)DirLogic.DIR_ACT_HIGH,
                plsOutMde = (int)PulseOutMode.OUT_DIR,
                reset = (int)HomeReset.HOME_RESET_EN,
                acc = Settings.Default.ZAcc,
                dec = Settings.Default.ZDec,
                ppu = Settings.Default.ZPPU,
                homeVelLow = Settings.Default.ZVelLow,
                homeVelHigh = Settings.Default.ZVelService
            };
            Settings.Default.UPPU = 1000;
            var upar = new MotionDeviceConfigs
            {
                maxAcc = 180,
                maxDec = 180,
                maxVel = 50,
                axDirLogic = (int)DirLogic.DIR_ACT_HIGH,
                plsOutMde = (int)PulseOutMode.OUT_DIR,
                reset = (int)HomeReset.HOME_RESET_EN,
                acc = Settings.Default.UAcc,
                dec = Settings.Default.UDec,
                ppu = Settings.Default.UPPU,
                homeVelLow = Settings.Default.UVelLow,
                homeVelHigh = Settings.Default.UVelService
            };
            var XVelRegimes = new Dictionary<Velocity, double>
            {
                {Velocity.Fast, Settings.Default.XVelHigh},
                {Velocity.Slow, Settings.Default.XVelLow},
                {Velocity.Service, Settings.Default.XVelService}
            };

            var YVelRegimes = new Dictionary<Velocity, double>
            {
                {Velocity.Fast, Settings.Default.YVelHigh},
                {Velocity.Slow, Settings.Default.YVelLow},
                {Velocity.Service, Settings.Default.YVelService}
            };

            var ZVelRegimes = new Dictionary<Velocity, double>
            {
                {Velocity.Fast, Settings.Default.ZVelHigh},
                {Velocity.Slow, Settings.Default.ZVelLow},
                {Velocity.Service, Settings.Default.ZVelService}
            };

            var UVelRegimes = new Dictionary<Velocity, double>
            {
                {Velocity.Fast, Settings.Default.UVelHigh},
                {Velocity.Slow, Settings.Default.UVelLow},
                {Velocity.Service, Settings.Default.UVelService}
            };

            _machine.ConfigureVelRegimes(new Dictionary<Ax, Dictionary<Velocity, double>>
            {
                {Ax.X, XVelRegimes},
                {Ax.Y, YVelRegimes},
                {Ax.Z, ZVelRegimes},
                {Ax.U, UVelRegimes}
            });


            _machine.SetConfigs(new (Ax axis, MotionDeviceConfigs configs)[]
            {
                (Ax.X, xpar),
                (Ax.Y, ypar),
                (Ax.Z, zpar),
                (Ax.U, upar)
            });

            _machine.SetVelocity(VelocityRegime);

            BCCenterXView = Settings.Default.XDisk;
            BCCenterYView = Settings.Default.YObjective + Settings.Default.DiskShift;
            CCCenterXView = Settings.Default.XObjective;
            CCCenterYView = Settings.Default.YObjective;
            ZBladeTouchView = Settings.Default.ZTouch;

            _machine.ConfigureGeometry(new Dictionary<Place, (Ax, double)[]>
            {
                {
                    Place.BladeChuckCenter,
                    new[]
                    {
                        (Ax.X, Settings.Default.XDisk), (Ax.Y, Settings.Default.YObjective + Settings.Default.DiskShift)
                    }
                },
                {
                    Place.CameraChuckCenter,
                    new[] {(Ax.X, Settings.Default.XObjective), (Ax.Y, Settings.Default.YObjective)}
                },
                {Place.Loading, new[] {(Ax.X, Settings.Default.XLoad), (Ax.Y, Settings.Default.YLoad)}},
                {Place.ZBladeTouch, new (Ax, double)[] {(Ax.Z, Settings.Default.ZTouch)}},
                {Place.ZFocus, new (Ax, double)[] {(Ax.Z, Settings.Default.ZObjective)}}
            });

            _machine.ConfigureGeometry(new Dictionary<Place, double>
                {{Place.ZBladeTouch, Settings.Default.ZTouch}}
            );

            _machine.ConfigureDoubleFeatures(new Dictionary<MFeatures, double>
            {
                {MFeatures.CameraBladeOffset, Settings.Default.DiskShift},
                {MFeatures.ZBladeTouch, Settings.Default.ZTouch},
                {MFeatures.CameraFocus, 3}
            });

            _machine.SetBridgeOnSensors(Sensors.ChuckVacuum, Settings.Default.VacuumSensorDsbl);
            _machine.SetBridgeOnSensors(Sensors.Coolant, Settings.Default.CoolantSensorDsbl);
            _machine.SetBridgeOnSensors(Sensors.Air, Settings.Default.AirSensorDsbl);
            _machine.SetBridgeOnSensors(Sensors.SpindleCoolant, Settings.Default.SpindleCoolantSensorDsbl);
        }

        private void AjustWaferTechnology(int side = -1)
        {
            var fileName = Settings.Default.WaferLastFile;
            var waf = new TempWafer();
            var tech = new Technology();

            if (File.Exists(fileName))
            {
                // TODO ASYNC
                ((IWafer)StatMethods.DeSerializeObjectJson<TempWafer>(fileName)).CopyPropertiesTo(waf);
                if (waf.IsRound)
                {
                    Wafer = new Wafer(new Vector2(0, 0), waf.Thickness, waf.Diameter, (0, waf.IndexW), (90, waf.IndexH));
                }
                else
                {
                    if (Substrate is null)
                    {
                        Substrate = new Substrate2D(waf.IndexH, waf.IndexW, waf.Thickness, new Rectangle2D(waf.Height, waf.Width));
                    }
                }
            }
            TracesCollectionView = new ObservableCollection<TraceLine>();
            ControlPointsView = new ObservableCollection<TraceLine>();
            //ResetView ^= true;
            var wfViewFactory = new WaferViewFactory(Substrate);
            //ResetView ^= true;
            WaferView = new();
            WaferView.SetView(wfViewFactory);

            fileName = Settings.Default.TechnologyLastFile;
            if (File.Exists(fileName))
            {
                // TODO ASYNC
                ((ITechnology)StatMethods.DeSerializeObjectJson<Technology>(fileName)).CopyPropertiesTo(tech);
                PropContainer.Technology = tech;
                Thickness = waf.Thickness;
            }


        }

        private void TechnologySettings()
        {
            var technologySettingsView = new TechnologySettingsView
            {
                DataContext = new TechnologySettingsViewModel()
            };

            technologySettingsView.ShowDialog();

            // TODO ASYNC
            _technology = StatMethods.DeSerializeObjectJson<Technology>(Settings.Default.TechnologyLastFile);
            Process?.RefresfTechnology(_technology);
            Wafer?.SetPassCount(PropContainer.Technology.PassCount);
        }

        private void Change()
        {
            Cols = new[] { Cols[1], Cols[0] };
            Rows = new[] { Rows[1], Rows[0] };
        }

        public async Task ToTeachVideoScaleAsync()
        {
            TeachVScaleMarkersVisibility = Visibility.Visible;
            ProcessMessage = "Подведите ориентир к одному из визиров и нажмите *";
            await WaitForConfirmationAsync();
            var y = YView;
            ProcessMessage = "Подведите ориентир ко второму визиру и нажмите *";
            await WaitForConfirmationAsync();
            _cameraScale = TeachMarkersRatio * Math.Abs(y - YView);
            Settings.Default.CameraScale = _cameraScale;
            Settings.Default.Save();
            ProcessMessage = "";
            TeachVScaleMarkersVisibility = Visibility.Hidden;
        }

        private async Task WaitForConfirmationAsync()
        {
            UserConfirmation = false;
            await Task.Run(() =>
            {
                while (!UserConfirmation) Task.Delay(1).Wait();
            });
        }

        private void ChangeScreensRegime(bool regime)
        {
            if (regime && Cols[0] == 0)
                Change();
            else if (Cols[0] == 1) Change();
        }

        private void OpenFile()
        {
            //var openFileDialog = new OpenFileDialog();
            //openFileDialog.Filter = "dxf files|*.dxf";
            //openFileDialog.Title = "Выберите файл";
            //if ((bool)openFileDialog.ShowDialog())
            //{
            //    string filePath = openFileDialog.FileName;
            //    var dxf = DxfDocument.Load(filePath);
            //    Wafer = new Wafer(1, dxf, "REZ");
            //}
            //Wafer = new Wafer(new Vector2(0, 0), 1, (0, 60000, 48000, 3100), (90, 48000, 60000, 5100));
            //Wafer = new Wafer(new Vector2(0, 0), 1, 5000, (0, 500), (60, 300));

            //Thickness = 1;
        }
    }
}