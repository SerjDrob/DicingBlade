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
        public Line2D(Point start, Point end)
        {
            Start = start;
            End = end;
        }
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
    public abstract class Wafer2D
    {
        protected IShape _shape;
        public double Thickness { get; protected set; }
        protected Dictionary<int, (double angle, double index, double sideshift, double realangle)> _directions;
        public int CurrentSide { get; private set; } = 0;
        public void SetChanges(double indexH, double indexW, double thickness, IShape shape)
        {
            Thickness = thickness;
            _shape = shape;
            _directions = new();
            _directions.Add(0, (0, indexH, 0, 0));
            _directions.Add(1, (90, indexW, 0, 90));
        }
        public bool XMirror { get; set; } = true;
        public int CurrentLinesCount
        {
            get
            {
                return (int)Math.Floor(_shape.GetIndexSide(CurrentSide) / CurrentIndex);
            }
        }
        public double CurrentIndex
        {
            get => _directions[CurrentSide].index;
        }
        public int SidesCount
        {
            get => _directions.Count;
        }
        public double CurrentSideAngle
        {
            get => _directions[CurrentSide].angle;
        }
        public double CurrentSideActualAngle
        {
            get => _directions[CurrentSide].realangle;
        }
        private double _prevSideAngle = 0;
        public double PrevSideAngle { get => _prevSideAngle; }
        private double _prevSideActualAngle = 0;
        public double PrevSideActualAngle { get => _prevSideActualAngle; }

        public bool IncrementSide()
        {
            if (CurrentSide == _directions.Count - 1)
            {
                return false;
            }
            else
            {
                SetSide(CurrentSide + 1);
                CurrentCutNum = 0;
                return true;
            }
        }
        public bool DecrementSide()
        {
            if (CurrentSide == 0)
            {
                return false;
            }
            else
            {
                SetSide(CurrentSide - 1);
                CurrentCutNum = 0;
                return true;
            }
        }
        public void SetSide(int side)
        {
            if (side < 0 | side > _directions.Count - 1)
            {
                throw new Exception("");
            }
            else
            {
                _prevSideAngle = _directions[CurrentSide].angle;
                _prevSideActualAngle = _directions[CurrentSide].realangle;
                CurrentSide = side;
            }
        }
        public void SetCurrentIndex(double index)
        {
            var tuple = _directions[CurrentSide];
            _directions[CurrentSide] = (tuple.angle, index, tuple.sideshift, tuple.realangle);
        }
        public double CurrentSideLength
        {
            get
            {
                return _shape.GetLengthSide(CurrentSide);
            }
        }
        public double CurrentShift
        {
            get
            {
                return _directions[CurrentSide].sideshift;
            }
        }
        public void SetShape(IShape shape)
        {
            _shape = shape;
        }
        public double GetNearestY(double y)
        {
            var side = _shape.GetIndexSide(CurrentSide);
            var index = _directions[CurrentSide].index;
            var bias = (side - Math.Floor(side / index) * index) / 2;

            var num = 0;
            if ((num = GetNearestNum(y)) != -1)
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
            var side = _shape.GetLengthSide(CurrentSide);
            var index = _directions[CurrentSide].index;
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
        public int GetNearestNum(double y)
        {
            var side = _shape.GetIndexSide(CurrentSide);
            var index = _directions[CurrentSide].index;
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
            _directions[CurrentSide] = (_directions[CurrentSide].angle, _directions[CurrentSide].index, -y + GetNearestY(y), _directions[CurrentSide].realangle);
        }
        public void AddToSideShift(double delta)
        {
            _directions[CurrentSide] = (_directions[CurrentSide].angle, _directions[CurrentSide].index, _directions[CurrentSide].sideshift + delta, _directions[CurrentSide].realangle);
        }
        public void TeachSideAngle(double angle)
        {
            _directions[CurrentSide] = (_directions[CurrentSide].angle, _directions[CurrentSide].index, _directions[CurrentSide].sideshift, angle);
        }
        public int CurrentCutNum { get; private set; } = 0;
        public bool SetCurrentCutNum(int num)
        {
            if (0 >= num && num < CurrentLinesCount)
            {
                CurrentCutNum = num;
                return true;
            }
            else
            {
                return false;
            }
        }
        public bool IncrementCut()
        {
            if (CurrentCutNum == CurrentLinesCount)
            {
                return false;
            }
            else
            {
                CurrentCutNum++;
                return true;
            }
        }
        public bool DecrementCut()
        {
            if (CurrentCutNum == 0)
            {
                return false;
            }
            else
            {
                CurrentCutNum--;
                return true;
            }
        }
        public Line2D GetCurrentCut() => this[CurrentCutNum];
        public Line2D this[int cutNum]
        {
            get
            {
                if (cutNum < 0) cutNum = 0;
                
                if (cutNum >= CurrentLinesCount)
                {
                    LastCutOfTheSide = true;
                    cutNum = CurrentLinesCount;
                }
                else
                {
                    LastCutOfTheSide = false;
                }
                var angle = _directions[CurrentSide].angle;
                var line = _shape.GetLine2D(CurrentIndex, cutNum, angle);
                var sign = XMirror ? -1 : 1;
                line.Start.X *= sign;
                line.End.X *= sign;
                return line;
            }
        }
        public bool LastCutOfTheSide { get; private set; } = false;
        public bool IsLastSide { get { return SidesCount - 1 == CurrentSide; } }
        public double this[double zratio]
        {
            get
            {
                return zratio * Thickness;
            }
        }
        public void ResetWafer()
        {
            CurrentCutNum = 0;
            CurrentSide = 0;
            LastCutOfTheSide = false;
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
                0 => new Line2D() { Start = new Point(-Width / 2, zeroShift - (Height / 2) + delta0), End = new Point(Width / 2, zeroShift - (Height / 2) + delta0) },
                90 => new Line2D() { Start = new Point(-Height / 2, zeroShift - (Width / 2) + delta90), End = new Point(Height / 2, zeroShift - (Width / 2) + delta90) }
            };
        }
        public bool InYArea(double zeroShift, double angle)
        {
            return angle switch
            {
                0 => (zeroShift + Height / 2) < Height & (zeroShift + Height / 2) > 0,
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
