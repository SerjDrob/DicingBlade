using System.Windows.Input;
using DicingBlade.Classes;
using DicingBlade.Properties;
using PropertyChanged;
namespace DicingBlade.ViewModels
{
    [AddINotifyPropertyChangedInterface]
    public class MachineSettingsViewModel
    {
        public ICommand XyObjectiveTeachCmd { get; set; }
        public ICommand XDiskTeachCmd { get; set; }
        public ICommand XyLoadTeachCmd { get; set; }
        private Machine _machine;
        internal MachineSettingsViewModel(Machine machine)
        {
            //this.machine = new Machine(true);
            _machine = machine;
            XyObjectiveTeachCmd = new Command(args => XyObjectiveTeach());
            XyLoadTeachCmd = new Command(args => XyLoadTeach());
            XDiskTeachCmd = new Command(args => XDiskTeach());
        }
        private void XyObjectiveTeach()
        {
            Settings.Default.XObjective = _machine.X.ActualPosition;
            Settings.Default.YObjective = _machine.Y.ActualPosition;
        }
        private void XyLoadTeach()
        {
            Settings.Default.XLoad = _machine.X.ActualPosition;
            Settings.Default.YLoad = _machine.Y.ActualPosition;
        }
        private void XDiskTeach()
        {
            Settings.Default.XDisk = _machine.X.ActualPosition;
        }
    }
}
