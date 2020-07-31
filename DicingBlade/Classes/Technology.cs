using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PropertyChanged;

namespace DicingBlade.Classes
{
    [AddINotifyPropertyChangedInterface]
    public class Technology : ITechnology
    {
        public Technology() { }
        public string FileName { get; set; }
        public int SpindleFreq { get; set; }
        public double FeedSpeed { get; set; }
        public double WaferBladeGap { get; set; }
        public double FilmThickness { get; set; }
        public double UnterCut { get; set; }
        public int PassCount { get; set; }
        public Directions PassType { get; set; }
        public int StartControlNum { get; set; }
        public int ControlPeriod { get; set; }
    }
}
