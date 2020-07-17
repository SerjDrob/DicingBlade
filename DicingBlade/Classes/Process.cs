using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Advantech.Motion;
using netDxf;
using Microsoft.VisualStudio.Workspace;

namespace DicingBlade.Classes
{   
    enum Diagram 
    {
        goWaferStartX,
        goWaferEndX,
        goNextDepthZ,
        cuttingX,
        goCameraPointXYZ,
        goOnWaferRightX,
        goOnWaferLeftX,
        goWaferCenterXY,
        goNextCutY,
        goNextCutXY,
        goTransferingHeightZ,
        goDockHeightZ,
        goNextDirection,
        goCameraPointLearningXYZ
    }
    enum Status 
    {
        StartLearning,
        Learning,
        Working,
        Correcting
    }
    /// <summary>
    /// Структура параметров процесса
    /// </summary>
    struct TempWafer2D
    {
        public bool Round;
        public double XIndex;
        public double XShift;
        public double YIndex;
        public double YShift;
        public double XAngle;
        public double YAngle;
        public Vector2 point1;
        public Vector2 point2;
        public double GetAngle() 
        {
            return Math.Atan2(point2.Y-point1.Y, point2.X - point1.X);
        }
    }
    class Process
    {

        private Wafer Wafer { get; set; }
        private Machine Machine { get; set; }
        private Blade Blade { get; set; }
        public Status ProcessStatus { get; set; }
        private double  BladeTransferGapZ { get; set; }
        private bool IsCutting { get; set; } = false;
        private bool InProcess { get; set; } = false;
       
        private bool pauseProcess_;
        public bool PauseProcess 
        {
            get { return pauseProcess_; }
            set 
            {
                pauseProcess_ = value;
                if (pauseToken != null) 
                {
                    if (value)
                    {
                        pauseToken.Pause();                                        
                    }
                    else pauseToken.Resume();
                }
            }
        }
        
        private PauseTokenSource pauseToken;

        private Diagram[] BaseProcess;
        
        private CancellationTokenSource cancellationToken;
        //private bool WaferInProcessed { }
        private bool SideDone { get; set; } = false;
        private int SideCounter { get; set; } = 0;
        private bool BladeInWafer 
        {
            get 
            {
                if (Machine.Z.ActualPosition > Machine.ZBladeTouch - Wafer.Thickness - BladeTransferGapZ) return true;
                else return false;
            }
        }
        private int CurrentLine { get; set; }
        private double RotationSpeed { get; set; } 
        private uint FeedSpeed { get; set; }        
        private bool Aligned { get; set; }

        //private Dictionary<int, double> AlignedAngles;
        private double OffsetAngle { get; set; }
        
        public Process(Machine machine, Wafer wafer, Blade blade, Diagram[] proc) // В конструкторе происходит загрузка технологических параметров
        {
            Machine = machine;
            Wafer = wafer;
            Blade = blade;
            BaseProcess = proc;
            Machine.OnAirWanished += Machine_OnAirWanished;
            Machine.OnCoolWaterWanished += Machine_OnCoolWaterWanished;
            Machine.OnSpinWaterWanished += Machine_OnSpinWaterWanished;
            Machine.OnVacuumWanished += Machine_OnVacuumWanished;            
        }
        public async Task PauseScenarioAsync() 
        {
            await ProcElementDispatcherAsync(Diagram.goCameraPointXYZ);
        }
        public async Task DoProcessAsync(Diagram[] diagrams)
        {
            if (!InProcess)
            {
                PauseProcess = false;
                pauseToken = new PauseTokenSource();
                cancellationToken = new CancellationTokenSource();
                InProcess = true;
                while (InProcess)
                {
                    foreach (var item in diagrams)
                    {
                        if (pauseToken.Equals(default)) await pauseToken.Token.WaitWhilePausedAsync();
                        await ProcElementDispatcherAsync(item);
                    }
                }
            }
        }
        private void NextLine() 
        {
            if (CurrentLine < Wafer.DirectionLinesCount) CurrentLine++;
            else if(CurrentLine == Wafer.DirectionLinesCount) 
            {
                SideDone = true;
            }
        }     
        private async Task MoveNextDirAsync() 
        {
            if (Wafer.NextDir())
            {
                await Machine.U.MoveAxisInPosAsync(Wafer.GetCurrentDiretionAngle);
            }
        }
        private async Task MovePrevDirAsync() 
        {
            if (Wafer.PrevDir())
            {
                await Machine.U.MoveAxisInPosAsync(Wafer.GetCurrentDiretionAngle);
            }
        }
        private void PrevLine() 
        {
            if (CurrentLine > 0) CurrentLine--;
        }
        private async Task ProcElementDispatcherAsync(Diagram element) 
        {
            #region MyRegion            
            // проверка перед каждым действием. асинхронные действия await()!!!
            // паузы, корректировки.
            //if(pauseToken.Equals(default)) await pauseToken.Token.WaitWhilePausedAsync();

            #endregion

            switch (element)
            {
                case Diagram.goWaferStartX:
                    if (BladeInWafer) break;
                    Machine.SetVelocity(Velocity.Service);
                    await Machine.X.MoveAxisInPosAsync(Wafer.GetCurrentLine(CurrentLine).start.X + Machine.BladeChuckCenter.X + Blade.XGap(Wafer.Thickness));
                    break;
                case Diagram.goWaferEndX:
                    if (BladeInWafer) break;
                    Machine.SetVelocity(Velocity.Service);
                    await Machine.X.MoveAxisInPosAsync(Wafer.GetCurrentLine(CurrentLine).end.X + Machine.BladeChuckCenter.X);
                    break;
                case Diagram.goNextDepthZ:
                    Machine.SetVelocity(Velocity.Service);
                    if (Wafer.CurrentCutIsDone(CurrentLine)) break;                    
                    await Machine.Z.MoveAxisInPosAsync(Machine.ZBladeTouch - Wafer.GetCurrentCutZ(CurrentLine));                   
                    break;
                case Diagram.cuttingX:
                    Machine.SwitchOnCoolantWater = true;
                    Machine.X.SetVelocity(FeedSpeed);
                    IsCutting = true;
                    await Machine.X.MoveAxisInPosAsync(Wafer.GetCurrentLine(CurrentLine).end.X);
                    IsCutting = false;                   
                    if (!Wafer.CurrentCutIncrement(CurrentLine))
                    {                       
                        NextLine();
                    }
                    break;
                case Diagram.goCameraPointXYZ:
                    Machine.SetVelocity(Velocity.Service);
                    await Machine.Z.MoveAxisInPosAsync(Machine.ZBladeTouch - Wafer.Thickness - BladeTransferGapZ);
                    await Machine.MoveInPosXYAsync(new netDxf.Vector2(
                        Machine.CameraChuckCenter.X,
                        Wafer.GetCurrentLine(CurrentLine).start.Y
                        ));
                    await Machine.Z.MoveAxisInPosAsync(Machine.CameraFocus);
                    break;
                case Diagram.goOnWaferRightX:
                    if (BladeInWafer) break;
                    Machine.SetVelocity(Velocity.Service);
                    await Machine.X.MoveAxisInPosAsync(Wafer.GetNearestCut(Machine.Y.ActualPosition - Machine.CameraChuckCenter.Y).EndPoint.X);
                    break;
                case Diagram.goOnWaferLeftX:
                    if (BladeInWafer) break;
                    Machine.SetVelocity(Velocity.Service);
                    await Machine.X.MoveAxisInPosAsync(Wafer.GetNearestCut(Machine.Y.ActualPosition - Machine.CameraChuckCenter.Y).StartPoint.X);
                    break;
                case Diagram.goWaferCenterXY:
                    if (BladeInWafer) break;
                    Machine.SetVelocity(Velocity.Service);
                    await Machine.MoveInPosXYAsync(Machine.CameraChuckCenter);
                    break;
                case Diagram.goNextCutY:
                    if (BladeInWafer) break;
                    Machine.SetVelocity(Velocity.Service);
                    await Machine.Y.MoveAxisInPosAsync(Machine.CtoBSystemCoors(Wafer.GetCurrentLine(CurrentLine).start).Y);
                    break;
                case Diagram.goNextCutXY:
                    if (BladeInWafer) break;
                    Machine.SetVelocity(Velocity.Service);
                    Vector2 vector = Machine.CtoBSystemCoors(Wafer.GetCurrentLine(CurrentLine).start);
                    await Machine.MoveInPosXYAsync(vector);
                    break;
                case Diagram.goTransferingHeightZ:
                    Machine.SetVelocity(Velocity.Service);
                    await Machine.Z.MoveAxisInPosAsync(Machine.ZBladeTouch - Wafer.Thickness - BladeTransferGapZ);
                    break;
                case Diagram.goDockHeightZ:
                    Machine.SetVelocity(Velocity.Service);
                    await Machine.Z.MoveAxisInPosAsync(0);
                    break;
                case Diagram.goNextDirection:
                    if (InProcess & SideDone/* | ProcessStatus == Status.Learning*/)
                    {
                        Machine.SetVelocity(Velocity.Service);
                        await MoveNextDirAsync();
                        SideDone = false;
                        SideCounter++;
                        if (SideCounter == Wafer.DirectionsCount)
                        {
                            InProcess = false;
                        }
                    }
                    break;
                case Diagram.goCameraPointLearningXYZ:
                    Machine.SetVelocity(Velocity.Service);
                    await Machine.Z.MoveAxisInPosAsync(Machine.ZBladeTouch - Wafer.Thickness - BladeTransferGapZ);
                    double y = Wafer.GetNearestCut(/*Machine.CameraChuckCenter.Y*/0).StartPoint.Y
                        + Machine.CameraChuckCenter.Y;
                    await Machine.MoveInPosXYAsync(new netDxf.Vector2(
                        Machine.CameraChuckCenter.X,
                        y
                        ));
                    await Machine.Z.MoveAxisInPosAsync(Machine.CameraFocus);
                    break;
                default:
                    break;
            }
        }

        public async Task StartPauseProc()
        {
            switch (ProcessStatus)
            {
                case Status.StartLearning:
                    await ProcElementDispatcherAsync(Diagram.goCameraPointLearningXYZ);
                    ProcessStatus = Status.Learning;
                    break;
                case Status.Learning:
                    Wafer.SetCurrentDirectionIndexShift = Machine.COSystemCurrentCoors.Y - Wafer.GetNearestCut(Machine.COSystemCurrentCoors.Y).StartPoint.Y;
                    Wafer.SetCurrentDirectionAngle = Machine.U.ActualPosition;
                    if (Wafer.NextDir())
                    {
                        await Machine.U.MoveAxisInPosAsync(Wafer.GetCurrentDiretionAngle);
                        await ProcElementDispatcherAsync(Diagram.goCameraPointLearningXYZ);
                    }
                    else
                    {
                        ProcessStatus = Status.Working;
                        await DoProcessAsync(BaseProcess);
                    }

                    break;
                case Status.Working:
                    PauseProcess = !PauseProcess;
                    if (PauseProcess) await PauseScenarioAsync();
                    break;
                case Status.Correcting:
                    break;
                default:
                    break;
            }
        }
        private void Machine_OnVacuumWanished(/*DIEventArgs eventArgs*/)
        {
            if (IsCutting) { }
            //throw new NotImplementedException();
        }

        private void Machine_OnSpinWaterWanished(/*DIEventArgs eventArgs*/)
        {
            if (IsCutting) { }
            //throw new NotImplementedException();
        }

        private void Machine_OnCoolWaterWanished(/*DIEventArgs eventArgs*/)
        {
            if (IsCutting) { }
            //throw new NotImplementedException();
        }

        private void Machine_OnAirWanished(/*DIEventArgs eventArgs*/)
        {
            if (IsCutting) { }
            //throw new NotImplementedException();
        }
    }
}
