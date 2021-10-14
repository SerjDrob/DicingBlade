using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PropertyChanged;
using Microsoft.VisualStudio.Workspace;
using System.Windows;
using DicingBlade.ViewModels;


namespace DicingBlade.Classes.BehaviourTrees
{
    [AddINotifyPropertyChangedInterface]
    class Process5 : IMessager
    {        
        private IMachine _machine;
        private Wafer2D _wafer;
        private ProcParams _procParamsEventArgs;
        private double _xActual;
        private double _yActual;
        private double _zActual;
        private double _uActual;
        private double _bladeTransferGapZ = 4;       
        private bool _spindleWorking;
        private double _zRatio = 0;
        private double _feedSpeed;
        private double _undercut;
        private double _lastCutY;
        private double _inspectX;
        private Sensors _offSensors = 0;
        private ITechnology _technology;
        private CheckCutControl _checkCut;
        private List<Task> _localTasks = new();
        public enum Stat
        {
            Cancelled,
            End
        }
        public Visibility CutWidthMarkerVisibility { get; set; } = Visibility.Hidden;
        public event Action<string> OnProcessStatusChanged;
        public event EventHandler<Stat> OnProcStatusChanged;        
        public event Action<bool> BladeTracingEvent;
        public event Action OnControlPointAppeared;
        public event Action<bool> ChangeScreensEvent;
        /// <summary>
        /// first parameter: angle
        /// second parameter: time
        /// </summary>
        public event Action<double,double> GetRotationEvent;
        public event Action<object, ProcParams> OnProcParamsChanged;
        public event Action<string, int> ThrowMessage;
        public double CutOffset { get; set; } = 0;
        private readonly Blade _blade;
        private bool IsCutting { get; set; } = false;

        Block _learningTickerBlock = new Block();
        Block _workingTickerBlock = new Block();
        Block _learningMoveNextDirBlock = new Block();
        Block _workingGoNextDirectionBlock = new Block();
        Block _inspectSequenceBlock = new Block();
        Block _inspectLeafBlock= new Block();
        Sequence _rootSequence = new Sequence();
        public Process5(IMachine machine, Wafer2D wafer, Blade blade, ITechnology technology)
        {
            //blade.Thickness = 0.11;
            //blade.Diameter = 55.6;
            _blade = blade;

            _machine = machine ?? throw new ProcessException("Не выбрана установка для процесса");
            _wafer = wafer ?? throw new ProcessException("Не выбрана подложка для процесса");
            _blade = blade ?? throw new ProcessException("Не выбран диск для процесса");
            _technology = technology;
            _inspectX = _machine.GetGeometry(Place.CameraChuckCenter, Ax.X);
            RefresfTechnology(_technology);
            
            _machine.OnSensorStateChanged += _machine_OnSensorStateChanged;
            _machine.OnAxisMotionStateChanged += _machine_OnAxisMotionStateChanged;
            _machine.OnSpindleStateChanging += _machine_OnSpindleStateChanging;

            _machine.SwitchOnValve(Valves.ChuckVacuum);
            Task.Delay(500).Wait();           

            _checkCut.Set(technology.StartControlNum, technology.ControlPeriod);
            TuneBT();
            _rootSequence.DoWork().ContinueWith(t=> 
            {
                if (t.Result) OnProcStatusChanged?.Invoke(this,Stat.End) ; 
            });
        }
        private void TuneBT()
        {
            var learnLeaf1 = new Leaf(StartLearningAsync).WaitForMe();
            var learnLeaf2 = new Leaf(LearningAsync);
            var learnLeaf3 = new Leaf(MovingNextDirAsync).SetBlock(_learningMoveNextDirBlock)
                                                         .SetActionBeforeWork(BeforeMovingNextDirAsync);



            var workingLeaf1 = new Leaf(SetProcessStatusAsync);
            var workingLeaf2 = new Leaf(GoNextCutXYAsync);
            var workingLeaf3 = new Leaf(GoNextDepthZAsync);
            var workingLeaf4 = new Leaf(CuttingXAsync);
            var workingLeaf5 = new Leaf(IncrementLineAsync);
            var workingLeaf6 = new Leaf(GoTransferingHeightZAsync);
            var workingLeaf7 = new Leaf(GoNextDirectionAsync).SetBlock(_workingGoNextDirectionBlock.BlockMe())
                                                             .SetActionBeforeWork(BeforeGoNextDirectionAsync);



            var inspectLeaf1 = new Leaf(TakeThePhotoAsync).SetBlock(_inspectLeafBlock.BlockMe())
                                                          .SetActionBeforeWork(BeforeTakeThePhotoAsync);



            var inspectLeaf2 = new Leaf(CorrectionAsync).WaitForMe();
            var inspectLeaf3 = new Leaf(EndCorrectionAsync);

            var learningSequence = new Sequence()
                .Hire(learnLeaf1)
                .Hire(learnLeaf2)
                .Hire(learnLeaf3);

            var learningTicker = new Ticker()
                .Hire(learningSequence)
                .SetBlock(_learningTickerBlock);

            var inspectSequence = new Sequence()
                .Hire(inspectLeaf2)
                .Hire(inspectLeaf3)
                .SetBlock(_inspectSequenceBlock.BlockMe());

            var workingSequence = new Sequence()
                .Hire(workingLeaf1)
                .Hire(workingLeaf2)
                .Hire(workingLeaf3)
                .Hire(workingLeaf4)
                .Hire(inspectLeaf1)
                .Hire(inspectSequence)
                .Hire(workingLeaf5)
                .Hire(workingLeaf6)
                .Hire(workingLeaf7);

            var workingTicker = new Ticker()
                .Hire(workingSequence)
                .SetBlock(_workingTickerBlock);

            _rootSequence
                 .Hire(learningTicker)                 
                 .Hire(workingTicker);
        }

        
        public void RefresfTechnology(ITechnology technology)
        {
            _feedSpeed = technology.FeedSpeed;
            _undercut = technology.UnterCut;
        }
        private async Task AwaitTaskAsync(Task task)
        {
            _localTasks.Add(task);
            await task.ConfigureAwait(false);
        }
        public async Task WaitProcDoneAsync()
        {
            await Task.WhenAll(_localTasks);          
        }
        public async Task StartPauseProc()
        {
            if (_learningTickerBlock.NotBlocked)
            {
                _rootSequence.PulseAction(false);
            }
            else
            {
                if (_inspectSequenceBlock.NotBlocked)
                {
                    _rootSequence.PulseAction(false);
                    _inspectSequenceBlock.BlockMe();
                }
                else
                {
                    _inspectSequenceBlock.UnBlockMe();
                    OnProcessStatusChanged?.Invoke("Пауза");
                }
            }
        }
        void BeforeMovingNextDirAsync()
        {
            if (_wafer.IsLastSide)
            {
                _learningMoveNextDirBlock.BlockMe();
                _learningTickerBlock.BlockMe();
            }
        }
        void BeforeGoNextDirectionAsync()
        {
            if (_wafer.LastCutOfTheSide)
            {
                if (_wafer.IsLastSide)
                {
                    _workingGoNextDirectionBlock.UnBlockMe();
                }
                else
                {
                    _workingTickerBlock.BlockMe();
                }
            }
            else
            {
                _workingGoNextDirectionBlock.BlockMe();
            }
        }
        void BeforeTakeThePhotoAsync()
        {
            if (_checkCut.Check & !_inspectSequenceBlock.NotBlocked)
            {
                _inspectLeafBlock.UnBlockMe();
            }
            else
            {
                _inspectLeafBlock.BlockMe();
            }
        }

        #region Functions for behaviour tree

        private void CorrectionAsync()
        {
            GoTransferingHeightZAsync();
            GoCameraPointXyzAsync();
            _machine.SwitchOnValve(Valves.Blowing);
            Task.Delay(500).Wait();
            _machine.SwitchOffValve(Valves.Blowing);
            CutWidthMarkerVisibility = Visibility.Visible;
            ChangeScreensEvent?.Invoke(true);            
            _machine.StartVideoCapture(0);            
        }
        private void EndCorrectionAsync()
        {
            var result = MessageBox.Show($"Сместить следующие резы на {CutOffset} мм?", "", MessageBoxButton.OKCancel);
            if (result == MessageBoxResult.OK)
            {
                _wafer.AddToSideShift(CutOffset);
            }
            var nearestNum = _wafer.GetNearestNum(_yActual - _machine.GetGeometry(Place.CameraChuckCenter, Ax.Y));
            if (_wafer.CurrentCutNum != nearestNum)
            {
                result = MessageBox.Show($"Изменить номер реза на {nearestNum}?", "", MessageBoxButton.OKCancel);
                if (result == MessageBoxResult.OK)
                {
                    _wafer.SetCurrentCutNum(nearestNum - 1);
                }
            }
            ChangeScreensEvent?.Invoke(false);
            CutWidthMarkerVisibility = Visibility.Hidden;
            CutOffset = 0;
            _machine.FreezeVideoCapture();
            
            OnProcessStatusChanged?.Invoke("Работа");
            
            _inspectX = _xActual;
            OnControlPointAppeared?.Invoke();
        }
        private void GoCameraPointXyzAsync()
        {
            _machine.SetVelocity(Velocity.Service);
            var z = _machine.TranslateSpecCoor(Place.ZBladeTouch, _wafer.Thickness + _bladeTransferGapZ, Ax.Z);
            AwaitTaskAsync(_machine.MoveAxInPosAsync(Ax.Z, z)).Wait();
            
            var y = -_machine.TranslateSpecCoor(Place.BladeChuckCenter, _yActual, Ax.Y);
            y = _machine.TranslateSpecCoor(Place.CameraChuckCenter, -y, Ax.Y);
            AwaitTaskAsync(_machine.MoveGpInPosAsync(Groups.XY, new double[] { _inspectX, y }, true)).Wait();
            AwaitTaskAsync(_machine.MoveAxesInPlaceAsync(Place.ZFocus)).Wait();
        }
        private void TakeThePhotoAsync()
        {
            _machine.StartVideoCapture(0);
            GoTransferingHeightZAsync();
            GoCameraPointXyzAsync();
            _machine.SwitchOnValve(Valves.Blowing);
            Task.Delay(100).Wait();
            _machine.FreezeVideoCapture();
            _machine.SwitchOffValve(Valves.Blowing);
            OnControlPointAppeared?.Invoke();
        }
        private void GoNextDirectionAsync()
        {
            _wafer.DecrementSide();
            _machine.SetVelocity(Velocity.Service);
            MoveNextDirAsync();
            _procParamsEventArgs.currentShift = _wafer.CurrentShift;
            _procParamsEventArgs.currentSideAngle = _wafer.CurrentSideAngle;
            OnProcParamsChanged?.Invoke(this, _procParamsEventArgs);           
        }
        private void GoTransferingHeightZAsync()
        {
            _machine.SetVelocity(Velocity.Service);
            AwaitTaskAsync(_machine.MoveAxInPosAsync(Ax.Z, _machine.GetFeature(MFeatures.ZBladeTouch) - _wafer.Thickness - _bladeTransferGapZ)).Wait();
        }
        private void IncrementLineAsync()
        {
            _wafer.IncrementCut();
        }
        private void CuttingXAsync()
        {
            _machine.SwitchOnValve(Valves.Coolant);
            AwaitTaskAsync(Task.Delay(300)).Wait();
            _machine.SetAxFeedSpeed(Ax.X, _feedSpeed);
            IsCutting = true;

            var xCurLineEnd = _wafer.GetCurrentCut().End.X;
            var x = _machine.TranslateSpecCoor(Place.BladeChuckCenter, -xCurLineEnd, 0);

            BladeTracingEvent?.Invoke(true);
            AwaitTaskAsync(_machine.MoveAxInPosAsync(Ax.X, x)).Wait();
            BladeTracingEvent?.Invoke(false);
            IsCutting = false;
            _lastCutY = _yActual;
            _checkCut.addToCurrentCut();
            _machine.SwitchOffValve(Valves.Coolant);
        }
        private void GoNextDepthZAsync()
        {
            _machine.SetVelocity(Velocity.Service);
            var z = _machine.TranslateSpecCoor(Place.ZBladeTouch, _wafer[_zRatio] + _undercut, Ax.Z);
            AwaitTaskAsync(_machine.MoveAxInPosAsync(Ax.Z, z)).Wait();
        }
        private void GoNextCutXYAsync()
        {
            // if (BladeInWafer) ;
            var line = _wafer.GetCurrentCut();
            _machine.SetVelocity(Velocity.Service);
            var x = line.Start.X;
            x = x + Math.Sign(x) * _blade.XGap(_wafer.Thickness);
            var y = line.Start.Y - _wafer.CurrentShift;
            var arr = _machine.TranslateActualCoors(Place.BladeChuckCenter, new (Ax, double)[] { (Ax.X, -x), (Ax.Y, -y) });
            var xy = new double[] { arr.GetVal(Ax.X), arr.GetVal(Ax.Y) };
            AwaitTaskAsync(_machine.MoveGpInPosAsync(Groups.XY, xy, true)).Wait();
        }
        private void SetProcessStatusAsync()
        {
            //ProcessStatus = Status.Working;
            OnProcessStatusChanged?.Invoke("Работа");
        }
        private void LearningAsync()
        {
            var y = _machine.TranslateActualCoors(Place.CameraChuckCenter, Ax.Y);
            _wafer.TeachSideShift(y);
            _procParamsEventArgs.currentShift = _wafer.CurrentShift;
            OnProcParamsChanged?.Invoke(this, _procParamsEventArgs);
            _wafer.TeachSideAngle(_uActual);            
        }        
        private void MovingNextDirAsync()
        {
            if (_wafer.IncrementSide())
            {                
                _machine.SetVelocity(Velocity.Service);
                MoveNextDirAsync(false);
                _procParamsEventArgs.currentShift = _wafer.CurrentShift;
                _procParamsEventArgs.currentSideAngle = _wafer.CurrentSideAngle;
                OnProcParamsChanged?.Invoke(this, _procParamsEventArgs);
            }
        }
        private void MoveNextDirAsync(bool next = true)
        {
            double angle = _wafer.CurrentSideAngle;
            double time = 0;
            var deltaAngle = _wafer.CurrentSideAngle - _wafer.PrevSideAngle;
            if (_wafer.CurrentSideActualAngle == _wafer.CurrentSideAngle)
            {
                angle = _wafer.PrevSideActualAngle - _wafer.PrevSideAngle + _wafer.CurrentSideAngle;
                time = Math.Abs(angle - _uActual) / _machine.GetAxisSetVelocity(Ax.U);
            }
            else
            {
                angle = _wafer.CurrentSideActualAngle;
                time = Math.Abs(_wafer.CurrentSideActualAngle - _uActual) / _machine.GetAxisSetVelocity(Ax.U);
            }

            GetRotationEvent?.Invoke(deltaAngle, time);
            _machine.MoveAxInPosAsync(Ax.U, angle).Wait();
        }
        private void StartLearningAsync()
        {
            _machine.SetVelocity(Velocity.Service);
            AwaitTaskAsync(_machine.MoveAxInPosAsync(Ax.Z, _machine.GetFeature(MFeatures.ZBladeTouch) - _wafer.Thickness - _bladeTransferGapZ)).Wait();
            var y = _wafer.GetNearestY(0);
            var arr = _machine.TranslateActualCoors(Place.CameraChuckCenter, new (Ax, double)[] { (Ax.X, 0), (Ax.Y, -y) });           
            var point = new double[] { arr.GetVal(Ax.X), arr.GetVal(Ax.Y) };
            AwaitTaskAsync(_machine.MoveGpInPosAsync(Groups.XY, point)).Wait();
            AwaitTaskAsync(_machine.MoveAxesInPlaceAsync(Place.ZFocus)).Wait();            
        }
#endregion
        public void SubstrateChanged(object obj, SettingsChangedEventArgs eventArgs)
        {
            //if (eventArgs.Settings is IWafer & (int)(ProcessStatus & (Status.Working | Status.Correcting | Status.MovingNextDir | Status.Ending)) == 0)
            //{
            //    var wf = (IWafer)eventArgs.Settings;
            //    _wafer.SetChanges(wf.IndexH, wf.IndexW, wf.Thickness, new Rectangle2D(wf.Height, wf.Width));
            //}
        }
        private void _machine_OnSensorStateChanged(Sensors sensor, bool state)
        {
            if (state)
            {
                _offSensors ^= sensor;
            }
            else
            {
                _offSensors |= sensor;
            }

        }
        private void _machine_OnAxisMotionStateChanged(Ax axis, double position, bool nLmt, bool pLmt, bool motionDone, bool motionStart)
        {
            switch (axis)
            {
                case Ax.X:
                    _xActual = position;
                    break;
                case Ax.Y:
                    _yActual = position;
                    break;
                case Ax.Z:
                    _zActual = position;
                    break;
                case Ax.U:
                    _uActual = position;
                    break;
                case Ax.All:
                    break;
                default:
                    break;
            }
        }
        private void _machine_OnSpindleStateChanging(object? obj, SpindleEventArgs e)
        {
            _spindleWorking = e.OnFreq;
            if (IsCutting & !_spindleWorking)
            {
                EmergencyScript();
            }                
        }
        public void EmergencyScript()
        {
            _rootSequence?.CancellAction(true);          
            _machine.Stop(Ax.X);
            _machine.MoveAxInPosAsync(Ax.Z, 0);
            _machine.StopSpindle();
        }
        
    }
}
