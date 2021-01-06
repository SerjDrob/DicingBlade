using System;
using System.Threading.Tasks;
using DicingBlade.Classes;
using System.Windows.Input;
using netDxf;
using System.Collections.ObjectModel;
using System.IO;
using PropertyChanged;
using System.Windows;
using DicingBlade.Properties;
using System.Windows.Media.Imaging;
using System.Collections.Generic;
using DicingBlade;
using DicingBlade.ViewModels;
using System.Windows.Media;

namespace DicingBlade.Classes
{
    public class TraceLine
    {
        public double XStart;
        public double XEnd;
        public double YStart;
        public double YEnd;
    }
}

namespace DicingBlade.ViewModels
{
    [AddINotifyPropertyChangedInterface]
    internal class MainViewModel : IDisposable
    {
        private IMachine _machine;





        public Velocity VelocityRegime { get; set; } = Velocity.Fast;
        private ExceptionsAgregator _exceptionsAgregator;

        public ObservableCollection<TraceLine> TracesCollectionView { get; set; } = new ObservableCollection<TraceLine>();

        private Task _tracingTask;

        public double XTrace { get; set; }
        public double YTrace { get; set; }
        public double XTraceEnd { get; set; }

        private System.Threading.CancellationTokenSource _tracingTaskCancellationTokenSource;
        private System.Threading.CancellationToken _tracingTaskCancellationToken;
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
        public bool AirSensorView { get; set; }
        public bool SpindleCoolantSensorView { get; set; }
        public double BCCenterXView { get; set; }
        public double BCCenterYView { get; set; }
        public double CCCenterXView { get; set; }
        public double CCCenterYView { get; set; }
        public double ZBladeTouchView { get; set; }
        public int SpindleFreqView { get; set; }
        public double SpindleCurrentView { get; set; }


        //private Parameters parameters;
        public Machine Machine { get; set; }
        public Process2 Process { get; set; }
        public Wafer Wafer { get; set; }
        public WaferView WaferView { get; set; }
        public ObservableCollection<TracePath> Traces { get; set; }
        public double WvAngle { get; set; }
        public bool WvRotate { get; set; } = false;
        public double RotatingTime { get; set; } = 1;

        private TempWafer2D _tempWafer2;
        private int[] _cols;
        private int[] _rows;
        private bool _test;
        public Map Condition { get; set; }
        public double Thickness { get; set; } = 1;
        public bool Test { get; set; }
        public int[] Rows
        {
            get => _rows;
            set => _rows = value;
        }
        public int[] Cols
        {
            get => _cols;
            set => _cols = value;
        }
        private Diagram[] BaseProcess { get; set; }
        public ICommand OpenFileCmd { get; set; }
        public ICommand RotateCmd { get; set; }
        public ICommand ChangeCmd { get; set; }
        public ICommand KeyDownCmd { get; set; }
        public ICommand KeyUpCmd { get; set; }
        public ICommand WaferSettingsCmd { get; set; }
        public ICommand MachineSettingsCmd { get; set; }
        public ICommand TechnologySettingsCmd { get; set; }
        public ICommand ToTeachChipSizeCmd { get; set; }
        public ICommand ToTeachVideoScaleCmd { get; set; }
        public ICommand TestCmd { get; set; }
        public MainViewModel()
        {
            Test = false;
            Cols = new int[] { 0, 1 };
            Rows = new int[] { 2, 1 };
            Condition = new Map();
            OpenFileCmd = new Command(args => OpenFile());
            ChangeCmd = new Command(args => Change());
            KeyDownCmd = new Command(args => KeyDownAsync(args));
            KeyUpCmd = new Command(args => KeyUp(args));
            WaferSettingsCmd = new Command(args => WaferSettings());
            MachineSettingsCmd = new Command(args => MachineSettings());
            TechnologySettingsCmd = new Command(args => TechnologySettings());
            ToTeachChipSizeCmd = new Command(args => ToTeachChipSizeAsync());
            ToTeachVideoScaleCmd = new Command(args => ToTeachVideoScaleAsync());
            TestCmd = new Command(args => Func(args));

            Bi = new BitmapImage();

            _exceptionsAgregator = ExceptionsAgregator.GetExceptionsAgregator();
            _exceptionsAgregator.SetShowMethod(s => { MessageBox.Show(s); });

            //Machine = new Machine(Test);
            //Machine.VideoCamera.OnBitmapChanged += GetCameraImage;
            try
            {
                _machine = new Machine4X();
                _machine.ConfigureAxes(new (Ax, double)[]
                {
                    (Ax.X,0),
                    (Ax.U,0),
                    (Ax.Z,0),
                    (Ax.Y,12.8)
                });

                _machine.ConfigureAxesGroups(new Dictionary<Groups, Ax[]> {
                { Groups.XY, new Ax[]{Ax.X,Ax.Y}}
                });

                _machine.ConfigureValves(new Dictionary<Valves, (Ax, Do)>
                {
                    {Valves.Blowing,(Ax.Z,Do.Out6) },
                    {Valves.ChuckVacuum,(Ax.Z,Do.Out4) },
                    {Valves.Coolant,(Ax.U,Do.Out4) }
                });

                _machine.SwitchOffValve(Valves.Blowing);
                _machine.SwitchOffValve(Valves.ChuckVacuum);
                _machine.SwitchOffValve(Valves.Coolant);

                _machine.ConfigureSensors(new Dictionary<Sensors, (Ax, Di)>
                {
                    {Sensors.Air,(Ax.Z,Di.In1)},
                    {Sensors.ChuckVacuum,(Ax.X,Di.In3)},
                    {Sensors.Coolant,(Ax.X,Di.In2)},
                    {Sensors.SpindleCoolant,(Ax.X,Di.In1) }
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





            BaseProcess = new Diagram[] {
                Diagram.GoNextCutXy,
                Diagram.GoNextDepthZ,
                Diagram.CuttingX,
                Diagram.GoTransferingHeightZ,
                Diagram.GoNextDirection
            };
            //Traces = new ObservableCollection<TracePath>();
            //_tempWafer2.FirstPointSet = false;

            AjustWaferTechnology();
        }

        private void _machine_OnSpindleStateChanging(int rpm, double current, bool spinningState)
        {
            SpindleFreqView = rpm;
            SpindleCurrentView = current;
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
                default:
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
                default:
                    break;
            }
        }

        private void _machine_OnAxisMotionStateChanged(Ax axis, double position, bool nLmt, bool pLmt, bool motionDone)
        {
            position = Math.Round(position, 3);
            switch (axis)
            {
                case Ax.X:
                    XView = position;
                    XpLmtView = pLmt;
                    XnLmtView = nLmt;
                    XMotDoneView = motionDone;
                    break;
                case Ax.Y:
                    YView = position;
                    YpLmtView = pLmt;
                    YnLmtView = nLmt;
                    YMotDoneView = motionDone;
                    break;
                case Ax.Z:
                    ZView = position;
                    ZpLmtView = pLmt;
                    ZnLmtView = nLmt;
                    ZMotDoneView = motionDone;
                    break;
                case Ax.U:
                    UView = position;
                    UpLmtView = pLmt;
                    UnLmtView = nLmt;
                    UMotDoneView = motionDone;
                    break;
                default:
                    break;
            }
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
            await Process.ToTeachChipSizeAsync();
            new TempWafer(PropContainer.WaferTemp).SerializeObjectJson(Settings.Default.WaferLastFile);
            AjustWaferTechnology();
        }
        private async Task ToTeachVideoScaleAsync()
        {
            await Process.ToTeachVideoScale();
        }
        private async Task KeyDownAsync(object args)
        {
            KeyEventArgs key = (KeyEventArgs)args;

            //test key
            if (key.Key == Key.Tab)
            {
                //Machine.SpindleModbus();
                //WVRotate ^= true;
                //Machine.GoTest();
                //if (process == null) process = new Process(Machine, Wafer, new Blade());
                //await process.ProcElementDispatcherAsync(Diagram.goCameraPointLearningXYZ);
            }

            if (key.Key == Key.Multiply)
            {
                Process.UserConfirmation = true;
            }
            if (key.Key == Key.Q)
            {
                if (_machine.GetValveState(Valves.ChuckVacuum))
                {
                    _machine.SwitchOffValve(Valves.ChuckVacuum);
                }
                else
                {
                    _machine.SwitchOnValve(Valves.ChuckVacuum);
                }

            }
            if (key.Key == Key.W)
            {
                if (_machine.GetValveState(Valves.Coolant))
                {
                    _machine.SwitchOffValve(Valves.Coolant);
                }
                else
                {
                    _machine.SwitchOnValve(Valves.Coolant);
                }
            }
            if (key.Key == Key.R)
            {
                if (_machine.GetValveState(Valves.Blowing))
                {
                    _machine.SwitchOffValve(Valves.Blowing);
                }
                else
                {
                    _machine.SwitchOnValve(Valves.Blowing);
                }
            }
            if (key.Key == Key.D)
            {
                _machine.GoWhile(Ax.U, AxDir.Pos);
            }

            if (key.Key == Key.S)
            {
                _machine.GoWhile(Ax.U, AxDir.Neg);
            }
            if (key.Key == Key.F2)
            {
                _machine.StartSpindle();
            }
            if (key.Key == Key.F3)
            {
                _machine.StopSpindle();
            }
            if (key.Key == Key.T) Change();
            if (key.Key == Key.Divide)
            {

                if (Process == null)
                {

                    try
                    {
                        Process = new Process2(_machine, Wafer, new Blade(), new Technology(), BaseProcess);
                        _exceptionsAgregator.RegisterMessager(Process);
                        //Machine.SetVelocity(Velocity.Service);
                        //await Machine.GoThereAsync(Place.CameraChuckCenter);
                        Process.GetRotationEvent += SetRotation;
                        Process.ChangeScreensEvent += ChangeScreensRegime;
                        Process.BladeTracingEvent += Process_BladeTracingEvent;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                    }


                }
                else
                {
                    if (Process.ProcessStatus == Status.Done)
                    {
                        Process = null;
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
            if (key.Key == Key.A)
            {
                _machine.GoWhile(Ax.Y, AxDir.Pos);
                //Machine.Y.GoWhile(AxDir.Pos);
            }
            if (key.Key == Key.Z)
            {
                _machine.GoWhile(Ax.Y, AxDir.Neg);
            }
            if (key.Key == Key.X)
            {
                _machine.GoWhile(Ax.X, AxDir.Neg);
            }
            if (key.Key == Key.C)
            {
                _machine.GoWhile(Ax.X, AxDir.Pos);
            }
            if (key.Key == Key.S)
            {
                _machine.GoWhile(Ax.U, AxDir.Pos);
            }
            if (key.Key == Key.D)
            {
                _machine.GoWhile(Ax.U, AxDir.Neg);
            }
            if (key.Key == Key.V)
            {
                _machine.GoWhile(Ax.Z, AxDir.Pos);
            }
            if (key.Key == Key.B)
            {
                _machine.GoWhile(Ax.Z, AxDir.Neg);
            }
            if (key.Key == Key.J)
            {
                //Technology tech = new Technology(PropContainer.Technology);
                //tech.SerializeObjectJson("D:/TechParams1.json");
                //Wafer.WriteObject<Wafer>("firstPAR");
            }
            if (key.Key == Key.K) { }
            if (key.Key == Key.L) { }
            if (key.Key == Key.I)
            {
                if (MessageBox.Show("Завершить процесс", "Процесс", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                {
                    Process.CancelProcess = true;
                }
            }
            if (key.Key == Key.Home)
            {
                try
                {
                    var task = Task.Factory.StartNew(() => _machine.GoThereAsync(Place.Home));
                    await task;
                    if (Process != null) Process = null;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, ex.StackTrace);
                }

                // await Machine.GoThereAsync(Place.Home);
            }
            if (key.Key == Key.OemMinus)
            {
                if (Process.ProcessStatus == Status.Learning)
                {
                    if (!_tempWafer2.FirstPointSet)
                    {
                        _tempWafer2.Point1 = new Vector2(Machine.X.ActualPosition, Machine.Y.ActualPosition);
                        _tempWafer2.FirstPointSet = true;
                    }
                    else
                    {
                        _tempWafer2.Point2 = new Vector2(Machine.X.ActualPosition, Machine.Y.ActualPosition);
                        await Machine.U.MoveAxisInPosAsync(Machine.U.ActualPosition - _tempWafer2.GetAngle());
                        _tempWafer2.FirstPointSet = false;
                    }
                }
            }
            if (key.Key == Key.Subtract)
            {
                Machine.VelocityRegime = Velocity.Step;
            }
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

            if (Keyboard.IsKeyDown(Key.RightCtrl) && Keyboard.IsKeyDown(Key.Oem6))//}
            {
                await _machine?.GoThereAsync(Place.CameraChuckCenter);
            }
            if (Keyboard.IsKeyDown(Key.RightCtrl) && Keyboard.IsKeyDown(Key.Oem4))//{
            {
                throw new NotImplementedException();
            }

            if (key.Key == Key.N)
            {
                if (Process.PauseProcess) Process.CutOffset += 0.001;
            }

            if (key.Key == Key.M)
            {
                if (Process.PauseProcess) Process.CutOffset -= 0.001;
            }
            //key.Handled = true;
        }

        private async void Process_BladeTracingEvent(bool trace)
        {
            if (trace)
            {
                _tracingTaskCancellationTokenSource = new System.Threading.CancellationTokenSource();
                _tracingTaskCancellationToken = _tracingTaskCancellationTokenSource.Token;
                _tracingTask = new Task(() => BladeTracingTaskAsync(), _tracingTaskCancellationToken);

                _tracingTask.Start();
            }
            else
            {
                _tracingTaskCancellationTokenSource.Cancel();
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

                    RotateTransform rotateTransform = new RotateTransform(
                       -Wafer.GetCurrentDiretionAngle,
                       BCCenterXView,
                       BCCenterYView
                       );

                    var point1 = rotateTransform.Transform(new System.Windows.Point(XTrace, YTrace + Wafer.GetCurrentDirectionIndexShift));
                    var point2 = rotateTransform.Transform(new System.Windows.Point(XTraceEnd, YTrace + Wafer.GetCurrentDirectionIndexShift));
                    point1 = new TranslateTransform(-BCCenterXView, -BCCenterYView).Transform(point1);
                    point2 = new TranslateTransform(-BCCenterXView, -BCCenterYView).Transform(point2);

                    TracesCollectionView.Add(new TraceLine()
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
            KeyEventArgs key = (KeyEventArgs)args;
            if (key.Key == Key.A | key.Key == Key.Z)
            {
                _machine.Stop(Ax.Y);
            }
            if (key.Key == Key.X | key.Key == Key.C)
            {
                _machine.Stop(Ax.X);
            }
            if (key.Key == Key.S | key.Key == Key.D)
            {
                _machine.Stop(Ax.U);
            }
            if (key.Key == Key.V | key.Key == Key.B)
            {
                _machine.Stop(Ax.Z);
            }
        }
        private void WaferSettings()
        {
            var waferSettingsView = new Views.WaferSettingsView
            {
                DataContext = new WaferSettingsViewModel()
            };

            waferSettingsView.ShowDialog();

            AjustWaferTechnology();
        }
        private void MachineSettings()
        {
            new Views.MachineSettingsView()
            {
                DataContext = new MachineSettingsViewModel(XView, YView)
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
            var xpar = new MotionDeviceConfigs()
            {
                maxAcc = 180,
                maxDec = 180,
                maxVel = 50,
                axDirLogic = (int)Advantech.Motion.DirLogic.DIR_ACT_HIGH,
                plsOutMde = (int)Advantech.Motion.PulseOutMode.OUT_DIR,
                reset = (int)Advantech.Motion.HomeReset.HOME_RESET_EN,
                acc = Settings.Default.XAcc,
                dec = Settings.Default.XDec,
                ppu = Settings.Default.XPPU,
                homeVelLow = Settings.Default.XVelLow,
                homeVelHigh = Settings.Default.XVelService
            };
            var ypar = new MotionDeviceConfigs()
            {
                maxAcc = 180,
                maxDec = 180,
                maxVel = 50,
                plsOutMde = (int)Advantech.Motion.PulseOutMode.OUT_DIR_ALL_NEG,
                axDirLogic = (int)Advantech.Motion.DirLogic.DIR_ACT_HIGH,
                reset = (int)Advantech.Motion.HomeReset.HOME_RESET_EN,
                acc = Settings.Default.YAcc,
                dec = Settings.Default.YDec,
                ppu = Settings.Default.YPPU,
                plsInMde = (int)Advantech.Motion.PulseInMode.AB_4X,
                homeVelLow = Settings.Default.YVelLow,
                homeVelHigh = Settings.Default.YVelService
            };
            var zpar = new MotionDeviceConfigs()
            {
                maxAcc = 180,
                maxDec = 180,
                maxVel = 50,
                axDirLogic = (int)Advantech.Motion.DirLogic.DIR_ACT_HIGH,
                plsOutMde = (int)Advantech.Motion.PulseOutMode.OUT_DIR,
                reset = (int)Advantech.Motion.HomeReset.HOME_RESET_EN,
                acc = Settings.Default.ZAcc,
                dec = Settings.Default.ZDec,
                ppu = Settings.Default.ZPPU,
                homeVelLow = Settings.Default.ZVelLow,
                homeVelHigh = Settings.Default.ZVelService
            };
            Settings.Default.UPPU = 1000;
            var upar = new MotionDeviceConfigs()
            {
                maxAcc = 180,
                maxDec = 180,
                maxVel = 50,
                axDirLogic = (int)Advantech.Motion.DirLogic.DIR_ACT_HIGH,
                plsOutMde = (int)Advantech.Motion.PulseOutMode.OUT_DIR,
                reset = (int)Advantech.Motion.HomeReset.HOME_RESET_EN,
                acc = Settings.Default.UAcc,
                dec = Settings.Default.UDec,
                ppu = Settings.Default.UPPU,
                homeVelLow = Settings.Default.UVelLow,
                homeVelHigh = Settings.Default.UVelService
            };
            var XVelRegimes = new Dictionary<Velocity, double>
            {
                {Velocity.Fast,Settings.Default.XVelHigh },
                {Velocity.Slow,Settings.Default.XVelLow },
                {Velocity.Service,Settings.Default.XVelService }
            };

            var YVelRegimes = new Dictionary<Velocity, double>
            {
                {Velocity.Fast,Settings.Default.YVelHigh },
                {Velocity.Slow,Settings.Default.YVelLow },
                {Velocity.Service,Settings.Default.YVelService }
            };

            var ZVelRegimes = new Dictionary<Velocity, double>
            {
                {Velocity.Fast,Settings.Default.ZVelHigh },
                {Velocity.Slow,Settings.Default.ZVelLow },
                {Velocity.Service,Settings.Default.ZVelService }
            };

            var UVelRegimes = new Dictionary<Velocity, double>
            {
                {Velocity.Fast,Settings.Default.UVelHigh },
                {Velocity.Slow,Settings.Default.UVelLow },
                {Velocity.Service,Settings.Default.UVelService }
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
                (Ax.X,xpar),
                (Ax.Y,ypar),
                (Ax.Z,zpar),
                (Ax.U,upar)
            });

            _machine.SetVelocity(VelocityRegime);

            BCCenterXView = Settings.Default.XDisk;
            BCCenterYView = Settings.Default.YObjective + Settings.Default.DiskShift;
            CCCenterXView = Settings.Default.XObjective;
            CCCenterYView = Settings.Default.YObjective;
            ZBladeTouchView = Settings.Default.ZTouch;

            _machine.ConfigureGeometry(new Dictionary<Place, (Ax, double)[]>
                {
                    {Place.BladeChuckCenter, new (Ax,double)[]{ (Ax.X, Settings.Default.XDisk), (Ax.Y, Settings.Default.YObjective + Settings.Default.DiskShift)} },
                    {Place.CameraChuckCenter, new (Ax,double)[]{(Ax.X, Settings.Default.XObjective),(Ax.Y,Settings.Default.YObjective)} },
                    {Place.Loading, new (Ax,double)[]{(Ax.X,Settings.Default.XLoad),(Ax.Y, Settings.Default.YLoad)} },
                    {Place.ZBladeTouch, new (Ax,double)[]{(Ax.Z, Settings.Default.ZTouch) } }
                });

            _machine.ConfigureGeometry(new Dictionary<Place, double>
                { { Place.ZBladeTouch, Settings.Default.ZTouch} }
                );

            _machine.ConfigureDoubleFeatures(new Dictionary<MFeatures, double>
                {
                    {MFeatures.CameraBladeOffset, Settings.Default.DiskShift},
                    {MFeatures.ZBladeTouch, Settings.Default.ZTouch },
                    {MFeatures.CameraFocus, 3 }
                });

            _machine.SetBridgeOnSensors(Sensors.ChuckVacuum, Settings.Default.VacuumSensorDsbl);
            _machine.SetBridgeOnSensors(Sensors.Coolant, Settings.Default.CoolantSensorDsbl);
            _machine.SetBridgeOnSensors(Sensors.Air, Settings.Default.AirSensorDsbl);
            _machine.SetBridgeOnSensors(Sensors.SpindleCoolant, Settings.Default.SpindleCoolantSensorDsbl);
        }
        private void AjustWaferTechnology()
        {
            string fileName = Settings.Default.WaferLastFile;
            TempWafer waf = new TempWafer();
            Technology tech = new Technology();

            if (File.Exists(fileName))
            {
                ((IWafer)new TempWafer().DeSerializeObjectJson(fileName)).CopyPropertiesTo(waf);
                if (waf.IsRound)
                {
                    Wafer = new Wafer(new Vector2(0, 0), waf.Thickness, waf.Diameter, (0, waf.IndexW), (90, waf.IndexH));
                }
                else
                {
                    Wafer = new Wafer(new Vector2(0, 0), waf.Thickness, (0, waf.Height, waf.Width, waf.IndexW), (90, waf.Width, waf.Height, waf.IndexH));
                }
            }
            fileName = Settings.Default.TechnologyLastFile;
            if (File.Exists(fileName))
            {
                ((ITechnology)new Technology().DeSerializeObjectJson(fileName)).CopyPropertiesTo(tech);
                PropContainer.Technology = tech;
                Wafer.SetPassCount(PropContainer.Technology.PassCount);
                WaferView = Wafer.MakeWaferView();
                Thickness = waf.Thickness;
            }
        }
        private void TechnologySettings()
        {
            var technologySettingsView = new Views.TechnologySettingsView
            {
                DataContext = new TechnologySettingsViewModel()
            };

            technologySettingsView.ShowDialog();

            Wafer?.SetPassCount(PropContainer.Technology.PassCount);
        }
        private void Change()
        {
            Cols = new[] { Cols[1], Cols[0] };
            Rows = new[] { Rows[1], Rows[0] };
        }

        private void ChangeScreensRegime(bool regime)
        {
            if (regime && Cols[0] == 0)
            {
                Change();
            }
            else if (Cols[0] == 1)
            {
                Change();
            }
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

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
