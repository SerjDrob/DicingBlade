using System.Collections.Generic;
using System.Linq;
using PropertyChanged;
using System.Collections.ObjectModel;
using netDxf.Entities;
using netDxf;

namespace DicingBlade.Classes
{
    [AddINotifyPropertyChangedInterface]
    public class WaferView
    {
        public WaferView(ICollection<Line> rawLines)
        {
            RawLines = new ObservableCollection<Line>(rawLines);
            ShapeSize = GetSize();
            Angle = 0;
        }
        public WaferView()
        {
            RawLines = new ObservableCollection<Line>();
        }
        public bool IsRound { get; set; }
        public double Angle { get; set; }
        public ObservableCollection<Line> RawLines { get; set; }
        private double[] GetSize()
        {
            return new double[]
            {
                RawLines.Max(l=>l.StartPoint.X>l.EndPoint.X?l.StartPoint.X:l.EndPoint.X)-RawLines.Min(l=>l.StartPoint.X<l.EndPoint.X?l.StartPoint.X:l.EndPoint.X),
                RawLines.Max(l=>l.StartPoint.Y>l.EndPoint.Y?l.StartPoint.Y:l.EndPoint.Y)-RawLines.Min(l=>l.StartPoint.Y<l.EndPoint.Y?l.StartPoint.Y:l.EndPoint.Y)
            };
        }
        public double[] ShapeSize { get; private set; }
    }
}
