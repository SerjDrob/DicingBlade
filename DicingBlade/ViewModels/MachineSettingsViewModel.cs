using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using DicingBlade.Classes;
using DicingBlade.Properties;
using PropertyChanged;
namespace DicingBlade.ViewModels
{
    [AddINotifyPropertyChangedInterface]
    public class MachineSettingsViewModel
    {
        public ICommand XYObjectiveTeachCmd { get; set; }
        public ICommand XDiskTeachCmd { get; set; }
        private Machine machine;
        internal MachineSettingsViewModel(Machine machine) 
        {
            //this.machine = new Machine(true);
            this.machine = machine;
            XYObjectiveTeachCmd = new Command(args => XYObjectiveTeach());
            XDiskTeachCmd = new Command(args => XDiskTeach());
        }
        private void XYObjectiveTeach() 
        {
            Settings.Default.XObjective = machine.X.ActualPosition;
            Settings.Default.YObjective = machine.Y.ActualPosition;
        }
        private void XDiskTeach() 
        {
            Settings.Default.XDisk = machine.X.ActualPosition;            
        }
    }
}
