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
        public Line2D GetLine2D(double index, int num, double angle);
        public double GetLengthSide(int side);
        public double GetIndexSide(int side);
    }

    public enum Side
    {
        W,
        H
    }
    public abstract class Wafer2D: ICloneable
    {          
        protected IShape Shape;
        public double Thickness { get; protected set; }
        protected Dictionary<int, (double angle, double index, double sideshift, double realangle)> Directions;
        public int CurrentSide { get; private set; }
        public void SetChanges(double indexH, double indexW, double thickness, IShape shape)
        {
            Thickness = thickness;
            Shape = shape;
            Directions = new();
            Directions.Add(0, (0, indexH, 0, 0));
            Directions.Add(1, (90, indexW, 0, 90));
        }
        public bool XMirror { get; set; } = true;
        public int CurrentLinesCount
        {
            get
            {
                return (int)Math.Floor(Shape.GetIndexSide(CurrentSide) / CurrentIndex);
            }
        }
        public double CurrentIndex 
        {
            get => Directions[CurrentSide].index;            
        }
        public int SidesCount
        {
            get => Directions.Count;
        }
        public double CurrentSideAngle
        {
            get => Directions[CurrentSide].angle;
        }
        public double CurrentSideActualAngle
        {
            get => Directions[CurrentSide].realangle;
        }
        private double _prevSideAngle = 0;
        public double PrevSideAngle { get => _prevSideAngle; }
        private double _prevSideActualAngle = 0;
        public double PrevSideActualAngle { get => _prevSideActualAngle; }
        public void SetSide(int side)
        {
            if (side < 0 | side > Directions.Count - 1)
            {
                throw new Exception("");
            }
            else
            {
                _prevSideAngle = Directions[CurrentSide].angle;
                _prevSideActualAngle = Directions[CurrentSide].realangle;
                CurrentSide = side;
            }
        }
        public void SetCurrentIndex(double index)
        {
            var tuple = Directions[CurrentSide];
            Directions[CurrentSide] = (tuple.angle, index, tuple.sideshift, tuple.realangle);
        }
        public double CurrentSideLength 
        { 
            get 
            {
                return Shape.GetLengthSide(CurrentSide);
            }
        }
        public double CurrentShift
        {
            get
            {
                return Directions[CurrentSide].sideshift;
            }
        }
        public void SetShape(IShape shape)
        {
            Shape = shape;
        }
        public double GetNearestY(double y)
        {
            var side = Shape.GetIndexSide(CurrentSide);
            var index = Directions[CurrentSide].index;
            var bias = (side - Math.Floor(side / index) * index) / 2;

            var num = 0;
            if ((num = GetNearestNum(y))!=-1)
            {
                return num * index + bias - side / 2;
            }
            else
            {
                throw new Exception("");
            }
        }
        public Line2D GetNearestCut(double y)
        {
            var side = Shape.GetLengthSide(CurrentSide);
            var index = Directions[CurrentSide].index;
            var num = 0;
            if ((num = GetNearestNum(y)) != -1)
            {
                return this[num];
            }
            else
            {
                throw new Exception("");
            }
        }
        private int GetNearestNum(double y)
        {
            var side = Shape.GetIndexSide(CurrentSide);
            var index = Directions[CurrentSide].index;
            var bias = (side - Math.Floor(side / index) * index) / 2;

            var ypos = y + side / 2;
            var delta = side;            
            var num = -1;
            for (int i = 0; i < side / index; i++)
            {
                var d = Math.Abs(ypos - i * index - bias);
                if (d <= delta)
                {                    
                    delta = d;
                    num = i;
                }
            }
            return num;
        }
        public void TeachSideShift(double y)
        {
            Directions[CurrentSide] = (Directions[CurrentSide].angle, Directions[CurrentSide].index, -y + GetNearestY(y), Directions[CurrentSide].realangle);
        }
        public void AddToSideShift(double delta)
        {
            Directions[CurrentSide] = (Directions[CurrentSide].angle, Directions[CurrentSide].index, Directions[CurrentSide].sideshift + delta, Directions[CurrentSide].realangle);
        }
        public void TeachSideAngle(double angle)
        {
            Directions[CurrentSide] = (Directions[CurrentSide].angle, Directions[CurrentSide].index, Directions[CurrentSide].sideshift, angle);
        }
        public Line2D this[int cutNum]
        {
            get 
            {   
                var angle = Directions[CurrentSide].angle;
                var line = Shape.GetLine2D(CurrentIndex, cutNum, angle);
                var sign = XMirror ? -1 : 1;
                line.Start.X *= sign;
                line.End.X *= sign;
                return line;    
            }
        }
        public double this[double zratio]
        {
            get
            {
                return zratio * Thickness;
            }
        }

        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }
    public class Rectangle2D : IShape
    {
        public double Width { get; private set; }
        public double Height { get; private set; }
        public Rectangle2D(double width, double height)
        {
            Width = width;
            Height = height;
        }
        public Line2D GetLine2D(double index, int num, double angle)
        {
            var delta0 = (Height - Math.Floor(Height / index) * index) / 2;
            var delta90 = (Width - Math.Floor(Width / index) * index) / 2;
            var zeroShift = index * num;           
            return angle switch
            {
                0 => new Line2D() { Start = new Point(-Width/2,zeroShift - (Height/2) + delta0), End=new Point(Width/2,zeroShift-(Height/2) + delta0) },
                90 => new Line2D() { Start = new Point(-Height / 2, zeroShift-(Width/2) + delta90), End = new Point(Height / 2, zeroShift-(Width/2) + delta90) }
            };
        }
        public bool InYArea(double zeroShift, double angle)
        {
            return angle switch
            {
                0 => (zeroShift + Height/2) < Height & (zeroShift + Height / 2) > 0,
                90 => (zeroShift + Width / 2) < Width & (zeroShift + Width / 2) > 0
            };
        }
        public double GetLengthSide(int side)
        {
            return side switch
            {
                0 => Width,
                1 => Height
            };
        }
        public double GetIndexSide(int side)
        {
            return side switch
            {
                1 => Width,
                0 => Height
            };
        }
    }
   
    public class Substrate2D : Wafer2D
    {
        public Substrate2D(double indexH, double indexW, double thickness, IShape shape)
        {
            base.SetChanges(indexH, indexW, thickness, shape);
        }

    }
}
