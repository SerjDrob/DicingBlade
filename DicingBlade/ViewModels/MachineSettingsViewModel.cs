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
        private double _xCurrentPosition;
        private double _yCurrentPosition;
        internal MachineSettingsViewModel(double x, double y)
        {
            //this.machine = new Machine(true);
            _xCurrentPosition = x;
            _yCurrentPosition = y;
            XyObjectiveTeachCmd = new Command(args => XyObjectiveTeach());
            XyLoadTeachCmd = new Command(args => XyLoadTeach());
            XDiskTeachCmd = new Command(args => XDiskTeach());
        }
        private void XyObjectiveTeach()
        {
            Settings.Default.XObjective = _xCurrentPosition;
            Settings.Default.YObjective = _yCurrentPosition;
        }
        private void XyLoadTeach()
        {
            Settings.Default.XLoad = _xCurrentPosition;
            Settings.Default.YLoad = _yCurrentPosition;
        }
        private void XDiskTeach()
        {
            Settings.Default.XDisk = _xCurrentPosition;
        }
    }
}
