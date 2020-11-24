using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PropertyChanged;

namespace DicingBlade.Classes
{
    [AddINotifyPropertyChangedInterface]
    class Machine4X : IMachine
    {
        public Machine4X() 
        {
            VideoCamera = new USBCamera();
            VideoCamera.StartCamera(0);
            MotionDevice = new MotionDevice();
        }
        public bool MachineInit { get; set; }
        public MotionDevice MotionDevice { get; set; }
        public ISpindle Spindle { get; set; }
        public IVideoCapture VideoCamera { get; set; }

        public event DiEventHandler CheckTheSensor;

        public void AddGroup(Groups group, Axis[] axes)
        {
            throw new NotImplementedException();
        }

        public void ConfigureAxes(Ax[] axes)
        {
            throw new NotImplementedException();
        }

       
        public void ConfigureGeometry(Dictionary<Place, (double, double, double, double)> places)
        {
            throw new NotImplementedException();
        }
               

        public void ConfigureSensors(Dictionary<Sensors, Di> sensors)
        {
            throw new NotImplementedException();
        }

       
        public void ConfigureValves(Dictionary<Valves, Do> valves)
        {
            throw new NotImplementedException();
        }

        public void EmgScenario()
        {
            throw new NotImplementedException();
        }

        public void EmgStop()
        {
            throw new NotImplementedException();
        }

        public Task GoThereAsync(Place place)
        {
            throw new NotImplementedException();
        }

        public void GoWhile(Ax axis, AxDir direction)
        {
            throw new NotImplementedException();
        }

        public Task MoveAxInPosAsync(double position)
        {
            throw new NotImplementedException();
        }

        public Task MoveGpInPosAsync(Groups group, (double, double, double, double) position)
        {
            throw new NotImplementedException();
        }

        public void ResetErrors()
        {
            throw new NotImplementedException();
        }

        public void SetConfigs(MotionDeviceConfigs configs)
        {
            throw new NotImplementedException();
        }

        public void SetVelocity(Velocity velocity)
        {
            throw new NotImplementedException();
        }

        public void Stop(Ax axis)
        {
            throw new NotImplementedException();
        }

        public void SwitchTheValve(Valves valve, bool state)
        {
            throw new NotImplementedException();
        }

        public (double, double, double, double) TranslateActualCoors(Place place)
        {
            throw new NotImplementedException();
        }

        public (double, double, double, double) TranslateActualCoors(Place place, (double, double, double, double) position)
        {
            throw new NotImplementedException();
        }
    }
}
