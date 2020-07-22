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
using PropertyChanged;


namespace DicingBlade.Classes
{    
    [AddINotifyPropertyChangedInterface]
    public class Grid
    {
        #region Constructors
        public Grid() { }
        public Grid(Vector2 origin,  params (double degree, double length, double side, double index)[] directions)
        {
            GridCenter = origin;
            this.directions = directions;
            Lines = new Dictionary<double, List<Cut>>();
            GenerateLines();
            //ShapeSize = GetSize();
        }
        public Grid(Vector2 origin, double diameter, params (double degree, double index)[] directions)
        {
            GridCenter = origin;            
            directionsD = directions;
            this.diameter = diameter;
            Lines = new Dictionary<double, List<Cut>>();            
            GenerateLinesD();
            //ShapeSize = GetSize();
        }
        public Grid(IEnumerable<Line> rawLines)
        {
            var xmax = rawLines.Max(l => l.EndPoint.X) > rawLines.Max(l => l.StartPoint.X) ? rawLines.Max(l => l.EndPoint.X) : rawLines.Max(l => l.StartPoint.X);
            var ymax = rawLines.Max(l => l.EndPoint.Y) > rawLines.Max(l => l.StartPoint.Y) ? rawLines.Max(l => l.EndPoint.Y) : rawLines.Max(l => l.StartPoint.Y);
            var xmin = rawLines.Min(l => l.EndPoint.X) < rawLines.Min(l => l.StartPoint.X) ? rawLines.Min(l => l.EndPoint.X) : rawLines.Min(l => l.StartPoint.X);
            var ymin = rawLines.Min(l => l.EndPoint.Y) < rawLines.Min(l => l.StartPoint.Y) ? rawLines.Min(l => l.EndPoint.Y) : rawLines.Min(l => l.StartPoint.Y);
            GridCenter = new Vector2((xmax - xmin) / 2, (ymax - ymin) / 2);
            var lines = new List<(double degree, Cut line)>();            
            RawLines = new ObservableCollection<Line>(rawLines);
            Lines = new Dictionary<double, List<Cut>>();
            foreach (var line in RawLines)
            {
                lines.Add((GetAngle(line), RotateLine(-GetAngle(line), line, GridCenter)));
            }

            foreach (var angle in lines.OrderBy(d => d.degree).Select(d => d.degree).Distinct())
            {
                Lines.Add(angle, new List<Cut>(lines.Where(d => d.degree == angle).Select(l => l.line)));
            }
            //ShapeSize = GetSize();
        }
        #endregion
        #region Privates
        private (double degree, double length, double side, double index)[] directions;
        private (double degree, double index)[] directionsD;
        private double diameter;
        //private double[] ShapeSize { get; set; }
        private Vector2 GridCenter { get; set; }

        #endregion
        #region Publics

        public ObservableCollection<Line> RawLines { get; set; }        
        public Dictionary<double, List<Cut>> Lines { get; }
        
        public (Vector2 start, Vector2 end) GetCenteredLine(double angle, int line)
        {
            //  if (!Lines.Keys.Contains(angle)) throw;
            Vector2 start = Lines[angle][line].StartPoint.SplitZ() - GridCenter;
            Vector2 end = Lines[angle][line].EndPoint.SplitZ() - GridCenter;
            return (start,end);
        }
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
        //public void RotateRawLines(double angle)
        //{
        //    List<Line> tempLines = new List<Line>(RawLines);
        //    var translating = new Matrix3(1, 0, -GridCenter.X, 0, 1, -GridCenter.Y, 0, 0, 1);
        //    var returning = new Matrix3(1, 0, GridCenter.X, 0, 1, GridCenter.Y, 0, 0, 1);
        //    var rotating = new Matrix3(Cos(angle), -Sin(angle), 0, Sin(angle), Cos(angle), 0, 0, 0, 1);
        //    //RawLines = new ObservableCollection<Line>();
        //    foreach (var line in tempLines)
        //    {                
        //        line.StartPoint = returning * rotating * translating * new Vector3(line.StartPoint.X,line.StartPoint.Y,1);
        //        line.EndPoint = returning * rotating * translating * new Vector3(line.EndPoint.X,line.EndPoint.Y,1);
        //       // RawLines.Add(line);
        //    }            
        //    RawLines = new ObservableCollection<Line>(tempLines);
        //}
        private double GetAngle(Line line)
        {
            return Atan2(line.EndPoint.Y - line.StartPoint.Y, line.EndPoint.X - line.StartPoint.X)*(180/PI);
        }

        private double[] GetSize()
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
                    var dx = GridCenter.X - direction.length / 2;
                    var dy = GridCenter.Y - direction.side / 2;
                    tempLines.Add(new Cut(new Vector3(dx, firstStep + direction.index * i + dy, 1), new Vector3(direction.length + dx, firstStep + direction.index * i + dy, 1)));
                }
                Lines.Add(direction.degree, tempLines);

            }            
        }
        public WaferView MakeGridView() 
        {
            var tempRaws = new List<Line>();
            foreach (var degree in Lines.Keys)
            {
                foreach (var cut in Lines[degree])
                {
                    var tempLine = RotateLine(degree * PI / 180, new Line(cut.StartPoint, cut.EndPoint), GridCenter);
                    Vector2 startPoint = new Vector2(tempLine.StartPoint.X - GridCenter.X, tempLine.StartPoint.Y - GridCenter.Y);
                    Vector2 endPoint = new Vector2(tempLine.EndPoint.X - GridCenter.X, tempLine.EndPoint.Y - GridCenter.Y);
                    tempRaws.Add(new Line(startPoint,endPoint));
                }
            }
            return new WaferView(tempRaws, GridCenter);  
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
                        var dx = GridCenter.X - diameter / 2;
                        var dy = GridCenter.Y - diameter / 2;
                        tempLines.Add(new Cut(new Vector3(x1 + dx, dy + firstStep + direction.index * i, 1), new Vector3(x2 + dx, dy + firstStep + direction.index * i, 1)));
                    }
                }
                Lines.Add(direction.degree, tempLines);
            }

            //var tempRaws = new List<Line>();
            //foreach (var degree in Lines.Keys)
            //{
            //    foreach (var cut in Lines[degree])
            //    {
            //        var tempLine = RotateLine(degree * PI / 180, new Line(cut.StartPoint, cut.EndPoint), GridCenter);
            //        tempRaws.Add(new Line(tempLine.StartPoint, tempLine.EndPoint));
            //    }
            //}
            //RawLines = new ObservableCollection<Line>(tempRaws);
        }
        #endregion
    }
}
