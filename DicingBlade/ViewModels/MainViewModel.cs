using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DicingBlade.Classes;
using System.Windows.Input;
using Microsoft.Win32;
using netDxf;
using System.ComponentModel;
using System.Collections.ObjectModel;
using PropertyChanged;
using System.Windows;
using DicingBlade.Properties;
using System.Windows.Input;


namespace DicingBlade.ViewModels
{
    public enum Signals
    {
        airSensor,
        waterSensor,
        vacuumSensor,
        coolingSensor,
        blowValve,
        waterValve,
        vacuumValve
    }
    [AddINotifyPropertyChangedInterface]
    class MainViewModel
    {       
        public Machine Machine { get; set; }
        private Process process;
        public Wafer Wafer { get; set; }
        public WaferView WaferView { get; set; }
        private TempWafer2D tempWafer2;
        private int[] cols;
        private int[] rows;
        private bool test;
        public Signals MyProperty { get; set; }
        public Map Condition { get; set; }        
        public double Thickness { get; set; }        
        public bool Test { get; set; }      
        public int[] Rows
        {
            get { return rows; }
            set
            {
                rows = value;                
            }
        }
        public int[] Cols
        {
            get { return cols; }
            set
            {
                cols = value;               
            }
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
            Machine = new Machine(false);
            Machine.BladeChuckCenter = new Vector2(10, 10);
            Machine.CameraChuckCenter = new Vector2(15, 20);
            BaseProcess = new Diagram[] {
                Diagram.goNextCutXY,
                Diagram.goWaferStartX,
                Diagram.goNextDepthZ,
                Diagram.cuttingX,
                Diagram.goNextDirection
            };



            // machine = new Machine();
            //  machine.OnAirWanished += Machine_OnAirWanished;
        }
        private void Machine_OnAirWanished(DIEventArgs eventArgs)
        {
            throw new NotImplementedException();
        }
        private async Task KeyDownAsync(object args) 
        {
            KeyEventArgs key = (KeyEventArgs)args;
            
            if (key.Key == Key.Q)
            {
                //Condition.Mask ^= (1 << (int)Signals.vacuumValve);
                Machine.SwitchOnChuckVacuum ^=true;
            }
            if (key.Key == Key.W)
            {
                //Condition.Mask ^= (1 << (int)Signals.waterValve);
                Machine.SwitchOnCoolantWater ^= true;
            }
            if (key.Key == Key.R)
            {
                //Condition.Mask ^= (1 << (int)Signals.blowValve);
                Machine.SwitchOnBlowing ^= true;
            }
            if (key.Key == Key.D) WaferView.Angle += 0.2;
            if (key.Key == Key.S) WaferView.Angle -= 0.2;
            if (key.Key == Key.F2) OpenFile();
            if (key.Key == Key.T) Change();
            if (key.Key == Key.Divide)
            {
                if (process == null) 
                {
                    if (Machine.SetOnChuck())
                    {
                        await Machine.GoThereAsync(Place.CameraChuckCenter);
                        process = new Process(Machine, Wafer, new Blade());
                        process.ProcessStatus = Status.StartLearning;                        
                    }
                    
                }
                else 
                {
                    switch (process.ProcessStatus)
                    {
                        case Status.StartLearning:
                            await process.ProcElementDispatcherAsync(Diagram.goCameraPointLearningXYZ);
                            process.ProcessStatus = Status.Learning;
                            break;
                        case Status.Learning:                            
                            Wafer.SetCurrentDirectionIndexShift = Machine.Y.ActualPosition - Wafer.GetNearestCut(Machine.Y.ActualPosition).StartPoint.Y;
                            Wafer.SetCurrentDirectionAngle = Machine.U.ActualPosition;
                            if (Wafer.NextDir()) 
                            {
                                await Machine.U.MoveAxisInPosAsync(Wafer.GetCurrentDiretionAngle);
                                await process.ProcElementDispatcherAsync(Diagram.goCameraPointLearningXYZ); 
                            }
                            else 
                            {
                                process.ProcessStatus = Status.Working;
                                process.DoProcessAsync(BaseProcess);
                            }                         
                            
                            break;
                        case Status.Working:
                            process.PauseProcess = !process.PauseProcess;
                            if (process.PauseProcess) process.PauseScenarioAsync();
                            break;
                        case Status.Correcting:
                            break;
                        default:
                            break;
                    }
                }
                //StartWorkAsync();
            }
            if (key.Key == Key.A) 
            {
                Machine.GoWhile(AxisDirections.YP);
            }
            if (key.Key == Key.Z) 
            {
                Machine.GoWhile(AxisDirections.YN);
            }
            if (key.Key == Key.X)
            {
                Machine.GoWhile(AxisDirections.XN);
            }
            if (key.Key == Key.C)
            {
                Machine.GoWhile(AxisDirections.XP);
            }
            if (key.Key == Key.S)
            {
                Machine.GoWhile(AxisDirections.UP);
            }
            if (key.Key == Key.D)
            {
                Machine.GoWhile(AxisDirections.UN);
            }
            if (key.Key == Key.V)
            {
                Machine.GoWhile(AxisDirections.ZP);
            }
            if (key.Key == Key.B)
            {
                Machine.GoWhile(AxisDirections.ZN);
            }
            if (key.Key == Key.J) { }
            if (key.Key == Key.K) { }
            if (key.Key == Key.L) { }
            if (key.Key == Key.I) { }
            if (key.Key == Key.OemMinus) 
            {
                if (process.ProcessStatus == Status.Learning)
                {
                    if (tempWafer2.point1 == null)
                    {
                        tempWafer2.point1 = new Vector2(Machine.X.ActualPosition, Machine.Y.ActualPosition);
                    }
                    else
                    {
                        tempWafer2.point2 = new Vector2(Machine.X.ActualPosition, Machine.Y.ActualPosition);
                        await Machine.U.MoveAxisInPosAsync(Machine.U.ActualPosition - tempWafer2.GetAngle());
                    }
                }
            }
            if (key.Key == Key.Subtract) 
            {
                Machine.VelocityRegime = Velocity.Step;
            }
            if (key.Key == Key.Add) 
            {
                if(Machine.VelocityRegime== Velocity.Slow) 
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
            new DicingBlade.Views.WaferSettingsView()
            {
                DataContext = new WaferSettingsViewModel()
            }.ShowDialog();
            Wafer = PropContainer.Wafer;
            WaferView = Wafer.MakeWaferView();
            Thickness = 1;
        }
        private void MachineSettings() 
        {
            new DicingBlade.Views.MachineSettingsView()
            {
                DataContext = Settings.Default
            }.ShowDialog();
            Settings.Default.Save();
        }
        private void TechnologySettings() 
        {
            new Views.TechnologySettingsView()
            {
                DataContext = new TechnologySettingsViewModel()
            }.ShowDialog();
        }
        private void Change() 
        {
            Cols = new int[] { Cols[1], Cols[0] };
            Rows = new int[] { Rows[1], Rows[0] };            
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
            
            Thickness = 1;
        }
        //private void Rotate() => Wafer.CurrentAngle += 5;
    }
}
