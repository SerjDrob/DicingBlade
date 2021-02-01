using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows;

namespace DicingBlade.Classes
{
    public struct Point3D
    {
        public double X;
        public double Y;
        public double Z;                
    }
    
    public struct Line2D
    {
        public Point Start;
        public Point End;
    }
    public interface IShape
    {
        public bool InYArea(double zeroShift, double angle);
        public Line2D GetLine2D(double zeroShift, double angle);
    }

    public enum Side
    {
        W,
        H
    }
    public abstract class Wafer2D
    {
        protected IShape _shape;
        protected double _thickness;
        protected double _indexH;
        protected double _indexW;
        protected Dictionary<int, double> _directions;
        public Side Side { get; private set; }
        public double CurrentIndex { get; private set; }
        public double CurrentShift { get; private set; }
        public Point3D GetNearestPoint(double y)
        {
            return new Point3D();
        }
        public Point3D this[int dirNum, int cutNum]
        {
            get 
            {
                var shift = cutNum * CurrentIndex + CurrentShift;
                var angle = _directions[dirNum];
                var line = _shape.GetLine2D(shift, angle);
                return new Point3D() { X = line.Start.X, Y = line.Start.Y, Z = 0 };
            }
        }
    }
    public class Rectangle2D : IShape
    {
        private double _width;
        private double _height;
        public Rectangle2D(double width, double height)
        {
            _width = width;
            _height = height;
        }
        public Line2D GetLine2D(double zeroShift, double angle)
        {
            return angle switch
            {
                0 => new Line2D() { Start = new Point(-_width/2,zeroShift), End=new Point(_width/2,zeroShift) },
                90 => new Line2D() { Start = new Point(-_height / 2, zeroShift), End = new Point(_height / 2, zeroShift) }
            };
        }

        public bool InYArea(double zeroShift, double angle)
        {
            throw new NotImplementedException();
        }
    }
}
