using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PropertyChanged;
using System.ComponentModel;
using DicingBlade.Classes;
using System.Windows.Input;
using System.Windows;
using netDxf;
using System.Windows.Forms;
using DicingBlade.Properties;
using System.IO;

namespace DicingBlade.ViewModels
{
    [AddINotifyPropertyChangedInterface]
    class WaferSettingsViewModel:IWafer
    {
        private bool _isRound;
        public bool IsRound 
        {
            get { return _isRound; }
            set 
            {
                _isRound = value;
                if (value) SquareVisibility = Visibility.Collapsed;
                else SquareVisibility = Visibility.Visible;
            }
        }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Thickness { get; set; }
        public double IndexW { get; set; }
        public double IndexH { get; set; }
        public double Diameter { get; set; }
        public string FileName { get; set; }        
        public Visibility SquareVisibility { get; set; }       
        public Wafer wafer { get; set; }
        public WaferSettingsViewModel() 
        {
            CloseCmd = new Command(args => ClosingWnd());
            OpenFileCmd = new Command(args => OpenFile());
            SaveFileAsCmd = new Command(args => SaveFileAs());
            ChangeShapeCmd = new Command(args => ChangeShape());
            FileName = Settings.Default.WaferLastFile;
            if (FileName == string.Empty | !File.Exists(FileName))
            {
                SquareVisibility = Visibility.Visible;
                Width = 30;
                Height = 10;
                Thickness = 1;
                IndexH = 1;
                IndexW = 2;
                Diameter = 40;
            }
            else
            {
                ((IWafer)(new TempWafer().DeSerializeObjectJson(FileName))).CopyPropertiesTo(this);                
            }
        }
        public ICommand CloseCmd { get; set; }
        public ICommand OpenFileCmd { get; set; }
        public ICommand SaveFileAsCmd { get; set; }
        public ICommand ChangeShapeCmd { get; set; }
        private void ClosingWnd()
        {
            PropContainer.WaferTemp = this;            
            new TempWafer(PropContainer.WaferTemp).SerializeObjectJson(FileName);
            Settings.Default.WaferLastFile = FileName;
            Settings.Default.Save();
        }

        private void ChangeShape() 
        {
            IsRound ^= true;
        }
        private void OpenFile()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "Файлы пластины (*.json)|*.json";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    FileName = dialog.FileName;
                    ((IWafer)(new TempWafer().DeSerializeObjectJson(FileName))).CopyPropertiesTo(this);
                }
            }
        }
        private void SaveFileAs()
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "Файлы пластины (*.json)|*.json";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    FileName = dialog.FileName;
                    ClosingWnd();
                }
            }
        }

    }
}
