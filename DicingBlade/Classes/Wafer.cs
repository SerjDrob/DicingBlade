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
using PropertyChanged;

namespace DicingBlade.Classes
{
    [AddINotifyPropertyChangedInterface]
    public class Wafer
    {
        private double thickness;       
        /// <summary>
        /// Признак выравненности по определённому углу
        /// </summary>
        private double alligned;        
        public bool IsRound { get; set; }
        public double CurrentAngle { get; set; }       
        public Grid Grid { get; set; }
        public double Thickness { get; set; }
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
        private void RotateWafer(double angle, Vector2 origin) => Grid.RotateRawLines(angle);
        public Cut GetNearestCut(double y) 
        {
            double diff = Math.Abs(Grid.Lines[CurrentAngle].First().StartPoint.Y-y);
            int index = 0;
            for (int i = 0; i < Grid.Lines[CurrentAngle].Count; i++)
            {
                Cut item = (Cut)Grid.Lines[CurrentAngle][i];
                if (Math.Abs(Grid.Lines[CurrentAngle][i].StartPoint.Y - y) < diff)
                {
                    diff = (Math.Abs(Grid.Lines[CurrentAngle][i].StartPoint.Y - y));
                    index = i;
                }
            }
            return (Cut)Grid.Lines[CurrentAngle][index];
        }


    }

}
