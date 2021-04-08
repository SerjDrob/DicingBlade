using System.Windows.Media;
using PropertyChanged;

namespace DicingBlade.Classes
{
    [AddINotifyPropertyChangedInterface]
    public class TraceLine
    {
        public double XStart { get; set; }
        public double XEnd { get; set; }
        public double YStart { get; set; }
        public double YEnd { get; set; }
        public Brush Brush { get; set; }
    }
}