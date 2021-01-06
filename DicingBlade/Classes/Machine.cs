using System;
using System.Diagnostics.SymbolStore;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using Advantech.Motion;
using AForge.Imaging.Filters;
using AForge.Video;
using AForge.Video.DirectShow;
using DicingBlade.Properties;
using netDxf;
using PropertyChanged;
using EasyModbus;
using System.Collections.Generic;

namespace DicingBlade.Classes
{
    [AddINotifyPropertyChangedInterface]
    internal class Machine
    {
        public IVideoCapture VideoCamera { get; set; }
        private readonly bool _testRegime;
        private Axis[] _axes;
        private byte _bitData;
        private Bridge _bridge;
        private uint _ioStatus;
        
        public IntPtr[] MAxishand = new IntPtr[32];
        private bool _mBInit;
        //private IntPtr _mDeviceHandle = IntPtr.Zero;
        private int _mUlAxisCount;
        
        private double _position;
        private uint _result;
        private bool _tokenXy = true;
        private Velocity _velocityRegime;
        private IntPtr _xYhandle = IntPtr.Zero;
        private ModbusClient _spindleModbus;
        public Machine(bool test) // В конструкторе происходит инициализация всех устройств, загрузка параметров.
        {
            _testRegime = test;

            if (!_testRegime)
            {
                _spindleModbus = new ModbusClient("COM1");
                //StartCamera();
                VideoCamera = new USBCamera();
                VideoCamera.StartCamera(0);
                DevicesConnection();
                SetConfigs();
                VelocityRegime = Velocity.Slow;
                OnAirWanished += EmgScenario;
                VelocityRegime = Velocity.Fast;
                RefreshSettings();
            }
            SwitchOnBlowing = false;
            SwitchOnChuckVacuum = false;
            SwitchOnCoolantWater = false;
            var threadCurrentState = new Thread(MachineState);
            threadCurrentState.Start();
        }

        public Velocity VelocityRegime
        {
            get => _velocityRegime;
            set
            {
                _velocityRegime = value;
                SetVelocity(value);
            }
        }
        
       
        public bool MachineInit { get; set; }
        public Vector2 BladeChuckCenter { get; set; }
        public double CameraScale { get; set; } = 1;
        public double TeachMarkersRatio { get; set; } = 2;//means 1/2
        public double CameraBladeOffset { get; set; }
        public Vector2 CameraChuckCenter { get; set; }
        public Vector2 WaferLoadCenter { get; set; }
        /// <summary>
        ///     Возвращает текущие координаты в системе центр столика - ось объектива камеры.
        /// </summary>
        public Vector2 CoSystemCurrentCoors => new Vector2(X.ActualPosition - CameraChuckCenter.X,
            Y.ActualPosition - CameraChuckCenter.Y);

        /// <summary>
        ///     Возвращает текущие координаты в системе центр столика - центр кромки диска.
        /// </summary>
        public Vector2 CbSystemCurrentCoors => new Vector2(X.ActualPosition - BladeChuckCenter.X, Y.ActualPosition - BladeChuckCenter.Y);

        /// <summary>
        ///     Перевод системы центр столика в систему центр кромки диска
        /// </summary>
        public Vector2 CtoBSystemCoors(Vector2 coordinates)
        {
            return new Vector2(coordinates.X + BladeChuckCenter.X, coordinates.Y + BladeChuckCenter.Y);
        }

        /// <summary>
        /// Перевод системы центр столика в систему центр столика - ось объектива камеры
        /// </summary>
        /// <param name="coordinates"></param>
        /// <returns></returns>
        public Vector2 CtoCSystemCoors(Vector2 coordinates)
        {
            return new Vector2(coordinates.X + CameraChuckCenter.X, coordinates.Y + CameraChuckCenter.Y);
        }

        public double CameraFocus { get; set; }
        public bool SpindleWater { get; set; }
        public bool CoolantWater { get; set; }
        public double SpindleFreq { get; set; }
        public double SpindleCurrent { get; set; }

        public bool SwitchOnCoolantWater
        {
            get => !_testRegime && U.GetDo(Do.Out4);
            set
            {
                if (!_testRegime) U.SetDo(Do.Out4, (byte)(value ? 1 : 0));
            }
        }

        public bool ChuckVacuum { get; set; }

        public bool SwitchOnChuckVacuum
        {
            get => !_testRegime && Z.GetDo(Do.Out5);
            set
            {
                if (!_testRegime) Z.SetDo(Do.Out5, (byte)(value ? 1 : 0));
            }
        }

        public bool Air { get; set; }

        public bool SwitchOnBlowing
        {
            get => !_testRegime && Z.GetDo(Do.Out6);
            set
            {
                if (!_testRegime) Z.SetDo(Do.Out6, (byte)(value ? 1 : 0));
            }
        }

        public bool BladeSensor { get; set; }
        public Axis X { get; set; }
        public Axis Y { get; set; }
        public Axis Z { get; set; }
        public Axis U { get; set; }

        /// <summary>
        ///     Координата касания диском стола
        /// </summary>
        public double ZBladeTouch { get; set; }

        //private IntPtr Hand(AxisDirections direction)
        //{
        //    switch (direction)
        //    {
        //        case AxisDirections.XP | AxisDirections.XN: return m_Axishand[0];
        //        case AxisDirections.YP | AxisDirections.YN: return m_Axishand[1];
        //        case AxisDirections.ZP | AxisDirections.ZN: return m_Axishand[2];
        //        case AxisDirections.UP | AxisDirections.UN: return m_Axishand[3];
        //        default: return new IntPtr();
        //    }
        //}

        private (IntPtr, ushort) MoveRelParam(AxisDirections direction)
        {
            return direction switch
            {
                AxisDirections.Xp => (X.Handle, (ushort) VelMoveDir.DIR_POSITIVE),
                AxisDirections.Xn => (X.Handle, (ushort) VelMoveDir.DIR_NEGATIVE),
                AxisDirections.Yp => (Y.Handle, (ushort) VelMoveDir.DIR_POSITIVE),
                AxisDirections.Yn => (Y.Handle, (ushort) VelMoveDir.DIR_NEGATIVE),
                AxisDirections.Zp => (Z.Handle, (ushort) VelMoveDir.DIR_POSITIVE),
                AxisDirections.Zn => (Z.Handle, (ushort) VelMoveDir.DIR_NEGATIVE),
                AxisDirections.Up => (U.Handle, (ushort) VelMoveDir.DIR_POSITIVE),
                AxisDirections.Un => (U.Handle, (ushort) VelMoveDir.DIR_NEGATIVE),
                _ => (new IntPtr(), 1),
            };
        }

        public event DiEventHandler OnVacuumWanished;
        public event DiEventHandler OnCoolWaterWanished;
        public event DiEventHandler OnSpinWaterWanished;
        public event DiEventHandler OnAirWanished;

        private void EmgScenario( /*DIEventArgs eventArgs*/)
        {
        }

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

        public void SayMessage(Messages message)
        {
            switch (message)
            {
                case Messages.SetAndTurnOnVacuum:
                    MessageBox.Show("Неустановленна пластина или неисправна вакуумная система");
                    break;
            }
        }

        

        private bool _stopCamera;
        public bool GetSnapShot
        {
            get => _stopCamera;
            set
            {
                if (value)
                {
                    VideoCamera.FreezeCameraImage();
                }
                else VideoCamera.StartCamera(0);
                _stopCamera = value;
            }

        }

        private bool DevicesConnection()
        {
            MotionDevice motionDevice = default;
            try
            {
                motionDevice = new MotionDevice();
              //  _mUlAxisCount = motionDevice.GetAxisCount();
            }
            catch (MotionException e)
            {
                MessageBox.Show(e.Message);
                return false;
            }

            string strTemp;

            var axisEnableEvent = new uint[_mUlAxisCount];
            var gpEnableEvent = new uint[1];

            uint result;
            for (var i = 0; i < axisEnableEvent.Length; i++)
            {
                result = Motion.mAcm_AxOpen(MotionDevice.DeviceHandle, (ushort)i, ref MAxishand[i]);
                if (result != (uint)ErrorCode.SUCCESS)
                {
                    strTemp = "Open Axis Failed With Error Code: [0x" + Convert.ToString(result, 16) + "]";
                    MessageBox.Show(strTemp + " " + result);
                    return false;
                }

                double cmdPosition = 0;
                //Set command position for the specified axis
                result = Motion.mAcm_AxSetCmdPosition(MAxishand[i], cmdPosition);
                //Set actual position for the specified axis
                result = Motion.mAcm_AxSetActualPosition(MAxishand[i], cmdPosition);

                axisEnableEvent[i] |= (uint)EventType.EVT_AX_MOTION_DONE;
                axisEnableEvent[i] |= (uint)EventType.EVT_AX_VH_START;
                axisEnableEvent[i] |= (uint)EventType.EVT_AX_HOME_DONE;

                // axisEnableEvent[i] |= (uint)EventType.EVT_AX_COMPARED;
            }

            Motion.mAcm_EnableMotionEvent(MotionDevice.DeviceHandle, axisEnableEvent, gpEnableEvent, (uint) _mUlAxisCount, 1);
            _mBInit = true;

            X = new Axis(0, MAxishand[0], 0);
            U = new Axis(0, MAxishand[1], 1);
            Z = new Axis(0, MAxishand[2], 2);
            Y = new Axis(12.8, MAxishand[3], 3);
           
            _axes = new Axis[4];
            _axes[X.AxisNum] = X;
            _axes[Y.AxisNum] = Y;
            _axes[Z.AxisNum] = Z;
            _axes[U.AxisNum] = U;

            result = Motion.mAcm_GpAddAxis(ref _xYhandle, X.Handle);
            if (result != (uint)ErrorCode.SUCCESS)
            {
                strTemp = "Open Axis Failed With Error Code: [0x" + Convert.ToString(result, 16) + "]";
                MessageBox.Show(strTemp + " " + result);
                return false;
            }

            result = Motion.mAcm_GpAddAxis(ref _xYhandle, Y.Handle);
            if (result != (uint)ErrorCode.SUCCESS)
            {
                strTemp = "Open Axis Failed With Error Code: [0x" + Convert.ToString(result, 16) + "]";
                MessageBox.Show(strTemp + " " + result);
                return false;
            }

            // _spindleModbus.Connect();

            if (_mBInit)
            {
                MachineInit = true;
            }

            return true;
        }
        public void SpindleModbus()
        {
            //ModbusClient modbusClient = new ModbusClient("COM1");
            //modbusClient.UnitIdentifier = 1; Not necessary since default slaveID = 1;
            //modbusClient.Baudrate = 9600;	// Not necessary since default baudrate = 9600
            //modbusClient.Parity = System.IO.Ports.Parity.None;
            //modbusClient.StopBits = System.IO.Ports.StopBits.Two;
            //modbusClient.ConnectionTimeout = 500;
            //modbusClient.Connect();
            //modbusClient.ConnectionTimeout = 100;
            //Console.WriteLine("Value of Discr. Input #1: " + modbusClient.ReadHoldingRegisters(0xF004, 1)[0].ToString());  //Reads Discrete Input #1
            _spindleModbus.WriteSingleRegister(0x1001, 0x0001);
            //Console.WriteLine("Value of Input Reg. #10: " + modbusClient.ReadInputRegisters(9, 1)[0].ToString());   //Reads Inp. Reg. #10

            //modbusClient.WriteSingleCoil(4, true);      //Writes Coil #5
            //modbusClient.WriteSingleRegister(19, 4711); //Writes Holding Reg. #20

            //Console.WriteLine("Value of Coil #5: " + modbusClient.ReadCoils(4, 1)[0].ToString());   //Reads Discrete Input #1
            //Console.WriteLine("Value of Holding Reg.. #20: " + modbusClient.ReadHoldingRegisters(19, 1)[0].ToString()); //Reads Inp. Reg. #10
            //modbusClient.WriteMultipleRegisters(49, new int[10] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            //modbusClient.WriteMultipleCoils(29, new bool[10] { true, true, true, true, true, true, true, true, true, true, });

            //Console.Write("Press any key to continue . . . ");
            //Console.ReadKey(true);
        }
        public void SetConfigs()
        {
            var xAcc = Settings.Default.XAcc;
            var xDec = Settings.Default.XDec;
            var xppu = Settings.Default.XPPU;
            var xJerk = Settings.Default.XJerk;
            X.Ppu = xppu;

            var yAcc = Settings.Default.YAcc;
            var yDec = Settings.Default.YDec;
            var yppu = Settings.Default.YPPU;
            var yJerk = Settings.Default.YJerk;
            Y.Ppu = yppu;

            var zAcc = Settings.Default.ZAcc;
            var zDec = Settings.Default.ZDec;
            var zppu = Settings.Default.ZPPU;
            var zJerk = Settings.Default.ZJerk;
            Z.Ppu = zppu;

            var uAcc = Settings.Default.UAcc;
            var uDec = Settings.Default.UDec;
            var uppu = Settings.Default.UPPU;
            var uJerk = Settings.Default.UJerk;
            U.Ppu = uppu;

            CameraChuckCenter = new Vector2(Settings.Default.XObjective, Settings.Default.YObjective);
            WaferLoadCenter = new Vector2(Settings.Default.XLoad, Settings.Default.YLoad);
            CameraFocus = Settings.Default.ZObjective;
            BladeChuckCenter = new Vector2(Settings.Default.XDisk,
                Settings.Default.YObjective + Settings.Default.DiskShift);
            CameraBladeOffset = Settings.Default.DiskShift;

            double axMaxVel = 50;
            double axMaxDec = 180;
            double axMaxAcc = 180;
            uint res;

            res = Motion.mAcm_SetProperty(X.Handle, (uint)PropertyID.CFG_AxPPU, ref xppu, 4);
            res = Motion.mAcm_SetProperty(X.Handle, (uint)PropertyID.PAR_AxJerk, ref xJerk, 8);
            res = Motion.mAcm_SetProperty(X.Handle, (uint)PropertyID.CFG_AxMaxAcc, ref axMaxAcc, 8);
            res = Motion.mAcm_SetProperty(X.Handle, (uint)PropertyID.CFG_AxMaxDec, ref axMaxDec, 8);
            res = Motion.mAcm_SetProperty(X.Handle, (uint)PropertyID.CFG_AxMaxVel, ref axMaxVel, 8);
            res = Motion.mAcm_SetProperty(X.Handle, (uint)PropertyID.PAR_AxAcc, ref xAcc, 8);
            res = Motion.mAcm_SetProperty(X.Handle, (uint)PropertyID.PAR_AxDec, ref xDec, 8);

            res = Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxPPU, ref yppu, 4);
            res = Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.PAR_AxJerk, ref yJerk, 8);
            res = Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxMaxAcc, ref axMaxAcc, 8);
            res = Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxMaxDec, ref axMaxDec, 8);
            res = Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxMaxVel, ref axMaxVel, 8);
            res = Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.PAR_AxAcc, ref yAcc, 8);
            res = Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.PAR_AxDec, ref yDec, 8);

            res = Motion.mAcm_SetProperty(Z.Handle, (uint)PropertyID.CFG_AxPPU, ref zppu, 4);
            res = Motion.mAcm_SetProperty(Z.Handle, (uint)PropertyID.PAR_AxJerk, ref zJerk, 8);
            res = Motion.mAcm_SetProperty(Z.Handle, (uint)PropertyID.CFG_AxMaxAcc, ref axMaxAcc, 8);
            res = Motion.mAcm_SetProperty(Z.Handle, (uint)PropertyID.CFG_AxMaxDec, ref axMaxDec, 8);
            res = Motion.mAcm_SetProperty(Z.Handle, (uint)PropertyID.CFG_AxMaxVel, ref axMaxVel, 8);
            res = Motion.mAcm_SetProperty(Z.Handle, (uint)PropertyID.PAR_AxAcc, ref zAcc, 8);
            res = Motion.mAcm_SetProperty(Z.Handle, (uint)PropertyID.PAR_AxDec, ref zDec, 8);

            res = Motion.mAcm_SetProperty(U.Handle, (uint)PropertyID.CFG_AxPPU, ref uppu, 4);
            res = Motion.mAcm_SetProperty(U.Handle, (uint)PropertyID.PAR_AxJerk, ref uJerk, 8);
            res = Motion.mAcm_SetProperty(U.Handle, (uint)PropertyID.CFG_AxMaxAcc, ref axMaxAcc, 8);
            res = Motion.mAcm_SetProperty(U.Handle, (uint)PropertyID.CFG_AxMaxDec, ref axMaxDec, 8);
            res = Motion.mAcm_SetProperty(U.Handle, (uint)PropertyID.CFG_AxMaxVel, ref axMaxVel, 8);
            res = Motion.mAcm_SetProperty(U.Handle, (uint)PropertyID.PAR_AxAcc, ref uAcc, 8);
            res = Motion.mAcm_SetProperty(U.Handle, (uint)PropertyID.PAR_AxDec, ref uDec, 8);


            res = Motion.mAcm_SetProperty(_xYhandle, (uint)PropertyID.CFG_GpPPU, ref yppu, 4);
            res = Motion.mAcm_SetProperty(_xYhandle, (uint)PropertyID.PAR_GpJerk, ref yJerk, 8);
            res = Motion.mAcm_SetProperty(_xYhandle, (uint)PropertyID.CFG_GpMaxAcc, ref axMaxAcc, 8);
            res = Motion.mAcm_SetProperty(_xYhandle, (uint)PropertyID.CFG_GpMaxDec, ref axMaxDec, 8);
            res = Motion.mAcm_SetProperty(_xYhandle, (uint)PropertyID.CFG_GpMaxVel, ref axMaxVel, 8);
            res = Motion.mAcm_SetProperty(_xYhandle, (uint)PropertyID.PAR_GpAcc, ref yAcc, 8);
            res = Motion.mAcm_SetProperty(_xYhandle, (uint)PropertyID.PAR_GpDec, ref yDec, 8);

            var plsInMde = (int)PulseInMode.AB_4X;
            var plsInLogic = (int)PulseInLogic.NOT_SUPPORT;
            var plsInSrc = (int)PulseInSource.NOT_SUPPORT;
            var plsOutMde = (int)PulseOutMode.OUT_DIR;
            var axDirLogic = (int)DirLogic.DIR_ACT_HIGH;

            var reset = (int)HomeReset.HOME_RESET_EN;
            var cmpEna = (uint)CmpEnable.CMP_EN;
            var cmpSrcAct = (uint)CmpSource.SRC_ACTUAL_POSITION;
            var cmpSrcCmd = (uint)CmpSource.SRC_COMMAND_POSITION;
            var cmpMethod = (uint)CmpMethod.MTD_GREATER_POSITION;


            //Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxCmpSrc, ref cmpSrcAct, 4);
            //else Motion.mAcm_SetProperty(item.Handle, (uint)PropertyID.CFG_AxCmpSrc, ref cmpSrcCmd, 8);

            //Motion.mAcm_SetProperty(item.Handle, (uint)PropertyID.CFG_AxCmpEnable, ref cmpEna, 8);
            //Motion.mAcm_SetProperty(item.Handle, (uint)PropertyID.CFG_AxCmpMethod, ref cmpMethod, 8);

            res = Motion.mAcm_SetProperty(X.Handle, (uint)PropertyID.CFG_AxGenDoEnable, ref plsOutMde, 4);
            res = Motion.mAcm_SetProperty(X.Handle, (uint)PropertyID.CFG_AxPulseInMode, ref plsInMde, 4);
            res = Motion.mAcm_SetProperty(X.Handle, (uint)PropertyID.CFG_AxPulseInLogic, ref plsInLogic, 4);
            res = Motion.mAcm_SetProperty(X.Handle, (uint)PropertyID.CFG_AxPulseInSource, ref plsInSrc, 4);
            res = Motion.mAcm_SetProperty(X.Handle, (uint)PropertyID.CFG_AxPulseOutMode, ref plsOutMde, 4);
            res = Motion.mAcm_SetProperty(X.Handle, (uint)PropertyID.CFG_AxHomeResetEnable, ref reset, 4);
            res = Motion.mAcm_SetProperty(X.Handle, (uint)PropertyID.CFG_AxDirLogic, ref axDirLogic, 4);


            res = Motion.mAcm_SetProperty(Z.Handle, (uint)PropertyID.CFG_AxGenDoEnable, ref plsOutMde, 4);
            res = Motion.mAcm_SetProperty(Z.Handle, (uint)PropertyID.CFG_AxPulseInMode, ref plsInMde, 4);
            res = Motion.mAcm_SetProperty(Z.Handle, (uint)PropertyID.CFG_AxPulseInLogic, ref plsInLogic, 4);
            res = Motion.mAcm_SetProperty(Z.Handle, (uint)PropertyID.CFG_AxPulseInSource, ref plsInSrc, 4);
            res = Motion.mAcm_SetProperty(Z.Handle, (uint)PropertyID.CFG_AxPulseOutMode, ref plsOutMde, 4);
            res = Motion.mAcm_SetProperty(Z.Handle, (uint)PropertyID.CFG_AxHomeResetEnable, ref reset, 4);
            res = Motion.mAcm_SetProperty(Z.Handle, (uint)PropertyID.CFG_AxDirLogic, ref axDirLogic, 4);

            res = Motion.mAcm_SetProperty(U.Handle, (uint)PropertyID.CFG_AxGenDoEnable, ref plsOutMde, 4);
            res = Motion.mAcm_SetProperty(U.Handle, (uint)PropertyID.CFG_AxPulseInMode, ref plsInMde, 4);
            res = Motion.mAcm_SetProperty(U.Handle, (uint)PropertyID.CFG_AxPulseInLogic, ref plsInLogic, 4);
            res = Motion.mAcm_SetProperty(U.Handle, (uint)PropertyID.CFG_AxPulseInSource, ref plsInSrc, 4);
            res = Motion.mAcm_SetProperty(U.Handle, (uint)PropertyID.CFG_AxPulseOutMode, ref plsOutMde, 4);
            res = Motion.mAcm_SetProperty(U.Handle, (uint)PropertyID.CFG_AxHomeResetEnable, ref reset, 4);
            res = Motion.mAcm_SetProperty(U.Handle, (uint)PropertyID.CFG_AxDirLogic, ref axDirLogic, 4);

            plsOutMde = (int)PulseOutMode.OUT_DIR_ALL_NEG;
            axDirLogic = (int)DirLogic.DIR_ACT_HIGH;
            plsInLogic = (int)PulseInLogic.NO_INV_DIR;
            //res = Motion.mAcm_AxSetSvOn(Y.Handle, 1);
            res = Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxGenDoEnable, ref plsOutMde, 4);
            res = Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxPulseInMode, ref plsInMde, 4);
            res = Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxPulseInLogic, ref plsInLogic, 4);
            res = Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxPulseInSource, ref plsInSrc, 4);
            res = Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxPulseOutMode, ref plsOutMde, 4);
            res = Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxHomeResetEnable, ref reset, 4);
            res = Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxDirLogic, ref axDirLogic, 4);


            uint buf = 0;

            buf = (uint)SwLmtEnable.SLMT_DIS;
            res = Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxSwPelEnable, ref buf, 4);
        }
        public void SetVelocity(Velocity velocity)
        {
            double xVel = 0;
            double yVel = 0;
            double zVel = 0;
            double uVel = 0;
            double xyVel = 0;

            switch (velocity)
            {
                case Velocity.Fast:
                    {
                        xVel = Settings.Default.XVelHigh;
                        yVel = Settings.Default.YVelHigh;
                        zVel = Settings.Default.ZVelHigh;
                        uVel = Settings.Default.UVelHigh;
                        xyVel = Math.Sqrt(xVel * xVel + yVel * yVel) / 2;
                    }
                    break;
                case Velocity.Slow:
                    {
                        xVel = Settings.Default.XVelLow;
                        yVel = Settings.Default.YVelLow;
                        zVel = Settings.Default.ZVelLow;
                        uVel = Settings.Default.UVelLow;
                        xyVel = Math.Sqrt(xVel * xVel + yVel * yVel) / 2;
                    }
                    break;
                case Velocity.Service:
                    {
                        xVel = Settings.Default.XVelService;
                        yVel = Settings.Default.YVelService;
                        zVel = Settings.Default.ZVelService;
                        uVel = Settings.Default.UVelService;
                        xyVel = Math.Sqrt(xVel * xVel + yVel * yVel) / 2;
                    }
                    break;
            }

            double axMaxVel = 100;
            double axMaxDec = 180;
            double axMaxAcc = 180;
            uint res = 0;

            res = Motion.mAcm_SetProperty(X.Handle, (uint)PropertyID.CFG_AxMaxAcc, ref axMaxAcc, 8);
            res = Motion.mAcm_SetProperty(X.Handle, (uint)PropertyID.CFG_AxMaxDec, ref axMaxDec, 8);
            res = Motion.mAcm_SetProperty(X.Handle, (uint)PropertyID.CFG_AxMaxVel, ref axMaxVel, 8);

            Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxMaxAcc, ref axMaxAcc, 8);
            Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxMaxDec, ref axMaxDec, 8);
            Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxMaxVel, ref axMaxVel, 8);

            Motion.mAcm_SetProperty(Z.Handle, (uint)PropertyID.CFG_AxMaxAcc, ref axMaxAcc, 8);
            Motion.mAcm_SetProperty(Z.Handle, (uint)PropertyID.CFG_AxMaxDec, ref axMaxDec, 8);
            Motion.mAcm_SetProperty(Z.Handle, (uint)PropertyID.CFG_AxMaxVel, ref axMaxVel, 8);

            Motion.mAcm_SetProperty(U.Handle, (uint)PropertyID.CFG_AxMaxAcc, ref axMaxAcc, 8);
            Motion.mAcm_SetProperty(U.Handle, (uint)PropertyID.CFG_AxMaxDec, ref axMaxDec, 8);
            Motion.mAcm_SetProperty(U.Handle, (uint)PropertyID.CFG_AxMaxVel, ref axMaxVel, 8);

            Motion.mAcm_SetProperty(_xYhandle, (uint)PropertyID.CFG_GpMaxAcc, ref axMaxAcc, 8);
            Motion.mAcm_SetProperty(_xYhandle, (uint)PropertyID.CFG_GpMaxDec, ref axMaxDec, 8);
            Motion.mAcm_SetProperty(_xYhandle, (uint)PropertyID.CFG_GpMaxVel, ref axMaxVel, 8);

            res = Motion.mAcm_SetProperty(X.Handle, (uint)PropertyID.PAR_AxVelHigh, ref xVel, 8);
            res = Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.PAR_AxVelHigh, ref yVel, 8);
            res = Motion.mAcm_SetProperty(Z.Handle, (uint)PropertyID.PAR_AxVelHigh, ref zVel, 8);
            res = Motion.mAcm_SetProperty(U.Handle, (uint)PropertyID.PAR_AxVelHigh, ref uVel, 8);
            res = Motion.mAcm_SetProperty(_xYhandle, (uint)PropertyID.PAR_GpVelHigh, ref xyVel, 8);

            xVel /= 3;
            yVel /= 3;
            zVel /= 3;
            uVel /= 3;
            xyVel /= 3;

            res = Motion.mAcm_SetProperty(X.Handle, (uint)PropertyID.PAR_AxVelLow, ref xVel, 8);
            res = Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.PAR_AxVelLow, ref yVel, 8);
            res = Motion.mAcm_SetProperty(Z.Handle, (uint)PropertyID.PAR_AxVelLow, ref zVel, 8);
            res = Motion.mAcm_SetProperty(U.Handle, (uint)PropertyID.PAR_AxVelLow, ref uVel, 8);
            res = Motion.mAcm_SetProperty(_xYhandle, (uint)PropertyID.PAR_GpVelLow, ref xyVel, 8);

            //ErrorCode
        }
        private void SaveParams()
        {
            //-------------Сохранение параметров в файле онфигурации----
            Settings.Default.Save();
            //----------------------------------------------------------
        }
        private void SetGpVel(Vector2 position, Velocity velocity)
        {
            var x = Math.Abs(position.X - X.ActualPosition);
            var y = Math.Abs(position.Y - Y.ActualPosition);
            double xvel = 0;
            double yvel = 0;
            switch (velocity)
            {
                case Velocity.Fast:
                    {
                        xvel = Settings.Default.XVelHigh;
                        yvel = Settings.Default.YVelHigh;
                    }
                    break;
                case Velocity.Slow:
                    {
                        xvel = Settings.Default.XVelLow;
                        yvel = Settings.Default.YVelLow;
                    }
                    break;
                case Velocity.Service:
                    {
                        xvel = Settings.Default.XVelService;
                        yvel = Settings.Default.YVelService;
                    }
                    break;
            }
            double xyvel;
            if (x / y > xvel / yvel)
            {
                xyvel = xvel * Math.Sqrt(y * y / (x * x) + 1);
            }
            else
            {
                xyvel = yvel * Math.Sqrt(x * x / (y * y) + 1);
            }
            var res = Motion.mAcm_SetProperty(_xYhandle, (uint)PropertyID.PAR_GpVelHigh, ref xyvel, 8);
            xyvel /= 2;
            res = Motion.mAcm_SetProperty(_xYhandle, (uint)PropertyID.PAR_GpVelLow, ref xyvel, 8);

        }

        public void Stop(Ax axis)
        {
            var handle = axis switch
            {
                Ax.X => X.Handle,
                Ax.Y => Y.Handle,
                Ax.Z => Z.Handle,
                Ax.U => U.Handle,
                _ => IntPtr.Zero,
            };

            Motion.mAcm_AxStopEmg(handle);
        }

        public void GoWhile(AxisDirections direction)
        {
            ResetErrors();
            Motion.mAcm_AxMoveVel(MoveRelParam(direction).Item1, MoveRelParam(direction).Item2);
        }

        public void EmgStop()
        {
            MAxishand.Select(Motion.mAcm_AxStopEmg).ToList();
            ResetErrors();
        }

        public async Task GoThereAsync(Place place)
        {
            switch (place)
            {
                case Place.Home:
                    ResetErrors();
                    SetVelocity(Velocity.Service);
                    _result = Motion.mAcm_AxHome(X.Handle, (uint)HomeMode.MODE2_Lmt, (uint)HomeDir.NegDir);
                    _result = Motion.mAcm_AxHome(Y.Handle, (uint)HomeMode.MODE6_Lmt_Ref, (uint)HomeDir.NegDir);
                    _result = Motion.mAcm_AxHome(Z.Handle, (uint)HomeMode.MODE2_Lmt, (uint)HomeDir.NegDir);
                    var done = 0;
                    var zlmt = true;
                    var xlmt = true;
                    while (done != 2)
                    {
                        if (Z.LmtN & zlmt & (Z.ActualPosition == 0))
                        {
                            done++;
                            zlmt = false;
                            ResetErrors();
                            Motion.mAcm_AxMoveAbs(Z.Handle, 1);
                        }

                        if (X.LmtN & xlmt & (X.ActualPosition == 0))
                        {
                            done++;
                            xlmt = false;
                            ResetErrors();
                            Motion.mAcm_AxMoveAbs(X.Handle, 1);
                        }
                    }

                    break;
                case Place.Loading:
                    await Z.MoveAxisInPosAsync(0);
                    await MoveInPosXyAsync(WaferLoadCenter);
                    break;
                case Place.CameraChuckCenter:
                    await MoveInPosXyAsync(CameraChuckCenter);
                    break;
                case Place.BladeChuckCenter:
                    await MoveInPosXyAsync(BladeChuckCenter);
                    break;
            }
        }

        //public async Task MoveInPosXY1Async(Vector2 position)
        //{
        //    uint ElCount = 2;
        //    var accuracy = 0.001;
        //    double backlash = 0;
        //    var state = new ushort();
        //    var vel = 0.1;
        //    bool gotItX;
        //    bool gotItY;
        //    var signx = 0;
        //    var signy = 0;

        //    await Task.Run(() =>
        //        {
        //            for (var recurcy = 0; recurcy < 20; recurcy++)
        //            {
        //                if (recurcy == 0)
        //                {
        //                    position.X = Math.Round(position.X, 3);
        //                    position.Y = Math.Round(position.Y, 3);
        //                    //Motion.mAcm_GpMoveLinearAbs(XYhandle, new double[2] {position.X, position.Y}, ref ElCount);
        //                    Motion.mAcm_GpMoveDirectAbs(XYhandle, new double[2] { position.X, position.Y }, ref ElCount);
        //                    while (state != (uint) GroupState.STA_Gp_Ready) Motion.mAcm_GpGetState(XYhandle, ref state);
        //                    Motion.mAcm_SetProperty(X.Handle, (uint) PropertyID.PAR_AxVelLow, ref vel, 8);
        //                    Motion.mAcm_SetProperty(Y.Handle, (uint) PropertyID.PAR_AxVelLow, ref vel, 8);
        //                    Motion.mAcm_SetProperty(X.Handle, (uint) PropertyID.PAR_AxVelHigh, ref vel, 8);
        //                    Motion.mAcm_SetProperty(Y.Handle, (uint) PropertyID.PAR_AxVelHigh, ref vel, 8);
        //                }

        //                Thread.Sleep(300);


        //                if (Math.Abs(Math.Round(X.ActualPosition, 3) - position.X) <= accuracy) gotItX = true;
        //                else gotItX = false;
        //                if (Math.Abs(Math.Round(Y.ActualPosition, 3) - position.Y) <= accuracy) gotItY = true;
        //                else gotItY = false;

        //                signx = Math.Sign(position.X - Math.Round(X.ActualPosition, 3));
        //                signy = Math.Sign(position.Y - Math.Round(Y.ActualPosition, 3));
        //                //Motion.mAcm_CheckMotionEvent(m_DeviceHandle, null, null, 2, 0, 10);
        //                if (!gotItX)
        //                {
        //                    Motion.mAcm_AxMoveRel(X.Handle,
        //                        signx * (Math.Abs(position.X - Math.Round(X.ActualPosition, 3)) + signx * backlash));

        //                    while (!gotItX)
        //                    {
        //                        if (Math.Abs(Math.Round(X.ActualPosition, 3) - position.X) <= accuracy)
        //                        {
        //                            Motion.mAcm_AxStopEmg(X.Handle);
        //                            gotItX = true;
        //                        }

        //                        Motion.mAcm_AxGetState(X.Handle, ref state);
        //                        if (state == (uint) AxisState.STA_AX_READY) break;
        //                    }
        //                }

        //                if (!gotItY)
        //                {
        //                    Motion.mAcm_AxMoveRel(Y.Handle,
        //                        signy * (Math.Abs(position.Y - Math.Round(Y.ActualPosition, 3)) + signy * backlash));

        //                    while (!gotItY)
        //                    {
        //                        if (Math.Abs(Math.Round(Y.ActualPosition, 3) - position.Y) <= accuracy)
        //                        {
        //                            Motion.mAcm_AxStopEmg(Y.Handle);
        //                            gotItY = true;
        //                        }

        //                        Motion.mAcm_AxGetState(Y.Handle, ref state);
        //                        if (state == (uint) AxisState.STA_AX_READY) break;
        //                    }
        //                }

        //                if (gotItX & gotItY) break;
        //            }
        //        }
        //    );
        //}


        public async Task MoveInPosXyAsync(Vector2 position)
        {
            if (_tokenXy)
            {
                _tokenXy = false;
                uint elCount = 2;
                var state = new ushort();
                uint res = 0;
                uint buf = 0;

                buf = (uint)SwLmtEnable.SLMT_DIS;
                res = Motion.mAcm_SetProperty(X.Handle, (uint)PropertyID.CFG_AxSwPelEnable, ref buf, 4);
                res = Motion.mAcm_SetProperty(X.Handle, (uint)PropertyID.CFG_AxSwMelEnable, ref buf, 4);
                res = Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxSwPelEnable, ref buf, 4);
                res = Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxSwMelEnable, ref buf, 4);
                SetGpVel(position, Velocity.Service);
                await Task.Run(() =>
                    {
                        position.X = Math.Round(position.X, 3);
                        position.Y = Math.Round(position.Y, 3);
                        var pos = new double[2] { position.X, position.Y };
                        //ResetErrors();
                        Motion.mAcm_GpResetError(_xYhandle);
                        //res = Motion.mAcm_GpMoveDirectAbs(XYhandle, pos, ref ElCount);
                        res = Motion.mAcm_GpMoveLinearAbs(_xYhandle, pos, ref elCount);
                        do
                        {
                            Thread.Sleep(1);
                            Motion.mAcm_GpGetState(_xYhandle, ref state);
                        } while ((state & (ushort)GroupState.STA_Gp_Motion) > 0);
                    }
                );
                X.MotionDone = true;
                Y.MotionDone = true;
                if (X.LineCoefficient != 0) await X.MoveAxisInPosAsync(position.X);
                if (Y.LineCoefficient != 0) await Y.MoveAxisInPosAsync(position.Y);
            }

            _tokenXy = true;
        }

        public void RefreshSettings()
        {
            _bridge = new Bridge
            {
                Air = Settings.Default.AirSensorDsbl,
                CoolantWater = Settings.Default.CoolantSensorDsbl,
                SpindleWater = Settings.Default.SpindleCntrlDsbl,
                ChuckVacuum = Settings.Default.VacuumSensorDsbl
            };
            CameraChuckCenter = new Vector2(Settings.Default.XObjective, Settings.Default.YObjective);
            CameraBladeOffset = Settings.Default.DiskShift;
            BladeChuckCenter = new Vector2(Settings.Default.XDisk, CameraChuckCenter.Y + CameraBladeOffset);
            WaferLoadCenter = new Vector2(Settings.Default.XLoad, Settings.Default.YLoad);
            ZBladeTouch = Settings.Default.ZTouch;
        }

        #region Методы

        //public async Task YGoToSwLmt(double position)
        //{
        //    uint buf = 0;
        //    uint res = 0;
        //    const double tolerance = 0.003;
        //    var pos = (int)(position * Y.Ppu);
        //    ushort direction = 0;

        //    buf = (uint)SwLmtEnable.SLMT_DIS;
        //    res = Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxSwPelEnable, ref buf, 4);
        //    res = Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxSwMelEnable, ref buf, 4);
        //    ResetErrors();

        //    var diff = position - Y.ActualPosition;
        //    if (Math.Abs(diff) > tolerance)
        //    {
        //        buf = (uint)SwLmtReact.SLMT_IMMED_STOP;
        //        res = Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxSwPelReact, ref buf, 4);
        //        res = Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxSwMelReact, ref buf, 4);
        //        var tol = 0;
        //        switch (Math.Sign(diff))
        //        {
        //            case 1:
        //                res = Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxSwPelValue, ref pos, 4);
        //                buf = (uint)SwLmtEnable.SLMT_EN;
        //                res = Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxSwPelEnable, ref buf, 4);
        //                buf = (uint)SwLmtToleranceEnable.TOLERANCE_ENABLE;
        //                res = Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxSwPelToleranceEnable, ref buf,
        //                    4);
        //                res = Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxSwPelToleranceValue, ref tol,
        //                    4);
        //                direction = (ushort)VelMoveDir.DIR_POSITIVE;
        //                break;
        //            case -1:
        //                res = Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxSwMelValue, ref pos, 4);
        //                buf = (uint)SwLmtEnable.SLMT_EN;
        //                res = Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxSwMelEnable, ref buf, 4);
        //                buf = (uint)SwLmtToleranceEnable.TOLERANCE_ENABLE;
        //                res = Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxSwMelToleranceEnable, ref buf,
        //                    4);
        //                res = Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxSwMelToleranceValue, ref tol,
        //                    4);
        //                direction = (ushort)VelMoveDir.DIR_NEGATIVE;
        //                break;
        //        }

        //        Motion.mAcm_AxMoveVel(Y.Handle, direction);
        //        uint status = 0;
        //        uint slmtp = 0;
        //        uint slmtn = 0;
        //        await Task.Run(() =>
        //            {
        //                do
        //                {
        //                    Thread.Sleep(1);
        //                    Motion.mAcm_AxGetMotionIO(Y.Handle, ref status);
        //                    slmtp = status & (uint)Ax_Motion_IO.AX_MOTION_IO_SLMTP;
        //                    slmtn = status & (uint)Ax_Motion_IO.AX_MOTION_IO_SLMTN;
        //                } while ((slmtp == 0) & (slmtn == 0));
        //            }
        //        );
        //        Y.SetVelocity(1);
        //        Motion.mAcm_AxSetCmdPosition(Y.Handle, Y.ActualPosition);
        //        await YGoToSwLmt(position);
        //        buf = (uint)SwLmtEnable.SLMT_DIS;
        //        //res = Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxSwPelEnable, ref buf, 4);
        //        //res = Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxSwMelEnable, ref buf, 4);
        //        ResetErrors();
        //    }
        //}

        public void ResetErrors()
        {
            foreach (var ax in _axes) Motion.mAcm_AxResetError(ax.Handle);
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
                    _result = Motion.mAcm_CheckMotionEvent(MotionDevice.DeviceHandle, axEvtStatusArray, gpEvtStatusArray, (uint) _mUlAxisCount, 0, 10);
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
                _result = Motion.mAcm_CheckMotionEvent(MotionDevice.DeviceHandle, axEvtStatusArray, gpEvtStatusArray, (uint) _mUlAxisCount,
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