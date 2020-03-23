using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using netDxf;
using Advantech.Motion;
namespace DicingBlade.Classes
{
    
    class Machine
    {
        public IntPtr[] m_Axishand = new IntPtr[32];
        UInt32 IOStatus = new UInt32();
        UInt32 Result;
        byte BitData;
        double position = new double();

        public Vector3 bladeChuckCenter;
        public Vector3 cameraBladeOffset;
        public bool PCI1240IsConnected;
        public double xLength;
        public double yLength;
        public double zLength;
        public double fAngle;
        public double gap; // зазор до концевика
        public bool spindleWater { get; set; }
        public bool coolantWater { get; set; }
        public bool chuckVacuum { get; set; }
        public bool air { get; set; }       
        public bool xP { get; set; } // Концевой выключатель положительного направления
        public bool xN { get; set; } // Концевой выключатель отрицательного направления
        public bool yP { get; set; }
        public bool yN { get; set; }
        public bool zP { get; set; }
        public bool zN { get; set; }
        public bool fP { get; set; }
        public bool fN { get; set; }

        public bool xDirP { get; set; } // Разрешение на положительное направление
        public bool xDirN { get; set; } // Разрешение на отрицательное направление
        public bool yDirP { get; set; }
        public bool yDirN { get; set; }
        public bool zDirP { get; set; }
        public bool zDirN { get; set; }
        public bool fDirP { get; set; }
        public bool fDirN { get; set; }

        public bool bladeSensor { get; set; }
        public double xPosition { get; set; }
        public double yPosition { get; set; }
        public double yPositionEnc { get; set; }
        public double zPosition { get; set; }
        public double fPosition { get; set; } // Абсолютное положение по углу поворота относительно концевика
        
        public Machine() // В конструкторе происходит инициализация всех устройств, загрузка параметров.
        {
            Thread threadCurrentState = new Thread(new ThreadStart(machineState));
            threadCurrentState.Start();
        }
        #region Методы
        public void machineState() // Производит опрос всех датчиков, линеек, координат
        {
            #region Опрос датчиков и концевиков
            Result = Motion.mAcm_AxGetMotionIO(m_Axishand[0],ref IOStatus);
            if(Result == (uint)ErrorCode.SUCCESS)
            {
                xN = ((IOStatus & (uint)Ax_Motion_IO.AX_MOTION_IO_LMTN) > 0) ? true : false;
                xP = ((IOStatus & (uint)Ax_Motion_IO.AX_MOTION_IO_LMTP) > 0) ? true : false;
            }

            Result = Motion.mAcm_AxGetMotionIO(m_Axishand[1], ref IOStatus);
            if (Result == (uint)ErrorCode.SUCCESS)
            {
                yN = ((IOStatus & (uint)Ax_Motion_IO.AX_MOTION_IO_LMTN) > 0) ? true : false;
                yP = ((IOStatus & (uint)Ax_Motion_IO.AX_MOTION_IO_LMTP) > 0) ? true : false;
            }

            Result = Motion.mAcm_AxGetMotionIO(m_Axishand[2], ref IOStatus);
            if (Result == (uint)ErrorCode.SUCCESS)
            {
                zN = ((IOStatus & (uint)Ax_Motion_IO.AX_MOTION_IO_LMTN) > 0) ? true : false;
                zP = ((IOStatus & (uint)Ax_Motion_IO.AX_MOTION_IO_LMTP) > 0) ? true : false;
            }

            Result = Motion.mAcm_AxGetMotionIO(m_Axishand[3], ref IOStatus);
            if (Result == (uint)ErrorCode.SUCCESS)
            {
                fN = ((IOStatus & (uint)Ax_Motion_IO.AX_MOTION_IO_LMTN) > 0) ? true : false;
                fP = ((IOStatus & (uint)Ax_Motion_IO.AX_MOTION_IO_LMTP) > 0) ? true : false;
            }

            Result = Motion.mAcm_AxDiGetBit(m_Axishand[0], 1, ref BitData);
            if (Result == (uint)ErrorCode.SUCCESS)
            {
                spindleWater = (BitData == 0) ? true : false;
            }

            Result = Motion.mAcm_AxDiGetBit(m_Axishand[0], 2, ref BitData);
            if (Result == (uint)ErrorCode.SUCCESS)
            {
                chuckVacuum = (BitData == 0) ? true : false;
            }

            Result = Motion.mAcm_AxDiGetBit(m_Axishand[1], 2, ref BitData);
            if (Result == (uint)ErrorCode.SUCCESS)
            {
                coolantWater = (BitData == 0) ? true : false;
            }

            Result = Motion.mAcm_AxDiGetBit(m_Axishand[3], 2, ref BitData);
            if (Result == (uint)ErrorCode.SUCCESS)
            {
                air = (BitData == 0) ? true : false;
            }
            #endregion
            #region Запрос текущих координат
            Result = Motion.mAcm_AxGetCmdPosition(m_Axishand[0], ref position);
            if (Result == (uint)ErrorCode.SUCCESS) xPosition = position;

            Result = Motion.mAcm_AxGetCmdPosition(m_Axishand[1], ref position);
            if (Result == (uint)ErrorCode.SUCCESS) yPosition = position;

            Result = Motion.mAcm_AxGetActualPosition(m_Axishand[1], ref position);
            if (Result == (uint)ErrorCode.SUCCESS) yPositionEnc = position;

            Result = Motion.mAcm_AxGetCmdPosition(m_Axishand[2], ref position);
            if (Result == (uint)ErrorCode.SUCCESS) zPosition = position;

            Result = Motion.mAcm_AxGetCmdPosition(m_Axishand[3], ref position);
            if (Result == (uint)ErrorCode.SUCCESS) fPosition = position;

            #endregion
            #region Проверка нахождения в разрешённой зоне

            xDirP = (xPosition < xLength - gap) ? true : false;
            xDirN = (xPosition > gap) ? true : false;

            yDirP = (xPosition < xLength - gap) ? true : false;
            yDirN = (xPosition > gap) ? true : false;

            zDirP = (xPosition < xLength - gap) ? true : false;
            zDirN = (xPosition > gap) ? true : false;

            fDirP = (xPosition < xLength - gap) ? true : false;
            fDirN = (xPosition > gap) ? true : false;

            #endregion
            Thread.Sleep(100);
        }
        #endregion
    }
}
