using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Printing;
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

namespace DicingBlade.Classes
{
    internal enum DO
    {
        OUT4 = 4,
        OUT5,
        OUT6,
        OUT7
    }

    internal enum DI : ushort
    {
        IN1,
        IN2,
        IN3
    }

    public struct DIEventArgs
    {
    }

    public delegate void DIEventHandler( /*DIEventArgs eventArgs*/);

    internal struct Bridge
    {
        public bool SpindleWater;
        public bool CoolantWater;
        public bool ChuckVacuum;
        public bool Air;
    }


    [AddINotifyPropertyChangedInterface]
    internal class Machine
    {
        private readonly bool testRegime;
        private Axis[] axes;
        private byte BitData;
        private Bridge Bridge;
        private uint IOStatus;
        private VideoCaptureDevice LocalWebCam;
        private FilterInfoCollection LocalWebCamsCollection;
        public IntPtr[] m_Axishand = new IntPtr[32];
        private bool m_bInit;
        private IntPtr m_DeviceHandle = IntPtr.Zero;
        private uint m_ulAxisCount;
        public bool PCI1240IsConnected;
        private double position;
        private uint Result;
        private bool tokenXY = true;
        private Velocity velocityRegime;
        private IntPtr XYhandle = IntPtr.Zero;
        private ModbusClient _spindleModbus;
        public Machine(bool test) // В конструкторе происходит инициализация всех устройств, загрузка параметров.
        {
            testRegime = test;

            if (!testRegime)
            {
                _spindleModbus = new ModbusClient("COM1");
                StartCamera();                
                DevicesConnection();
                SetConfigs();
                VelocityRegime = Velocity.Slow;
                OnAirWanished += EMGScenario;                
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
            get => velocityRegime;
            set
            {
                velocityRegime = value;
                SetVelocity(value);
            }
        }

        public BitmapImage Bi { get; set; }
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
        public Vector2 COSystemCurrentCoors => new Vector2(X.ActualPosition - CameraChuckCenter.X,
            Y.ActualPosition - CameraChuckCenter.Y);

        /// <summary>
        ///     Возвращает текущие координаты в системе центр столика - центр кромки диска.
        /// </summary>
        public Vector2 CBSystemCurrentCoors => new Vector2(X.ActualPosition - BladeChuckCenter.X, Y.ActualPosition - BladeChuckCenter.Y);

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
            get
            {
                if (!testRegime) return U.GetDO(DO.OUT4);
                return false;
            }
            set
            {
                if (!testRegime) U.SetDo(DO.OUT4, (byte) (value ? 1 : 0));                
            }
        }

        public bool ChuckVacuum { get; set; }

        public bool SwitchOnChuckVacuum
        {
            get
            {                
                if (!testRegime) return Z.GetDO(DO.OUT5);
                return false;
            }
            set
            {
                if (!testRegime) Z.SetDo(DO.OUT5, (byte) (value ? 1 : 0));
            }
        }

        public bool Air { get; set; }

        public bool SwitchOnBlowing
        {
            get
            {                
                if (!testRegime) return Z.GetDO(DO.OUT6);
                return false;
            }
            set
            {
                if (!testRegime) Z.SetDo(DO.OUT6, (byte) (value ? 1 : 0));
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
            switch (direction)
            {
                case AxisDirections.XP: return (X.Handle, (ushort) VelMoveDir.DIR_POSITIVE);
                case AxisDirections.XN: return (X.Handle, (ushort) VelMoveDir.DIR_NEGATIVE);
                case AxisDirections.YP: return (Y.Handle, (ushort) VelMoveDir.DIR_POSITIVE);
                case AxisDirections.YN: return (Y.Handle, (ushort) VelMoveDir.DIR_NEGATIVE);
                case AxisDirections.ZP: return (Z.Handle, (ushort) VelMoveDir.DIR_POSITIVE);
                case AxisDirections.ZN: return (Z.Handle, (ushort) VelMoveDir.DIR_NEGATIVE);
                case AxisDirections.UP: return (U.Handle, (ushort) VelMoveDir.DIR_POSITIVE);
                case AxisDirections.UN: return (U.Handle, (ushort) VelMoveDir.DIR_NEGATIVE);
                default: return (new IntPtr(), 1);
            }
        }

        public event DIEventHandler OnVacuumWanished;
        public event DIEventHandler OnCoolWaterWanished;
        public event DIEventHandler OnSpinWaterWanished;
        public event DIEventHandler OnAirWanished;

        private void EMGScenario( /*DIEventArgs eventArgs*/)
        {
        }

        public bool SetOnChuck()
        {
            if (!ChuckVacuum) SwitchOnChuckVacuum = true;
            Thread.Sleep(100);
            if (!ChuckVacuum)
            {
                SayMessage(Messages.SetAndTurnOnVacuum);
                return false;
            }

            return true;
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

        public void StartCamera()
        {
            LocalWebCamsCollection = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            try
            {
                LocalWebCam = new VideoCaptureDevice(LocalWebCamsCollection[0].MonikerString);
            } //1
            catch
            {
                MessageBox.Show("Включите питание видеокамеры !");
                StartCamera();
            }
            finally
            {
                LocalWebCam.VideoResolution = LocalWebCam.VideoCapabilities[1]; //8                
                LocalWebCam.NewFrame += Cam_NewFrame;
                LocalWebCam.Start();
            }
        }

        public void Cam_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            BitmapImage bitmap;
            try
            {
                var filter = new Mirror(false, true);
                var img = (Bitmap) eventArgs.Frame.Clone();
                filter.ApplyInPlace(img);
                var ms = new MemoryStream();
                img.Save(ms, ImageFormat.Bmp);
                ms.Seek(0, SeekOrigin.Begin);
                bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze();
                Bi = bitmap;
            }
            catch (Exception ex)
            {
            }

            Thread.Sleep(1);
        }

        public void StopCamera()
        {
            LocalWebCam.Stop();
        }

        private bool DevicesConnection()
        {
            uint result;
            string strTemp;
            uint i = 0;
            var axesPerDev = new uint();
            uint deviceCount = 0;
            uint deviceNum = 0;
            var curAvailableDevs = new DEV_LIST[Motion.MAX_DEVICES];


            var resAvlb = Motion.mAcm_GetAvailableDevs(curAvailableDevs, Motion.MAX_DEVICES, ref deviceCount);

            if (resAvlb != (int) ErrorCode.SUCCESS)
            {
                strTemp = "Get Device Numbers Failed With Error Code: [0x" + Convert.ToString(resAvlb, 16) + "]";
                MessageBox.Show(strTemp + " " + resAvlb);
                return false;
            }

            if (deviceCount > 0) deviceNum = curAvailableDevs[0].DeviceNum;

            //deviceNum = curAvailableDevs[0].DeviceNum;

            result = Motion.mAcm_DevOpen(deviceNum, ref m_DeviceHandle);
            if (result != (uint) ErrorCode.SUCCESS)
            {
                strTemp = "Open Device Failed With Error Code: [0x" + Convert.ToString(result, 16) + "]";
                MessageBox.Show(strTemp + result);
                return false;
            }

            result = Motion.mAcm_GetU32Property(m_DeviceHandle, (uint) PropertyID.FT_DevAxesCount, ref axesPerDev);
            if (result != (uint) ErrorCode.SUCCESS)
            {
                strTemp = "Get Axis Number Failed With Error Code: [0x" + Convert.ToString(result, 16) + "]";
                MessageBox.Show(strTemp + " " + result);
                return false;
            }

            m_ulAxisCount = axesPerDev;
            var axisEnableEvent = new uint[m_ulAxisCount];
            var gpEnableEvent = new uint[1];

            for (i = 0; i < m_ulAxisCount; i++)
            {
                result = Motion.mAcm_AxOpen(m_DeviceHandle, (ushort) i, ref m_Axishand[i]);
                if (result != (uint) ErrorCode.SUCCESS)
                {
                    strTemp = "Open Axis Failed With Error Code: [0x" + Convert.ToString(result, 16) + "]";
                    MessageBox.Show(strTemp + " " + result);
                    return false;
                }

                var cmdPosition = new double();
                cmdPosition = 0;
                //Set command position for the specified axis
                result = Motion.mAcm_AxSetCmdPosition(m_Axishand[i], cmdPosition);
                //Set actual position for the specified axis
                result = Motion.mAcm_AxSetActualPosition(m_Axishand[i], cmdPosition);

                axisEnableEvent[i] |= (uint) EventType.EVT_AX_MOTION_DONE;
                axisEnableEvent[i] |= (uint) EventType.EVT_AX_VH_START;
                axisEnableEvent[i] |= (uint) EventType.EVT_AX_HOME_DONE;

                // axisEnableEvent[i] |= (uint)EventType.EVT_AX_COMPARED;
            }

            Motion.mAcm_EnableMotionEvent(m_DeviceHandle, axisEnableEvent, gpEnableEvent, m_ulAxisCount, 1);
            m_bInit = true;

            X = new Axis(0, m_Axishand[0], 0);
            Y = new Axis(12.8, m_Axishand[3], 3);
            Z = new Axis(0, m_Axishand[2], 2);
            U = new Axis(0, m_Axishand[1], 1);
            axes = new Axis[4];
            axes[X.AxisNum] = X;
            axes[Y.AxisNum] = Y;
            axes[Z.AxisNum] = Z;
            axes[U.AxisNum] = U;

            result = Motion.mAcm_GpAddAxis(ref XYhandle, X.Handle);
            if (result != (uint) ErrorCode.SUCCESS)
            {
                strTemp = "Open Axis Failed With Error Code: [0x" + Convert.ToString(result, 16) + "]";
                MessageBox.Show(strTemp + " " + result);
                return false;
            }

            result = Motion.mAcm_GpAddAxis(ref XYhandle, Y.Handle);
            if (result != (uint) ErrorCode.SUCCESS)
            {
                strTemp = "Open Axis Failed With Error Code: [0x" + Convert.ToString(result, 16) + "]";
                MessageBox.Show(strTemp + " " + result);
                return false;
            }

            _spindleModbus.Connect();

            if (m_bInit) MachineInit = true;

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
            var XAcc = Settings.Default.XAcc;
            var XDec = Settings.Default.XDec;
            var XPPU = Settings.Default.XPPU;
            var XJerk = Settings.Default.XJerk;
            X.PPU = XPPU;

            var YAcc = Settings.Default.YAcc;
            var YDec = Settings.Default.YDec;
            var YPPU = Settings.Default.YPPU;
            var YJerk = Settings.Default.YJerk;
            Y.PPU = YPPU;

            var ZAcc = Settings.Default.ZAcc;
            var ZDec = Settings.Default.ZDec;
            var ZPPU = Settings.Default.ZPPU;
            var ZJerk = Settings.Default.ZJerk;
            Z.PPU = ZPPU;

            var UAcc = Settings.Default.UAcc;
            var UDec = Settings.Default.UDec;
            var UPPU = Settings.Default.UPPU;
            var UJerk = Settings.Default.UJerk;
            U.PPU = UPPU;

            CameraChuckCenter = new Vector2(Settings.Default.XObjective, Settings.Default.YObjective);
            WaferLoadCenter = new Vector2(Settings.Default.XLoad, Settings.Default.YLoad);
            CameraFocus = Settings.Default.ZObjective;
            BladeChuckCenter = new Vector2(Settings.Default.XDisk,
                Settings.Default.YObjective + Settings.Default.DiskShift);
            CameraBladeOffset = Settings.Default.DiskShift;

            double AxMaxVel = 50;
            double AxMaxDec = 180;
            double AxMaxAcc = 180;
            uint res;

            res = Motion.mAcm_SetProperty(X.Handle, (uint) PropertyID.CFG_AxPPU, ref XPPU, 4);
            res = Motion.mAcm_SetProperty(X.Handle, (uint) PropertyID.PAR_AxJerk, ref XJerk, 8);
            res = Motion.mAcm_SetProperty(X.Handle, (uint) PropertyID.CFG_AxMaxAcc, ref AxMaxAcc, 8);
            res = Motion.mAcm_SetProperty(X.Handle, (uint) PropertyID.CFG_AxMaxDec, ref AxMaxDec, 8);
            res = Motion.mAcm_SetProperty(X.Handle, (uint) PropertyID.CFG_AxMaxVel, ref AxMaxVel, 8);
            res = Motion.mAcm_SetProperty(X.Handle, (uint) PropertyID.PAR_AxAcc, ref XAcc, 8);
            res = Motion.mAcm_SetProperty(X.Handle, (uint) PropertyID.PAR_AxDec, ref XDec, 8);

            res = Motion.mAcm_SetProperty(Y.Handle, (uint) PropertyID.CFG_AxPPU, ref YPPU, 4);
            res = Motion.mAcm_SetProperty(Y.Handle, (uint) PropertyID.PAR_AxJerk, ref YJerk, 8);
            res = Motion.mAcm_SetProperty(Y.Handle, (uint) PropertyID.CFG_AxMaxAcc, ref AxMaxAcc, 8);
            res = Motion.mAcm_SetProperty(Y.Handle, (uint) PropertyID.CFG_AxMaxDec, ref AxMaxDec, 8);
            res = Motion.mAcm_SetProperty(Y.Handle, (uint) PropertyID.CFG_AxMaxVel, ref AxMaxVel, 8);
            res = Motion.mAcm_SetProperty(Y.Handle, (uint) PropertyID.PAR_AxAcc, ref YAcc, 8);
            res = Motion.mAcm_SetProperty(Y.Handle, (uint) PropertyID.PAR_AxDec, ref YDec, 8);

            res = Motion.mAcm_SetProperty(Z.Handle, (uint) PropertyID.CFG_AxPPU, ref ZPPU, 4);
            res = Motion.mAcm_SetProperty(Z.Handle, (uint) PropertyID.PAR_AxJerk, ref ZJerk, 8);
            res = Motion.mAcm_SetProperty(Z.Handle, (uint) PropertyID.CFG_AxMaxAcc, ref AxMaxAcc, 8);
            res = Motion.mAcm_SetProperty(Z.Handle, (uint) PropertyID.CFG_AxMaxDec, ref AxMaxDec, 8);
            res = Motion.mAcm_SetProperty(Z.Handle, (uint) PropertyID.CFG_AxMaxVel, ref AxMaxVel, 8);
            res = Motion.mAcm_SetProperty(Z.Handle, (uint) PropertyID.PAR_AxAcc, ref ZAcc, 8);
            res = Motion.mAcm_SetProperty(Z.Handle, (uint) PropertyID.PAR_AxDec, ref ZDec, 8);

            res = Motion.mAcm_SetProperty(U.Handle, (uint) PropertyID.CFG_AxPPU, ref UPPU, 4);
            res = Motion.mAcm_SetProperty(U.Handle, (uint) PropertyID.PAR_AxJerk, ref UJerk, 8);
            res = Motion.mAcm_SetProperty(U.Handle, (uint) PropertyID.CFG_AxMaxAcc, ref AxMaxAcc, 8);
            res = Motion.mAcm_SetProperty(U.Handle, (uint) PropertyID.CFG_AxMaxDec, ref AxMaxDec, 8);
            res = Motion.mAcm_SetProperty(U.Handle, (uint) PropertyID.CFG_AxMaxVel, ref AxMaxVel, 8);
            res = Motion.mAcm_SetProperty(U.Handle, (uint) PropertyID.PAR_AxAcc, ref UAcc, 8);
            res = Motion.mAcm_SetProperty(U.Handle, (uint) PropertyID.PAR_AxDec, ref UDec, 8);


            res = Motion.mAcm_SetProperty(XYhandle, (uint)PropertyID.CFG_GpPPU, ref YPPU, 4);
            res = Motion.mAcm_SetProperty(XYhandle, (uint)PropertyID.PAR_GpJerk, ref YJerk, 8);
            res = Motion.mAcm_SetProperty(XYhandle, (uint)PropertyID.CFG_GpMaxAcc, ref AxMaxAcc, 8);
            res = Motion.mAcm_SetProperty(XYhandle, (uint)PropertyID.CFG_GpMaxDec, ref AxMaxDec, 8);
            res = Motion.mAcm_SetProperty(XYhandle, (uint)PropertyID.CFG_GpMaxVel, ref AxMaxVel, 8);
            res = Motion.mAcm_SetProperty(XYhandle, (uint)PropertyID.PAR_GpAcc, ref YAcc, 8);
            res = Motion.mAcm_SetProperty(XYhandle, (uint)PropertyID.PAR_GpDec, ref YDec, 8);

            var PlsInMde = (int) PulseInMode.AB_4X;
            var PlsInLogic = (int) PulseInLogic.NOT_SUPPORT;
            var PlsInSrc = (int) PulseInSource.NOT_SUPPORT;
            var PlsOutMde = (int) PulseOutMode.OUT_DIR;
            var AxDirLogic = (int) DirLogic.DIR_ACT_HIGH;

            var Reset = (int) HomeReset.HOME_RESET_EN;
            var cmpEna = (uint) CmpEnable.CMP_EN;
            var cmpSrcAct = (uint) CmpSource.SRC_ACTUAL_POSITION;
            var cmpSrcCmd = (uint) CmpSource.SRC_COMMAND_POSITION;
            var cmpMethod = (uint) CmpMethod.MTD_GREATER_POSITION;


            //Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxCmpSrc, ref cmpSrcAct, 4);
            //else Motion.mAcm_SetProperty(item.Handle, (uint)PropertyID.CFG_AxCmpSrc, ref cmpSrcCmd, 8);

            //Motion.mAcm_SetProperty(item.Handle, (uint)PropertyID.CFG_AxCmpEnable, ref cmpEna, 8);
            //Motion.mAcm_SetProperty(item.Handle, (uint)PropertyID.CFG_AxCmpMethod, ref cmpMethod, 8);

            res = Motion.mAcm_SetProperty(X.Handle, (uint) PropertyID.CFG_AxGenDoEnable, ref PlsOutMde, 4);
            res = Motion.mAcm_SetProperty(X.Handle, (uint) PropertyID.CFG_AxPulseInMode, ref PlsInMde, 4);
            res = Motion.mAcm_SetProperty(X.Handle, (uint) PropertyID.CFG_AxPulseInLogic, ref PlsInLogic, 4);
            res = Motion.mAcm_SetProperty(X.Handle, (uint) PropertyID.CFG_AxPulseInSource, ref PlsInSrc, 4);
            res = Motion.mAcm_SetProperty(X.Handle, (uint) PropertyID.CFG_AxPulseOutMode, ref PlsOutMde, 4);
            res = Motion.mAcm_SetProperty(X.Handle, (uint) PropertyID.CFG_AxHomeResetEnable, ref Reset, 4);
            res = Motion.mAcm_SetProperty(X.Handle, (uint) PropertyID.CFG_AxDirLogic, ref AxDirLogic, 4);


            res = Motion.mAcm_SetProperty(Z.Handle, (uint) PropertyID.CFG_AxGenDoEnable, ref PlsOutMde, 4);
            res = Motion.mAcm_SetProperty(Z.Handle, (uint) PropertyID.CFG_AxPulseInMode, ref PlsInMde, 4);
            res = Motion.mAcm_SetProperty(Z.Handle, (uint) PropertyID.CFG_AxPulseInLogic, ref PlsInLogic, 4);
            res = Motion.mAcm_SetProperty(Z.Handle, (uint) PropertyID.CFG_AxPulseInSource, ref PlsInSrc, 4);
            res = Motion.mAcm_SetProperty(Z.Handle, (uint) PropertyID.CFG_AxPulseOutMode, ref PlsOutMde, 4);
            res = Motion.mAcm_SetProperty(Z.Handle, (uint) PropertyID.CFG_AxHomeResetEnable, ref Reset, 4);
            res = Motion.mAcm_SetProperty(Z.Handle, (uint) PropertyID.CFG_AxDirLogic, ref AxDirLogic, 4);

            res = Motion.mAcm_SetProperty(U.Handle, (uint) PropertyID.CFG_AxGenDoEnable, ref PlsOutMde, 4);
            res = Motion.mAcm_SetProperty(U.Handle, (uint) PropertyID.CFG_AxPulseInMode, ref PlsInMde, 4);
            res = Motion.mAcm_SetProperty(U.Handle, (uint) PropertyID.CFG_AxPulseInLogic, ref PlsInLogic, 4);
            res = Motion.mAcm_SetProperty(U.Handle, (uint) PropertyID.CFG_AxPulseInSource, ref PlsInSrc, 4);
            res = Motion.mAcm_SetProperty(U.Handle, (uint) PropertyID.CFG_AxPulseOutMode, ref PlsOutMde, 4);
            res = Motion.mAcm_SetProperty(U.Handle, (uint) PropertyID.CFG_AxHomeResetEnable, ref Reset, 4);
            res = Motion.mAcm_SetProperty(U.Handle, (uint) PropertyID.CFG_AxDirLogic, ref AxDirLogic, 4);

            PlsOutMde = (int) PulseOutMode.OUT_DIR_ALL_NEG;
            AxDirLogic = (int) DirLogic.DIR_ACT_HIGH;
            PlsInLogic = (int) PulseInLogic.NO_INV_DIR;
            //res = Motion.mAcm_AxSetSvOn(Y.Handle, 1);
            res = Motion.mAcm_SetProperty(Y.Handle, (uint) PropertyID.CFG_AxGenDoEnable, ref PlsOutMde, 4);
            res = Motion.mAcm_SetProperty(Y.Handle, (uint) PropertyID.CFG_AxPulseInMode, ref PlsInMde, 4);
            res = Motion.mAcm_SetProperty(Y.Handle, (uint) PropertyID.CFG_AxPulseInLogic, ref PlsInLogic, 4);
            res = Motion.mAcm_SetProperty(Y.Handle, (uint) PropertyID.CFG_AxPulseInSource, ref PlsInSrc, 4);
            res = Motion.mAcm_SetProperty(Y.Handle, (uint) PropertyID.CFG_AxPulseOutMode, ref PlsOutMde, 4);
            res = Motion.mAcm_SetProperty(Y.Handle, (uint) PropertyID.CFG_AxHomeResetEnable, ref Reset, 4);
            res = Motion.mAcm_SetProperty(Y.Handle, (uint) PropertyID.CFG_AxDirLogic, ref AxDirLogic, 4);


            uint buf = 0;

            buf = (uint) SwLmtEnable.SLMT_DIS;
            res = Motion.mAcm_SetProperty(Y.Handle, (uint) PropertyID.CFG_AxSwPelEnable, ref buf, 4);
        }

        public void SetVelocity(Velocity velocity)
        {
            double XVel = 0;
            double YVel = 0;
            double ZVel = 0;
            double UVel = 0;
            double XYVel = 0;

            switch (velocity)
            {
                case Velocity.Fast:
                    {
                        XVel = Settings.Default.XVelHigh;
                        YVel = Settings.Default.YVelHigh;
                        ZVel = Settings.Default.ZVelHigh;
                        UVel = Settings.Default.UVelHigh;
                        XYVel = Math.Sqrt(XVel * XVel + YVel * YVel);
                    }
                    break;
                case Velocity.Slow:
                    {
                        XVel = Settings.Default.XVelLow;
                        YVel = Settings.Default.YVelLow;
                        ZVel = Settings.Default.ZVelLow;
                        UVel = Settings.Default.UVelLow; 
                        XYVel = Math.Sqrt(XVel * XVel + YVel * YVel);
                    }
                    break;
                case Velocity.Service:
                    {
                        XVel = Settings.Default.XVelService;
                        YVel = Settings.Default.YVelService;
                        ZVel = Settings.Default.ZVelService;
                        UVel = Settings.Default.UVelService;
                        XYVel = Math.Sqrt(XVel * XVel + YVel * YVel);
                    }
                    break;
            }

            double AxMaxVel = 100;
            double AxMaxDec = 180;
            double AxMaxAcc = 180;
            uint res = 0;

            res = Motion.mAcm_SetProperty(X.Handle, (uint) PropertyID.CFG_AxMaxAcc, ref AxMaxAcc, 8);
            res = Motion.mAcm_SetProperty(X.Handle, (uint) PropertyID.CFG_AxMaxDec, ref AxMaxDec, 8);
            res = Motion.mAcm_SetProperty(X.Handle, (uint) PropertyID.CFG_AxMaxVel, ref AxMaxVel, 8);

            Motion.mAcm_SetProperty(Y.Handle, (uint) PropertyID.CFG_AxMaxAcc, ref AxMaxAcc, 8);
            Motion.mAcm_SetProperty(Y.Handle, (uint) PropertyID.CFG_AxMaxDec, ref AxMaxDec, 8);
            Motion.mAcm_SetProperty(Y.Handle, (uint) PropertyID.CFG_AxMaxVel, ref AxMaxVel, 8);

            Motion.mAcm_SetProperty(Z.Handle, (uint) PropertyID.CFG_AxMaxAcc, ref AxMaxAcc, 8);
            Motion.mAcm_SetProperty(Z.Handle, (uint) PropertyID.CFG_AxMaxDec, ref AxMaxDec, 8);
            Motion.mAcm_SetProperty(Z.Handle, (uint) PropertyID.CFG_AxMaxVel, ref AxMaxVel, 8);

            Motion.mAcm_SetProperty(U.Handle, (uint) PropertyID.CFG_AxMaxAcc, ref AxMaxAcc, 8);
            Motion.mAcm_SetProperty(U.Handle, (uint) PropertyID.CFG_AxMaxDec, ref AxMaxDec, 8);
            Motion.mAcm_SetProperty(U.Handle, (uint) PropertyID.CFG_AxMaxVel, ref AxMaxVel, 8);

            Motion.mAcm_SetProperty(XYhandle, (uint) PropertyID.CFG_GpMaxAcc, ref AxMaxAcc, 8);
            Motion.mAcm_SetProperty(XYhandle, (uint) PropertyID.CFG_GpMaxDec, ref AxMaxDec, 8);
            Motion.mAcm_SetProperty(XYhandle, (uint) PropertyID.CFG_GpMaxVel, ref AxMaxVel, 8);

            res = Motion.mAcm_SetProperty(X.Handle, (uint) PropertyID.PAR_AxVelHigh, ref XVel, 8);
            res = Motion.mAcm_SetProperty(Y.Handle, (uint) PropertyID.PAR_AxVelHigh, ref YVel, 8);
            res = Motion.mAcm_SetProperty(Z.Handle, (uint) PropertyID.PAR_AxVelHigh, ref ZVel, 8);
            res = Motion.mAcm_SetProperty(U.Handle, (uint) PropertyID.PAR_AxVelHigh, ref UVel, 8);
            res = Motion.mAcm_SetProperty(XYhandle, (uint) PropertyID.PAR_GpVelHigh, ref XYVel, 8);

            XVel /= 3;
            YVel /= 3;
            ZVel /= 3;
            UVel /= 3;
            XYVel /= 3;

            res = Motion.mAcm_SetProperty(X.Handle, (uint) PropertyID.PAR_AxVelLow, ref XVel, 8);
            res = Motion.mAcm_SetProperty(Y.Handle, (uint) PropertyID.PAR_AxVelLow, ref YVel, 8);
            res = Motion.mAcm_SetProperty(Z.Handle, (uint) PropertyID.PAR_AxVelLow, ref ZVel, 8);
            res = Motion.mAcm_SetProperty(U.Handle, (uint) PropertyID.PAR_AxVelLow, ref UVel, 8);
            res = Motion.mAcm_SetProperty(XYhandle, (uint) PropertyID.PAR_GpVelLow, ref XYVel, 8);
            //ErrorCode
        }

        private void SaveParams()
        {
            //-------------Сохранение параметров в файле онфигурации----
            Settings.Default.Save();
            //----------------------------------------------------------
        }

        public void Stop(Ax axis)
        {
            IntPtr handle;
            switch (axis)
            {
                case Ax.X:
                    handle = X.Handle;
                    break;
                case Ax.Y:
                    handle = Y.Handle;
                    break;
                case Ax.Z:
                    handle = Z.Handle;
                    break;
                case Ax.U:
                    handle = U.Handle;
                    break;
                default:
                    handle = new IntPtr();
                    break;
            }

            Motion.mAcm_AxStopEmg(handle);
        }

        public void GoWhile(AxisDirections direction)
        {
            ResetErrors();
            Motion.mAcm_AxMoveVel(MoveRelParam(direction).Item1, MoveRelParam(direction).Item2);
        }

        public void EmgStop()
        {
            m_Axishand.Select(Motion.mAcm_AxStopEmg).ToList();
            ResetErrors();
        }

        public async Task GoTest()
        {
            Motion.mAcm_AxMoveAbs(Z.Handle, 5);
        }

        public async Task GoThereAsync(Place place)
        {
            switch (place)
            {
                case Place.Home:
                    ResetErrors();

                    Motion.mAcm_AxHome(X.Handle, (uint) HomeMode.MODE2_Lmt, (uint) HomeDir.NegDir);
                    Motion.mAcm_AxHome(Y.Handle, (uint) HomeMode.MODE6_Lmt_Ref, (uint) HomeDir.NegDir);
                    Motion.mAcm_AxHome(Z.Handle, (uint) HomeMode.MODE2_Lmt, (uint) HomeDir.NegDir);
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
                    await MoveInPosXYAsync(WaferLoadCenter);
                    break;
                case Place.CameraChuckCenter:
                    await MoveInPosXYAsync(CameraChuckCenter);
                    break;
                case Place.BladeChuckCenter:
                    await MoveInPosXYAsync(BladeChuckCenter);
                    break;
            }
        }

        public async Task MoveInPosXY1Async(Vector2 position)
        {
            uint ElCount = 2;
            var accuracy = 0.001;
            double backlash = 0;
            var state = new ushort();
            var vel = 0.1;
            bool gotItX;
            bool gotItY;
            var signx = 0;
            var signy = 0;

            await Task.Run(() =>
                {
                    for (var recurcy = 0; recurcy < 20; recurcy++)
                    {
                        if (recurcy == 0)
                        {
                            position.X = Math.Round(position.X, 3);
                            position.Y = Math.Round(position.Y, 3);
                            Motion.mAcm_GpMoveLinearAbs(XYhandle, new double[2] {position.X, position.Y}, ref ElCount);

                            while (state != (uint) GroupState.STA_Gp_Ready) Motion.mAcm_GpGetState(XYhandle, ref state);
                            Motion.mAcm_SetProperty(X.Handle, (uint) PropertyID.PAR_AxVelLow, ref vel, 8);
                            Motion.mAcm_SetProperty(Y.Handle, (uint) PropertyID.PAR_AxVelLow, ref vel, 8);
                            Motion.mAcm_SetProperty(X.Handle, (uint) PropertyID.PAR_AxVelHigh, ref vel, 8);
                            Motion.mAcm_SetProperty(Y.Handle, (uint) PropertyID.PAR_AxVelHigh, ref vel, 8);
                        }

                        Thread.Sleep(300);


                        if (Math.Abs(Math.Round(X.ActualPosition, 3) - position.X) <= accuracy) gotItX = true;
                        else gotItX = false;
                        if (Math.Abs(Math.Round(Y.ActualPosition, 3) - position.Y) <= accuracy) gotItY = true;
                        else gotItY = false;

                        signx = Math.Sign(position.X - Math.Round(X.ActualPosition, 3));
                        signy = Math.Sign(position.Y - Math.Round(Y.ActualPosition, 3));
                        //Motion.mAcm_CheckMotionEvent(m_DeviceHandle, null, null, 2, 0, 10);
                        if (!gotItX)
                        {
                            Motion.mAcm_AxMoveRel(X.Handle,
                                signx * (Math.Abs(position.X - Math.Round(X.ActualPosition, 3)) + signx * backlash));

                            while (!gotItX)
                            {
                                if (Math.Abs(Math.Round(X.ActualPosition, 3) - position.X) <= accuracy)
                                {
                                    Motion.mAcm_AxStopEmg(X.Handle);
                                    gotItX = true;
                                }

                                Motion.mAcm_AxGetState(X.Handle, ref state);
                                if (state == (uint) AxisState.STA_AX_READY) break;
                            }
                        }

                        if (!gotItY)
                        {
                            Motion.mAcm_AxMoveRel(Y.Handle,
                                signy * (Math.Abs(position.Y - Math.Round(Y.ActualPosition, 3)) + signy * backlash));

                            while (!gotItY)
                            {
                                if (Math.Abs(Math.Round(Y.ActualPosition, 3) - position.Y) <= accuracy)
                                {
                                    Motion.mAcm_AxStopEmg(Y.Handle);
                                    gotItY = true;
                                }

                                Motion.mAcm_AxGetState(Y.Handle, ref state);
                                if (state == (uint) AxisState.STA_AX_READY) break;
                            }
                        }

                        if (gotItX & gotItY) break;
                    }
                }
            );
        }

        public async Task MoveInPosXYAsync(Vector2 position)
        {
            if (tokenXY)
            {
                tokenXY = false;
                uint ElCount = 2;
                var state = new ushort();
                uint res = 0;
                uint buf = 0;

                buf = (uint) SwLmtEnable.SLMT_DIS;
                res = Motion.mAcm_SetProperty(X.Handle, (uint) PropertyID.CFG_AxSwPelEnable, ref buf, 4);
                res = Motion.mAcm_SetProperty(X.Handle, (uint) PropertyID.CFG_AxSwMelEnable, ref buf, 4);
                res = Motion.mAcm_SetProperty(Y.Handle, (uint) PropertyID.CFG_AxSwPelEnable, ref buf, 4);
                res = Motion.mAcm_SetProperty(Y.Handle, (uint) PropertyID.CFG_AxSwMelEnable, ref buf, 4);
                await Task.Run(() =>
                    {
                        position.X = Math.Round(position.X, 3);
                        position.Y = Math.Round(position.Y, 3);
                        var pos = new double[2] {position.X, position.Y};
                        //ResetErrors();
                        Motion.mAcm_GpResetError(XYhandle);
                        //res = Motion.mAcm_GpMoveDirectAbs(XYhandle, pos, ref ElCount);
                        res = Motion.mAcm_GpMoveLinearAbs(XYhandle, pos, ref ElCount);
                        do
                        {
                            Thread.Sleep(1);
                            Motion.mAcm_GpGetState(XYhandle, ref state);
                        } while ((state & (ushort) GroupState.STA_Gp_Motion) > 0);
                    }
                );
                X.MotionDone = true;
                Y.MotionDone = true;
                if (X.LineCoefficient != 0) await X.MoveAxisInPosAsync(position.X);
                if (Y.LineCoefficient != 0) await Y.MoveAxisInPosAsync(position.Y);
            }

            tokenXY = true;
        }

        public void RefreshSettings()
        {
            Bridge = new Bridge
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

        public async Task YGoToSwLmt(double position)
        {
            uint buf = 0;
            uint res = 0;
            var tolerance = 0.003;
            var pos = (int) (position * Y.PPU);
            ushort direction = 0;

            buf = (uint) SwLmtEnable.SLMT_DIS;
            res = Motion.mAcm_SetProperty(Y.Handle, (uint) PropertyID.CFG_AxSwPelEnable, ref buf, 4);
            res = Motion.mAcm_SetProperty(Y.Handle, (uint) PropertyID.CFG_AxSwMelEnable, ref buf, 4);
            ResetErrors();

            var diff = position - Y.ActualPosition;
            if (Math.Abs(diff) > tolerance)
            {
                buf = (uint) SwLmtReact.SLMT_IMMED_STOP;
                res = Motion.mAcm_SetProperty(Y.Handle, (uint) PropertyID.CFG_AxSwPelReact, ref buf, 4);
                res = Motion.mAcm_SetProperty(Y.Handle, (uint) PropertyID.CFG_AxSwMelReact, ref buf, 4);
                var tol = 0;
                switch (Math.Sign(diff))
                {
                    case 1:
                        res = Motion.mAcm_SetProperty(Y.Handle, (uint) PropertyID.CFG_AxSwPelValue, ref pos, 4);
                        buf = (uint) SwLmtEnable.SLMT_EN;
                        res = Motion.mAcm_SetProperty(Y.Handle, (uint) PropertyID.CFG_AxSwPelEnable, ref buf, 4);
                        buf = (uint) SwLmtToleranceEnable.TOLERANCE_ENABLE;
                        res = Motion.mAcm_SetProperty(Y.Handle, (uint) PropertyID.CFG_AxSwPelToleranceEnable, ref buf,
                            4);
                        res = Motion.mAcm_SetProperty(Y.Handle, (uint) PropertyID.CFG_AxSwPelToleranceValue, ref tol,
                            4);
                        direction = (ushort) VelMoveDir.DIR_POSITIVE;
                        break;
                    case -1:
                        res = Motion.mAcm_SetProperty(Y.Handle, (uint) PropertyID.CFG_AxSwMelValue, ref pos, 4);
                        buf = (uint) SwLmtEnable.SLMT_EN;
                        res = Motion.mAcm_SetProperty(Y.Handle, (uint) PropertyID.CFG_AxSwMelEnable, ref buf, 4);
                        buf = (uint) SwLmtToleranceEnable.TOLERANCE_ENABLE;
                        res = Motion.mAcm_SetProperty(Y.Handle, (uint) PropertyID.CFG_AxSwMelToleranceEnable, ref buf,
                            4);
                        res = Motion.mAcm_SetProperty(Y.Handle, (uint) PropertyID.CFG_AxSwMelToleranceValue, ref tol,
                            4);
                        direction = (ushort) VelMoveDir.DIR_NEGATIVE;
                        break;
                }

                Motion.mAcm_AxMoveVel(Y.Handle, direction);
                uint status = 0;
                uint slmtp = 0;
                uint slmtn = 0;
                await Task.Run(() =>
                    {
                        do
                        {
                            Thread.Sleep(1);
                            Motion.mAcm_AxGetMotionIO(Y.Handle, ref status);
                            slmtp = status & (uint) Ax_Motion_IO.AX_MOTION_IO_SLMTP;
                            slmtn = status & (uint) Ax_Motion_IO.AX_MOTION_IO_SLMTN;
                        } while ((slmtp == 0) & (slmtn == 0));
                    }
                );
                Y.SetVelocity(1);
                Motion.mAcm_AxSetCmdPosition(Y.Handle, Y.ActualPosition);
                await YGoToSwLmt(position);
                buf = (uint) SwLmtEnable.SLMT_DIS;
                //res = Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxSwPelEnable, ref buf, 4);
                //res = Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxSwMelEnable, ref buf, 4);
                ResetErrors();
            }
        }

        public void ResetErrors()
        {
            foreach (var ax in axes) Motion.mAcm_AxResetError(ax.Handle);
        }

        private void MachineState() // Производит опрос всех датчиков, линеек, координат
        {
            var AxEvtStatusArray = new uint[4];//axes.Length
            var GpEvtStatusArray = new uint[1];
            uint res = 0;
            while (true)
            {
                CheckSensors();                
                //SpindleFreq = _spindleModbus.ReadHoldingRegisters(0xD000, 1)[0]/10;
                //SpindleCurrent = _spindleModbus.ReadHoldingRegisters(0xD001, 1)[0]/10;
                if (!testRegime)
                {
                    res = Motion.mAcm_CheckMotionEvent(m_DeviceHandle, AxEvtStatusArray, GpEvtStatusArray, m_ulAxisCount, 0, 10);                
                    foreach (var ax in axes)
                    {
                        Result = Motion.mAcm_AxGetMotionIO(ax.Handle, ref IOStatus);
                        if (Result == (uint) ErrorCode.SUCCESS)
                        {
                            ax.LmtN = (IOStatus & (uint) Ax_Motion_IO.AX_MOTION_IO_LMTN) > 0 ? true : false;
                            ax.LmtP = (IOStatus & (uint) Ax_Motion_IO.AX_MOTION_IO_LMTP) > 0 ? true : false;
                        }

                        for (var channel = 0; channel < 4; channel++)
                        {
                            Result = Motion.mAcm_AxDiGetBit(ax.Handle, (ushort) channel, ref BitData);
                            if (Result == (uint) ErrorCode.SUCCESS) ax.DIs &= ~BitData << channel;
                        }

                        Result = Motion.mAcm_AxGetCmdPosition(ax.Handle, ref position);
                        if (Result == (uint) ErrorCode.SUCCESS) ax.CmdPosition = position;

                        Result = Motion.mAcm_AxGetActualPosition(ax.Handle, ref position);
                        if (Result == (uint) ErrorCode.SUCCESS) ax.ActualPosition = position;

                        //if (!SpindleWater) OnSpinWaterWanished(/*new DIEventArgs()*/);
                        //if (!CoolantWater) OnCoolWaterWanished(/*new DIEventArgs()*/);
                        //if (!ChuckVacuum) OnVacuumWanished(/*new DIEventArgs()*/);
                        if (!Air) OnAirWanished?.Invoke( /*new DIEventArgs()*/);


                        //TrigVar trig = new TrigVar();
                        //DIEventArgs dI = new DIEventArgs();
                        ////trig.trigger(Air, (dI)=>OnAirWanished);
                        ///

                        if (res == (uint)ErrorCode.SUCCESS)
                        {
                            for (var i = 0; i < axes.Length; i++)
                            {
                                if ((AxEvtStatusArray[i] & (uint)EventType.EVT_AX_MOTION_DONE) > 0)
                                    axes[i].MotionDone = true;

                                if ((AxEvtStatusArray[i] & (uint)EventType.EVT_AX_HOME_DONE) > 0) axes[i].HomeDone = true;
                            }
                        }
                    }
                }
                Thread.Sleep(1);
            }
        }

        private void MachineEvents()
        {
            var AxEvtStatusArray = new uint[axes.Length];
            var GpEvtStatusArray = new uint[1];

            while ( /*m_bInit*/ true)
            {
                Result = Motion.mAcm_CheckMotionEvent(m_DeviceHandle, AxEvtStatusArray, GpEvtStatusArray, m_ulAxisCount,
                    0, 10);
                if (Result == (uint) ErrorCode.SUCCESS)
                {
                    for (var i = 0; i < axes.Length; i++)
                    {
                        if ((AxEvtStatusArray[i] & (uint) EventType.EVT_AX_MOTION_DONE) > 0) axes[i].MotionDone = true;

                        //if ((AxEvtStatusArray[i] & (uint)EventType.EVT_AX_VH_START) == 0)
                        //{
                        //    axes[i].MotionDone = true;
                        //}


                        if ((AxEvtStatusArray[i] & (uint) EventType.EVT_AX_HOME_DONE) > 0) axes[i].HomeDone = true;
                        //else axes[i].HomeDone = false;
                    }

                    Thread.Sleep(100);
                }
            }
        }

        private void CheckSensors()
        {
            if (!testRegime)
            {
                ChuckVacuum = Bridge.ChuckVacuum | X.GetDI(DI.IN3);
                SpindleWater = Bridge.SpindleWater | X.GetDI(DI.IN1);
                CoolantWater = Bridge.CoolantWater | X.GetDI(DI.IN2);
                Air = Bridge.Air | Z.GetDI(DI.IN1);
            }
            else
            {
                ChuckVacuum = Bridge.ChuckVacuum;
                SpindleWater = Bridge.SpindleWater;
                CoolantWater = Bridge.CoolantWater;
                Air = Bridge.Air;
            }
        }

        #endregion

        //private void CheckDevEvents()
        //{
        //    uint Result;
        //    UInt32[] AxEvtStatusArray = new UInt32[32];
        //    UInt32[] GpEvtStatusArray = new UInt32[32];
        //    UInt32 i;
        //    while (MachineInit)
        //    {
        //        Result = Motion.mAcm_CheckMotionEvent(m_DeviceHandle, AxEvtStatusArray, GpEvtStatusArray, m_ulAxisCount, 2, 1);
        //        if (Result == (uint)ErrorCode.SUCCESS)
        //        {
        //            for (i = 0; i < m_ulAxisCount; i++)
        //            {

        //                if ((AxEvtStatusArray[i] & (uint)EventType.EVT_AX_MOTION_DONE) > 0)
        //                {
        //                    AxMotionDone |= i + 1;
        //                }
        //                else
        //                {
        //                    //  globalProperties.AxMotionDone &= i + 1;
        //                }
        //                if ((AxEvtStatusArray[i] & (uint)EventType.EVT_AX_COMPARED) > 0)
        //                {

        //                }

        //            }
        //            if (m_GpHand != IntPtr.Zero)
        //            {

        //                if ((GpEvtStatusArray[0] & (uint)EventType.EVT_GP1_MOTION_DONE) > 0)
        //                {
        //                    GpMotionDone = true;
        //                }
        //                else
        //                {
        //                    GpMotionDone = false;
        //                }

        //            }
        //        }
        //    }
        //}

        //private void EnableDevEvents()
        //{
        //    UInt32 Result;
        //    uint[] AxEnableEvtArray = new uint[m_ulAxisCount];
        //    uint[] GpEnableEvt = new uint[1];
        //    if (MachineInit)
        //    {
        //        for (int i = 0; i < m_ulAxisCount; i++)
        //        {
        //            AxEnableEvtArray[i] |= (uint)EventType.EVT_AX_MOTION_DONE;
        //            AxEnableEvtArray[i] |= (uint)EventType.EVT_AX_COMPARED;
        //            AxEnableEvtArray[i] |= (uint)EventType.EVT_AX_HOME_DONE;
        //        }
        //        GpEnableEvt[0] |= (uint)EventType.EVT_GP1_MOTION_DONE;
        //        Result = Motion.mAcm_EnableMotionEvent(m_DeviceHandle, AxEnableEvtArray, GpEnableEvt, m_ulAxisCount, 1);
        //        if (Result != (uint)ErrorCode.SUCCESS)
        //        {
        //            MessageBox.Show(Result.ToString());
        //            return;
        //        }
        //    }
        //}
    }

    public enum Messages
    {
        [Description("Установити пластину и включите вакуум")]
        SetAndTurnOnVacuum
    }

    public enum AxisDirections
    {
        XP,
        XN,
        YP,
        YN,
        ZP,
        ZN,
        UP,
        UN
    }

    public enum AxDir
    {
        POS,
        NEG
    }

    public enum Place
    {
        Home,
        Loading,
        CameraChuckCenter,
        BladeChuckCenter
    }

    public enum Velocity
    {
        Fast,
        Slow,
        Step,
        Service,
        Stop,
        Work
    }

    public enum Ax
    {
        X,
        Y,
        Z,
        U
    }

    [AddINotifyPropertyChangedInterface]
    internal class Axis
    {
        private double actualPosition;
        private double storeSpeed;

        public Axis(double lineCoefficient, IntPtr handle, int AxisNum)
        {
            LineCoefficient = lineCoefficient;
            Handle = handle;
            this.AxisNum = AxisNum;
        }

        public int AxisNum { get; }
        public IntPtr Handle { get; }
        public double LineCoefficient { get; }
        public bool LmtP { get; set; }
        public bool LmtN { get; set; }
        public double CmdPosition { get; set; }

        public double ActualPosition
        {
            get => LineCoefficient != 0 ? LineCoefficient * actualPosition : CmdPosition;
            set => actualPosition = value;
        }

        public int DIs { get; set; }
        public int DOs { get; set; }
        public int PPU { get; set; }
        public bool MotionDone { get; set; }
        public bool HomeDone { get; set; }
        public bool Compared { get; set; }

        public bool GetDI(DI din)
        {
            return (DIs & (1 << (int) din)) != 0 ? true : false;
        }

        public bool GetDO(DO dout)
        {
            byte bit = 0;
            var result = Motion.mAcm_AxDoGetBit(Handle, (ushort) dout, ref bit);            
            return bit != 0 ? true : false;
        }

        public bool SetDo(DO dout, byte val)
        {            
            var result = Motion.mAcm_AxDoSetBit(Handle, (ushort)dout, val);
            Thread.Sleep(100);
            return  result == (uint) ErrorCode.SUCCESS;
        }

        /// <summary>
        ///     Установка скорости по оси
        /// </summary>
        /// <param name="feed">unit per second</param>
        public void SetVelocity(double feed)
        {
            //uint result = 0;           
            var VelHigh = feed;
            var VelLow = feed / 2;
            double AxMaxVel = 30;
            double AxMaxDec = 180;
            double AxMaxAcc = 180;
            Motion.mAcm_SetProperty(Handle, (uint) PropertyID.CFG_AxMaxAcc, ref AxMaxAcc, 8);
            Motion.mAcm_SetProperty(Handle, (uint) PropertyID.CFG_AxMaxDec, ref AxMaxDec, 8);
            Motion.mAcm_SetProperty(Handle, (uint) PropertyID.CFG_AxMaxVel, ref AxMaxVel, 8);
            Motion.mAcm_SetProperty(Handle, (uint) PropertyID.PAR_AxVelHigh, ref VelHigh, 8);
            Motion.mAcm_SetProperty(Handle, (uint) PropertyID.PAR_AxVelLow, ref VelLow, 8);
        }

        public double GetVelocity()
        {
            uint res = 0;
            double vel = 0;
            uint bufLength = 8;
            res = Motion.mAcm_GetProperty(Handle, (uint) PropertyID.PAR_AxVelHigh, ref vel, ref bufLength);
            return vel;
        }

        public void GoWhile(AxDir direction)
        {
            ushort dir;
            switch (direction)
            {
                case AxDir.POS:
                    dir = (ushort) VelMoveDir.DIR_POSITIVE;
                    break;
                case AxDir.NEG:
                    dir = (ushort) VelMoveDir.DIR_NEGATIVE;
                    break;
                default:
                    dir = (ushort) VelMoveDir.NOT_SUPPORT;
                    break;
            }

            Motion.mAcm_AxResetError(Handle);
            MotionDone = false;
            Motion.mAcm_AxMoveVel(Handle, dir);
        }

        public async Task MoveAxisInPos1Async(double position)
        {
            var accuracy = 0.003;
            double backlash = 0;
            var state = new ushort();
            var motionStatus = new uint();
            var vel = 0.1;
            bool gotIt;
            var sign = 0;

            if (LineCoefficient != 0)
                await Task.Run(() =>
                    {
                        for (var recurcy = 0; recurcy < 20; recurcy++)
                        {
                            if (recurcy == 0)
                            {
                                position = Math.Round(position, 3);
                                Motion.mAcm_AxMoveAbs(Handle, position);
                                do
                                {
                                    Motion.mAcm_AxGetMotionStatus(Handle, ref motionStatus);
                                } while ((motionStatus & 0b_1) != 1);

                                Motion.mAcm_SetProperty(Handle, (uint) PropertyID.PAR_AxVelLow, ref vel, 8);
                                Motion.mAcm_SetProperty(Handle, (uint) PropertyID.PAR_AxVelHigh, ref vel, 8);
                            }
                            //Thread.Sleep(300);

                            if (Math.Abs(Math.Round(ActualPosition, 3) - position) <= accuracy) gotIt = true;
                            else gotIt = false;
                            sign = Math.Sign(position - Math.Round(ActualPosition, 3));
                            if (!gotIt)
                            {
                                Motion.mAcm_AxMoveRel(Handle,
                                    sign * (Math.Abs(position - Math.Round(ActualPosition, 3)) + sign * backlash));
                                while (!gotIt)
                                {
                                    if (Math.Abs(Math.Round(ActualPosition, 3) - position) <= accuracy)
                                    {
                                        Motion.mAcm_AxStopEmg(Handle);
                                        gotIt = true;
                                    }

                                    Motion.mAcm_AxGetMotionStatus(Handle, ref motionStatus);
                                    if ((motionStatus & 0b_1) == 1) break;
                                }
                            }

                            if (gotIt) break;
                        }
                    }
                );
            else
                await Task.Run(() =>
                    {
                        Motion.mAcm_AxMoveAbs(Handle, position);
                        do
                        {
                            Motion.mAcm_AxGetMotionStatus(Handle, ref motionStatus);
                            //Motion.mAcm_AxGetState(Handle, ref state);
                        } while ( /*state == (uint)AxisState.STA_AX_BUSY*/(motionStatus & 0b_1) != 1);
                    }
                );
        }

        public async Task WaitUntilStopAsync()
        {
            var status = new uint();
            await Task.Run(() =>
            {
                do
                {
                    Motion.mAcm_AxGetMotionStatus(Handle, ref status);
                } while ((status & 0x1) == 0);
            });
        }

        private async Task SearchPositionAsync(double position, double accuracy)
        {
            var flag = true;
            position = Math.Round(position, 3);
            var sign = 0;
            double difpos = 0;

            await Task.Run(() =>
                {
                    //sign = Math.Sign(position - Math.Round(ActualPosition, 3));

                    Motion.mAcm_AxGetActualPosition(Handle, ref difpos);
                    difpos = Math.Round(difpos * LineCoefficient, 3) + position;
                    MotionDone = false;
                    Motion.mAcm_AxMoveRel(Handle, difpos * 1.1);
                    do
                    {
                        Motion.mAcm_AxGetActualPosition(Handle, ref difpos);
                        difpos = Math.Abs(-Math.Round(difpos * LineCoefficient, 3) - position);
                        if (difpos <= accuracy)
                        {
                            Motion.mAcm_AxStopEmg(Handle);
                            flag = false;
                            break;
                        }
                    } while (!MotionDone);
                }
            );
            if (flag) await SearchPositionAsync(position, accuracy);
        }

        public async Task MoveAxisInPos2Async(double position)
        {
            var accuracy = 0.013;
            var dif = Math.Abs(Math.Round(ActualPosition, 3) - position);
            if (dif >= accuracy)
            {
                position = Math.Round(position, 3);
                double backlash = 0;
                var vel = 0.1;
                var sign = 0;
                var n = AxisNum;
                MotionDone = false;

                await Task.Run(() =>
                    {
                        if (dif > 0) ;
                        Motion.mAcm_AxMoveAbs(Handle, position);
                        while (!MotionDone) ;
                    }
                );
                if (LineCoefficient != 0)
                    if (Math.Abs(Math.Round(ActualPosition, 3) - position) >= accuracy)
                    {
                        SetVelocity(vel);
                        await SearchPositionAsync(position, accuracy);
                    }
            }
        }

        //public double GetOperationTime()
        //{
        //    //double vel
        //    //Motion.mAcm_AxGetCmdVelocity(Handle,)
        //    return ;
        //}

        public void ResetErrors()
        {
            Motion.mAcm_AxResetError(Handle);
        }

        public async Task MoveAxisInPosAsync(double position, int rec = 0, double trace = 0)
        {
            var state = new uint();
            if (rec == 0) storeSpeed = GetVelocity();
            uint buf = 0;
            uint res = 0;
            var tolerance = 0.003;
            var pos = (int) (position * PPU);
            ushort direction = 0;

            buf = (uint) SwLmtEnable.SLMT_DIS;
            res = Motion.mAcm_SetProperty(Handle, (uint) PropertyID.CFG_AxSwPelEnable, ref buf, 4);
            res = Motion.mAcm_SetProperty(Handle, (uint) PropertyID.CFG_AxSwMelEnable, ref buf, 4);
            if (LineCoefficient != 0)
            {
                var diff = position - ActualPosition;
                if (Math.Abs(diff) > tolerance)
                {
                    ResetErrors();
                    buf = (uint) SwLmtReact.SLMT_IMMED_STOP;
                    res = Motion.mAcm_SetProperty(Handle, (uint) PropertyID.CFG_AxSwPelReact, ref buf, 4);
                    res = Motion.mAcm_SetProperty(Handle, (uint) PropertyID.CFG_AxSwMelReact, ref buf, 4);
                    var tol = 0;
                    switch (Math.Sign(diff))
                    {
                        case 1:
                            res = Motion.mAcm_SetProperty(Handle, (uint) PropertyID.CFG_AxSwPelValue, ref pos, 4);
                            buf = (uint) SwLmtEnable.SLMT_EN;
                            res = Motion.mAcm_SetProperty(Handle, (uint) PropertyID.CFG_AxSwPelEnable, ref buf, 4);
                            buf = (uint) SwLmtToleranceEnable.TOLERANCE_ENABLE;
                            res = Motion.mAcm_SetProperty(Handle, (uint) PropertyID.CFG_AxSwPelToleranceEnable, ref buf,
                                4);
                            res = Motion.mAcm_SetProperty(Handle, (uint) PropertyID.CFG_AxSwPelToleranceValue, ref tol,
                                4);
                            direction = (ushort) VelMoveDir.DIR_POSITIVE;
                            break;
                        case -1:
                            res = Motion.mAcm_SetProperty(Handle, (uint) PropertyID.CFG_AxSwMelValue, ref pos, 4);
                            buf = (uint) SwLmtEnable.SLMT_EN;
                            res = Motion.mAcm_SetProperty(Handle, (uint) PropertyID.CFG_AxSwMelEnable, ref buf, 4);
                            buf = (uint) SwLmtToleranceEnable.TOLERANCE_ENABLE;
                            res = Motion.mAcm_SetProperty(Handle, (uint) PropertyID.CFG_AxSwMelToleranceEnable, ref buf,
                                4);
                            res = Motion.mAcm_SetProperty(Handle, (uint) PropertyID.CFG_AxSwMelToleranceValue, ref tol,
                                4);
                            direction = (ushort) VelMoveDir.DIR_NEGATIVE;
                            break;
                    }

                    Motion.mAcm_AxMoveVel(Handle, direction);
                    uint status = 0;
                    uint slmtp = 0;
                    uint slmtn = 0;
                    await Task.Run(() =>
                        {
                            do
                            {
                                Thread.Sleep(1);
                                Motion.mAcm_AxGetMotionIO(Handle, ref status);
                                slmtp = status & (uint) Ax_Motion_IO.AX_MOTION_IO_SLMTP;
                                slmtn = status & (uint) Ax_Motion_IO.AX_MOTION_IO_SLMTN;
                            } while ((slmtp == 0) & (slmtn == 0));
                        }
                    );
                    SetVelocity(1);
                    Motion.mAcm_AxSetCmdPosition(Handle, ActualPosition);
                    await MoveAxisInPosAsync(position, ++rec);
                    rec--;
                    if (rec == 0)
                    {
                        SetVelocity(storeSpeed);
                        ResetErrors();
                    }
                }
            }
            else
            {
                Motion.mAcm_AxMoveAbs(Handle, position);
                await Task.Run(() =>
                {
                    do
                    {
                        trace = ActualPosition;
                        Thread.Sleep(1);
                        Motion.mAcm_AxGetMotionStatus(Handle, ref state);
                    } while ((state & 0x1) == 0);
                });
            }
        }
    }
}