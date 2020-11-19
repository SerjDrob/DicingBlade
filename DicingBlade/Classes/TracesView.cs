using PropertyChanged;
using System.Collections.ObjectModel;
using netDxf.Entities;

namespace DicingBlade.Classes
{
    [AddINotifyPropertyChangedInterface]
    internal class TracesView
    {
        public TracesView()
        {
            Traces = new ObservableCollection<Line>();
        }
        public ObservableCollection<Line> Traces { get; set; }
    }
}
