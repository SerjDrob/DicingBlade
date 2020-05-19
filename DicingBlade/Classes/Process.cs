using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Advantech.Motion;
using netDxf;

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
        goNextDirection
    }
    /// <summary>
    /// Структура параметров процесса
    /// </summary>
    //struct ProcParams 
    //{
    //    int currentCut;
    //    double currentDepth;
    //    double currentAngle;
    //}
    class Process
    {
        public Wafer Wafer { get; set; }
        public Machine Machine { get; set; }
        public Blade Blade { get; set; }
        public double  BladeTransferGapZ { get; set; }
        private bool IsCutting { get; set; } = false;
        private bool InProcess { get; set; } = false;
        private bool SideDone { get; set; } = false;
        private bool BladeInWafer 
        {
            get 
            {
                if (Machine.Z.ActualPosition > Machine.ZBladeTouch - Wafer.Thickness - BladeTransferGapZ) return true;
                else return false;
            }
        }
        private int CurrenLine { get; set; }
        public double RotationSpeed { get; set; } 
        public uint FeedSpeed { get; set; }
        public Diagram[] BaseProcess { get; set; }
        private bool Aligned { get; set; }
        private IEnumerator<double> WaferAngles { get; set; }
        /// <summary>
        /// LinesAngle -> AlignedAngles
        /// </summary>
        private Dictionary<double, double> AlignedAngles;

        public Process(Machine machine, Wafer wafer, Blade blade) // В конструкторе происходит загрузка технологических параметров
        {
            Machine = machine;
            Wafer = wafer;
            Blade = blade;
            WaferAngles = Wafer.Grid.Lines.Keys.GetEnumerator();
            Machine.OnAirWanished += Machine_OnAirWanished;
            Machine.OnCoolWaterWanished += Machine_OnCoolWaterWanished;
            Machine.OnSpinWaterWanished += Machine_OnSpinWaterWanished;
            Machine.OnVacuumWanished += Machine_OnVacuumWanished;
            BaseProcess = new Diagram[] {
                Diagram.goNextCutXY,
                Diagram.goWaferStartX,
                Diagram.goNextDepthZ,
                Diagram.cuttingX
            };
        }

        public void DoProcess(Diagram[] diagrams) 
        {
            InProcess = true;
            foreach (var item in diagrams)
            {
                ProcElementDispatcher(item);
            }
        }
        private void NextLine() 
        {
            if (CurrenLine < Wafer.Grid.Lines[Wafer.CurrentAngle].Count) CurrenLine++;
            else if(CurrenLine == Wafer.Grid.Lines[Wafer.CurrentAngle].Count) 
            {
                SideDone = true;
            }
        }       
        private void PrevLine() 
        {
            if (CurrenLine > 0) CurrenLine--;
        }
        public async void ProcElementDispatcher(Diagram element) 
        {
            #region MyRegion
            // проверка перед каждым действием. асинхронные действия await()!!!
            // паузы, корректировки.
            #endregion

            switch (element)
            {
                case Diagram.goWaferStartX:
                    if (BladeInWafer) break;
                    Machine.SetVelocity(Velocity.Service);
                    await Machine.MoveAxisInPos(Ax.X, Wafer.Grid.Lines[Wafer.CurrentAngle][CurrenLine].StartPoint.X - Wafer.Grid.Origin.X + Machine.BladeChuckCenter.X + Blade.XGap(Wafer.Thickness));
                    break;
                case Diagram.goWaferEndX:
                    if (BladeInWafer) break;
                    Machine.SetVelocity(Velocity.Service);
                    await Machine.MoveAxisInPos(Ax.X, Wafer.Grid.Lines[Wafer.CurrentAngle][CurrenLine].EndPoint.X - Wafer.Grid.Origin.X + Machine.BladeChuckCenter.X);
                    break;
                case Diagram.goNextDepthZ:
                    Machine.SetVelocity(Velocity.Service);
                    if (Wafer.Grid.Lines[Wafer.CurrentAngle][CurrenLine].CurrentCut == Wafer.Grid.Lines[Wafer.CurrentAngle][CurrenLine].CutCount) break;
                    double z = Wafer.Grid.Lines[Wafer.CurrentAngle][CurrenLine].CurrentCut * Wafer.Thickness / Wafer.Grid.Lines[Wafer.CurrentAngle][CurrenLine].CutCount;
                    await Machine.MoveAxisInPos(Ax.Z, Machine.ZBladeTouch - z);                   
                    break;
                case Diagram.cuttingX:
                    Machine.X.SetVelocity(FeedSpeed);
                    IsCutting = true;
                    await Machine.MoveAxisInPos(Ax.X, Wafer.Grid.Lines[Wafer.CurrentAngle][CurrenLine].EndPoint.X);
                    IsCutting = false;
                    Wafer.Grid.Lines[Wafer.CurrentAngle][CurrenLine].CurrentCut++;
                    if (Wafer.Grid.Lines[Wafer.CurrentAngle][CurrenLine].CurrentCut / Wafer.Grid.Lines[Wafer.CurrentAngle][CurrenLine].CutCount == 1)
                    {
                        Wafer.Grid.Lines[Wafer.CurrentAngle][CurrenLine].Status = false;
                        NextLine();
                    }
                    break;
                case Diagram.goCameraPointXYZ:
                    Machine.SetVelocity(Velocity.Service);
                    await Machine.MoveAxisInPos(Ax.Z, Machine.ZBladeTouch - Wafer.Thickness - BladeTransferGapZ);
                    await Machine.MoveInPosXY(new netDxf.Vector2(
                        Machine.CameraChuckCenter.X,
                        Wafer.Grid.Lines[Wafer.CurrentAngle][CurrenLine-1].StartPoint.Y
                        ));
                    await Machine.MoveAxisInPos(Ax.Z, Machine.CameraFocus);
                    break;
                case Diagram.goOnWaferRightX:
                    if (BladeInWafer) break;
                    Machine.SetVelocity(Velocity.Service);
                    await Machine.MoveAxisInPos(Ax.X, Wafer.GetNearestCut(Machine.Y.ActualPosition - Machine.CameraChuckCenter.Y).EndPoint.X);
                    break;
                case Diagram.goOnWaferLeftX:
                    if (BladeInWafer) break;
                    Machine.SetVelocity(Velocity.Service);
                    await Machine.MoveAxisInPos(Ax.X, Wafer.GetNearestCut(Machine.Y.ActualPosition - Machine.CameraChuckCenter.Y).StartPoint.X);
                    break;
                case Diagram.goWaferCenterXY:
                    if (BladeInWafer) break;
                    Machine.SetVelocity(Velocity.Service);
                    await Machine.MoveInPosXY(Machine.CameraChuckCenter);
                    break;
                case Diagram.goNextCutY:
                    if (BladeInWafer) break;
                    Machine.SetVelocity(Velocity.Service);
                    await Machine.MoveAxisInPos(Ax.Y, Wafer.Grid.Lines[Wafer.CurrentAngle][CurrenLine].StartPoint.Y);
                    break;
                case Diagram.goNextCutXY:
                    if (BladeInWafer) break;
                    Machine.SetVelocity(Velocity.Service);
                    await Machine.MoveInPosXY(new Vector2(Wafer.Grid.Lines[Wafer.CurrentAngle][CurrenLine].StartPoint.X, Wafer.Grid.Lines[Wafer.CurrentAngle][CurrenLine].StartPoint.Y));
                    break;
                case Diagram.goTransferingHeightZ:
                    Machine.SetVelocity(Velocity.Service);
                    await Machine.MoveAxisInPos(Ax.Z, Machine.ZBladeTouch - Wafer.Thickness - BladeTransferGapZ);
                    break;
                case Diagram.goDockHeightZ:
                    Machine.SetVelocity(Velocity.Service);
                    await Machine.MoveAxisInPos(Ax.Z, 0);
                    break;
                case Diagram.goNextDirection:
                    if (InProcess&SideDone) 
                    {
                        if (WaferAngles.MoveNext())                         
                        {
                            Machine.SetVelocity(Velocity.Service);
                            //await Machine.MoveAxisInPos(Ax.U, AlignedAngles[Wafer.Grid.Lines.Keys.GetEnumerator().MoveNext]);
                        }
                       
                    }
                    break;
                default:
                    break;
            }
        }

        private void Machine_OnVacuumWanished(DIEventArgs eventArgs)
        {
            if (IsCutting) { }
            throw new NotImplementedException();
        }

        private void Machine_OnSpinWaterWanished(DIEventArgs eventArgs)
        {
            if (IsCutting) { }
            throw new NotImplementedException();
        }

        private void Machine_OnCoolWaterWanished(DIEventArgs eventArgs)
        {
            if (IsCutting) { }
            throw new NotImplementedException();
        }

        private void Machine_OnAirWanished(DIEventArgs eventArgs)
        {
            if (IsCutting) { }
            throw new NotImplementedException();
        }
    }
}
