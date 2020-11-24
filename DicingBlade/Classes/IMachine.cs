using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DicingBlade.Classes
{
    enum Groups
    {
        XY
    }
    enum Valves
    {

    }
    enum Sensors
    {

    }
    struct MotionDeviceConfigs
    {

    }
    interface IMachine
    {
        //private Dictionary<Ax, Axis> _axes;// { get; set; }
        //private Dictionary<Ax, Axis> GetAxes(Ax[] axes);
        //private Dictionary<Valves, Do> _valves;// { get; set; }
        //private Dictionary<Sensors, Di> _sensors;// { get; set; }
        //private Dictionary<Place, (Double, Double, Double, Double)> _places;// { get; set; }
        //private void SetGpVel(Groups group, (Double, Double, Double, Double) position, Velocity velocity);

        public bool MachineInit { get; set; }
        public MotionDevice MotionDevice { get; set; }
        public ISpindle Spindle { get; set; }
        public IVideoCapture VideoCamera { get; set; }

        public event DiEventHandler CheckTheSensor;//by sensor name from sensor enum
        public void ConfigureValves(Dictionary<Valves, Do> valves);
        public void ConfigureSensors(Dictionary<Sensors, Di> sensors);
        public void ConfigureAxes(Ax[] axes);
        public void AddGroup(Groups group, Axis[] axes);
        public void ConfigureGeometry(Dictionary<Place, (Double, Double, Double, Double)> places);
        public void SwitchTheValve(Valves valve,bool state);
        #region Motions
        public void Stop(Ax axis);
        public void GoWhile(Ax axis, AxDir direction);
        public void EmgStop();
        public Task GoThereAsync(Place place);
        public Task MoveGpInPosAsync(Groups group, (Double, Double, Double, Double) position);
        public Task MoveAxInPosAsync(double position);
        public (Double, Double, Double, Double) TranslateActualCoors(Place place);
        public (Double, Double, Double, Double) TranslateActualCoors(Place place, (Double, Double, Double, Double) position);
        public void EmgScenario( /*DIEventArgs eventArgs*/);

        #region Settings
        //private bool DevicesConnection()
        //{
        //    MotionDevice motionDevice = default;
        //    try
        //    {
        //        motionDevice = new MotionDevice();
        //        _mUlAxisCount = motionDevice.GetAxisCount();
        //    }
        //    catch (MotionException e)
        //    {
        //        MessageBox.Show(e.Message);
        //        return false;
        //    }

        //    string strTemp;

        //    var axisEnableEvent = new uint[_mUlAxisCount];
        //    var gpEnableEvent = new uint[1];

        //    uint result;
        //    for (var i = 0; i < axisEnableEvent.Length; i++)
        //    {
        //        result = Motion.mAcm_AxOpen(MotionDevice.DeviceHandle, (ushort)i, ref MAxishand[i]);
        //        if (result != (uint)ErrorCode.SUCCESS)
        //        {
        //            strTemp = "Open Axis Failed With Error Code: [0x" + Convert.ToString(result, 16) + "]";
        //            MessageBox.Show(strTemp + " " + result);
        //            return false;
        //        }

        //        double cmdPosition = 0;
        //        //Set command position for the specified axis
        //        result = Motion.mAcm_AxSetCmdPosition(MAxishand[i], cmdPosition);
        //        //Set actual position for the specified axis
        //        result = Motion.mAcm_AxSetActualPosition(MAxishand[i], cmdPosition);

        //        axisEnableEvent[i] |= (uint)EventType.EVT_AX_MOTION_DONE;
        //        axisEnableEvent[i] |= (uint)EventType.EVT_AX_VH_START;
        //        axisEnableEvent[i] |= (uint)EventType.EVT_AX_HOME_DONE;

        //        // axisEnableEvent[i] |= (uint)EventType.EVT_AX_COMPARED;
        //    }

        //    Motion.mAcm_EnableMotionEvent(MotionDevice.DeviceHandle, axisEnableEvent, gpEnableEvent, (uint)_mUlAxisCount, 1);
        //    _mBInit = true;

        //    X = new Axis(0, MAxishand[0], 0);
        //    Y = new Axis(12.8, MAxishand[3], 3);
        //    Z = new Axis(0, MAxishand[2], 2);
        //    U = new Axis(0, MAxishand[1], 1);
        //    _axes = new Axis[4];
        //    _axes[X.AxisNum] = X;
        //    _axes[Y.AxisNum] = Y;
        //    _axes[Z.AxisNum] = Z;
        //    _axes[U.AxisNum] = U;

        //    result = Motion.mAcm_GpAddAxis(ref _xYhandle, X.Handle);
        //    if (result != (uint)ErrorCode.SUCCESS)
        //    {
        //        strTemp = "Open Axis Failed With Error Code: [0x" + Convert.ToString(result, 16) + "]";
        //        MessageBox.Show(strTemp + " " + result);
        //        return false;
        //    }

        //    result = Motion.mAcm_GpAddAxis(ref _xYhandle, Y.Handle);
        //    if (result != (uint)ErrorCode.SUCCESS)
        //    {
        //        strTemp = "Open Axis Failed With Error Code: [0x" + Convert.ToString(result, 16) + "]";
        //        MessageBox.Show(strTemp + " " + result);
        //        return false;
        //    }

        //    // _spindleModbus.Connect();

        //    if (_mBInit)
        //    {
        //        MachineInit = true;
        //    }

        //    return true;
        //}
        public void SetConfigs(MotionDeviceConfigs configs);
        public void SetVelocity(Velocity velocity);
        public void ResetErrors();
                            //public void RefreshSettings()
                            //{
                            //    _bridge = new Bridge
                            //    {
                            //        Air = Settings.Default.AirSensorDsbl,
                            //        CoolantWater = Settings.Default.CoolantSensorDsbl,
                            //        SpindleWater = Settings.Default.SpindleCntrlDsbl,
                            //        ChuckVacuum = Settings.Default.VacuumSensorDsbl
                            //    };
                            //    CameraChuckCenter = new Vector2(Settings.Default.XObjective, Settings.Default.YObjective);
                            //    CameraBladeOffset = Settings.Default.DiskShift;
                            //    BladeChuckCenter = new Vector2(Settings.Default.XDisk, CameraChuckCenter.Y + CameraBladeOffset);
                            //    WaferLoadCenter = new Vector2(Settings.Default.XLoad, Settings.Default.YLoad);
                            //    ZBladeTouch = Settings.Default.ZTouch;
                            //}
       

        #endregion
        #endregion

        //public Axis X { get; set; }
        //public Axis Y { get; set; }
        //public Axis Z { get; set; }
        //public Axis U { get; set; }



        //public event DiEventHandler OnVacuumWanished;
        //public event DiEventHandler OnCoolWaterWanished;
        //public event DiEventHandler OnSpinWaterWanished;
        //public event DiEventHandler OnAirWanished;



        //#region Better to be placed in View

        //public double CameraScale { get; set; } = 1;
        //public double TeachMarkersRatio { get; set; } = 2;//means 1/2
        //#endregion


        #region MachineGeometry
        //public Vector2 BladeChuckCenter { get; set; }
        //public Vector2 WaferLoadCenter { get; set; }
        //public double CameraBladeOffset { get; set; }
        //public Vector2 CameraChuckCenter { get; set; }
        //public double CameraFocus { get; set; }
        ///// <summary>
        /////     Координата касания диском стола
        ///// </summary>
        //public double ZBladeTouch { get; set; }
        /// <summary>
        ///     Возвращает текущие координаты в системе центр столика - ось объектива камеры.
        /// </summary>
        //public Vector2 CoSystemCurrentCoors => new Vector2(X.ActualPosition - CameraChuckCenter.X,
        //    Y.ActualPosition - CameraChuckCenter.Y);

        /// <summary>
        ///     Возвращает текущие координаты в системе центр столика - центр кромки диска.
        /// </summary>
        //public Vector2 CbSystemCurrentCoors => new Vector2(X.ActualPosition - BladeChuckCenter.X, Y.ActualPosition - BladeChuckCenter.Y);

        /// <summary>
        ///     Перевод системы центр столика в систему центр кромки диска
        /// </summary>
        //public Vector2 CtoBSystemCoors(Vector2 coordinates)
        //{
        //    return new Vector2(coordinates.X + BladeChuckCenter.X, coordinates.Y + BladeChuckCenter.Y);
        //}

        /// <summary>
        /// Перевод системы центр столика в систему центр столика - ось объектива камеры
        /// </summary>
        /// <param name = "coordinates" ></ param >
        /// < returns ></ returns >
        //public Vector2 CtoCSystemCoors(Vector2 coordinates)
        //{
        //    return new Vector2(coordinates.X + CameraChuckCenter.X, coordinates.Y + CameraChuckCenter.Y);
        //}

        //#endregion








        #region Camera
        
        //public bool GetSnapShot
        //{
        //    get => _stopCamera;
        //    set
        //    {
        //        if (value)
        //        {
        //            VideoCamera.FreezeCameraImage();
        //        }
        //        else VideoCamera.StartCamera(0);
        //        _stopCamera = value;
        //    }

        //}
        #endregion

        

        //#region Valves
        //public bool SwitchOnCoolantWaterValve
        //{
        //    get => !_testRegime && U.GetDo(Do.Out4);
        //    set
        //    {
        //        if (!_testRegime) U.SetDo(Do.Out4, (byte)(value ? 1 : 0));
        //    }
        //}
        //public bool SwitchOnChuckVacuumValve
        //{
        //    get => !_testRegime && Z.GetDo(Do.Out5);
        //    set
        //    {
        //        if (!_testRegime) Z.SetDo(Do.Out5, (byte)(value ? 1 : 0));
        //    }
        //}
        //public bool SwitchOnBlowingValve
        //{
        //    get => !_testRegime && Z.GetDo(Do.Out6);
        //    set
        //    {
        //        if (!_testRegime) Z.SetDo(Do.Out6, (byte)(value ? 1 : 0));
        //    }
        //}
        //#endregion

        //#region Sensors
        //public bool CoolantWaterSensor { get; set; }
        //public bool ChuckVacuumSensor { get; set; }
        //public bool AirSensor { get; set; }
        //public bool BladeSensor { get; set; }
        //public bool SpindleWaterSensor { get; set; }
        //#endregion

        


       

















        #region Методы

        public bool SetOnChuck()
        {
            if (!ChuckVacuum) SwitchOnChuckVacuum = true;
            Thread.Sleep(100);
            if (ChuckVacuum)
            {
                return true;
            }

            SayMessage(Messages.SetAndTurnOnVacuum);
            return false;
        }

        private void MachineState() // Производит опрос всех датчиков, линеек, координат
        {
            var axEvtStatusArray = new uint[4];//axes.Length
            var gpEvtStatusArray = new uint[1];
            while (true)
            {
                CheckSensors();
                //SpindleFreq = _spindleModbus.ReadHoldingRegisters(0xD000, 1)[0]/10;
                //SpindleCurrent = _spindleModbus.ReadHoldingRegisters(0xD001, 1)[0]/10;
                if (!_testRegime)
                {
                    _result = Motion.mAcm_CheckMotionEvent(MotionDevice.DeviceHandle, axEvtStatusArray, gpEvtStatusArray, (uint)_mUlAxisCount, 0, 10);
                    foreach (var ax in _axes)
                    {
                        _result = Motion.mAcm_AxGetMotionIO(ax.Handle, ref _ioStatus);
                        if (_result == (uint)ErrorCode.SUCCESS)
                        {
                            ax.LmtN = (_ioStatus & (uint)Ax_Motion_IO.AX_MOTION_IO_LMTN) > 0 ? true : false;
                            ax.LmtP = (_ioStatus & (uint)Ax_Motion_IO.AX_MOTION_IO_LMTP) > 0 ? true : false;
                        }

                        for (var channel = 0; channel < 4; channel++)
                        {
                            _result = Motion.mAcm_AxDiGetBit(ax.Handle, (ushort)channel, ref _bitData);
                            if (_result == (uint)ErrorCode.SUCCESS) ax.DIs &= ~_bitData << channel;
                        }

                        _result = Motion.mAcm_AxGetCmdPosition(ax.Handle, ref _position);
                        if (_result == (uint)ErrorCode.SUCCESS) ax.CmdPosition = _position;

                        _result = Motion.mAcm_AxGetActualPosition(ax.Handle, ref _position);
                        if (_result == (uint)ErrorCode.SUCCESS) ax.ActualPosition = _position;

                        //if (!SpindleWater) OnSpinWaterWanished(/*new DIEventArgs()*/);
                        //if (!CoolantWater) OnCoolWaterWanished(/*new DIEventArgs()*/);
                        //if (!ChuckVacuum) OnVacuumWanished(/*new DIEventArgs()*/);
                        if (!Air) OnAirWanished?.Invoke( /*new DIEventArgs()*/);


                        //TrigVar trig = new TrigVar();
                        //DIEventArgs dI = new DIEventArgs();
                        ////trig.trigger(Air, (dI)=>OnAirWanished);
                        ///

                        if (_result == (uint)ErrorCode.SUCCESS)
                        {
                            for (var i = 0; i < _axes.Length; i++)
                            {
                                if ((axEvtStatusArray[i] & (uint)EventType.EVT_AX_MOTION_DONE) > 0)
                                    _axes[i].MotionDone = true;

                                if ((axEvtStatusArray[i] & (uint)EventType.EVT_AX_HOME_DONE) > 0) _axes[i].HomeDone = true;
                            }
                        }
                    }
                }
                Thread.Sleep(1);
            }
        }

        private void MachineEvents()
        {
            var axEvtStatusArray = new uint[_axes.Length];
            var gpEvtStatusArray = new uint[1];

            while ( /*m_bInit*/ true)
            {
                _result = Motion.mAcm_CheckMotionEvent(MotionDevice.DeviceHandle, axEvtStatusArray, gpEvtStatusArray, (uint)_mUlAxisCount,
                    0, 10);
                if (_result == (uint)ErrorCode.SUCCESS)
                {
                    for (var i = 0; i < _axes.Length; i++)
                    {
                        if ((axEvtStatusArray[i] & (uint)EventType.EVT_AX_MOTION_DONE) > 0) _axes[i].MotionDone = true;

                        //if ((AxEvtStatusArray[i] & (uint)EventType.EVT_AX_VH_START) == 0)
                        //{
                        //    axes[i].MotionDone = true;
                        //}


                        if ((axEvtStatusArray[i] & (uint)EventType.EVT_AX_HOME_DONE) > 0) _axes[i].HomeDone = true;
                        //else axes[i].HomeDone = false;
                    }

                    Thread.Sleep(100);
                }
            }
        }

        private void CheckSensors()
        {
            if (!_testRegime)
            {
                ChuckVacuum = _bridge.ChuckVacuum | X.GetDi(Di.In3);
                SpindleWater = _bridge.SpindleWater | X.GetDi(Di.In1);
                CoolantWater = _bridge.CoolantWater | X.GetDi(Di.In2);
                Air = _bridge.Air | Z.GetDi(Di.In1);
            }
            else
            {
                ChuckVacuum = _bridge.ChuckVacuum;
                SpindleWater = _bridge.SpindleWater;
                CoolantWater = _bridge.CoolantWater;
                Air = _bridge.Air;
            }
        }

        #endregion
    }
}
