using System.Collections.Generic;
using System.Linq;
using PropertyChanged;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace DicingBlade.Classes
{
    [AddINotifyPropertyChangedInterface]
    public class WaferView
    {
        public WaferView(ICollection<Line2D> rawLines)
        {
            RawLines = new ObservableCollection<Line2D>(rawLines);
            ShapeSize = GetSize();
        }
        public WaferView()
        {
            RawLines = new ObservableCollection<Line2D>();
        }
        public bool IsRound { get; set; }
        public ObservableCollection<Line2D> RawLines { get; set; }
        private double[] GetSize()
        {
            if (RawLines.Any())
            {
                return new double[]
                            {
                            RawLines.Max(l=>l.Start.X>l.End.X?l.Start.X:l.End.X)-RawLines.Min(l=>l.Start.X<l.End.X?l.Start.X:l.End.X),
                            RawLines.Max(l=>l.Start.Y>l.End.Y?l.Start.Y:l.End.Y)-RawLines.Min(l=>l.Start.Y<l.End.Y?l.Start.Y:l.End.Y)
                            };
            }
            else
            {
                return new double[] { 10, 10 };
            }
        }

        public double[] ShapeSize { get; set; }
        public void SetView(IWaferViewFactory concreteFactory)
        {
            RawLines = concreteFactory.GetWaferView();
            ShapeSize = GetSize();
        }
    }

    public interface IWaferViewFactory
    {
        public ObservableCollection<Line2D> GetWaferView();
    }
    public class WaferViewFactory : IWaferViewFactory
    {
        private Wafer2D _substrate;
        private int _side;
        public WaferViewFactory(Wafer2D wafer)
        {
            _substrate = wafer;
            _side = wafer.CurrentSide;
        }
        public ObservableCollection<Line2D> GetWaferView()
        {

            var rotation = new RotateTransform(0);
            var shift = 0;
            var tempLines = new List<Line2D>();
            for (int i = 0; i < _substrate.SidesCount; i++)
            {
                _substrate.SetSide(_substrate.CurrentSide + i + shift);
                for (int j = 0; j < _substrate.CurrentLinesCount + 1; j++)
                {
                    var pointStart = rotation.Transform(_substrate[j].Start);
                    var pointEnd = rotation.Transform(_substrate[j].End);
                    tempLines.Add(new Line2D() { Start = pointStart, End = pointEnd });
                }

                rotation.Angle += 90;
                if (_substrate.CurrentSide == _substrate.SidesCount - 1)
                {
                    shift = -(i + 2);
                }
            }
            _substrate.SetSide(_side);
            return new(tempLines);
        }
    }
}
