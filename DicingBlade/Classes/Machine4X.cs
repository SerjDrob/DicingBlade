using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace DicingBlade.Classes
{
    //[addinotifypropertychangedinterface]
    internal class Machine4X : IMachine, IDisposable
    {
        private readonly ExceptionsAgregator _exceptionsAgregator;
        private Dictionary<Ax, IAxis> _axes; // { get; set; }
        private Dictionary<Groups, (int groupNum, Ax[] axes)> _axesGroups;
        private Dictionary<MFeatures, double> _doubleFeatures;
        private Dictionary<Place, (Ax axis, double pos)[]> _places; // { get; set; }
        private Dictionary<Sensors, (Ax axis, Di dIn, bool invertion, string name)> _sensors; // { get; set; }
        private Dictionary<Place, double> _singlePlaces;
        private Dictionary<Valves, (Ax axis, Do dOut)> _valves; // { get; set; }
        private Dictionary<Ax, Dictionary<Velocity, double>> _velRegimes;

        public Machine4X(ExceptionsAgregator exceptionsAgregator, MotionDevice motionDevice, IVideoCapture usbVideoCamera)
        {
            _exceptionsAgregator = exceptionsAgregator;
            VideoCamera = usbVideoCamera;
            VideoCamera.OnBitmapChanged += GetBitmap;
            try
            {
                // TODO use IoC
                Spindle = new Spindle3();
                Spindle.GetSpindleState += _spindle_GetSpindleState;
            }
            catch (SpindleException ex)
            {
                throw new MachineException($"Spindle initialization was failed with message: {ex.Message}");
            }

            MotionDevice = motionDevice;
            _exceptionsAgregator.RegisterMessager(MotionDevice);
            if (MotionDevice.DevicesConnection())
            {
                MotionDevice.StartMonitoringAsync();
                MotionDevice.TransmitAxState += MotionDevice_TransmitAxState;
            }
        }

        private ISpindle Spindle { get; }
        private IVideoCapture VideoCamera { get; }
        public Velocity VelocityRegime { get; set; }
        public bool MachineInit { get; set; }
        public MotionDevice MotionDevice { get; set; }

        public event SensorStateHandler OnSensorStateChanged;
        public event ValveStateHandler OnValveStateChanged;
        public event AxisMotioStateHandler OnAxisMotionStateChanged;
        public event BitmapHandler OnVideoSourceBmpChanged;
        public event EventHandler<SpindleEventArgs> OnSpindleStateChanging;

        public void AddGroup(Groups group, IAxis[] axes)
        {
            throw new NotImplementedException();
        }

        public void ConfigureAxes((Ax axis, double linecoefficient)[] ax)
        {
            if (ax.Count() <= MotionDevice.AxisCount)
            {
                _axes = new Dictionary<Ax, IAxis>(ax.Count());
                for (var axnum = 0; axnum < ax.Count(); axnum++)
                    _axes.Add(ax[axnum].axis, new Axis2(ax[axnum].linecoefficient, axnum));
            }
        }

        public void ConfigureGeometry(Dictionary<Place, (Ax, double)[]> places)
        {
            if (_places is not null)
                places.ToList().ForEach(e =>
                {
                    if (_places.ContainsKey(e.Key))
                        _places[e.Key] = e.Value;
                    else
                        _places.Add(e.Key, e.Value);
                });
            else
                _places = new Dictionary<Place, (Ax, double)[]>(places);
        }

        public void ConfigureSensors(Dictionary<Sensors, (Ax, Di, bool, string)> sensors)
        {
            _sensors = new Dictionary<Sensors, (Ax, Di, bool, string)>(sensors);
        }

        public void ConfigureValves(Dictionary<Valves, (Ax, Do)> valves)
        {
            _valves = new Dictionary<Valves, (Ax, Do)>(valves);
        }

        public void SetBridgeOnSensors(Sensors sensor, bool setBridge)
        {
            var num = _axes[_sensors[sensor].axis].AxisNum;
            MotionDevice.SetBridgeOnAxisDin(num, (int) _sensors[sensor].dIn, setBridge);
        }

        public void EmgScenario()
        {
            throw new NotImplementedException();
        }

        public void EmgStop()
        {
            throw new NotImplementedException();
        }

        public async Task GoThereAsync(Place place, bool precisely = false)
        {
            if (place != Place.Home)
            {
                if (precisely)
                {
                    var ax = new (int, double, double)[_places[place].Length];
                    for (var i = 0; i < _places[place].Length; i++)
                    {
                        var axis = _places[place][i].axis;
                        ax[i] = (_axes[axis].AxisNum, _places[place][i].pos, _axes[axis].LineCoefficient);
                    }

                    MotionDevice.MoveAxesByCoorsPrecAsync(ax);
                }
                else
                {
                    var ax = new (int, double)[_places[place].Length];
                    for (var i = 0; i < _places[place].Length; i++)
                    {
                        var axis = _places[place][i].axis;
                        ax[i] = (_axes[axis].AxisNum, _places[place][i].pos);
                    }

                    MotionDevice.MoveAxesByCoorsAsync(ax);
                }
            }
            else
            {
                var arr = new (int, double, uint)[]
                {
                    (_axes[Ax.X].AxisNum, _velRegimes[Ax.X][Velocity.Service], 1),
                    (_axes[Ax.Y].AxisNum, _velRegimes[Ax.Y][Velocity.Service], 5),
                    (_axes[Ax.Z].AxisNum, _velRegimes[Ax.Z][Velocity.Service], 1)
                };
                var axArr = new[] {Ax.X, Ax.Z};
                MotionDevice.HomeMoving(arr);
                foreach (var axis in axArr)
                    Task.Run(() =>
                    {
                        while (!_axes[axis].LmtN) Task.Delay(10).Wait();
                        ResetErrors(axis);
                        MotionDevice.ResetAxisCounter(_axes[axis].AxisNum);
                        MoveAxInPosAsync(axis, 1, true);
                    });

                MotionDevice.ResetAxisCounter(_axes[Ax.U].AxisNum);
            }
        }

        public void GoWhile(Ax axis, AxDir direction)
        {
            ResetErrors(axis);
            MotionDevice.MoveAxisContiniouslyAsync(_axes[axis].AxisNum, direction);
        }

        public async Task MoveAxInPosAsync(Ax axis, double position, bool precisely = false)
        {
            if (!_axes[axis].Busy)
            {
                SetAxisBusy(axis);
                if (precisely)
                    await MotionDevice.MoveAxisPreciselyAsync(_axes[axis].AxisNum, _axes[axis].LineCoefficient,
                        position);
                else
                    await Task.Run(() =>
                    {
                        MotionDevice.MoveAxisAsync(_axes[axis].AxisNum, position);
                        while (!_axes[axis].MotionDone) ;
                    });
                ResetAxisBusy(axis);
            }
        }

        private void SetAxisBusy(Ax axis)
        {
            _axes[axis].Busy = true;
        }

        private void ResetAxisBusy(Ax axis)
        {
            _axes[axis].Busy = false;
        }
        public void ResetErrors(Ax axis = Ax.All)
        {
            if (axis == Ax.All)
                MotionDevice.ResetErrors();
            else
                MotionDevice.ResetErrors(_axes[axis].AxisNum);
        }

        public void SetConfigs((Ax axis, MotionDeviceConfigs configs)[] axesConfigs)
        {
            var count = axesConfigs.Length <= 4 ? axesConfigs.Length : 4;
            for (var i = 0; i < count; i++)
            {
                var ax = axesConfigs[i].axis;
                var configs = axesConfigs[i].configs;
                MotionDevice.SetAxisConfig(_axes[ax].AxisNum, configs);
            }
        }

        public void SetVelocity(Velocity velocity)
        {
            VelocityRegime = velocity;
            foreach (var axis in _axes)
                if (axis.Value.VelRegimes != null)
                {
                    double vel = default;
                    if (axis.Value.VelRegimes.TryGetValue(velocity, out vel))
                    {
                        MotionDevice.SetAxisVelocity(axis.Value.AxisNum, axis.Value.VelRegimes[velocity]);
                    }
                }
                else
                    throw new MotionException($"Не настроенны скоростные режимы оси {axis.Key.ToString()}");
            foreach (var group in _axesGroups.Values)
            {
                //   MotionDevice.SetGroupVelocity(group.groupNum);
            }
        }

        public void SetAxFeedSpeed(Ax axis, double feed)
        {
            MotionDevice.SetAxisVelocity(_axes[axis].AxisNum, feed);
        }

        public void EmergencyStop()
        {
            throw new NotImplementedException();
        }

        public void Stop(Ax axis)
        {
            MotionDevice.StopAxis(_axes[axis].AxisNum);
        }

        public void SwitchOnValve(Valves valve)
        {
            MotionDevice.SetAxisDout(_axes[_valves[valve].axis].AxisNum, (ushort) _valves[valve].dOut, true);
        }

        public void SwitchOffValve(Valves valve)
        {
            MotionDevice.SetAxisDout(_axes[_valves[valve].axis].AxisNum, (ushort) _valves[valve].dOut, false);
        }

        public bool GetValveState(Valves valve)
        {
            return MotionDevice.GetAxisDout(_axes[_valves[valve].axis].AxisNum, (ushort) _valves[valve].dOut);
        }

        private bool BusyGroup(Groups group)
        {
            var busy = false;
            foreach (var axis in _axesGroups[group].axes)
            {
                busy |= _axes[axis].Busy;
            }

            return busy;
        }

        public async Task MoveGpInPosAsync(Groups group, double[] position, bool precisely = false)
        {


            if (!BusyGroup(group))
            {
                var k = new double();
                try
                {
                    k = Math.Abs((position.First() - _axes[Ax.X].CmdPosition) /
                                 (position.Last() - _axes[Ax.Y].ActualPosition)); //ctg a
                }
                catch (DivideByZeroException)
                {
                    k = 1000;
                }

                var vx = _velRegimes[Ax.X][VelocityRegime];
                var vy = _velRegimes[Ax.Y][VelocityRegime];
                var kmax = vx / vy; // ctg a

                var v = (k / kmax) switch
                {
                    1 => Math.Sqrt(vx * vx + vy * vy),
                    < 1 => vy / Math.Sin(Math.Atan(1 / k)), // / Math.Sqrt(1 / (1 + k * k)),//yconst
                    > 1 => vx / Math.Cos(Math.Atan(1 / k)) //Math.Sqrt(k * k / (1 + k * k)) //xconst
                };
                MotionDevice.SetGroupVelocity(_axesGroups[group].groupNum, v);

                if (precisely)
                {
                    var gpNum = _axesGroups[group].groupNum;
                    var axesNums = _axes.Where(a => _axesGroups[group].axes.Contains(a.Key)).Select(n => n.Value.AxisNum);
                    var lineCoeffs = _axes.Where(a => _axesGroups[group].axes.Contains(a.Key))
                        .Select(n => n.Value.LineCoefficient);
                    var gpAxes = axesNums.Zip(lineCoeffs, (a, b) => new ValueTuple<int, double>(a, b)).ToArray();

                    var n = _axesGroups[group].axes.FindIndex(a => a == Ax.Y);

                    position[n] -= 0.03;

                    await MotionDevice.MoveGroupPreciselyAsync(gpNum, position, gpAxes);

                    position[n] += 0.03;

                    await MotionDevice.MoveAxisPreciselyAsync(_axes[Ax.Y].AxisNum, _axes[Ax.Y].LineCoefficient,
                        position[n]);
                }
                else
                {
                    await MotionDevice.MoveGroupAsync(_axesGroups[group].groupNum, position);
                }
            }
        }

        public async Task MoveGpInPlaceAsync(Groups group, Place place, bool precisely = false)
        {
            MoveGpInPosAsync(group, _places[place].Select(p => p.pos).ToArray(), precisely);
        }

        public async Task MoveAxesInPlaceAsync(Place place)
        {
            foreach (var axpos in _places[place]) await MoveAxInPosAsync(axpos.axis, axpos.pos);
        }

        /// <summary>
        ///     Actual coordinates translation
        /// </summary>
        /// <param name="place"></param>
        /// <returns>Axes actual coordinates - place coordinates relatively</returns>
        public (Ax, double)[] TranslateActualCoors(Place place)
        {
            var count = _places[place].Length;
            var arr = new (Ax, double)[count];
            for (var i = 0; i < count; i++)
            {
                var pl = _places[place][i];
                arr[i] = (pl.axis, _axes[pl.axis].ActualPosition - pl.pos);
            }

            return arr;
        }

        public double TranslateActualCoors(Place place, Ax axis)
        {
            var res = new double();
            try
            {
                var ax = _axes[axis];
                res = ax.ActualPosition - _places[place].Where(a => a.axis == axis).First().pos;
            }
            catch (KeyNotFoundException)
            {
                throw new MachineException("Запрашиваемое место отсутствует");
            }
            catch (IndexOutOfRangeException)
            {
                throw new MachineException($"Для места {place} не обозначена координата {axis}");
            }

            return res;
        }

        /// <summary>
        ///     Coordinate translation
        /// </summary>
        /// <param name="place"></param>
        /// <param name="position"></param>
        /// <returns>place coors - position coors</returns>
        public (Ax, double)[] TranslateActualCoors(Place place, (Ax axis, double pos)[] position)
        {
            var temp = new List<(Ax, double)>();
            foreach (var p in position)
                if (_places[place].Select(a => a.axis).Contains(p.axis))
                    temp.Add((p.axis, _places[place].GetVal(p.axis) - p.pos));
            var arr = temp.ToArray();
            return arr;
        }

        public double TranslateSpecCoor(Place place, double position, Ax axis)
        {
            var pl = new double();

            try
            {
                pl = _places[place].Where(a => a.axis == axis).Select(p => p.pos).First() - position;
            }
            catch (ArgumentNullException)
            {
                throw new MachineException($"Координаты {axis} места {place} не существует");
            }

            return pl;
        }

        public void ConfigureVelRegimes(Dictionary<Ax, Dictionary<Velocity, double>> velRegimes)
        {
            _velRegimes = new Dictionary<Ax, Dictionary<Velocity, double>>(velRegimes);
            foreach (var axis in _axes)
                try
                {
                    axis.Value.VelRegimes = new Dictionary<Velocity, double>(_velRegimes[axis.Key]);
                }
                catch (KeyNotFoundException)
                {
                    throw new MotionException($"Для оси {axis.Key} не заданы скоростные режимы");
                }
        }

        public string GetSensorName(Sensors sensor)
        {
            
            var name = "";
            try
            {
                name = _sensors[sensor].name;
            }
            catch (KeyNotFoundException)
            {
                throw new MachineException($"Датчик {sensor} не сконфигурирован");
            }

            return name;
        }

        public void StartVideoCapture(int ind)
        {
            VideoCamera.StartCamera(ind);
        }

        public void FreezeVideoCapture()
        {
            VideoCamera.FreezeCameraImage();
        }

        public void ConfigureGeometry(Dictionary<Place, double> places)
        {
            _singlePlaces = new Dictionary<Place, double>(places);
        }

        public double GetGeometry(Place place, int arrNum)
        {
            var pl = new double();

            try
            {
                pl = _places[place][arrNum].pos;
            }
            catch (KeyNotFoundException)
            {
                throw new MachineException("Запрашиваемое место отсутствует");
            }
            catch (IndexOutOfRangeException)
            {
                throw new MachineException($"Для места {place} не обозначена координата № {arrNum}");
            }

            return pl;
        }

        public double GetGeometry(Place place, Ax axis)
        {
            var pl = new double();
            var arrNum = new int();

            if (!_places.ContainsKey(place))
                throw new MachineException("Запрашиваемое место отсутствует");
            try
            {
                pl = _places[place].Where(a => a.axis == axis).First().pos;
            }
            catch (KeyNotFoundException)
            {
                throw new MachineException($"Ось {axis} не сконфигурированна");
            }
            catch (ArgumentOutOfRangeException ex)
            {
                throw new MachineException($"Координаты в позиции {arrNum} места {place} не существует");
            }

            return pl;
        }

        public async Task WaitUntilAxisStopAsync(Ax axis)
        {
            var status = new uint();
            await Task.Run(() =>
            {
                while (!_axes[axis].MotionDone) Task.Delay(10).Wait();
            });
        }

        public double GetAxisSetVelocity(Ax axis)
        {
            var velocity = new double();
            var regimes = new Dictionary<Velocity, double>();
            if (_velRegimes.TryGetValue(axis, out regimes))
            {
                if (!regimes.TryGetValue(VelocityRegime, out velocity))
                    throw new MachineException($"Заданный режим скорости не установлен для оси {axis}");
            }
            else
            {
                throw new MachineException($"Для оси {axis} не установленны режимы скоростные режимы");
            }

            return velocity;
        }

        public void ConfigureAxesGroups(Dictionary<Groups, Ax[]> groups)
        {
            _axesGroups = new Dictionary<Groups, (int groupNum, Ax[] axes)>();
            foreach (var group in groups)
            {
                var axesNums = _axes.Where(a => group.Value.Contains(a.Key)).Select(n => n.Value.AxisNum).ToArray();
                _axesGroups.Add(@group.Key, (MotionDevice.FormAxesGroup(axesNums), @group.Value));
            }
        }

        public void ConfigureDoubleFeatures(Dictionary<MFeatures, double> doubleFeatures)
        {
            _doubleFeatures = new Dictionary<MFeatures, double>(doubleFeatures);
        }

        public double GetFeature(MFeatures feature)
        {
            return _doubleFeatures[feature];
        }

        public void SetSpindleFreq(int frequency)
        {
            Spindle.SetSpeed((ushort) frequency);
        }

        public void StartSpindle(params Sensors[] blockers)
        {
            _spindleBlockers = new(blockers);
            foreach (var blocker in blockers)
            {
                var axis = _axes[_sensors[blocker].axis];
                var di = _sensors[blocker].dIn;
                if (!axis.GetDi(di)^_sensors[blocker].invertion)
                {
                    throw new MachineException($"Отсутствует {_sensors[blocker].name}");
                }
            }

            Spindle.Start();
        }

        private List<Sensors> _spindleBlockers;
        public void StopSpindle()
        {
            Spindle.Stop();
        }

        private void _spindle_GetSpindleState(object? obj, SpindleEventArgs e)
        {
            OnSpindleStateChanging?.Invoke(null, e);
        }

        private void MotionDevice_TransmitAxState(int axisNum, AxisState state)
        {
            if (_axes != null)
            {
                var axis = _axes.Where(a => a.Value.AxisNum == axisNum).First().Key;
                _axes[axis].ActualPosition = state.actPos * _axes[axis].LineCoefficient;
                _axes[axis].CmdPosition = state.cmdPos;
                _axes[axis].DIs = state.sensors;
                _axes[axis].DOs = state.outs;
                _axes[axis].LmtN = state.nLmt;
                _axes[axis].LmtP = state.pLmt;
                _axes[axis].HomeDone = state.homeDone;
                _axes[axis].MotionDone = state.motionDone;
                var position = _axes[axis].ActualPosition;
                if (_axes[axis].LineCoefficient == 0) position = state.cmdPos;

                OnAxisMotionStateChanged?.Invoke(axis, position, state.nLmt, state.pLmt, state.motionDone,
                    state.vhStart);



                foreach (var sensor in Enum.GetValues(typeof(Sensors)))
                    if (_sensors != null)
                    {
                        var ax = _sensors[(Sensors) sensor].axis;
                        var condition = _axes[ax].GetDi(_sensors[(Sensors) sensor].dIn) ^
                                        _sensors[(Sensors) sensor].invertion;
                        if (!condition & (_spindleBlockers?.Contains((Sensors) sensor) ?? false))
                        {
                            StopSpindle();
                            //throw new MachineException(
                            //    $"Аварийное отключение шпинделя. {_sensors[(Sensors) sensor].name}");
                        }
                        OnSensorStateChanged?.Invoke((Sensors) sensor, condition);
                    }

                foreach (var valve in Enum.GetValues(typeof(Valves)))
                    if (_valves != null)
                    {
                        var ax = _valves[(Valves) valve].axis;
                        OnValveStateChanged?.Invoke((Valves) valve, _axes[ax].GetDo(_valves[(Valves) valve].dOut));
                    }
            }
        }

        private void GetBitmap(BitmapImage bitmap)
        {
            OnVideoSourceBmpChanged?.Invoke(bitmap);
        }

        private Dictionary<Ax, IAxis> GetAxes(Ax[] axes)
        {
            return default;
        }

        private void SetGpVel(Groups group, (double, double, double, double) position, Velocity velocity)
        {
        }

        public void Dispose()
        {
            Spindle.Dispose();
        }
    }
}