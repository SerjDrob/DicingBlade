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

namespace DicingBlade.ViewModels
{
    [AddINotifyPropertyChangedInterface]
    class WaferSettingsViewModel
    {
        private bool IsRound;
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
        public double Width { get; set; }
        public double Height { get; set; }
        public double Thickness { get; set; }
        public double IndexW { get; set; }
        public double IndexH { get; set; }
        public double Diameter { get; set; }
        public Wafer wafer { get; set; }
        public WaferSettingsViewModel() 
        {
            CloseCmd = new Command(args => ClosingWnd());
            SquareVisibility = Visibility.Visible;
            Width = 60;
            Height = 48;
            Thickness = 1;
            IndexH = 1.1;
            IndexW = 2.4;
            Diameter = 40;
        }
        public ICommand CloseCmd { get; set; }
        public void ClosingWnd() 
        {
            if (IsRound) 
            {
                PropContainer.IsRound = true;
                PropContainer.Wafer=new Wafer(new Vector2(Diameter/2, Diameter/2), Thickness, Diameter, (0, IndexW), (90, IndexH));
                //wafer = new Wafer(new Vector2(0, 0), Thickness, Diameter, (0, IndexW), (90, IndexH));
                //wafer.WriteObject<Wafer>("/Wafer.xml");
            }
            else 
            {
                PropContainer.IsRound = false;
                PropContainer.Wafer= new Wafer(new Vector2(Width/2, Height/2), Thickness, (0, Height, Width, IndexW), (90, Width, Height, IndexH));
                //wafer = new Wafer(new Vector2(0, 0), Thickness, (0, Height, Width, IndexW), (90, Width, Height, IndexH));
                //wafer.WriteObject<Wafer>("/Wafer.xml");
            }
        }

    }
}
