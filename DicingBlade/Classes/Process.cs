
//#nullable enable

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
using PropertyChanged;
using System.ComponentModel;
using netDxf.Entities;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Forms;
using MessageBox = System.Windows.Forms.MessageBox;
using System.Reflection.PortableExecutable;
using System.Windows.Media;

namespace DicingBlade.Classes
{
    public delegate void GetRotation(double angle, double time);
    public delegate void ChangeScreens(bool regime);
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
        Correcting,
        Done
    }
    /// <summary>
    /// Структура параметров процесса
    /// </summary>
    struct TempWafer2D
    {        
        //public bool Round;
        //public double XIndex;
        //public double XShift;
        //public double YIndex;
        //public double YShift;
        //public double XAngle;
        //public double YAngle;
        public bool firstPointSet;
        public Vector2 point1;
        public Vector2 point2;
        public double GetAngle()
        {
            return Math.Atan2(point2.Y - point1.Y, point2.X - point1.X);
        }
    }

    //public delegate void SetPause(bool pause);
    [AddINotifyPropertyChangedInterface]
    class Process
    {
        public string ProcessMessage { get; set; } = "";
        public bool UserConfirmation { get; set; } = false;
        private readonly Wafer Wafer;
        private readonly Machine Machine;
        private readonly Blade Blade;
        public ChangeScreens ChangeScreensEvent;
        public Visibility TeachVScaleMarkersVisibility { get; set; } = Visibility.Hidden;
        public Visibility CutWidthMarkerVisibility { get; set; } = Visibility.Hidden;
        public Status ProcessStatus { get; set; }
        public TracePath TracingLine { get; set; }
        public WaferView TracesView { get; set; }
        public ObservableCollection<TracePath> Traces { get; set; }
        public double CutWidth { get; set; } = 0.05;
        public double CutOffset { get; set; } = 0;
        private List<TracePath> traces;
        private double BladeTransferGapZ /*{ get; set; }*/ = 1;
        private bool IsCutting { get; set; } = false;
        private bool InProcess { get; set; } = false;
        public bool CutsRotate { get; set; } = true;
        //private bool procToken = true;
        private bool _pauseProcess;
        public bool PauseProcess 
        {
            get { return _pauseProcess; }
            set 
            {
                _pauseProcess = value;
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
        private bool _cancelProcess;
        public bool CancelProcess
        {            
            set
            {
                _cancelProcess = value;
                if (cancellationToken != null)
                {
                    if (value)
                    {
                        cancellationToken.Cancel();
                    }                    
                }
            }
        }

        private PauseTokenSource pauseToken;

        private Diagram[] BaseProcess;
        
        private CancellationTokenSource cancellationToken;
        public bool SideDone { get; private set; } = false;
        public int SideCounter { get; private set; } = 0;
        private bool BladeInWafer 
        {
            get 
            {
                if (Machine.Z.ActualPosition > Machine.ZBladeTouch - Wafer.Thickness - BladeTransferGapZ) return true;
                else return false;
            }
        }
        public int CurrentLine { get; private set; }
        //private double RotationSpeed { get; set; } 
        private double FeedSpeed { get; set; }        
        //private bool Aligned { get; set; }
        //private double OffsetAngle { get; set; }
        public GetRotation GetRotationEvent;
        public bool Rotation { get; set; } = false;
        public Process(Machine machine, Wafer wafer, Blade blade,ITechnology technology, Diagram[] proc) // В конструкторе происходит загрузка технологических параметров
        {
            Machine = machine;
            Wafer = wafer;
            Blade = blade;
            Traces = new ObservableCollection<TracePath>();
            TracesView = new WaferView();
            BaseProcess = proc;
            CancelProcess = false;
            FeedSpeed = PropContainer.Technology.FeedSpeed;            
            Machine.OnAirWanished += Machine_OnAirWanished;
            Machine.OnCoolWaterWanished += Machine_OnCoolWaterWanished;
            Machine.OnSpinWaterWanished += Machine_OnSpinWaterWanished;
            Machine.OnVacuumWanished += Machine_OnVacuumWanished;            
        }
        public async Task PauseScenarioAsync() 
        {
            await Machine.X.WaitUntilStopAsync();
            //Machine.EmgStop();
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
                        if (cancellationToken.IsCancellationRequested) 
                        {
                            InProcess = false;
                            cancellationToken.Dispose();
                            CancelProcess = false;
                            break;
                        }
                        await pauseToken.Token.WaitWhilePausedAsync();
                        await ProcElementDispatcherAsync(item);
                    }
                }
                ProcessStatus = Status.Done;
                Wafer.ResetWafer();
                await Machine.GoThereAsync(Place.Loading);
            }
        }
        private void NextLine() 
        {
            if (CurrentLine < Wafer.DirectionLinesCount - 1) CurrentLine++;
            else if(CurrentLine == Wafer.DirectionLinesCount - 1) 
            {
                SideDone = true;
            }
        }

        private async Task MoveNextDirAsync(bool next = true)
        {
            if (next ? !Wafer.NextDir(true) : true)
            {
                
                double angle = 0;
                double time = 0;
                var deltaAngle = Wafer.GetCurrentDiretionAngle - Wafer.GetPrevDiretionAngle;
                if (Wafer.GetCurrentDiretionActualAngle == Wafer.GetCurrentDiretionAngle)
                {
                    angle = Wafer.GetPrevDiretionActualAngle - Wafer.GetPrevDiretionAngle + Wafer.GetCurrentDiretionAngle;
                    time = Math.Abs(angle - Machine.U.ActualPosition) / Machine.U.GetVelocity();                                                            
                }
                else
                {
                    angle = Wafer.GetCurrentDiretionActualAngle;
                    time = Math.Abs(Wafer.GetCurrentDiretionActualAngle - Machine.U.ActualPosition) / Machine.U.GetVelocity();                    
                }
                Rotation = true;
                GetRotationEvent(deltaAngle, time);
                await Machine.U.MoveAxisInPosAsync(angle);                
                Rotation = false;
            }

        }
        private async Task MovePrevDirAsync() 
        {
            if (Wafer.PrevDir())
            {
                await Machine.U.MoveAxisInPosAsync(Wafer.GetCurrentDiretionActualAngle);
            }
        }
        private void PrevLine() 
        {
            if (CurrentLine > 0) CurrentLine--;
        }
        public async Task ToTeachVideoScale()
        {
            TeachVScaleMarkersVisibility = Visibility.Hidden;
            ProcessMessage = "Подведите ориентир к одному из визиров и нажмите *";
            await WaitForConfirmationAsync();
            var y = Machine.Y.ActualPosition;
            ProcessMessage = "Подведите ориентир ко второму визиру и нажмите *";
            await WaitForConfirmationAsync();
            Machine.CameraScale = Machine.TeachMarkersRatio / Math.Abs(y - Machine.Y.ActualPosition);
            ProcessMessage = "";
            TeachVScaleMarkersVisibility = Visibility.Hidden;
        }
        public async Task ToTeachChipSizeAsync()
        {
            if (System.Windows.MessageBox.Show("Обучить размер кристалла?", "Обучение", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
            {
                ProcessMessage = "Подведите ориентир к перекрестию и нажмите *";
                await WaitForConfirmationAsync();
                var y = Machine.Y.ActualPosition;
                ProcessMessage = "Подведите следующий ориентир к перекрестию и нажмите *";
                await WaitForConfirmationAsync();
                var size = Math.Round(Math.Abs(y - Machine.Y.ActualPosition),3);
                ProcessMessage = "";
                if (System.Windows.MessageBox.Show($"\rНовый размер кристалла {size} мм.\n Запомнить?" , "Обучение", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                {
                    PropContainer.WaferTemp.IndexH=size;//currentIndex?                    
                }    
            }
        }
        private async Task WaitForConfirmationAsync()
        {
            UserConfirmation = false;
            await Task.Run(() =>
            {
                while (!UserConfirmation)
                {
                    Thread.Sleep(1);
                }
            });
        }
        private async Task ProcElementDispatcherAsync(Diagram element) 
        {
            #region MyRegion            
            // проверка перед каждым действием. асинхронные действия await()!!!
            // паузы, корректировки.
            //if(pauseToken.Equals(default)) await pauseToken.Token.WaitWhilePausedAsync();

            #endregion
            Vector2 target;
            switch (element)
            {
                case Diagram.goWaferStartX:
                    if (BladeInWafer) break;
                    Machine.SetVelocity(Velocity.Service);
                    target = Machine.CtoBSystemCoors(Wafer.GetCurrentLine(CurrentLine).start);
                    double xGap = Blade.XGap(Wafer.Thickness);
                    await Machine.X.MoveAxisInPosAsync(target.X + xGap);
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

                    target = Machine.CtoBSystemCoors(Wafer.GetCurrentLine(CurrentLine).end);
                    var traceX = Machine.X.ActualPosition;
                    var traceY = Machine.Y.ActualPosition;
                    var angle = Machine.U.ActualPosition;
                    Thread tracingThread = new Thread(new ThreadStart(() =>
                    {
                        do
                        {                            
                            TracingLine = new TracePath(traceY, traceX, Machine.X.ActualPosition, angle);
                            Thread.Sleep(1);
                        } while (IsCutting);
                    }));
                    tracingThread.Start();
                    await Machine.X.MoveAxisInPosAsync(target.X);
                    IsCutting = false;
                    tracingThread.Abort();
                    //traces.Add(TracingLine);

                    RotateTransform rotateTransform = new RotateTransform(
                        -Wafer.GetCurrentDiretionAngle,
                        Machine.BladeChuckCenter.X,
                        Machine.BladeChuckCenter.Y
                        );
                    
                    var point1 = rotateTransform.Transform(new System.Windows.Point(traceX, traceY+Wafer.GetCurrentDirectionIndexShift));
                    var point2 = rotateTransform.Transform(new System.Windows.Point(Machine.X.ActualPosition, traceY+Wafer.GetCurrentDirectionIndexShift));
                    point1 = new TranslateTransform(-Machine.BladeChuckCenter.X, -Machine.BladeChuckCenter.Y).Transform(point1);
                    point2 = new TranslateTransform(-Machine.BladeChuckCenter.X, -Machine.BladeChuckCenter.Y).Transform(point2);

                    
                    TracesView.RawLines.Add(new Line(
                        new Vector2(point1.X, point1.Y),
                        new Vector2(point2.X, point2.Y)
                        ));
                    TracesView.RawLines = new ObservableCollection<Line>(TracesView.RawLines);
                    TracingLine = null;
                    Machine.SwitchOnCoolantWater = false;
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
                        Machine.CtoBSystemCoors(Wafer.GetCurrentLine(CurrentLine!=0?CurrentLine-1:0).start).Y - Machine.CameraBladeOffset
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
                   // if (BladeInWafer) break;
                    Machine.SetVelocity(Velocity.Service);
                    target = Machine.CtoBSystemCoors(Wafer.GetCurrentLine(CurrentLine).start);
                    target.X -= Blade.XGap(Wafer.Thickness);
                    await Machine.MoveInPosXYAsync(target);
                    break;
                case Diagram.goTransferingHeightZ:
                    Machine.SetVelocity(Velocity.Service);
                    await Machine.Z.MoveAxisInPosAsync(Machine.ZBladeTouch - Wafer.Thickness - BladeTransferGapZ);
                    break;
                case Diagram.goDockHeightZ:
                    Machine.SetVelocity(Velocity.Service);
                    await Machine.Z.MoveAxisInPosAsync(1);
                    break;
                case Diagram.goNextDirection:
                    if (InProcess & SideDone /*| ProcessStatus == Status.Learning*/) //if the blade isn't in the wafer
                    {
                        Machine.SetVelocity(Velocity.Service);
                        //CutsRotate = true;
                        await MoveNextDirAsync();
                        //CutsRotate = false;
                        SideDone = false;
                        CurrentLine = 0;
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

                    var y = Wafer.GetNearestCut(0).StartPoint.Y;
                    var point = Machine.CtoCSystemCoors(new Vector2(0, Wafer.GetNearestCut(0).StartPoint.Y));
                   
                    await Machine.MoveInPosXYAsync(point);
                    await Machine.Z.MoveAxisInPosAsync(/*Machine.CameraFocus*/3.5);
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
                    Wafer.AddToCurrentDirectionIndexShift = Machine.COSystemCurrentCoors.Y - Wafer.GetNearestCut(Machine.COSystemCurrentCoors.Y).StartPoint.Y;
                    Wafer.SetCurrentDirectionAngle = Machine.U.ActualPosition;
                    if (Wafer.NextDir())
                    {
                        Machine.SetVelocity(Velocity.Service);
                        await MoveNextDirAsync(false);
                        await ProcElementDispatcherAsync(Diagram.goCameraPointLearningXYZ);
                    }
                    else
                    {
                        ProcessStatus = Status.Working;                        
                        
                        traces = new List<TracePath>();
                        
                        DoProcessAsync(BaseProcess);
                    }
                    break;
                case Status.Working:
                    PauseProcess = true;
                    if (PauseProcess) await PauseScenarioAsync();
                    CutWidthMarkerVisibility = Visibility.Visible;
                    ChangeScreensEvent(true);
                    ProcessStatus = Status.Correcting;
                    break;
                case Status.Correcting:
                    var result  = MessageBox.Show($"Сместить следующие резы на {CutOffset} мм?","",MessageBoxButtons.OKCancel);
                    if(result == DialogResult.OK) Wafer.AddToCurrentDirectionIndexShift = CutOffset;
                    ChangeScreensEvent(false);
                    ProcessStatus = Status.Working;
                    CutWidthMarkerVisibility = Visibility.Hidden;
                    CutOffset = 0;
                    PauseProcess = false;
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
