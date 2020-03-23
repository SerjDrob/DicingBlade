using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using netDxf;
using static System.Math;
using netDxf.Entities;
using System.ComponentModel;
using System.Windows;

namespace DicingBlade.Classes
{
    public class Wafer:INotifyPropertyChanged
    {
        private double thickness;
        private double currentAngle;
        /// <summary>
        /// Признак выравненности по определённому углу
        /// </summary>
        private double alligned;
        private Grid grid;
        public bool IsRound { get; set; }
        public double CurrentAngle 
        {
            get { return currentAngle; }
            set 
            {
                //RotateWafer(value - currentAngle,new Vector2(25000,25000));
                currentAngle = value;
                OnPropertyChanged("CurrentAngle");
            } 
        }
        public Grid Grid
        {
            get { return grid; }
            set 
            {
                grid = value;
                OnPropertyChanged("Grid");
            }
        }
        public Wafer() { }
        public Wafer(double thickness, DxfDocument dxf, string layer)
        {
            this.thickness = thickness;
            Grid = new Grid(dxf.Lines.Where(l => l.Layer.Name == layer));           
        }       
        public Wafer(Vector2 origin, double thickness, params (double degree, double length, double side, double index)[] directions) 
        {            
            this.thickness = thickness;
            Grid = new Grid(origin, directions);
            IsRound = false;
        }
        public Wafer(Vector2 origin, double thickness, double diameter, params (double degree, double index)[] directions) 
        {
            this.thickness = thickness;
            Grid = new Grid(origin, diameter, directions);
            IsRound = true;
        }
        private void RotateWafer(double angle, Vector2 origin) 
        {
            Grid.RotateRawLines(angle);
        }
        
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string prop)
        {
            if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }
    }
   
}
