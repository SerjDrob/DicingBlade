using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PropertyChanged;
using System.Collections.ObjectModel;
using netDxf.Entities;
using netDxf;

namespace DicingBlade.Classes
{
    [AddINotifyPropertyChangedInterface]
    class TracesView
    {
        public TracesView() 
        {
            Traces = new ObservableCollection<Line>();
        }
        public ObservableCollection<Line> Traces { get; set; }
    }
}
