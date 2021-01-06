using System;
using System.Threading;
using System.Threading.Tasks;
using Advantech.Motion;
using PropertyChanged;

namespace DicingBlade.Classes
{
    [AddINotifyPropertyChangedInterface]
    internal class Axis
    {
        private double _actualPosition;
        private double _storeSpeed;

        public Axis(double lineCoefficient, IntPtr handle, int axisNum)
        {
            LineCoefficient = lineCoefficient;
            Handle = handle;
            AxisNum = axisNum;
        }

        public int AxisNum { get; }
        public IntPtr Handle { get; }
        public double LineCoefficient { get; }
        public bool LmtP { get; set; }
        public bool LmtN { get; set; }
        public double CmdPosition { get; set; }

        public double ActualPosition
        {
            get => LineCoefficient != 0 ? LineCoefficient * _actualPosition : CmdPosition;
            set => _actualPosition = value;
        }

        public int DIs { get; set; }
        public int DOs { get; set; }
        public int Ppu { get; set; }
        public bool MotionDone { get; set; }
        public bool HomeDone { get; set; }
        public bool Compared { get; set; }

        public bool GetDi(Di din)
        {
            return (DIs & (1 << (int)din)) != 0;
        }

        public bool GetDo(Do dout)
        {
            byte bit = 0;
            var result = Motion.mAcm_AxDoGetBit(Handle, (ushort)dout, ref bit);
            return bit != 0;
        }

        public bool SetDo(Do dout, byte val)
        {
            var result = Motion.mAcm_AxDoSetBit(Handle, (ushort)dout, val);
            Thread.Sleep(100);
            return result == (uint)ErrorCode.SUCCESS;
        }

        /// <summary>
        ///     Установка скорости по оси
        /// </summary>
        /// <param name="feed">unit per second</param>
        public void SetVelocity(double feed)
        {
            //uint result = 0;
            var velHigh = feed;
            var velLow = feed / 2;
            double axMaxVel = 30;
            double axMaxDec = 180;
            double axMaxAcc = 180;
            Motion.mAcm_SetProperty(Handle, (uint)PropertyID.CFG_AxMaxAcc, ref axMaxAcc, 8);
            Motion.mAcm_SetProperty(Handle, (uint)PropertyID.CFG_AxMaxDec, ref axMaxDec, 8);
            Motion.mAcm_SetProperty(Handle, (uint)PropertyID.CFG_AxMaxVel, ref axMaxVel, 8);
            Motion.mAcm_SetProperty(Handle, (uint)PropertyID.PAR_AxVelHigh, ref velHigh, 8);
            Motion.mAcm_SetProperty(Handle, (uint)PropertyID.PAR_AxVelLow, ref velLow, 8);
        }

        public double GetVelocity()
        {
            uint res = 0;
            double vel = 0;
            uint bufLength = 8;
            res = Motion.mAcm_GetProperty(Handle, (uint)PropertyID.PAR_AxVelHigh, ref vel, ref bufLength);
            return vel;
        }

        public void GoWhile(AxDir direction)
        {
            ushort dir;
            switch (direction)
            {
                case AxDir.Pos:
                    dir = (ushort)VelMoveDir.DIR_POSITIVE;
                    break;
                case AxDir.Neg:
                    dir = (ushort)VelMoveDir.DIR_NEGATIVE;
                    break;
                default:
                    dir = (ushort)VelMoveDir.NOT_SUPPORT;
                    break;
            }

            Motion.mAcm_AxResetError(Handle);
            MotionDone = false;
            var res = Motion.mAcm_AxMoveVel(Handle, dir);
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

                                Motion.mAcm_SetProperty(Handle, (uint)PropertyID.PAR_AxVelLow, ref vel, 8);
                                Motion.mAcm_SetProperty(Handle, (uint)PropertyID.PAR_AxVelHigh, ref vel, 8);
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
            if (rec == 0) _storeSpeed = GetVelocity();
            uint buf = 0;
            uint res = 0;
            var tolerance = 0.003;
            var pos = (int)(position * Ppu);
            ushort direction = 0;

            buf = (uint)SwLmtEnable.SLMT_DIS;
            res = Motion.mAcm_SetProperty(Handle, (uint)PropertyID.CFG_AxSwPelEnable, ref buf, 4);
            res = Motion.mAcm_SetProperty(Handle, (uint)PropertyID.CFG_AxSwMelEnable, ref buf, 4);
            if (LineCoefficient != 0)
            {
                var diff = position - ActualPosition;
                if (Math.Abs(diff) > tolerance)
                {
                    ResetErrors();
                    buf = (uint)SwLmtReact.SLMT_IMMED_STOP;
                    res = Motion.mAcm_SetProperty(Handle, (uint)PropertyID.CFG_AxSwPelReact, ref buf, 4);
                    res = Motion.mAcm_SetProperty(Handle, (uint)PropertyID.CFG_AxSwMelReact, ref buf, 4);
                    var tol = 0;
                    switch (Math.Sign(diff))
                    {
                        case 1:
                            res = Motion.mAcm_SetProperty(Handle, (uint)PropertyID.CFG_AxSwPelValue, ref pos, 4);
                            buf = (uint)SwLmtEnable.SLMT_EN;
                            res = Motion.mAcm_SetProperty(Handle, (uint)PropertyID.CFG_AxSwPelEnable, ref buf, 4);
                            buf = (uint)SwLmtToleranceEnable.TOLERANCE_ENABLE;
                            res = Motion.mAcm_SetProperty(Handle, (uint)PropertyID.CFG_AxSwPelToleranceEnable, ref buf,
                                4);
                            res = Motion.mAcm_SetProperty(Handle, (uint)PropertyID.CFG_AxSwPelToleranceValue, ref tol,
                                4);
                            direction = (ushort)VelMoveDir.DIR_POSITIVE;
                            break;
                        case -1:
                            res = Motion.mAcm_SetProperty(Handle, (uint)PropertyID.CFG_AxSwMelValue, ref pos, 4);
                            buf = (uint)SwLmtEnable.SLMT_EN;
                            res = Motion.mAcm_SetProperty(Handle, (uint)PropertyID.CFG_AxSwMelEnable, ref buf, 4);
                            buf = (uint)SwLmtToleranceEnable.TOLERANCE_ENABLE;
                            res = Motion.mAcm_SetProperty(Handle, (uint)PropertyID.CFG_AxSwMelToleranceEnable, ref buf,
                                4);
                            res = Motion.mAcm_SetProperty(Handle, (uint)PropertyID.CFG_AxSwMelToleranceValue, ref tol,
                                4);
                            direction = (ushort)VelMoveDir.DIR_NEGATIVE;
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
                                slmtp = status & (uint)Ax_Motion_IO.AX_MOTION_IO_SLMTP;
                                slmtn = status & (uint)Ax_Motion_IO.AX_MOTION_IO_SLMTN;
                            } while ((slmtp == 0) & (slmtn == 0));
                        }
                    );
                    SetVelocity(1);
                    Motion.mAcm_AxSetCmdPosition(Handle, ActualPosition);
                    await MoveAxisInPosAsync(position, ++rec);
                    rec--;
                    if (rec == 0)
                    {
                        SetVelocity(_storeSpeed);
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