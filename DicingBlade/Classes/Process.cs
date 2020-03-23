using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Advantech.Motion;

namespace DicingBlade.Classes
{   
    class Process
    {
        public Wafer wafer;
        public Machine machine;
        private double thickness_;
        private string material_;
        private int cutCount_;
        private int currentCut_;
        public double RotationSpeed { get; set; } 
        public double FeedSpeed { get; set; }
        bool isBusy; // флаг процесса

        public Process() // В конструкторе происходит загрузка технологических параметров
        {
        }

        #region Методы

        public int goHome()
        {
            if (isAlowed("goXN", "goYN", "goZN","goFN")&!isBusy)
            { 

                Motion.mAcm_AxMoveHome(machine.m_Axishand[2], (uint)HomeMode.MODE2_Lmt, (uint)HomeDir.NegDir);

                waitAxReady();

                Motion.mAcm_AxMoveHome(machine.m_Axishand[0], (uint)HomeMode.MODE2_Lmt, (uint)HomeDir.NegDir);
                Motion.mAcm_AxMoveHome(machine.m_Axishand[1], (uint)HomeMode.MODE2_Lmt, (uint)HomeDir.NegDir);
                Motion.mAcm_AxMoveHome(machine.m_Axishand[3], (uint)HomeMode.MODE2_Lmt, (uint)HomeDir.NegDir);

                waitAxReady();                

                Motion.mAcm_AxMoveRel(machine.m_Axishand[0], machine.gap);
                Motion.mAcm_AxMoveRel(machine.m_Axishand[1], machine.gap);
                Motion.mAcm_AxMoveRel(machine.m_Axishand[2], machine.gap);
                Motion.mAcm_AxMoveRel(machine.m_Axishand[3], machine.gap);

                waitAxReady();

                Motion.mAcm_AxSetCmdPosition(machine.m_Axishand[0], 0);
                Motion.mAcm_AxSetCmdPosition(machine.m_Axishand[1], 0);
                Motion.mAcm_AxSetCmdPosition(machine.m_Axishand[2], 0);
                Motion.mAcm_AxSetCmdPosition(machine.m_Axishand[3], 0);

                Motion.mAcm_AxSetActualPosition(machine.m_Axishand[1], 0);
            };
            return (int)error.SUCCESS;
        }

        public int waitAxReady()
        {
            Task t;
            ushort state = new ushort();
            t = Task.Run(() => {
                                while (state != (ushort)AxisState.STA_AX_READY)
                                {
                                    Motion.mAcm_AxGetState(machine.m_Axishand[0], ref state);
                                    if (state != (ushort)AxisState.STA_AX_READY) continue;
                                    Motion.mAcm_AxGetState(machine.m_Axishand[1], ref state);
                                    if (state != (ushort)AxisState.STA_AX_READY) continue;
                                    Motion.mAcm_AxGetState(machine.m_Axishand[2], ref state);
                                    if (state != (ushort)AxisState.STA_AX_READY) continue;
                                    Motion.mAcm_AxGetState(machine.m_Axishand[3], ref state);
                                }
                            });
                            t.Wait();
            return (int)error.SUCCESS;
        }

        public int goCamera()
        {
            if (isAlowed("sds","dsds","dsds"))
            {
               
            }
            return (int)error.SUCCESS;
        }

        public int jumpToCut(int direction) { return (int)error.SUCCESS; }

        public int toCut(int number) // Выполняет заданный номер реза
        {
            return (int)error.SUCCESS;
        }

        public int findBladeSensor()
        {
            return (int)error.SUCCESS;
        }

        public bool isAlowed(params string[] pars) // Отвечает разрешено ли запрашиваемое действие
        {
            bool allow = false;
            foreach(string parameter in pars)
            {
                #region Проверка концевиков                
                if (parameter == "goXP")
                {
                    if (machine.xP) allow = false;
                    else allow = true;
                }

                if (parameter == "goXN")
                {
                    if (machine.xN) allow = false;
                    else allow = true;
                }
                allow &= allow;
                #endregion
            }
            return allow;
        }
        #endregion
    }
}
