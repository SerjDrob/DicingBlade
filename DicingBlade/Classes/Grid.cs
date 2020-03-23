using System.Collections.Generic;
using static System.Math;
using netDxf.Entities;
using netDxf;
using System.Linq;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System;
using System.Xml.Serialization;
using System.Runtime.Serialization;


namespace DicingBlade.Classes
{    
    public class Grid:INotifyPropertyChanged
    {
        #region Constructors
        public Grid() { }
        public Grid(Vector2 origin,  params (double degree, double length, double side, double index)[] directions)
        {
            Origin = origin;
            this.directions = directions;
            Lines = new List<(double degree, List<Cut>)>();
            GenerateLines();
            ShapeSize = GetSize();
        }
        public Grid(Vector2 origin, double diameter, params (double degree, double index)[] directions)
        {
            Origin = origin;            
            directionsD = directions;
            this.diameter = diameter;
            Lines = new List<(double degree, List<Cut>)>();            
            GenerateLinesD();
            ShapeSize = GetSize();
        }
        public Grid(IEnumerable<Line> rawLines)
        {
            var xmax = rawLines.Max(l => l.EndPoint.X) > rawLines.Max(l => l.StartPoint.X) ? rawLines.Max(l => l.EndPoint.X) : rawLines.Max(l => l.StartPoint.X);
            var ymax = rawLines.Max(l => l.EndPoint.Y) > rawLines.Max(l => l.StartPoint.Y) ? rawLines.Max(l => l.EndPoint.Y) : rawLines.Max(l => l.StartPoint.Y);
            var xmin = rawLines.Min(l => l.EndPoint.X) < rawLines.Min(l => l.StartPoint.X) ? rawLines.Min(l => l.EndPoint.X) : rawLines.Min(l => l.StartPoint.X);
            var ymin = rawLines.Min(l => l.EndPoint.Y) < rawLines.Min(l => l.StartPoint.Y) ? rawLines.Min(l => l.EndPoint.Y) : rawLines.Min(l => l.StartPoint.Y);
            Origin = new Vector2((xmax - xmin) / 2, (ymax - ymin) / 2);
            var lines = new List<(double degree, Cut line)>();            
            RawLines = new ObservableCollection<Line>(rawLines);
            Lines = new List<(double degree, List<Cut>)>();
            foreach (var line in RawLines)
            {
                lines.Add((GetAngle(line), RotateLine(-GetAngle(line), line, Origin)));
            }

            foreach (var angle in lines.OrderBy(d => d.degree).Select(d => d.degree).Distinct())
            {
                Lines.Add((angle, new List<Cut>(lines.Where(d => d.degree == angle).Select(l => l.line))));
            }
            ShapeSize = GetSize();
        }
        #endregion
        #region Privates
        private (double degree, double length, double side, double index)[] directions;
        private (double degree, double index)[] directionsD;
        private double diameter;
        private ObservableCollection<Line> rawLines;
        private double[] shapeSize;
        #endregion
        #region Publics
        public double[] ShapeSize
        {
            get { return shapeSize; }
            set
            {
                shapeSize = value;
                OnPropertyChanged("ShapeSize");
            }
        }
        [XmlArray]
        public ObservableCollection<Line> RawLines 
        {
            get { return rawLines; }
            private set 
            {
                rawLines = value;
                OnPropertyChanged("RawLines");
            }
        }
        [XmlIgnore]
        public List<(double degree, List<Cut> cuts)> Lines { get; }
        public Vector2 Origin { get; set; }
        #endregion
        #region Functions
        private Cut RotateLine(double angle, Line line, Vector2 origin)
        {
            var translating = new Matrix3(1, 0, -origin.X, 0, 1, -origin.Y, 0, 0, 1);
            var returning = new Matrix3(1, 0, origin.X, 0, 1, origin.Y, 0, 0, 1);
            var rotating = new Matrix3(Cos(angle), -Sin(angle), 0, Sin(angle), Cos(angle), 0, 0, 0, 1);
            var startPoint = returning * rotating * translating * new Vector3(line.StartPoint.X, line.StartPoint.Y, 1);
            var endPoint = returning * rotating * translating * new Vector3(line.EndPoint.X, line.EndPoint.Y, 1);
            return new Cut(startPoint, endPoint);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="angle"></param>
        public void RotateRawLines(double angle)
        {
            List<Line> tempLines = new List<Line>(rawLines);
            var translating = new Matrix3(1, 0, -Origin.X, 0, 1, -Origin.Y, 0, 0, 1);
            var returning = new Matrix3(1, 0, Origin.X, 0, 1, Origin.Y, 0, 0, 1);
            var rotating = new Matrix3(Cos(angle), -Sin(angle), 0, Sin(angle), Cos(angle), 0, 0, 0, 1);
            //RawLines = new ObservableCollection<Line>();
            foreach (var line in tempLines)
            {                
                line.StartPoint = returning * rotating * translating * new Vector3(line.StartPoint.X,line.StartPoint.Y,1);
                line.EndPoint = returning * rotating * translating * new Vector3(line.EndPoint.X,line.EndPoint.Y,1);
               // RawLines.Add(line);
            }            
            RawLines = new ObservableCollection<Line>(tempLines);
        }
        private double GetAngle(Line line)
        {
            return Atan2(line.EndPoint.Y - line.StartPoint.Y, line.EndPoint.X - line.StartPoint.X)*(180/PI);
        }

        public double[] GetSize()
        {
            return new double[]
            {
                RawLines.Max(l=>l.StartPoint.X>l.EndPoint.X?l.StartPoint.X:l.EndPoint.X)-RawLines.Min(l=>l.StartPoint.X<l.EndPoint.X?l.StartPoint.X:l.EndPoint.X),
                RawLines.Max(l=>l.StartPoint.Y>l.EndPoint.Y?l.StartPoint.Y:l.EndPoint.Y)-RawLines.Min(l=>l.StartPoint.Y<l.EndPoint.Y?l.StartPoint.Y:l.EndPoint.Y)
            };
        }

        private void GenerateLines()
        {
            foreach (var direction in directions)
            {                
                List<Cut> tempLines = new List<Cut>();
                int count = (int)Floor(direction.side / direction.index);
                double firstStep = (direction.side - direction.index * count) / 2;
                for (int i = 0; i < count+1; i++)
                {
                    var dx = Origin.X - direction.length / 2;
                    var dy = Origin.Y - direction.side / 2;
                    tempLines.Add(new Cut(new Vector3(dx, firstStep + direction.index * i + dy, 1), new Vector3(direction.length + dx, firstStep + direction.index * i + dy, 1)));
                }
                Lines.Add((direction.degree, tempLines));

            }
            var tempRaws = new List<Line>();
            foreach (var line in Lines)
            {
                foreach (var cut in line.cuts)
                {
                    var tempLine = RotateLine(line.degree*PI/180, new Line(cut.StartPoint, cut.EndPoint), Origin);
                    tempRaws.Add(new Line(tempLine.StartPoint, tempLine.EndPoint));
                }
            }
            RawLines = new ObservableCollection<Line>(tempRaws);
        }
        private void GenerateLinesD()
        {
            double c;
            double D;
            double x1;
            double x2;
            foreach (var direction in directionsD)
            {
                List<Cut> tempLines = new List<Cut>();
                int count = (int)Floor(diameter / direction.index);
                double firstStep = (diameter - direction.index * count) / 2;
                for (int i = 0; i < count + 1; i++)
                {
                    c = firstStep + direction.index * i;
                    D = 4 * (diameter * c - Pow(c, 2));

                    if (D > 0)
                    {
                        x1 = (diameter - Sqrt(D)) / 2;
                        x2 = (diameter + Sqrt(D)) / 2;
                        var dx = Origin.X - diameter / 2;
                        var dy = Origin.Y - diameter / 2;
                        tempLines.Add(new Cut(new Vector3(x1 + dx, dy + firstStep + direction.index * i, 1), new Vector3(x2 + dx, dy + firstStep + direction.index * i, 1)));
                    }
                }
                Lines.Add((direction.degree, tempLines));
            }

            var tempRaws = new List<Line>();
            foreach (var line in Lines)
            {
                foreach (var cut in line.cuts)
                {
                    var tempLine = RotateLine(line.degree * PI / 180, new Line(cut.StartPoint, cut.EndPoint), Origin);
                    tempRaws.Add(new Line(tempLine.StartPoint, tempLine.EndPoint));
                }
            }
            RawLines = new ObservableCollection<Line>(tempRaws);
        }
        #endregion
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string prop)
        {
            if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }
    }
}
