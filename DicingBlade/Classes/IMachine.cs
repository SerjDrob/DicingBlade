using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DicingBlade.Classes
{
    public enum Groups
    {
        Xy
    }
    public enum Valves
    {
        Blowing,
        Coolant,
        ChuckVacuum,
        SpindleContact
    }
    public enum Sensors
    {
        ChuckVacuum,
        Air,
        Coolant,
        SpindleCoolant
    }
    public struct MotionDeviceConfigs
    {        
        public double MaxAcc;
        public double MaxDec;
        public double MaxVel;
        public int AxDirLogic; // (int)DirLogic.DIR_ACT_HIGH;
        public int PlsInLogic; // (int)PulseInLogic.NOT_SUPPORT;
        public int PlsInMde; // (int)PulseInMode.AB_4X;
        public int PlsInSrc; // (int)PulseInSource.NOT_SUPPORT;
        public int PlsOutMde; // (int)PulseOutMode.OUT_DIR;
        public int Reset; // (int)HomeReset.HOME_RESET_EN;
        public double Acc;
        public double Dec;
        public int Jerk;
        public int Ppu;
        public uint CmpEna; // (uint)CmpEnable.CMP_EN;
        public uint CmpMethod; // (uint)CmpMethod.MTD_GREATER_POSITION;
        public uint CmpSrcAct; // (uint)CmpSource.SRC_ACTUAL_POSITION;
        public uint CmpSrcCmd; // (uint)CmpSource.SRC_COMMAND_POSITION;
        public double HomeVelLow;
        public double HomeVelHigh;
    }
    public struct AxisState
    {        
        public double CmdPos;
        public double ActPos;
        public int Sensors;
        public int Outs;
        public bool PLmt;
        public bool NLmt;
        public bool MotionDone;
        public bool HomeDone;
        public bool VhStart;
    }
    public delegate void AxisStateHandler(int axisNum, AxisState state);
    public delegate void SensorStateHandler(Sensors sensor, bool state);
    public delegate void ValveStateHandler(Valves valve, bool state);
    public delegate void AxisMotioStateHandler(Ax axis, double position, bool nLmt, bool pLmt, bool motionDone, bool motionStart);
    interface IMachine
    {
        public bool MachineInit { get; set; }
        public MotionDevice MotionDevice { get; set; }        
        public Velocity VelocityRegime { get; set; }
        public event SensorStateHandler OnSensorStateChanged;
        public event ValveStateHandler OnValveStateChanged;
        public event AxisMotioStateHandler OnAxisMotionStateChanged;        
        public event BitmapHandler OnVideoSourceBmpChanged;
        public void StartVideoCapture(int ind);
        public void SetBridgeOnSensors(Sensors sensor, bool setBridge);
        public void FreezeVideoCapture();
        public void ConfigureValves(Dictionary<Valves, (Ax, Do)> valves);
        public void ConfigureSensors(Dictionary<Sensors, (Ax, Di)> sensors);
        public void ConfigureAxes((Ax axis, double linecoefficient)[] ax);
        public void ConfigureVelRegimes(Dictionary<Ax, Dictionary<Velocity, double>> velRegimes);
        public void AddGroup(Groups group, IAxis[] axes);
        public void ConfigureGeometry(Dictionary<Place,(Ax, double)[]> places);
        public void ConfigureGeometry(Dictionary<Place, double> places);
        public void ConfigureAxesGroups(Dictionary<Groups, Ax[]> groups);
        public void ConfigureDoubleFeatures(Dictionary<MFeatures, double> doubleFeatures);
        public double GetFeature(MFeatures feature);
        public void SwitchOnValve(Valves valve);
        public void SwitchOffValve(Valves valve);
        public bool GetValveState(Valves valve);
        public double GetGeometry(Place place, int arrNum);
        public double GetGeometry(Place place, Ax axis);
        public double GetAxisSetVelocity(Ax axis);
        #region Motions
        public void Stop(Ax axis);
        public Task WaitUntilAxisStopAsync(Ax axis);
        public void GoWhile(Ax axis, AxDir direction);
        public void EmgStop();
        public Task GoThereAsync(Place place, bool precisely = false);
        public Task MoveGpInPosAsync(Groups group, double[] position, bool precisely = false);
        public Task MoveGpInPlaceAsync(Groups group, Place place, bool precisely = false);
        public Task MoveAxInPosAsync(Ax axis, double position, bool precisely = false);        
        public Task MoveAxesInPlaceAsync(Place place);       
        public (Ax, double)[] TranslateActualCoors(Place place);
        public double TranslateActualCoors(Place place, Ax axis);
        public (Ax, double)[] TranslateActualCoors(Place place, (Ax axis,double pos)[] position);        
        public double TranslateSpecCoor(Place place, double position, Ax axis);
        public void EmgScenario( /*DIEventArgs eventArgs*/);
        #endregion
        #region Settings

        public void SetConfigs((Ax axis, MotionDeviceConfigs configs)[] axesConfigs);
        public void SetVelocity(Velocity velocity);
        public void SetAxFeedSpeed(Ax axis, double feed);
        public void ResetErrors(Ax axis);

        #endregion
        #region Spindle
        public void SetSpindleFreq(int frequency);
        public void StartSpindle();
        public void StopSpindle();
        public event Action<int, double, bool> OnSpindleStateChanging;
        #endregion
    }
}
