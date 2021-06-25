using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PropertyChanged;
using Microsoft.VisualStudio.Workspace;
using System.Windows;
using DicingBlade.ViewModels;


namespace DicingBlade.Classes.Test
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
        private double _bladeTransferGapZ = 2;
        private bool _learningNextDir = true;
        private bool _learningDone = false;
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
        private PauseTokenSource _pauseProcTokenSource = new();
        public Visibility CutWidthMarkerVisibility { get; set; } = Visibility.Hidden;
        public event Action<string> OnProcessStatusChanged;
        public event Action<bool> BladeTracingEvent;
        public event Action OnControlPointAppeared;
        public event Action<bool> ChangeScreensEvent;
        /// <summary>
        /// first parameter: angle
        /// second parameter: time
        /// </summary>
        public Action<double,double> GetRotationEvent;
        public int CurrentLine { get; private set; }
        public double CutOffset { get; set; } = 0;
        private readonly Blade _blade;
        public int CurrentDirection { get; private set; }
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
            blade.Thickness = 0.11;
            blade.Diameter = 55.6;

            _machine = machine ?? throw new ProcessException("Не выбрана установка для процесса"); ;
            _wafer = wafer ?? throw new ProcessException("Не выбрана подложка для процесса");
            _blade = blade ?? throw new ProcessException("Не выбран диск для процесса");
            _technology = technology;
            RefresfTechnology(_technology);
            
            _machine.OnSensorStateChanged += _machine_OnSensorStateChanged;
            _machine.OnAxisMotionStateChanged += _machine_OnAxisMotionStateChanged;
            _machine.OnSpindleStateChanging += _machine_OnSpindleStateChanging;

            _machine.SwitchOnValve(Valves.ChuckVacuum);
            Task.Delay(500).Wait();           

            _checkCut.Set(technology.StartControlNum, technology.ControlPeriod);
            TuneBT();
            _rootSequence.DoWork();
        }
        private void TuneBT()
        {
            var learnLeaf1 = new Leaf(StartLearningAsync);
            var learnLeaf2 = new Leaf(LearningAsync);
            var learnLeaf3 = new Leaf(MovingNextDirAsync).SetBlock(_learningMoveNextDirBlock).SayMyName("MovingNextDirAsync");           

            var workingLeaf1 = new Leaf(SetProcessStatusAsync);
            var workingLeaf2 = new Leaf(GoNextCutXYAsync);
            var workingLeaf3 = new Leaf(GoNextDepthZAsync);
            var workingLeaf4 = new Leaf(CuttingXAsync);
            var workingLeaf5 = new Leaf(IncrementLineAsync);
            var workingLeaf6 = new Leaf(GoTransferingHeightZAsync);
            var workingLeaf7 = new Leaf(GoNextDirectionAsync).SetBlock(_workingGoNextDirectionBlock.BlockMe()).SayMyName("GoNextDirectionAsync");

            var inspectLeaf1 = new Leaf(TakeThePhotoAsync).SetBlock(_inspectLeafBlock.BlockMe()).SayMyName("TakeThePhotoAsync");
            var inspectLeaf2 = new Leaf(CorrectionAsync);
            var inspectLeaf3 = new Leaf(EndCorrectionAsync);

            var learningSequence = new Sequence()
                .Hire(learnLeaf1).WaitForMe()
                .Hire(learnLeaf2).WaitForMe()
                .Hire(learnLeaf3).WaitForMe();                
            
            var learningTicker = new Ticker()
                .Hire(learningSequence)
                .SetBlock(_learningTickerBlock);

            var inspectSequence = new Sequence()
                .Hire(inspectLeaf2).WaitForMe()
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
                 .Hire(new Leaf(async () => {_wafer.SetSide(CurrentDirection = 0);}))
                 .Hire(workingTicker)
                 .SubscribeAllOnCheckEvent(CheckAllFlagsBeforeWorking);                            
        }


        private void CheckAllFlagsBeforeWorking(string name)
        {
            if (name == "MovingNextDirAsync")
            {
                if (CurrentDirection < _wafer.SidesCount - 1)
                {
                    if (_wafer.SidesCount - 1 == _wafer.CurrentSide)
                    {
                        _learningMoveNextDirBlock.BlockMe();
                    }
                }
                else
                {
                    _learningTickerBlock.BlockMe();
                }
            }
            if (name == "GoNextDirectionAsync")
            {
                if (CurrentLine == _wafer.CurrentLinesCount)
                {
                    if (_wafer.CurrentSide != 0)
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
            if (name == "TakeThePhotoAsync")
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
                _rootSequence.ResumeWaitersWork();
            }
            else
            {
                if (_inspectSequenceBlock.NotBlocked)
                {
                    _rootSequence.ResumeWaitersWork();
                    _inspectSequenceBlock.BlockMe();
                }
                else
                {
                    _inspectSequenceBlock.UnBlockMe();
                    OnProcessStatusChanged?.Invoke("Пауза");
                }                
            }
        }


        #region Functions for behaviour tree
        private async Task CorrectionAsync()
        {
            await GoTransferingHeightZAsync();
            await GoCameraPointXyzAsync();
            _machine.SwitchOnValve(Valves.Blowing);
            await Task.Delay(100).ConfigureAwait(false);
            _machine.SwitchOffValve(Valves.Blowing);
            CutWidthMarkerVisibility = Visibility.Visible;
            ChangeScreensEvent?.Invoke(true);            
            _machine.StartVideoCapture(0);            
        }
        private async Task EndCorrectionAsync()
        {
            var result = MessageBox.Show($"Сместить следующие резы на {CutOffset} мм?", "", MessageBoxButton.OKCancel);
            if (result == MessageBoxResult.OK)
            {
                _wafer.AddToSideShift(CutOffset);
            }
            var nearestNum = _wafer.GetNearestNum(_yActual - _machine.GetGeometry(Place.CameraChuckCenter, Ax.Y));
            if (CurrentLine != nearestNum)
            {
                result = MessageBox.Show($"Изменить номер реза на {nearestNum}?", "", MessageBoxButton.OKCancel);
                if (result == MessageBoxResult.OK)
                {
                    CurrentLine = nearestNum - 1;
                }
            }
            ChangeScreensEvent?.Invoke(false);
            CutWidthMarkerVisibility = Visibility.Hidden;
            CutOffset = 0;
            _machine.FreezeVideoCapture();
            
            OnProcessStatusChanged?.Invoke("Работа");
            
            _inspectX = _xActual;
            OnControlPointAppeared();
        }
        private async Task GoCameraPointXyzAsync()
        {
            _machine.SetVelocity(Velocity.Service);
            var z = _machine.TranslateSpecCoor(Place.ZBladeTouch, _wafer.Thickness + _bladeTransferGapZ, Ax.Z);
            await AwaitTaskAsync(_machine.MoveAxInPosAsync(Ax.Z, z));
            var y = -_machine.TranslateSpecCoor(Place.BladeChuckCenter, _yActual, Ax.Y);
            y = _machine.TranslateSpecCoor(Place.CameraChuckCenter, -y, Ax.Y);
            await AwaitTaskAsync(_machine.MoveGpInPosAsync(Groups.XY, new double[] { _inspectX, y }, true));
            await AwaitTaskAsync(_machine.MoveAxesInPlaceAsync(Place.ZFocus));
        }
        private async Task TakeThePhotoAsync()
        {
            _machine.StartVideoCapture(0);
            await GoTransferingHeightZAsync();
            await GoCameraPointXyzAsync();
            _machine.SwitchOnValve(Valves.Blowing);
            await Task.Delay(100).ConfigureAwait(false);
            _machine.FreezeVideoCapture();
            _machine.SwitchOffValve(Valves.Blowing);
            OnControlPointAppeared();
        }
        private async Task GoNextDirectionAsync()
        {
            _wafer.SetSide(_wafer.CurrentSide - 1);
            _machine.SetVelocity(Velocity.Service);
            await AwaitTaskAsync(MoveNextDirAsync());
            _procParamsEventArgs.currentShift = _wafer.CurrentShift;
            _procParamsEventArgs.currentSideAngle = _wafer.CurrentSideAngle;
            OnProcParamsChanged(this, _procParamsEventArgs);
            CurrentLine = 0;
        }
        private async Task GoTransferingHeightZAsync()
        {
            _machine.SetVelocity(Velocity.Service);
            await AwaitTaskAsync(_machine.MoveAxInPosAsync(Ax.Z, _machine.GetFeature(MFeatures.ZBladeTouch) - _wafer.Thickness - _bladeTransferGapZ));
        }
        private async Task IncrementLineAsync()
        {
            CurrentLine++;
        }
        private async Task CuttingXAsync()
        {
            _machine.SwitchOnValve(Valves.Coolant);
            await AwaitTaskAsync(Task.Delay(300));
            _machine.SetAxFeedSpeed(Ax.X, _feedSpeed);
            IsCutting = true;

            var xCurLineEnd = _wafer[CurrentLine].End.X;
            var x = _machine.TranslateSpecCoor(Place.BladeChuckCenter, -xCurLineEnd, 0);

            BladeTracingEvent(true);
            await AwaitTaskAsync(_machine.MoveAxInPosAsync(Ax.X, x));
            BladeTracingEvent(false);
            IsCutting = false;
            _lastCutY = _yActual;
            _checkCut.addToCurrentCut();
            _machine.SwitchOffValve(Valves.Coolant);
        }
        private async Task GoNextDepthZAsync()
        {
            _machine.SetVelocity(Velocity.Service);
            var z = _machine.TranslateSpecCoor(Place.ZBladeTouch, _wafer[_zRatio] + _undercut, Ax.Z);
            await AwaitTaskAsync(_machine.MoveAxInPosAsync(Ax.Z, z));
        }
        private async Task GoNextCutXYAsync()
        {
            // if (BladeInWafer) ;
            _machine.SetVelocity(Velocity.Service);
            var x = _wafer[CurrentLine].Start.X;
            x = x + Math.Sign(x) * _blade.XGap(_wafer.Thickness);
            var y = _wafer[CurrentLine].Start.Y - _wafer.CurrentShift;
            var arr = _machine.TranslateActualCoors(Place.BladeChuckCenter, new (Ax, double)[] { (Ax.X, -x), (Ax.Y, -y) });
            var xy = new double[] { arr.GetVal(Ax.X), arr.GetVal(Ax.Y) };
            await AwaitTaskAsync(_machine.MoveGpInPosAsync(Groups.XY, xy, true));
        }
        private async Task SetProcessStatusAsync()
        {
            //ProcessStatus = Status.Working;
            OnProcessStatusChanged("Работа");
        }
        private async Task LearningAsync()
        {
            var y = _machine.TranslateActualCoors(Place.CameraChuckCenter, Ax.Y);
            _wafer.TeachSideShift(y);
            _procParamsEventArgs.currentShift = _wafer.CurrentShift;
            OnProcParamsChanged(this, _procParamsEventArgs);
            _wafer.TeachSideAngle(_uActual);            
        }        
        private async Task MovingNextDirAsync()
        {
            if (CurrentDirection < _wafer.SidesCount - 1)
            {
                _wafer.SetSide(++CurrentDirection);
                _machine.SetVelocity(Velocity.Service);
                await AwaitTaskAsync(MoveNextDirAsync(false));
                _procParamsEventArgs.currentShift = _wafer.CurrentShift;
                _procParamsEventArgs.currentSideAngle = _wafer.CurrentSideAngle;
                OnProcParamsChanged(this, _procParamsEventArgs);
            }
        }
        private async Task MoveNextDirAsync(bool next = true)
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

            GetRotationEvent(deltaAngle, time);
            await _machine.MoveAxInPosAsync(Ax.U, angle);
        }
        private async Task StartLearningAsync()
        {
            _machine.SetVelocity(Velocity.Service);
            await AwaitTaskAsync(_machine.MoveAxInPosAsync(Ax.Z, _machine.GetFeature(MFeatures.ZBladeTouch) - _wafer.Thickness - _bladeTransferGapZ));
            var y = _wafer.GetNearestCut(0).Start.Y;
            var arr = _machine.TranslateActualCoors(Place.CameraChuckCenter, new (Ax, double)[] { (Ax.X, 0), (Ax.Y, -y) });           
            var point = new double[] { arr.GetVal(Ax.X), arr.GetVal(Ax.Y) };
            await AwaitTaskAsync(_machine.MoveGpInPosAsync(Groups.XY, point));
            await AwaitTaskAsync(_machine.MoveAxesInPlaceAsync(Place.ZFocus));            
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
            _pauseProcTokenSource.Pause();            
            _machine.Stop(Ax.X);
            _machine.MoveAxInPosAsync(Ax.Z, 0);
            _machine.StopSpindle();
        }
        public event Action<object, ProcParams> OnProcParamsChanged;
        public event Action<string, int> ThrowMessage;
    }
}
