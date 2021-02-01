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
        public ICommand ZObjectiveTeachCmd { get; set; }
        private double _xCurrentPosition;
        private double _yCurrentPosition;
        private double _zCurrentPosition;
        internal MachineSettingsViewModel(double x, double y, double z)
        {
            //this.machine = new Machine(true);
            _xCurrentPosition = x;
            _yCurrentPosition = y;
            _zCurrentPosition = z;
            XyObjectiveTeachCmd = new Command(args => XyObjectiveTeach());
            XyLoadTeachCmd = new Command(args => XyLoadTeach());
            XDiskTeachCmd = new Command(args => XDiskTeach());
            ZObjectiveTeachCmd = new Command(args => ZObjectiveTeach());
        }
        private void ZObjectiveTeach()
        {
            Settings.Default.ZObjective = _zCurrentPosition;
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
