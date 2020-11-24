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


namespace DicingBlade.ViewModels
{
    [AddINotifyPropertyChangedInterface]
    internal class MainViewModel
    {

        public BitmapImage Bi { get; set; }


        //private Parameters parameters;
        public Machine Machine { get; set; }
        public Process Process { get; set; }
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
            //RotateCmd = new Command(args => Rotate());
            ChangeCmd = new Command(args => Change());
            KeyDownCmd = new Command(args => KeyDownAsync(args));
            KeyUpCmd = new Command(args => KeyUp(args));
            WaferSettingsCmd = new Command(args => WaferSettings());
            MachineSettingsCmd = new Command(args => MachineSettings());
            TechnologySettingsCmd = new Command(args => TechnologySettings());
            ToTeachChipSizeCmd = new Command(args => ToTeachChipSizeAsync());
            ToTeachVideoScaleCmd = new Command(args => ToTeachVideoScaleAsync());
            TestCmd = new Command(args => Func(args));
            Machine = new Machine(Test);
            Machine.VideoCamera.OnBitmapChanged += GetCameraImage;
            BaseProcess = new Diagram[] {
                Diagram.GoNextCutXy,
                Diagram.GoNextDepthZ,
                Diagram.CuttingX,
                Diagram.GoTransferingHeightZ,
                Diagram.GoNextDirection
            };
            Traces = new ObservableCollection<TracePath>();
            _tempWafer2.FirstPointSet = false;
            // machine = new Machine();
            //  machine.OnAirWanished += Machine_OnAirWanished;

            AjustWaferTechnology();
        }
        private void SetRotation(double angle, double time)
        {
            WvAngle = angle;
            RotatingTime = time;
            WvRotate ^= true;
        }

        private async void GetCameraImage(BitmapImage bi) 
        {
            var tmp = Bi;            
            if (tmp?.StreamSource != null)
            {
                await tmp.StreamSource.DisposeAsync().ConfigureAwait(false);
            }
            Bi = new BitmapImage();
            Bi = bi;
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
                Machine.SpindleModbus();
                //WVRotate ^= true;
                //Machine.GoTest();
                //if(process==null) process = new Process(Machine, Wafer, new Blade());
                //await process.ProcElementDispatcherAsync(Diagram.goCameraPointLearningXYZ);
            }

            if (key.Key == Key.Multiply)
            {
                Process.UserConfirmation = true;
            }
            if (key.Key == Key.Q)
            {
                Machine.SwitchOnChuckVacuum ^= true;
            }
            if (key.Key == Key.W)
            {
                Machine.SwitchOnCoolantWater ^= true;
            }
            if (key.Key == Key.R)
            {
                Machine.SwitchOnBlowing ^= true;
            }
            if (key.Key == Key.D) WaferView.Angle += 0.2;
            if (key.Key == Key.S) WaferView.Angle -= 0.2;
            if (key.Key == Key.F2) OpenFile();
            if (key.Key == Key.T) Change();
            if (key.Key == Key.Divide)
            {
                if (!Test)
                {
                    if (Process == null)
                    {
                        if (Machine.SetOnChuck())
                        {
                            Machine.SetVelocity(Velocity.Service);
                            await Machine.GoThereAsync(Place.CameraChuckCenter);

                            Process = new Process(Machine, Wafer, new Blade(), new Technology(), BaseProcess);
                            Process.GetRotationEvent += SetRotation;
                            Process.ChangeScreensEvent += ChangeScreensRegime;
                            Process.ProcessStatus = Status.StartLearning;
                        }

                    }
                    else
                    {
                        if (Process.ProcessStatus == Status.Done)
                        {
                            Process = null;
                        }
                        else await Process.StartPauseProc();
                    }
                }
                else
                {
                    Process = new Process(Machine, Wafer, new Blade(), new Technology(), BaseProcess);
                }
                //StartWorkAsync();
            }
            if (key.Key == Key.A)
            {
                Machine.Y.GoWhile(AxDir.Pos);
            }
            if (key.Key == Key.Z)
            {
                Machine.Y.GoWhile(AxDir.Neg);
            }
            if (key.Key == Key.X)
            {
                Machine.X.GoWhile(AxDir.Neg);
            }
            if (key.Key == Key.C)
            {
                Machine.X.GoWhile(AxDir.Pos);
            }
            if (key.Key == Key.S)
            {
                Machine.GoWhile(AxisDirections.Up);
            }
            if (key.Key == Key.D)
            {
                Machine.GoWhile(AxisDirections.Un);
            }
            if (key.Key == Key.V)
            {
                Machine.Z.GoWhile(AxDir.Pos);
            }
            if (key.Key == Key.B)
            {
                Machine.Z.GoWhile(AxDir.Neg);
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
                await Machine.GoThereAsync(Place.Home);
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
                if (Machine.VelocityRegime == Velocity.Slow)
                {
                    Machine.VelocityRegime = Velocity.Fast;
                }
                else
                {
                    Machine.VelocityRegime = Velocity.Slow;
                }
            }

            if (Keyboard.IsKeyDown(Key.RightCtrl) && Keyboard.IsKeyDown(Key.Oem6))//}
            {
                throw new NotImplementedException();
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
        private void KeyUp(object args)
        {
            KeyEventArgs key = (KeyEventArgs)args;
            if (key.Key == Key.A | key.Key == Key.Z)
            {
                Machine.Stop(Ax.Y);
            }
            if (key.Key == Key.X | key.Key == Key.C)
            {
                Machine.Stop(Ax.X);
            }
            if (key.Key == Key.S | key.Key == Key.D)
            {
                Machine.Stop(Ax.U);
            }
            if (key.Key == Key.V | key.Key == Key.B)
            {
                Machine.Stop(Ax.Z);
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
            var machineSettingsView = new Views.MachineSettingsView
            {
                DataContext = new MachineSettingsViewModel(Machine)
            };
            machineSettingsView.ShowDialog();
            Settings.Default.Save();
            Machine.RefreshSettings();
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
            else if(Cols[0] == 1)
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
    }
}
