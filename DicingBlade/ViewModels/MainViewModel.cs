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
    class MainViewModel:INotifyPropertyChanged
    {
        private Wafer wafer;
        private Machine machine;
        private int[] cols;
        private int[] rows;
        private bool test;
        public Signals MyProperty { get; set; }
        public Map Condition { get; set; }
        private double thickness;
        public double Thickness 
        {
            get { return thickness; }
            set { thickness = value; OnPropertyChanged("Thickness"); }
        }
        public bool Test
        {
            get { return test; }
            set
            {
                test = value;
                OnPropertyChanged("Test");
            }
        }
        public int[] Rows
        {
            get { return rows; }
            set
            {
                rows = value;
                OnPropertyChanged("Rows");
            }
        }
        public int[] Cols
        {
            get { return cols; }
            set
            {
                cols = value;
                OnPropertyChanged("Cols");
            }
        }

        public Wafer Wafer 
        {
            get { return wafer; }
            set 
            {
                wafer = value;
                OnPropertyChanged("Wafer");
            }
        }

        public ICommand OpenFileCmd { get; set; }
        public ICommand RotateCmd { get; set; }
        public ICommand ChangeCmd { get; set; }
        public ICommand KeyDownCmd { get; set; }
        public ICommand WaferSettingsCmd { get; set; }
        public ICommand MachineSettingsCmd { get; set; }
        public MainViewModel()
        {
            Test = false;
            Cols = new int[] { 0, 1 };
            Rows = new int[] { 2, 1 };
            Condition = new Map();
            OpenFileCmd = new Command(args => OpenFile());
            RotateCmd = new Command(args => Rotate());
            ChangeCmd = new Command(args => Change());
            KeyDownCmd = new Command(args => KeyDown(args));
            WaferSettingsCmd = new Command(args => WaferSettings());
            MachineSettingsCmd = new Command(args => MachineSettings());
           // machine = new Machine();
          //  machine.OnAirWanished += Machine_OnAirWanished;
        }

        private void Machine_OnAirWanished(DIEventArgs eventArgs)
        {
            throw new NotImplementedException();
        }

        private void KeyDown(object args) 
        {
            KeyEventArgs key = (KeyEventArgs)args;
            if (key.Key == Key.Q) Condition.Mask ^= (1 << (int)Signals.vacuumValve);
            if (key.Key == Key.W) Condition.Mask ^= (1 << (int)Signals.waterValve);
            if (key.Key == Key.R) Condition.Mask ^= (1 << (int)Signals.blowValve);
            if (key.Key == Key.D) Wafer.CurrentAngle += 0.2;
            if (key.Key == Key.S) Wafer.CurrentAngle -= 0.2;
            if (key.Key == Key.F2) OpenFile();
            if (key.Key == Key.T) Change();
        }

        private void WaferSettings() 
        {            
            new DicingBlade.Views.WaferSettingsView()
            {
                DataContext = new WaferSettingsViewModel()
            }.ShowDialog();
            Wafer = PropContainer.Wafer;
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
        private void Rotate() => Wafer.CurrentAngle += 5;
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string prop)
        {
            if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }
    }
}
