using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using netDxf;
using Advantech.Motion;
using AForge;
using AForge.Video;
using AForge.Video.DirectShow;
using AForge.Imaging.Filters;
using PropertyChanged;
using System.ComponentModel;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using DicingBlade.Properties;
namespace DicingBlade.Classes
{
    enum DO 
    {   
        OUT4,
        OUT5,
        OUT6,
        OUT7    
    }

    enum DI:ushort
    {
        IN1,
        IN2,
        IN3
    }
    public struct DIEventArgs { }
    public delegate void DIEventHandler(DIEventArgs eventArgs);




    [AddINotifyPropertyChangedInterface]
    class Machine : INotifyPropertyChanged
    {
        public IntPtr[] m_Axishand = new IntPtr[32];
        UInt32 IOStatus = new UInt32();
        UInt32 Result;
        byte BitData = new byte();


        double position = new double();
        private IntPtr m_DeviceHandle = IntPtr.Zero;
        private uint m_ulAxisCount = 0;
        private bool m_bInit = false;
        private VideoCaptureDevice LocalWebCam;
        private FilterInfoCollection LocalWebCamsCollection;
        public event PropertyChangedEventHandler PropertyChanged;
        public BitmapImage Bi { get; set; }
        public bool MachineInit { get; set; } = false;

        public Vector3 bladeChuckCenter;
        public Vector3 cameraBladeOffset;
        public bool PCI1240IsConnected;
        public double xLength;
        public double yLength;
        public double zLength;
        public double fAngle;
        public double gap; // зазор до концевика
        public bool Fast { get; set; }

        private bool spindleWater;
        private bool coolantWater;
        private bool chuckVacuum;
        private bool air;
        private bool bladeSensor;

        public bool SpindleWater
        {
            get { return X.GetDI(DI.IN1); }
        }
        public bool CoolantWater
        {
            get { return X.GetDI(DI.IN2); }
        }
        public bool ChuckVacuum
        {
            get { return X.GetDI(DI.IN3); }
        }
        public bool Air
        {
            get { return Z.GetDI(DI.IN1); }
        }
        public bool BladeSensor { get; set; }

        public bool xDirP { get; set; } // Разрешение на положительное направление
        public bool xDirN { get; set; } // Разрешение на отрицательное направление
        public bool yDirP { get; set; }
        public bool yDirN { get; set; }
        public bool zDirP { get; set; }
        public bool zDirN { get; set; }
        public bool fDirP { get; set; }
        public bool fDirN { get; set; }
        public Axis X { get; set; }
        public Axis Y { get; set; }
        public Axis Z { get; set; }
        public Axis U { get; set; }

        private Axis[] axes;

        private IntPtr Hand(AxisDirections direction)
        {
            switch (direction)
            {
                case AxisDirections.XP | AxisDirections.XN: return m_Axishand[0];
                case AxisDirections.YP | AxisDirections.YN: return m_Axishand[1];
                case AxisDirections.ZP | AxisDirections.ZN: return m_Axishand[2];
                case AxisDirections.UP | AxisDirections.UN: return m_Axishand[3];
                default: return new IntPtr();
            }
        }

        private (IntPtr, ushort) MoveRelParam(AxisDirections direction)
        {
            switch (direction)
            {
                case AxisDirections.XP: return (m_Axishand[0], 1);
                case AxisDirections.XN: return (m_Axishand[1], 0);
                case AxisDirections.YP: return (m_Axishand[2], 1);
                case AxisDirections.YN: return (m_Axishand[3], 0);
                case AxisDirections.ZP: return (m_Axishand[0], 1);
                case AxisDirections.ZN: return (m_Axishand[1], 0);
                case AxisDirections.UP: return (m_Axishand[2], 1);
                case AxisDirections.UN: return (m_Axishand[3], 0);
                default: return (new IntPtr(), 1);
            }
        }
        public Machine() // В конструкторе происходит инициализация всех устройств, загрузка параметров.
        {
            StartCamera();
            DevicesConnection();
            SetConfigs();
            SetVelocity(Velocity.Slow);

            axes = new Axis[] { X, Y, Z, U };
            OnAirWanished += EMGScenario;           
            Thread threadCurrentState = new Thread(new ThreadStart(MachineState));
            threadCurrentState.Start();
        }
        #region Методы


        public void MachineState() // Производит опрос всех датчиков, линеек, координат
        {
            foreach (var ax in axes)
            {
                Result = Motion.mAcm_AxGetMotionIO(ax.handle, ref IOStatus);
                if (Result == (uint)ErrorCode.SUCCESS)
                {
                    ax.LmtN = ((IOStatus & (uint)Ax_Motion_IO.AX_MOTION_IO_LMTN) > 0) ? true : false;
                    ax.LmtP = ((IOStatus & (uint)Ax_Motion_IO.AX_MOTION_IO_LMTP) > 0) ? true : false;
                }
                for (int channel = 0; channel < 4; channel++)
                {
                    Result = Motion.mAcm_AxDiGetBit(ax.handle, (ushort)channel, ref BitData);
                    if (Result == (uint)ErrorCode.SUCCESS)
                    {
                        ax.DIs &= ~BitData << channel;                       
                    }
                }
                Result = Motion.mAcm_AxGetCmdPosition(ax.handle, ref position);
                if (Result == (uint)ErrorCode.SUCCESS) ax.CmdPosition = position;

                Result = Motion.mAcm_AxGetActualPosition(ax.handle, ref position);
                if (Result == (uint)ErrorCode.SUCCESS) ax.ActualPosition = position;

                if (SpindleWater) OnSpinWaterWanished(new DIEventArgs());
                if (CoolantWater) OnCoolWaterWanished(new DIEventArgs());
                if (ChuckVacuum) OnVacuumWanished(new DIEventArgs());
                if (Air) OnAirWanished(new DIEventArgs());
            }
            Thread.Sleep(100);
        }
        #endregion
       
        public event DIEventHandler OnVacuumWanished;
        public event DIEventHandler OnCoolWaterWanished;
        public event DIEventHandler OnSpinWaterWanished;
        public event DIEventHandler OnAirWanished;
        private void EMGScenario(DIEventArgs eventArgs) { }

        public void StartCamera()
        {
            LocalWebCamsCollection = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            try { LocalWebCam = new VideoCaptureDevice(LocalWebCamsCollection[1].MonikerString); }
            catch
            {
               // MessageBox.Show("Включите питание видеокамеры !");
                StartCamera();
            }
            finally
            {
                LocalWebCam.VideoResolution = LocalWebCam.VideoCapabilities[8];
                LocalWebCam.NewFrame += new NewFrameEventHandler(Cam_NewFrame);
                LocalWebCam.Start();
            }
        }
        public void Cam_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            BitmapImage bitmap;
            try
            {
                Mirror filter = new Mirror(false, true);
                System.Drawing.Bitmap img = (Bitmap)eventArgs.Frame.Clone();
                filter.ApplyInPlace(img);
                MemoryStream ms = new MemoryStream();
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
        }
        public void StopCamera() => LocalWebCam.Stop();
        private bool DevicesConnection()
        {
            uint Result;
            string strTemp;
            int ResAvlb;
            uint i = 0;
            uint[] slaveDevs = new uint[16];
            uint AxesPerDev = new uint();
            uint deviceCount = 0;
            uint DeviceNum = 0;
            DEV_LIST[] CurAvailableDevs = new DEV_LIST[Motion.MAX_DEVICES];


            ResAvlb = Motion.mAcm_GetAvailableDevs(CurAvailableDevs, Motion.MAX_DEVICES, ref deviceCount);

            if (ResAvlb != (int)ErrorCode.SUCCESS)
            {
                strTemp = "Get Device Numbers Failed With Error Code: [0x" + Convert.ToString(ResAvlb, 16) + "]";
                MessageBox.Show(strTemp + " " + ResAvlb);
                return false;
            }

            if (deviceCount > 0)
            {
                DeviceNum = CurAvailableDevs[0].DeviceNum;
            }

            DeviceNum = CurAvailableDevs[0].DeviceNum;

            Result = Motion.mAcm_DevOpen(DeviceNum, ref m_DeviceHandle);
            if (Result != (uint)ErrorCode.SUCCESS)
            {
                strTemp = "Open Device Failed With Error Code: [0x" + Convert.ToString(Result, 16) + "]";
                MessageBox.Show(strTemp + Result);
                return false;
            }

            Result = Motion.mAcm_GetU32Property(m_DeviceHandle, (uint)PropertyID.FT_DevAxesCount, ref AxesPerDev);
            if (Result != (uint)ErrorCode.SUCCESS)
            {
                strTemp = "Get Axis Number Failed With Error Code: [0x" + Convert.ToString(Result, 16) + "]";
                MessageBox.Show(strTemp + " " + Result);
                return false;
            }

            m_ulAxisCount = AxesPerDev;

            for (i = 0; i < m_ulAxisCount; i++)
            {

                Result = Motion.mAcm_AxOpen(m_DeviceHandle, (UInt16)i, ref m_Axishand[i]);
                if (Result != (uint)ErrorCode.SUCCESS)
                {
                    strTemp = "Open Axis Failed With Error Code: [0x" + Convert.ToString(Result, 16) + "]";
                    MessageBox.Show(strTemp + " " + Result);
                    return false;
                }

                double cmdPosition = new double();
                cmdPosition = 0;
                //Set command position for the specified axis
                Motion.mAcm_AxSetCmdPosition(m_Axishand[i], cmdPosition);
                //Set actual position for the specified axis
                Motion.mAcm_AxSetActualPosition(m_Axishand[i], cmdPosition);
            }
            m_bInit = true;

            X = new Axis() { handle = m_Axishand[0] };
            Y = new Axis() { handle = m_Axishand[1] };
            Z = new Axis() { handle = m_Axishand[2] };
            U = new Axis() { handle = m_Axishand[3] };


            if (m_bInit) MachineInit = true;
            return true;
        }
        public void SetConfigs()
        {   
            double XAcc = Settings.Default.XAcc;
            double XDec =  Settings.Default.XDec;
            int XPPU =  Settings.Default.XPPU;
            int XJerk =  Settings.Default.XJerk;

            double YAcc =  Settings.Default.YAcc;
            double YDec =  Settings.Default.YDec;
            int YPPU =  Settings.Default.YPPU;
            int YJerk =  Settings.Default.YJerk;

            double ZAcc =  Settings.Default.ZAcc;
            double ZDec =  Settings.Default.ZDec;
            int ZPPU =  Settings.Default.ZPPU;
            int ZJerk =  Settings.Default.ZJerk;
            
            double UAcc = Settings.Default.UAcc;
            double UDec = Settings.Default.UDec;
            int UPPU = Settings.Default.UPPU;
            int UJerk = Settings.Default.UJerk;

            //OffsetX =  Settings.Default.OffsetX;
            //OffsetY =  Settings.Default.OffsetY;

            Motion.mAcm_SetProperty(X.handle, (uint)PropertyID.CFG_AxPPU, ref XPPU, 8);
            Motion.mAcm_SetProperty(X.handle, (uint)PropertyID.PAR_AxJerk, ref XJerk, 8);            
            Motion.mAcm_SetProperty(X.handle, (uint)PropertyID.PAR_AxAcc, ref XAcc, 8);
            Motion.mAcm_SetProperty(X.handle, (uint)PropertyID.PAR_AxDec, ref XDec, 8);

            Motion.mAcm_SetProperty(Y.handle, (uint)PropertyID.CFG_AxPPU, ref YPPU, 8);
            Motion.mAcm_SetProperty(Y.handle, (uint)PropertyID.PAR_AxJerk, ref YJerk, 8);            
            Motion.mAcm_SetProperty(Y.handle, (uint)PropertyID.PAR_AxAcc, ref YAcc, 8);
            Motion.mAcm_SetProperty(Y.handle, (uint)PropertyID.PAR_AxDec, ref YDec, 8);

            Motion.mAcm_SetProperty(Z.handle, (uint)PropertyID.CFG_AxPPU, ref ZPPU, 8);
            Motion.mAcm_SetProperty(Z.handle, (uint)PropertyID.PAR_AxJerk, ref ZJerk, 8);            
            Motion.mAcm_SetProperty(Z.handle, (uint)PropertyID.PAR_AxAcc, ref ZAcc, 8);
            Motion.mAcm_SetProperty(Z.handle, (uint)PropertyID.PAR_AxDec, ref ZDec, 8);

            Motion.mAcm_SetProperty(U.handle, (uint)PropertyID.CFG_AxPPU, ref UPPU, 8);
            Motion.mAcm_SetProperty(U.handle, (uint)PropertyID.PAR_AxJerk, ref UJerk, 8);            
            Motion.mAcm_SetProperty(U.handle, (uint)PropertyID.PAR_AxAcc, ref UAcc, 8);
            Motion.mAcm_SetProperty(U.handle, (uint)PropertyID.PAR_AxDec, ref UDec, 8);

            int PlsInMde = 2;
            int PlsInLogic = 2;
            int PlsInSrc = 2;
            int PlsOutMde = 1;           
            //ushort state = new ushort();

            Motion.mAcm_SetProperty(X.handle, (uint)PropertyID.CFG_AxPulseInMode, ref PlsInMde, 8);
            Motion.mAcm_SetProperty(X.handle, (uint)PropertyID.CFG_AxPulseInLogic, ref PlsInLogic, 8);
            Motion.mAcm_SetProperty(X.handle, (uint)PropertyID.CFG_AxPulseInSource, ref PlsInSrc, 8);
            Motion.mAcm_SetProperty(X.handle, (uint)PropertyID.CFG_AxPulseOutMode, ref PlsOutMde, 8);

            Motion.mAcm_SetProperty(Y.handle, (uint)PropertyID.CFG_AxPulseInMode, ref PlsInMde, 8);
            Motion.mAcm_SetProperty(Y.handle, (uint)PropertyID.CFG_AxPulseInLogic, ref PlsInLogic, 8);
            Motion.mAcm_SetProperty(Y.handle, (uint)PropertyID.CFG_AxPulseInSource, ref PlsInSrc, 8);
            Motion.mAcm_SetProperty(Y.handle, (uint)PropertyID.CFG_AxPulseOutMode, ref PlsOutMde, 8);

            Motion.mAcm_SetProperty(Z.handle, (uint)PropertyID.CFG_AxPulseInMode, ref PlsInMde, 8);
            Motion.mAcm_SetProperty(Z.handle, (uint)PropertyID.CFG_AxPulseInLogic, ref PlsInLogic, 8);
            Motion.mAcm_SetProperty(Z.handle, (uint)PropertyID.CFG_AxPulseInSource, ref PlsInSrc, 8);
            Motion.mAcm_SetProperty(Z.handle, (uint)PropertyID.CFG_AxPulseOutMode, ref PlsOutMde, 8);

            Motion.mAcm_SetProperty(U.handle, (uint)PropertyID.CFG_AxPulseInMode, ref PlsInMde, 8);
            Motion.mAcm_SetProperty(U.handle, (uint)PropertyID.CFG_AxPulseInLogic, ref PlsInLogic, 8);
            Motion.mAcm_SetProperty(U.handle, (uint)PropertyID.CFG_AxPulseInSource, ref PlsInSrc, 8);
            Motion.mAcm_SetProperty(U.handle, (uint)PropertyID.CFG_AxPulseOutMode, ref PlsOutMde, 8);

            //int EZ = 0;

            //Motion.mAcm_SetProperty(m_Axishand[0], (uint)PropertyID.CFG_AxEzLogic, ref EZ, 8);
            //Motion.mAcm_SetProperty(m_Axishand[1], (uint)PropertyID.CFG_AxEzLogic, ref EZ, 8);


            int Reset = 1;
            Motion.mAcm_SetProperty(X.handle, (uint)PropertyID.CFG_AxHomeResetEnable, ref Reset, 8);
            Motion.mAcm_SetProperty(Y.handle, (uint)PropertyID.CFG_AxHomeResetEnable, ref Reset, 8);
            Motion.mAcm_SetProperty(Z.handle, (uint)PropertyID.CFG_AxHomeResetEnable, ref Reset, 8);
            Motion.mAcm_SetProperty(U.handle, (uint)PropertyID.CFG_AxHomeResetEnable, ref Reset, 8);

        }
        public void SetVelocity(Velocity velocity)
        {
            uint XVel = 0;
            uint YVel = 0;
            uint ZVel = 0;
            uint UVel = 0;

            switch (velocity)
            {
                case Velocity.Fast:
                    {
                        XVel = Settings.Default.XVelHigh;
                        YVel = Settings.Default.YVelHigh;
                        ZVel = Settings.Default.ZVelHigh;
                        UVel = Settings.Default.UVelHigh;
                    }
                    break;
                case Velocity.Slow:
                    {
                        XVel = Settings.Default.XVelLow;
                        YVel = Settings.Default.YVelLow;
                        ZVel = Settings.Default.ZVelLow;
                        UVel = Settings.Default.UVelLow;
                    }
                    break;
                case Velocity.Service:
                    {
                        XVel = Settings.Default.XVelService;
                        YVel = Settings.Default.YVelService;
                        ZVel = Settings.Default.ZVelService;
                        UVel = Settings.Default.UVelService;
                    }
                    break;
               
                default:
                    break;
            }
            Motion.mAcm_SetProperty(X.handle, (uint)PropertyID.CFG_AxMaxVel, ref XVel, 8);           
            Motion.mAcm_SetProperty(Y.handle, (uint)PropertyID.CFG_AxMaxVel, ref YVel, 8);            
            Motion.mAcm_SetProperty(Z.handle, (uint)PropertyID.CFG_AxMaxVel, ref ZVel, 8);
            Motion.mAcm_SetProperty(U.handle, (uint)PropertyID.CFG_AxMaxVel, ref UVel, 8);

            XVel /= 3;
            YVel /= 3;
            ZVel /= 3;
            UVel /= 3;

            Motion.mAcm_SetProperty(X.handle, (uint)PropertyID.CFG_AxMaxVel, ref XVel, 8);
            Motion.mAcm_SetProperty(Y.handle, (uint)PropertyID.CFG_AxMaxVel, ref YVel, 8);
            Motion.mAcm_SetProperty(Z.handle, (uint)PropertyID.CFG_AxMaxVel, ref ZVel, 8);
            Motion.mAcm_SetProperty(U.handle, (uint)PropertyID.CFG_AxMaxVel, ref UVel, 8);
        }

        private void SaveParams()
        {
            //-------------Сохранение параметров в файле онфигурации----
            Settings.Default.Save();
            //----------------------------------------------------------
        }

        public void GoWhile(AxisDirections direction, Velocity velocity)
        {
            if (velocity!=Velocity.Stop) Motion.mAcm_AxMoveVel(MoveRelParam(direction).Item1, MoveRelParam(direction).Item2);           
        }

        public void GoThere(Place place)
        {
            throw new System.NotImplementedException();
        }

        //private IntPtr m_GpHand = new IntPtr();
        //private IntPtr m_GpXYHand = new IntPtr();
        //public Machine(string configfilepath)
        //{
        //    Configs = new Features().ReadFeatures(configfilepath);
        //    DevicesConnection();
        //    SetConfigs();
        //    SetVelocity();
        //    dispatcher = Dispatcher.FromThread(Thread.CurrentThread);
        //    //------------------------------------------------------------

        //    Thread myThread = new Thread(new ThreadStart(GetPosition));
        //    myThread.Start();
        //    EnableDevEvents();
        //    DevEvents = new Thread(new ThreadStart(CheckDevEvents));
        //    DevEvents.Start();
        //    //------------------------------------------------------------
        //}
        //private List<(double, double)> lineXCoefficients;
        //private uint _AxMotionDone;
        //private bool _GpMotionDone;
        //private double _XCmd;
        //private double _YCmd;
        //private double _ZCmd;
        //private double _XActual;
        //private double _YActual;
        //private Vector3 _ActualCoors;
        
         



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

        //public int MoveInPos(Vector3 position, int recurcy)
        //{
        //    Task task;
        //    uint ElCount = 3;
        //    double posX = new double();
        //    double posY = new double();
        //    double accuracy = 0.001;
        //    double backlash = 0;
        //    ushort state = new ushort();
        //    double vel = 0.1;
        //    bool gotItX;
        //    bool gotItY;
        //    int signx = 0;
        //    int signy = 0;
        //    if (recurcy == 0)
        //    {
        //        position.X = Math.Round(position.X, 3);
        //        position.Y = Math.Round(position.Y, 3);


        //        Motion.mAcm_GpMoveLinearAbs(m_GpHand, new double[3] { position.X, position.Y, position.Z }, ref ElCount);
        //        task = Task.Run(() => { while (state != (uint)GroupState.STA_Gp_Ready) Motion.mAcm_GpGetState(m_GpHand, ref state); });
        //        task.Wait();
        //        task.Dispose();
        //        // while (!globalProperties.GpMotionDone) { };

        //        Motion.mAcm_SetProperty(m_Axishand[0], (uint)PropertyID.PAR_AxVelLow, ref vel, 8);
        //        Motion.mAcm_SetProperty(m_Axishand[1], (uint)PropertyID.PAR_AxVelLow, ref vel, 8);
        //        Motion.mAcm_SetProperty(m_Axishand[0], (uint)PropertyID.PAR_AxVelHigh, ref vel, 8);
        //        Motion.mAcm_SetProperty(m_Axishand[1], (uint)PropertyID.PAR_AxVelHigh, ref vel, 8);
        //    }



        //    Motion.mAcm_AxGetActualPosition(m_Axishand[0], ref posX);
        //    Motion.mAcm_AxGetActualPosition(m_Axishand[1], ref posY);


        //    if (Math.Abs(Math.Round(posX * xLine, 3) - position.X) <= accuracy) gotItX = true;
        //    else gotItX = false;
        //    if (Math.Abs(Math.Round(posY * yLine, 3) - position.Y) <= accuracy) gotItY = true;
        //    else gotItY = false;

        //    signx = Math.Sign(position.X - Math.Round(XActual, 3));
        //    signy = Math.Sign(position.Y - Math.Round(YActual, 3));
        //    //Motion.mAcm_CheckMotionEvent(m_DeviceHandle, null, null, 2, 0, 10);


        //    if (!gotItX)
        //    {
        //        Motion.mAcm_AxMoveRel(m_Axishand[0], signx * (Math.Abs(position.X - Math.Round(posX * xLine, 3)) + signx * backlash));
        //        task = Task.Run(() =>
        //        {
        //            while (!gotItX)
        //            {
        //                Motion.mAcm_AxGetActualPosition(m_Axishand[0], ref posX);
        //                if (Math.Abs(Math.Round(posX * xLine, 3) - position.X) <= accuracy)
        //                {
        //                    Motion.mAcm_AxStopEmg(m_Axishand[0]);
        //                    gotItX = true;
        //                }
        //                Motion.mAcm_AxGetState(m_Axishand[0], ref state);
        //                if (state == (uint)AxisState.STA_AX_READY) break;
        //            }
        //        });
        //        task.Wait();
        //        task.Dispose();
        //    }
        //    if (!gotItY)
        //    {
        //        Motion.mAcm_AxMoveRel(m_Axishand[1], signy * (Math.Abs(position.Y - Math.Round(posY * yLine, 3)) + signy * backlash));
        //        task = Task.Run(() =>
        //        {
        //            while (!gotItY)
        //            {
        //                Motion.mAcm_AxGetActualPosition(m_Axishand[1], ref posY);
        //                if (Math.Abs(Math.Round(posY * yLine, 3) - position.Y) <= accuracy)
        //                {
        //                    Motion.mAcm_AxStopEmg(m_Axishand[1]);
        //                    gotItY = true;
        //                }
        //                Motion.mAcm_AxGetState(m_Axishand[1], ref state);
        //                if (state == (uint)AxisState.STA_AX_READY) break;
        //            }
        //        });
        //        task.Wait();
        //        task.Dispose();
        //    }


        //    if (!((gotItX & gotItY) || recurcy > 15)) recurcy = MoveInPos(position, ++recurcy);
        //    else recurcy = recurcy > 20 ? 1000 : recurcy;
        //    return recurcy;
        //}

        //public async Task MoveInPos(Vector3 position)
        //{
        //    uint ElCount = 2;
        //    double posX = new double();
        //    double posY = new double();
        //    double accuracy = 0.001;
        //    double backlash = 0;
        //    ushort state = new ushort();
        //    ushort stateZ = new ushort();
        //    double vel = 0.1;
        //    bool gotItX;
        //    bool gotItY;
        //    int signx = 0;
        //    int signy = 0;
        //    await Task.Run(() =>
        //    {
        //        for (int recurcy = 0; recurcy < 20; recurcy++)
        //        {
        //            if (recurcy == 0)
        //            {
        //                position.X = Math.Round(position.X, 3);
        //                position.Y = Math.Round(position.Y, 3);
        //                Motion.mAcm_AxMoveAbs(m_Axishand[2], position.Z);
        //                Motion.mAcm_GpMoveLinearAbs(GpXYHand, new double[2] { position.X, position.Y }, ref ElCount);
        //                while ((state != (uint)GroupState.STA_Gp_Ready))
        //                {
        //                    Motion.mAcm_AxGetState(m_Axishand[2], ref stateZ);
        //                    Motion.mAcm_GpGetState(m_GpHand, ref state);
        //                }

        //                // while (!globalProperties.GpMotionDone) { };

        //                Motion.mAcm_SetProperty(m_Axishand[0], (uint)PropertyID.PAR_AxVelLow, ref vel, 8);
        //                Motion.mAcm_SetProperty(m_Axishand[1], (uint)PropertyID.PAR_AxVelLow, ref vel, 8);
        //                Motion.mAcm_SetProperty(m_Axishand[0], (uint)PropertyID.PAR_AxVelHigh, ref vel, 8);
        //                Motion.mAcm_SetProperty(m_Axishand[1], (uint)PropertyID.PAR_AxVelHigh, ref vel, 8);
        //            }
        //            Thread.Sleep(300);
        //            Motion.mAcm_AxGetActualPosition(m_Axishand[0], ref posX);
        //            Motion.mAcm_AxGetActualPosition(m_Axishand[1], ref posY);


        //            if (Math.Abs(Math.Round(posX * xLine, 3) - position.X) <= accuracy) gotItX = true;
        //            else gotItX = false;
        //            if (Math.Abs(Math.Round(posY * yLine, 3) - position.Y) <= accuracy) gotItY = true;
        //            else gotItY = false;

        //            signx = Math.Sign(position.X - Math.Round(XActual, 3));
        //            signy = Math.Sign(position.Y - Math.Round(YActual, 3));
        //            //Motion.mAcm_CheckMotionEvent(m_DeviceHandle, null, null, 2, 0, 10);


        //            if (!gotItX)
        //            {
        //                Motion.mAcm_AxMoveRel(m_Axishand[0], signx * (Math.Abs(position.X - Math.Round(posX * xLine, 3)) + signx * backlash));

        //                while (!gotItX)
        //                {
        //                    Motion.mAcm_AxGetActualPosition(m_Axishand[0], ref posX);
        //                    if (Math.Abs(Math.Round(posX * xLine, 3) - position.X) <= accuracy)
        //                    {
        //                        Motion.mAcm_AxStopEmg(m_Axishand[0]);
        //                        gotItX = true;
        //                    }
        //                    Motion.mAcm_AxGetState(m_Axishand[0], ref state);
        //                    if (state == (uint)AxisState.STA_AX_READY) break;
        //                }

        //            }
        //            if (!gotItY)
        //            {
        //                Motion.mAcm_AxMoveRel(m_Axishand[1], signy * (Math.Abs(position.Y - Math.Round(posY * yLine, 3)) + signy * backlash));

        //                while (!gotItY)
        //                {
        //                    Motion.mAcm_AxGetActualPosition(m_Axishand[1], ref posY);
        //                    if (Math.Abs(Math.Round(posY * yLine, 3) - position.Y) <= accuracy)
        //                    {
        //                        Motion.mAcm_AxStopEmg(m_Axishand[1]);
        //                        gotItY = true;
        //                    }
        //                    Motion.mAcm_AxGetState(m_Axishand[1], ref state);
        //                    if (state == (uint)AxisState.STA_AX_READY) break;
        //                }

        //            }

        //            if (gotItX & gotItY) break;
        //        }
        //    }
        //    );
        //}

        //public void HomeMoving()
        //{
        //    Fast = true;
        //    SetVelocity();
        //    Task t;

        //    Motion.mAcm_AxResetError(m_Axishand[0]);
        //    Motion.mAcm_AxResetError(m_Axishand[1]);
        //    Motion.mAcm_AxResetError(m_Axishand[2]);


        //    Motion.mAcm_AxHome(m_Axishand[0], (uint)HomeMode.MODE6_Lmt_Ref, 1);
        //    Motion.mAcm_AxHome(m_Axishand[1], (uint)HomeMode.MODE6_Lmt_Ref, 0);
        //    Motion.mAcm_AxHome(m_Axishand[2], (uint)HomeMode.MODE13_LmtSearchReFind, 0);

        //    t = Task.Run(() => IsAxisesFree(m_Axishand[0], m_Axishand[1]));
        //    t.Wait();

        //    Motion.mAcm_AxResetError(m_Axishand[2]);
        //}


        //}
        

        // #endregion


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
        Service,
        Stop
    }

    class Axis
    {
        public IntPtr handle;
        public bool LmtP { get; set; }
        public bool LmtN { get; set; }
        public double CmdPosition { get; set; }
        public double ActualPosition { get; set; }
        public int DIs { get; set; }
        public int DOs { get; set; }
        public bool GetDI(DI din) { return true; }
    }
}
