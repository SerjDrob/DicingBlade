﻿using System;
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
        OUT4=4,
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
    public delegate void DIEventHandler(/*DIEventArgs eventArgs*/);




    [AddINotifyPropertyChangedInterface]
    class Machine
    {
        private bool testRegime;

        public IntPtr[] m_Axishand = new IntPtr[32];
        UInt32 IOStatus = new UInt32();
        UInt32 Result;
        byte BitData = new byte();
        private Velocity velocityRegime;
        public Velocity VelocityRegime 
        {
            get { return velocityRegime; }
            set 
            {
                velocityRegime = value;
                SetVelocity(value);
            }
        }
        double position = new double();
        private IntPtr m_DeviceHandle = IntPtr.Zero;
        private IntPtr XYhandle = IntPtr.Zero;
        private uint m_ulAxisCount = 0;
        private bool m_bInit = false;
        private VideoCaptureDevice LocalWebCam;
        private FilterInfoCollection LocalWebCamsCollection;       
        public BitmapImage Bi { get; set; }
        public bool MachineInit { get; set; } = false;
        public Vector2 BladeChuckCenter { get; set; }
        public double CameraBladeOffset { get; set; }
        public Vector2 CameraChuckCenter { get; set; }
        public double CameraFocus { get; set; }
        public bool PCI1240IsConnected;        
        public bool SpindleWater
        {
            get { return X.GetDI(DI.IN1); }
        }
        public bool CoolantWater
        {
            get { return X.GetDI(DI.IN2); }
        }
        public bool SwitchOnCoolantWater
        {
            get { return U.GetDO(DO.OUT4); }
            set { U.SetDo(DO.OUT4, (byte)(value ? 1 : 0)); }
        }
        public bool ChuckVacuum
        {
            get { return X.GetDI(DI.IN3); }
        }
        public bool SwitchOnChuckVacuum
        {
            get { return Z.GetDO(DO.OUT5); }
            set { Z.SetDo(DO.OUT5, (byte)(value ? 1 : 0)); }
        }
        public bool Air
        {
            get { return Z.GetDI(DI.IN1); }
        }
        public bool SwitchOnBlowing 
        {
            get { return Z.GetDO(DO.OUT6); }
            set { Z.SetDo(DO.OUT6,(byte)(value?1:0)); }
        }
        public bool BladeSensor { get; set; }
        public Axis X { get; set; }
        public Axis Y { get; set; }
        public Axis Z { get; set; }
        public Axis U { get; set; }
        /// <summary>
        /// Координата касания диском стола
        /// </summary>
        public double ZBladeTouch { get; set; }

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
                case AxisDirections.XP: return (X.Handle, 1);
                case AxisDirections.XN: return (X.Handle, 0);
                case AxisDirections.YP: return (Y.Handle, 1);
                case AxisDirections.YN: return (Y.Handle, 0);
                case AxisDirections.ZP: return (Z.Handle, 1);
                case AxisDirections.ZN: return (Z.Handle, 0);
                case AxisDirections.UP: return (U.Handle, 1);
                case AxisDirections.UN: return (U.Handle, 0);
                default: return (new IntPtr(), 1);
            }
        }
        public Machine(bool test) // В конструкторе происходит инициализация всех устройств, загрузка параметров.
        {
            testRegime = test;

            if (!testRegime)
            {
                StartCamera();
                DevicesConnection();
                SetConfigs();
                VelocityRegime = Velocity.Slow;               
            }
            axes = new Axis[] { X, Y, Z, U };
            OnAirWanished += EMGScenario;           
            Thread threadCurrentState = new Thread(new ThreadStart(MachineState));
            threadCurrentState.Start();
            VelocityRegime = Velocity.Fast;
        }
        #region Методы


        public void MachineState() // Производит опрос всех датчиков, линеек, координат
        {
            while (true)
            {
                foreach (var ax in axes)
                {
                    Result = Motion.mAcm_AxGetMotionIO(ax.Handle, ref IOStatus);
                    if (Result == (uint)ErrorCode.SUCCESS)
                    {
                        ax.LmtN = ((IOStatus & (uint)Ax_Motion_IO.AX_MOTION_IO_LMTN) > 0) ? true : false;
                        ax.LmtP = ((IOStatus & (uint)Ax_Motion_IO.AX_MOTION_IO_LMTP) > 0) ? true : false;
                    }
                    for (int channel = 0; channel < 4; channel++)
                    {
                        Result = Motion.mAcm_AxDiGetBit(ax.Handle, (ushort)channel, ref BitData);
                        if (Result == (uint)ErrorCode.SUCCESS)
                        {
                            ax.DIs &= ~BitData << channel;
                        }
                    }
                    Result = Motion.mAcm_AxGetCmdPosition(ax.Handle, ref position);
                    if (Result == (uint)ErrorCode.SUCCESS) ax.CmdPosition = position;

                    Result = Motion.mAcm_AxGetActualPosition(ax.Handle, ref position);
                    if (Result == (uint)ErrorCode.SUCCESS) ax.ActualPosition = position;

                    //if (!SpindleWater) OnSpinWaterWanished(/*new DIEventArgs()*/);
                    //if (!CoolantWater) OnCoolWaterWanished(/*new DIEventArgs()*/);
                    //if (!ChuckVacuum) OnVacuumWanished(/*new DIEventArgs()*/);
                    if (!Air) OnAirWanished?.Invoke(/*new DIEventArgs()*/);


                    //TrigVar trig = new TrigVar();
                    //DIEventArgs dI = new DIEventArgs();
                    ////trig.trigger(Air, (dI)=>OnAirWanished);
                }
                Thread.Sleep(100);
            }
        }
        #endregion
       
        public event DIEventHandler OnVacuumWanished;
        public event DIEventHandler OnCoolWaterWanished;
        public event DIEventHandler OnSpinWaterWanished;
        public event DIEventHandler OnAirWanished;
        private void EMGScenario(/*DIEventArgs eventArgs*/) { }
        public bool SetOnChuck() 
        {
            if(!ChuckVacuum) SwitchOnChuckVacuum = true;
            Thread.Sleep(100);
            if (!ChuckVacuum)
            {
                SayMessage(Messages.SetAndTurnOnVacuum);
                return false;
            }
            else return true;
        }
        public void SayMessage(Messages message) 
        {
            switch (message)
            {
                case Messages.SetAndTurnOnVacuum:
                    MessageBox.Show("Неустановленна пластина или неисправна вакуумная система");
                    break;
                default:
                    break;
            }
        }
        public void StartCamera()
        {
            LocalWebCamsCollection = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            try { LocalWebCam = new VideoCaptureDevice(LocalWebCamsCollection[1].MonikerString); }
            catch
            {
                MessageBox.Show("Включите питание видеокамеры !");
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
                        
            X = new Axis(6400, m_Axishand[0]);
            Y = new Axis(6400, m_Axishand[1]);
            Z = new Axis(0, m_Axishand[2]);
            U = new Axis(0, m_Axishand[3]);
            Result = Motion.mAcm_GpAddAxis(ref XYhandle, X.Handle);
            if (Result != (uint)ErrorCode.SUCCESS)
            {
                strTemp = "Open Axis Failed With Error Code: [0x" + Convert.ToString(Result, 16) + "]";
                MessageBox.Show(strTemp + " " + Result);
                return false;
            }
            Result = Motion.mAcm_GpAddAxis(ref XYhandle, Y.Handle);
            if (Result != (uint)ErrorCode.SUCCESS)
            {
                strTemp = "Open Axis Failed With Error Code: [0x" + Convert.ToString(Result, 16) + "]";
                MessageBox.Show(strTemp + " " + Result);
                return false;
            }
            if (m_bInit) MachineInit = true;
            return true;
        }
        public void SetConfigs()
        {   
            double XAcc = Settings.Default.XAcc;
            double XDec =  Settings.Default.XDec;
            int XPPU =  Settings.Default.XPPU;
            int XJerk =  Settings.Default.XJerk;
            X.PPU = XPPU;

            double YAcc =  Settings.Default.YAcc;
            double YDec =  Settings.Default.YDec;
            int YPPU =  Settings.Default.YPPU;
            int YJerk =  Settings.Default.YJerk;
            Y.PPU = YPPU;

            double ZAcc =  Settings.Default.ZAcc;
            double ZDec =  Settings.Default.ZDec;
            int ZPPU =  Settings.Default.ZPPU;
            int ZJerk =  Settings.Default.ZJerk;
            Z.PPU = ZPPU;
            
            double UAcc = Settings.Default.UAcc;
            double UDec = Settings.Default.UDec;
            int UPPU = Settings.Default.UPPU;
            int UJerk = Settings.Default.UJerk;
            U.PPU = UPPU;

            CameraChuckCenter = new Vector2(Settings.Default.XObjective, Settings.Default.YObjective);
            CameraFocus = Settings.Default.ZObjective;
            BladeChuckCenter = new Vector2(Settings.Default.XDisk,Settings.Default.YObjective+Settings.Default.DiskShift);
            CameraBladeOffset = Settings.Default.DiskShift;

            ////OffsetX =  Settings.Default.OffsetX;
            ////OffsetY =  Settings.Default.OffsetY;

            double AxMaxVel = 30;
            double AxMaxDec = 180;
            double AxMaxAcc = 180;

            Motion.mAcm_SetProperty(X.Handle, (uint)PropertyID.CFG_AxPPU, ref XPPU, 8);
            Motion.mAcm_SetProperty(X.Handle, (uint)PropertyID.PAR_AxJerk, ref XJerk, 8);
            Motion.mAcm_SetProperty(X.Handle, (uint)PropertyID.CFG_AxMaxAcc, ref AxMaxAcc, 8);
            Motion.mAcm_SetProperty(X.Handle, (uint)PropertyID.CFG_AxMaxDec, ref AxMaxDec, 8);
            Motion.mAcm_SetProperty(X.Handle, (uint)PropertyID.CFG_AxMaxVel, ref AxMaxVel, 8);
            Motion.mAcm_SetProperty(X.Handle, (uint)PropertyID.PAR_AxAcc, ref XAcc, 8);
            Motion.mAcm_SetProperty(X.Handle, (uint)PropertyID.PAR_AxDec, ref XDec, 8);

            Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxPPU, ref YPPU, 8);
            Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.PAR_AxJerk, ref YJerk, 8);
            Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxMaxAcc, ref AxMaxAcc, 8);
            Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxMaxDec, ref AxMaxDec, 8);
            Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxMaxVel, ref AxMaxVel, 8);
            Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.PAR_AxAcc, ref YAcc, 8);
            Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.PAR_AxDec, ref YDec, 8);

            Motion.mAcm_SetProperty(Z.Handle, (uint)PropertyID.CFG_AxPPU, ref ZPPU, 8);
            Motion.mAcm_SetProperty(Z.Handle, (uint)PropertyID.PAR_AxJerk, ref ZJerk, 8);
            Motion.mAcm_SetProperty(Z.Handle, (uint)PropertyID.CFG_AxMaxAcc, ref AxMaxAcc, 8);
            Motion.mAcm_SetProperty(Z.Handle, (uint)PropertyID.CFG_AxMaxDec, ref AxMaxDec, 8);
            Motion.mAcm_SetProperty(Z.Handle, (uint)PropertyID.CFG_AxMaxVel, ref AxMaxVel, 8);
            Motion.mAcm_SetProperty(Z.Handle, (uint)PropertyID.PAR_AxAcc, ref ZAcc, 8);
            Motion.mAcm_SetProperty(Z.Handle, (uint)PropertyID.PAR_AxDec, ref ZDec, 8);

            Motion.mAcm_SetProperty(U.Handle, (uint)PropertyID.CFG_AxPPU, ref UPPU, 8);
            Motion.mAcm_SetProperty(U.Handle, (uint)PropertyID.PAR_AxJerk, ref UJerk, 8);
            Motion.mAcm_SetProperty(U.Handle, (uint)PropertyID.CFG_AxMaxAcc, ref AxMaxAcc, 8);
            Motion.mAcm_SetProperty(U.Handle, (uint)PropertyID.CFG_AxMaxDec, ref AxMaxDec, 8);
            Motion.mAcm_SetProperty(U.Handle, (uint)PropertyID.CFG_AxMaxVel, ref AxMaxVel, 8);
            Motion.mAcm_SetProperty(U.Handle, (uint)PropertyID.PAR_AxAcc, ref UAcc, 8);
            Motion.mAcm_SetProperty(U.Handle, (uint)PropertyID.PAR_AxDec, ref UDec, 8);

            int PlsInMde = 2;
            int PlsInLogic = 2;
            int PlsInSrc = 2;
            int PlsOutMde = 1;
            //ushort state = new ushort();

            Motion.mAcm_SetProperty(X.Handle, (uint)PropertyID.CFG_AxPulseInMode, ref PlsInMde, 8);
            Motion.mAcm_SetProperty(X.Handle, (uint)PropertyID.CFG_AxPulseInLogic, ref PlsInLogic, 8);
            Motion.mAcm_SetProperty(X.Handle, (uint)PropertyID.CFG_AxPulseInSource, ref PlsInSrc, 8);
            Motion.mAcm_SetProperty(X.Handle, (uint)PropertyID.CFG_AxPulseOutMode, ref PlsOutMde, 8);

            Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxPulseInMode, ref PlsInMde, 8);
            Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxPulseInLogic, ref PlsInLogic, 8);
            Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxPulseInSource, ref PlsInSrc, 8);
            Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxPulseOutMode, ref PlsOutMde, 8);

            Motion.mAcm_SetProperty(Z.Handle, (uint)PropertyID.CFG_AxPulseInMode, ref PlsInMde, 8);
            Motion.mAcm_SetProperty(Z.Handle, (uint)PropertyID.CFG_AxPulseInLogic, ref PlsInLogic, 8);
            Motion.mAcm_SetProperty(Z.Handle, (uint)PropertyID.CFG_AxPulseInSource, ref PlsInSrc, 8);
            Motion.mAcm_SetProperty(Z.Handle, (uint)PropertyID.CFG_AxPulseOutMode, ref PlsOutMde, 8);

            Motion.mAcm_SetProperty(U.Handle, (uint)PropertyID.CFG_AxPulseInMode, ref PlsInMde, 8);
            Motion.mAcm_SetProperty(U.Handle, (uint)PropertyID.CFG_AxPulseInLogic, ref PlsInLogic, 8);
            Motion.mAcm_SetProperty(U.Handle, (uint)PropertyID.CFG_AxPulseInSource, ref PlsInSrc, 8);
            Motion.mAcm_SetProperty(U.Handle, (uint)PropertyID.CFG_AxPulseOutMode, ref PlsOutMde, 8);

            //int EZ = 0;

            //Motion.mAcm_SetProperty(m_Axishand[0], (uint)PropertyID.CFG_AxEzLogic, ref EZ, 8);
            //Motion.mAcm_SetProperty(m_Axishand[1], (uint)PropertyID.CFG_AxEzLogic, ref EZ, 8);


            int Reset = 1;
            Motion.mAcm_SetProperty(X.Handle, (uint)PropertyID.CFG_AxHomeResetEnable, ref Reset, 8);
            Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxHomeResetEnable, ref Reset, 8);
            Motion.mAcm_SetProperty(Z.Handle, (uint)PropertyID.CFG_AxHomeResetEnable, ref Reset, 8);
            Motion.mAcm_SetProperty(U.Handle, (uint)PropertyID.CFG_AxHomeResetEnable, ref Reset, 8);

        }
        public void SetVelocity(Velocity velocity)
        {
            double XVel = 0;
            double YVel = 0;
            double ZVel = 0;
            double UVel = 0;

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
            double AxMaxVel = 30;
            double AxMaxDec = 180;
            double AxMaxAcc = 180;            
            Motion.mAcm_SetProperty(X.Handle, (uint)PropertyID.CFG_AxMaxAcc, ref AxMaxAcc, 8);
            Motion.mAcm_SetProperty(X.Handle, (uint)PropertyID.CFG_AxMaxDec, ref AxMaxDec, 8);
            Motion.mAcm_SetProperty(X.Handle, (uint)PropertyID.CFG_AxMaxVel, ref AxMaxVel, 8);

            Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxMaxAcc, ref AxMaxAcc, 8);
            Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxMaxDec, ref AxMaxDec, 8);
            Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.CFG_AxMaxVel, ref AxMaxVel, 8);

            Motion.mAcm_SetProperty(Z.Handle, (uint)PropertyID.CFG_AxMaxAcc, ref AxMaxAcc, 8);
            Motion.mAcm_SetProperty(Z.Handle, (uint)PropertyID.CFG_AxMaxDec, ref AxMaxDec, 8);
            Motion.mAcm_SetProperty(Z.Handle, (uint)PropertyID.CFG_AxMaxVel, ref AxMaxVel, 8);

            Motion.mAcm_SetProperty(U.Handle, (uint)PropertyID.CFG_AxMaxAcc, ref AxMaxAcc, 8);
            Motion.mAcm_SetProperty(U.Handle, (uint)PropertyID.CFG_AxMaxDec, ref AxMaxDec, 8);
            Motion.mAcm_SetProperty(U.Handle, (uint)PropertyID.CFG_AxMaxVel, ref AxMaxVel, 8);

            Motion.mAcm_SetProperty(X.Handle, (uint)PropertyID.PAR_AxVelHigh, ref XVel, 8);            
            Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.PAR_AxVelHigh, ref YVel, 8);            
            Motion.mAcm_SetProperty(Z.Handle, (uint)PropertyID.PAR_AxVelHigh, ref ZVel, 8);
            Motion.mAcm_SetProperty(U.Handle, (uint)PropertyID.PAR_AxVelHigh, ref UVel, 8);

            XVel /= 3;
            YVel /= 3;
            ZVel /= 3;
            UVel /= 3;

            Motion.mAcm_SetProperty(X.Handle, (uint)PropertyID.PAR_AxVelLow, ref XVel, 8);
            Motion.mAcm_SetProperty(Y.Handle, (uint)PropertyID.PAR_AxVelLow, ref YVel, 8);
            Motion.mAcm_SetProperty(Z.Handle, (uint)PropertyID.PAR_AxVelLow, ref ZVel, 8);
            Motion.mAcm_SetProperty(U.Handle, (uint)PropertyID.PAR_AxVelLow, ref UVel, 8);
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
            Motion.mAcm_AxMoveVel(MoveRelParam(direction).Item1, MoveRelParam(direction).Item2);     
        }
        public async Task GoThereAsync(Place place)
        {
            switch (place)
            {
                case Place.Home:
                    break;
                case Place.Loading:
                    break;
                case Place.CameraChuckCenter:
                    break;
                case Place.BladeChuckCenter:
                    break;
                default:
                    break;
            }
        }       
        public async Task MoveInPosXYAsync(Vector2 position)
        {
            uint ElCount = 2;           
            double accuracy = 0.001;
            double backlash = 0;
            ushort state = new ushort();
            ushort stateZ = new ushort();
            double vel = 0.1;
            bool gotItX;
            bool gotItY;
            int signx = 0;
            int signy = 0;
            await Task.Run(() =>
            {
                for (int recurcy = 0; recurcy < 20; recurcy++)
                {
                    if (recurcy == 0)
                    {
                        position.X = Math.Round(position.X, 3);
                        position.Y = Math.Round(position.Y, 3);                        
                        Motion.mAcm_GpMoveLinearAbs(XYhandle, new double[2] { position.X, position.Y }, ref ElCount);
                        while ((state != (uint)GroupState.STA_Gp_Ready))
                        {
                            Motion.mAcm_AxGetState(m_Axishand[2], ref stateZ);
                            Motion.mAcm_GpGetState(XYhandle, ref state);
                        }
                        Motion.mAcm_SetProperty(m_Axishand[0], (uint)PropertyID.PAR_AxVelLow, ref vel, 8);
                        Motion.mAcm_SetProperty(m_Axishand[1], (uint)PropertyID.PAR_AxVelLow, ref vel, 8);
                        Motion.mAcm_SetProperty(m_Axishand[0], (uint)PropertyID.PAR_AxVelHigh, ref vel, 8);
                        Motion.mAcm_SetProperty(m_Axishand[1], (uint)PropertyID.PAR_AxVelHigh, ref vel, 8);
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
                        Motion.mAcm_AxMoveRel(m_Axishand[0], signx * (Math.Abs(position.X - Math.Round(X.ActualPosition, 3)) + signx * backlash));

                        while (!gotItX)
                        {
                            if (Math.Abs(Math.Round(X.ActualPosition, 3) - position.X) <= accuracy)
                            {
                                Motion.mAcm_AxStopEmg(m_Axishand[0]);
                                gotItX = true;
                            }
                            Motion.mAcm_AxGetState(m_Axishand[0], ref state);
                            if (state == (uint)AxisState.STA_AX_READY) break;
                        }

                    }
                    if (!gotItY)
                    {
                        Motion.mAcm_AxMoveRel(m_Axishand[1], signy * (Math.Abs(position.Y - Math.Round(Y.ActualPosition, 3)) + signy * backlash));

                        while (!gotItY)
                        {
                            if (Math.Abs(Math.Round(Y.ActualPosition, 3) - position.Y) <= accuracy)
                            {
                                Motion.mAcm_AxStopEmg(m_Axishand[1]);
                                gotItY = true;
                            }
                            Motion.mAcm_AxGetState(m_Axishand[1], ref state);
                            if (state == (uint)AxisState.STA_AX_READY) break;
                        }

                    }

                    if (gotItX & gotItY) break;
                }
            }
            );
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

    public enum Messages
    {
        [Description ("Установити пластину и включите вакуум")]
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
    class Axis
    {
        public Axis(int lineCoefficient, IntPtr handle)
        {
            this.LineCoefficient = lineCoefficient;
            this.Handle = handle;
        }
        public IntPtr Handle { get; }
        public int LineCoefficient { get; }
        private double actualPosition;
        public bool LmtP { get; set; }
        public bool LmtN { get; set; }
        public double CmdPosition { get; set; }
        public double ActualPosition
        {
            get 
            { 
                return LineCoefficient != 0 ? LineCoefficient * actualPosition : CmdPosition;
            }
            set { actualPosition = value; }
        }
        public int DIs { get; set; }
        public int DOs { get; set; }
        public int PPU { get; set; }
        public bool GetDI(DI din)
        {
            return (DIs & 1 << (int)din) != 0 ? true : false;
        }
        public bool GetDO(DO dout)
        {
            byte bit = 0;
            Motion.mAcm_AxDoGetBit(Handle, (ushort)dout, ref bit);
            return bit != 0 ? true : false;
        }
        public bool SetDo(DO dout, byte val) 
        {
            return (Motion.mAcm_AxDoSetBit(Handle, (ushort)dout, val) == (uint)ErrorCode.SUCCESS) ? true : false; 
        }
        /// <summary>
        /// Установка скорости по оси
        /// </summary>
        /// <param name="feed">unit per second</param>
        public void SetVelocity(double feed)
        {
            uint Vel = (uint)Convert.ToInt32(feed * PPU);
            Motion.mAcm_SetProperty(Handle, (uint)PropertyID.PAR_AxVelHigh, ref Vel, 8);
            Vel /= 3;
            Motion.mAcm_SetProperty(Handle, (uint)PropertyID.PAR_AxVelLow, ref Vel, 8);
        }
        public async Task MoveAxisInPosAsync(double position)
        {
            double accuracy = 0.001;
            double backlash = 0;
            ushort state = new ushort();
            double vel = 0.1;
            bool gotIt;
            int sign = 0;
           
            if (LineCoefficient != 0)
            {
                await Task.Run(() =>
                {
                    for (int recurcy = 0; recurcy < 20; recurcy++)
                    {
                        if (recurcy == 0)
                        {
                            position = Math.Round(position, 3);
                            Motion.mAcm_AxMoveAbs(Handle, position);
                            while ((state != (uint)AxisState.STA_AX_READY))
                            {
                                Motion.mAcm_AxGetState(Handle, ref state);
                            }
                            Motion.mAcm_SetProperty(Handle, (uint)PropertyID.PAR_AxVelLow, ref vel, 8);
                            Motion.mAcm_SetProperty(Handle, (uint)PropertyID.PAR_AxVelHigh, ref vel, 8);
                        }
                        Thread.Sleep(300);

                        if (Math.Abs(Math.Round(ActualPosition, 3) - position) <= accuracy) gotIt = true;
                        else gotIt = false;
                        sign = Math.Sign(position - Math.Round(ActualPosition, 3));
                        if (!gotIt)
                        {
                            Motion.mAcm_AxMoveRel(Handle, sign * (Math.Abs(position - Math.Round(ActualPosition, 3)) + sign * backlash));
                            while (!gotIt)
                            {
                                if (Math.Abs(Math.Round(ActualPosition, 3) - position) <= accuracy)
                                {
                                    Motion.mAcm_AxStopEmg(Handle);
                                    gotIt = true;
                                }
                                Motion.mAcm_AxGetState(Handle, ref state);
                                if (state == (uint)AxisState.STA_AX_READY) break;
                            }
                        }
                        if (gotIt) break;
                    }
                }
                );
            }
            else Motion.mAcm_AxMoveAbs(Handle, position);
        }
    }
}
