using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PropertyChanged;
using System.Collections.ObjectModel;
using netDxf.Entities;
using netDxf;
using static System.Math;

namespace DicingBlade.Classes
{
    [AddINotifyPropertyChangedInterface]
    public class WaferView
    {
        public WaferView(ICollection<Line> RawLines, Vector2 center)
        {
            this.RawLines = new ObservableCollection<Line>(RawLines);
            GridCenter = new Vector2(center.X,center.Y);
            ShapeSize = GetSize();
            this.Angle = 0;
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
        private Vector2 GridCenter { get; set; }        
    }
}
