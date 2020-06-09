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
        //private double alligned;
        public bool IsRound { get; set; }
        public bool CurrentCutIsDone(int currentLine)
        {
            return !GetCurrentCut(currentLine).Status;
        }
        public int DirectionLinesCount
        {
            get
            {
                return Grid.Lines[Directions[CurrentAngleNum].angle].Count;
            }
        }
        public int DirectionsCount
        {
            get
            {
                return Directions.Count;
            }
        }
        public int CurrentAngleNum { get; private set; }
        private Grid Grid { get; set; }
        public double Thickness { get; set; }
        private List<(double angle,double indexShift)> Directions { get; set; }
        public double GetCurrentDiretionAngle
        {
            get
            {
                return Directions[CurrentAngleNum].angle;
            }
        }
        public double SetCurrentDirectionAngle
        {
            set
            {
                Directions[CurrentAngleNum] = (value, Directions[CurrentAngleNum].indexShift);
            }
        }
        public double SetCurrentDirectionIndexShift
        {
            set
            {
                Directions[CurrentAngleNum] = (Directions[CurrentAngleNum].angle, value);
            }
        }
        public Wafer() { }
        public Wafer(double thickness, DxfDocument dxf, string layer)
        {
            this.thickness = thickness;
            Grid = new Grid(dxf.Lines.Where(l => l.Layer.Name == layer));
            MakeDirections(Directions, new List<double>(Grid.Lines.Keys));
            CurrentAngleNum = 0;
        }
        private void MakeDirections(List<(double,double)> directions, List<double> list) 
        {
            directions = new List<(double, double)>();
            foreach (var item in list)
            {
                directions.Add((item, 0));
            }
        }
        public Wafer(Vector2 origin, double thickness, params (double degree, double length, double side, double index)[] directions)
        {
            this.thickness = thickness;
            Grid = new Grid(origin, directions);
            MakeDirections(Directions, new List<double>(Grid.Lines.Keys));
            CurrentAngleNum = 0;
            IsRound = false;
        }
        public Wafer(Vector2 origin, double thickness, double diameter, params (double degree, double index)[] directions)
        {
            this.thickness = thickness;
            Grid = new Grid(origin, diameter, directions);
            MakeDirections(Directions, new List<double>(Grid.Lines.Keys));
            CurrentAngleNum = 0;
            IsRound = true;
        }
        public WaferView MakeWaferView() 
        {
            var tempView = Grid.MakeGridView();
            tempView.IsRound = IsRound;
            return tempView;
        }
        public bool NextDir()
        {
            if (Directions.Count - 1 == CurrentAngleNum)
            {
                CurrentAngleNum = 0;
                return false;
            }
            else
            {
                CurrentAngleNum++;
                return true;
            }
        }
        public bool PrevDir() 
        {
            if (CurrentAngleNum == 0)
            {
                CurrentAngleNum = Directions.Count - 1;
                return false;
            }
            else
            {
                CurrentAngleNum--;
                return true;
            }
        }
        //private void RotateWafer(double angle, Vector2 origin) => Grid.RotateRawLines(angle);
        public Cut GetNearestCut(double y) 
        {
            double diff = Math.Abs(Grid.Lines[Directions[CurrentAngleNum].angle].First().StartPoint.Y-y);
            int index = 0;
            for (int i = 0; i < Grid.Lines[Directions[CurrentAngleNum].angle].Count; i++)
            {
                Cut item = (Cut)Grid.Lines[Directions[CurrentAngleNum].angle][i];
                if (Math.Abs(Grid.Lines[Directions[CurrentAngleNum].angle][i].StartPoint.Y - y) < diff)
                {
                    diff = (Math.Abs(Grid.Lines[Directions[CurrentAngleNum].angle][i].StartPoint.Y - y));
                    index = i;
                }
            }
            return (Cut)Grid.Lines[Directions[CurrentAngleNum].angle][index];
        }
        private Cut GetCurrentCut(int currentLine)
        {
            return Grid.Lines[Directions[CurrentAngleNum].angle][currentLine];
        }       
        public (Vector2 start,Vector2 end) GetCurrentLine(int currentLine) 
        {
            return Grid.GetCenteredLine(Directions[CurrentAngleNum].angle, currentLine);
        }
        public double GetCurrentCutZ(int currentLine)
        {
            return (1 - GetCurrentCut(currentLine).CutRatio) * Thickness;
        }
        public bool CurrentCutIncrement(int currentLine) 
        {
            return Grid.Lines[CurrentAngleNum][currentLine].NextCut();
        }
    }

}
