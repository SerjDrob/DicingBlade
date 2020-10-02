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

namespace DicingBlade.ViewModels
{
    [AddINotifyPropertyChangedInterface]
    class WaferSettingsViewModel:IWafer
    {
        public bool IsRound { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Thickness { get; set; }
        public double IndexW { get; set; }
        public double IndexH { get; set; }
        public double Diameter { get; set; }
        public string FileName { get; set; }
        
        private Visibility squareVisibility;
        public Visibility SquareVisibility 
        {
            get 
            {
                return squareVisibility; }
            set 
            {
                squareVisibility = value;
                IsRound = value == Visibility.Visible ? false : true;
            }
        }
        
        public Wafer wafer { get; set; }
        public WaferSettingsViewModel() 
        {
            CloseCmd = new Command(args => ClosingWnd());
            OpenFileCmd = new Command(args => OpenFile());
            SaveFileAsCmd = new Command(args => SaveFileAs());
            FileName = Settings.Default.WaferLastFile;
            if (FileName == string.Empty)
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
        

        //public void ClosingWnd() 
        //{
        //    if (IsRound) 
        //    {
        //        PropContainer.IsRound = true;
        //        PropContainer.Wafer=new Wafer(new Vector2(Diameter/2, Diameter/2), Thickness, Diameter, (0, IndexW), (90, IndexH));
        //        //wafer = new Wafer(new Vector2(0, 0), Thickness, Diameter, (0, IndexW), (90, IndexH));
        //        //wafer.WriteObject<Wafer>("/Wafer.xml");
        //    }
        //    else 
        //    {
        //        PropContainer.IsRound = false;
        //        PropContainer.Wafer= new Wafer(new Vector2(Width/2, Height/2), Thickness, (0, Height, Width, IndexW), (90, Width, Height, IndexH));
        //        //wafer = new Wafer(new Vector2(0, 0), Thickness, (0, Height, Width, IndexW), (90, Width, Height, IndexH));
        //        //wafer.WriteObject<Wafer>("/Wafer.xml");
        //    }
        //}

        private void ClosingWnd()
        {
            PropContainer.WaferTemp = this;
            new TempWafer(PropContainer.WaferTemp).SerializeObjectJson(FileName);
            Settings.Default.WaferLastFile = FileName;
            Settings.Default.Save();
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
