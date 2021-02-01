﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PropertyChanged;
using System.Windows.Media.Imaging;

namespace DicingBlade.Classes
{
    //[addinotifypropertychangedinterface]
    internal class Machine4X : IMachine
    {
        public Machine4X() 
        {
            _videoCamera = new USBCamera();            
            _videoCamera.OnBitmapChanged += GetBitmap;
            try
            {
                _spindle = new Spindle3();
                _spindle.GetSpindleState += _spindle_GetSpindleState;
            }
            catch (SpindleException ex)
            {
                throw new MachineException($"Spindle initialization was failed with message: {ex.Message}");
            }
            
            MotionDevice = new MotionDevice();
            _exceptionsAgregator = ExceptionsAgregator.GetExceptionsAgregator();
            _exceptionsAgregator.RegisterMessager(MotionDevice);
            if (MotionDevice.DevicesConnection()) 
            {
                MotionDevice.StartMonitoringAsync();
                MotionDevice.TransmitAxState += MotionDevice_TransmitAxState;
            }
        }

        private void _spindle_GetSpindleState(int rpm, double current, bool spinningState)
        {
            OnSpindleStateChanging?.Invoke(rpm, current, spinningState);
        }

        private ExceptionsAgregator _exceptionsAgregator;
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
                if (_axes[axis].LineCoefficient == 0)
                {
                    position = state.cmdPos;
                }
                
                OnAxisMotionStateChanged?.Invoke(axis, position, state.nLmt, state.pLmt, state.motionDone);

                foreach (var sensor in Enum.GetValues(typeof(Sensors)))
                {
                    if (_sensors != null)
                    {
                        var ax = _sensors[(Sensors)sensor].axis;
                        OnSensorStateChanged?.Invoke((Sensors)sensor, _axes[ax].GetDi(_sensors[(Sensors)sensor].dIn));
                    }
                }
                foreach (var valve in Enum.GetValues(typeof(Valves)))
                {
                    if (_valves != null)
                    {
                        var ax = _valves[(Valves)valve].axis;
                        OnValveStateChanged?.Invoke((Valves)valve, _axes[ax].GetDo(_valves[(Valves)valve].dOut));
                    }
                }
            }

        }
        private void GetBitmap(BitmapImage bitmap)
        {
            OnVideoSourceBmpChanged?.Invoke(bitmap);
        }
        private Dictionary<Ax, IAxis> _axes;// { get; set; }
        private Dictionary<Ax, IAxis> GetAxes(Ax[] axes) { return default; }
        private Dictionary<Valves, (Ax axis, Do dOut)> _valves;// { get; set; }
        private Dictionary<Sensors, (Ax axis, Di dIn)> _sensors;// { get; set; }
        private Dictionary<Place, (Ax axis,double pos)[]> _places;// { get; set; }
        private Dictionary<Place, double> _singlePlaces;
        private Dictionary<Ax, Dictionary<Velocity, double>> _velRegimes;
        private Dictionary<Groups, (int groupNum,Ax[] axes)> _axesGroups;
        private Dictionary<MFeatures, double> _doubleFeatures;
        private void SetGpVel(Groups group, (Double, Double, Double, Double) position, Velocity velocity) { }
        public Velocity VelocityRegime { get; set; } = default;
        public bool MachineInit { get; set; }
        public MotionDevice MotionDevice { get; set; }
        private ISpindle _spindle { get; set; }
        private IVideoCapture _videoCamera { get; set; }
        
        public event SensorStateHandler OnSensorStateChanged;
        public event ValveStateHandler OnValveStateChanged;
        public event AxisMotioStateHandler OnAxisMotionStateChanged;
        public event BitmapHandler OnVideoSourceBmpChanged;
        public event Action<int, double, bool> OnSpindleStateChanging;

        public void AddGroup(Groups group, IAxis[] axes)
        {
            throw new NotImplementedException();
        }

        public void ConfigureAxes((Ax axis, double linecoefficient)[] ax)
        {
            if (ax.Count() <= MotionDevice.AxisCount)
            {
                _axes = new Dictionary<Ax, IAxis>(ax.Count());
                for (int axnum = 0; axnum < ax.Count(); axnum++)
                {
                    _axes.Add(ax[axnum].axis, new Axis2(ax[axnum].linecoefficient, axnum));
                }
            }
            else
            {
                //throw new Exception();
            }            
        }
          
        
        public void ConfigureGeometry(Dictionary<Place, (Ax,double)[]> places)
        {
            _places = new Dictionary<Place, (Ax,double)[]>(places);
        }

        public void ConfigureSensors(Dictionary<Sensors, (Ax, Di)> sensors)
        {
            _sensors = new Dictionary<Sensors, (Ax, Di)>(sensors);
        }

        public void ConfigureValves(Dictionary<Valves, (Ax, Do)> valves)
        {
            _valves = new Dictionary<Valves, (Ax, Do)>(valves);
        }

        public void SetBridgeOnSensors(Sensors sensor, bool setBridge)
        {
            ////var dis = _axes[_sensors[sensor].axis].DIs;
            //var bridge = setBridge ? dis.SetBit((int)_sensors[sensor].dIn) : dis.ResetBit((int)_sensors[sensor].dIn);
            var num = _axes[_sensors[sensor].axis].AxisNum;
            MotionDevice.SetBridgeOnAxisDin(num, (int)_sensors[sensor].dIn,setBridge);
        }
        public void EmgScenario()
        {
            throw new NotImplementedException();
        }

        public void EmgStop()
        {
            throw new NotImplementedException();
        }

        public async Task GoThereAsync(Place place, bool precisely=false)
        {
            if (place!=Place.Home)
            {
                if (precisely)
                {
                    var ax = new (int, double, double)[_places[place].Length];
                    for (int i = 0; i < _places[place].Length; i++)
                    {
                        var axis = _places[place][i].axis;
                        ax[i] = ( _axes[axis].AxisNum,_places[place][i].pos, _axes[axis].LineCoefficient);
                    }
                    MotionDevice.MoveAxesByCoorsPrecAsync(ax);
                }
                else
                {
                    var ax = new (int, double)[_places[place].Length];
                    for (int i = 0; i < _places[place].Length; i++)
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
                    (_axes[Ax.X].AxisNum,_velRegimes[Ax.X][Velocity.Service],1),
                    (_axes[Ax.Y].AxisNum,_velRegimes[Ax.Y][Velocity.Service],5),
                    (_axes[Ax.Z].AxisNum,_velRegimes[Ax.Z][Velocity.Service],1)
                };
                var axArr = new Ax[] { Ax.X, Ax.Z };                
                MotionDevice.HomeMoving(arr);
                foreach (var axis in axArr)
                {                    
                    Task.Run(() =>
                    {
                        while (!_axes[axis].LmtN)
                        {
                            Task.Delay(10).Wait();
                        }
                        ResetErrors(axis);
                        MotionDevice.ResetAxisCounter(_axes[axis].AxisNum);
                        MoveAxInPosAsync(axis, 1, true);
                    });
                }

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
            if (precisely)
            {
                await MotionDevice.MoveAxisPreciselyAsync(_axes[axis].AxisNum, _axes[axis].LineCoefficient, position);
            }
            else
            { 
                await Task.Run(() =>
                {
                    MotionDevice.MoveAxisAsync(_axes[axis].AxisNum, position);
                    while (!_axes[axis].MotionDone) ;                   
                });
            }            
        }
             
        public void ResetErrors(Ax axis = Ax.All)
        {
            if (axis == Ax.All)
            {
                MotionDevice.ResetErrors();
            }
            else
            {
                MotionDevice.ResetErrors(_axes[axis].AxisNum);
            }
        }

        public void SetConfigs((Ax axis, MotionDeviceConfigs configs)[] axesConfigs)
        {
            var count = axesConfigs.Length <= 4 ? axesConfigs.Length : 4;
            for (int i = 0; i < count; i++)
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
            {
                if (axis.Value.VelRegimes != null)
                {
                    MotionDevice.SetAxisVelocity(axis.Value.AxisNum, axis.Value.VelRegimes[velocity]);
                }
                else
                {
                    throw new MotionException($"Не настроенны скоростные режимы оси {axis.Key.ToString()}");
                }
                
            }
            foreach (var group in _axesGroups.Values)
            {
             //   MotionDevice.SetGroupVelocity(group.groupNum);
            }
        }
        public void SetAxFeedSpeed(Ax axis, double feed)
        {
            MotionDevice.SetAxisVelocity(_axes[axis].AxisNum, feed);
        }

        public void Stop(Ax axis)
        {
            MotionDevice.StopAxis(_axes[axis].AxisNum);
        }

        public void SwitchOnValve(Valves valve)
        {
            MotionDevice.SetAxisDout(_axes[_valves[valve].axis].AxisNum, (ushort)_valves[valve].dOut, true);
        }
        public void SwitchOffValve(Valves valve)
        {
            MotionDevice.SetAxisDout(_axes[_valves[valve].axis].AxisNum, (ushort)_valves[valve].dOut, false);
        }
        public bool GetValveState(Valves valve)
        {
            return MotionDevice.GetAxisDout(_axes[_valves[valve].axis].AxisNum, (ushort)_valves[valve].dOut);
        }
        public async Task MoveGpInPosAsync(Groups group, double[] position, bool precisely = false)
        {
            var k = new double();
            try
            {
                k = Math.Abs((position.First() - _axes[Ax.X].CmdPosition) / (position.Last() - _axes[Ax.Y].ActualPosition));//ctg a    
            }
            catch (DivideByZeroException)
            {
                k = 1000;
            }
                        
            var vx = _velRegimes[Ax.X][VelocityRegime];
            var vy = _velRegimes[Ax.Y][VelocityRegime];
            var kmax = vx / vy;// ctg a

            var v = (k / kmax) switch
            {
                1 => Math.Sqrt(vx * vx + vy * vy),
                < 1 => vy / Math.Sin(Math.Atan(1/k)),// / Math.Sqrt(1 / (1 + k * k)),//yconst
                > 1 => vx / Math.Cos(Math.Atan(1/k)) //Math.Sqrt(k * k / (1 + k * k)) //xconst
            };
            MotionDevice.SetGroupVelocity(_axesGroups[group].groupNum, v);

            if (precisely)
            {
                var gpNum = _axesGroups[group].groupNum;
                var axesNums = _axes.Where(a => _axesGroups[group].axes.Contains(a.Key)).Select(n => n.Value.AxisNum);
                var lineCoeffs = _axes.Where(a => _axesGroups[group].axes.Contains(a.Key)).Select(n => n.Value.LineCoefficient);
                var gpAxes = axesNums.Zip(lineCoeffs, (a, b) => new ValueTuple<int,double>(a, b)).ToArray();
                
                await MotionDevice.MoveGroupPreciselyAsync(gpNum, position, gpAxes);
            }
            else
            {
                await MotionDevice.MoveGroupAsync(_axesGroups[group].groupNum, position);
            }
        }

        public async Task MoveGpInPlaceAsync(Groups group, Place place, bool precisely = false)
        {
            MoveGpInPosAsync(group, _places[place].Select(p=>p.pos).ToArray(), precisely);
        }
        public async Task MoveAxesInPlaceAsync(Place place)
        {
            foreach (var axpos in _places[place])
            {
                await MoveAxInPosAsync(axpos.axis, axpos.pos);
            }
        }
        /// <summary>
        /// Actual coordinates translation
        /// </summary>
        /// <param name="place"></param>
        /// <returns>Axes actual coordinates - place coordinates relatively</returns>
        public (Ax,double)[] TranslateActualCoors(Place place)
        {                       
            var count = _places[place].Length;
            var arr = new (Ax,double)[count];
            for (int i = 0; i < count; i++)
            {
                var pl = _places[place][i];                
                arr[i] = (pl.axis,_axes[pl.axis].ActualPosition - pl.pos);
            }
            return arr;
        }
        public double TranslateActualCoors(Place place, Ax axis)
        {
            var res = new double();
            try
            {
                var ax = _axes[axis];
                res = ax.ActualPosition - _places[place].Where(a=>a.axis==axis).First().pos;
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
        /// Coordinate translation
        /// </summary>
        /// <param name="place"></param>
        /// <param name="position"></param>
        /// <returns>place coors - position coors</returns>
        public (Ax,double)[] TranslateActualCoors(Place place, (Ax axis,double pos)[] position)
        {            
            var temp = new List<(Ax, double)>();
            foreach (var p in position)
            {
                if (_places[place].Select(a => a.axis).Contains(p.axis))
                {
                    temp.Add((p.axis,_places[place].GetVal(p.axis) - p.pos));
                }
            }
            var arr = temp.ToArray();
            return arr;
        }
        public double TranslateSpecCoor(Place place, double position, Ax axis)
        {
            var pl = new double();
            
            try
            {
                pl = _places[place].Where(a=>a.axis==axis).Select(p=>p.pos).First() - position;
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
            {
                try
                {
                    axis.Value.VelRegimes = new Dictionary<Velocity, double>(_velRegimes[axis.Key]);
                }
                catch(KeyNotFoundException)
                {
                    throw new MotionException($"Для оси {axis.Key} не заданы скоростные режимы");
                }
            }
        }

        public void StartVideoCapture(int ind)
        {
            _videoCamera.StartCamera(ind);
        }

        public void FreezeVideoCapture()
        {
            _videoCamera.FreezeCameraImage();
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
            {
                throw new MachineException("Запрашиваемое место отсутствует");
            }
            else
            {
                try
                {                   
                    pl = _places[place].Where(a=>a.axis==axis).First().pos;
                }
                catch (KeyNotFoundException)
                {
                    throw new MachineException($"Ось {axis} не сконфигурированна");
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    throw new MachineException($"Координаты в позиции {arrNum} места {place} не существует");
                }
            }
            return pl;
        }

        public async Task WaitUntilAxisStopAsync(Ax axis)
        {
            var status = new uint();
            await Task.Run(() =>
            {
                while (!_axes[axis].MotionDone)
                {
                    Task.Delay(10).Wait();
                }
                
            });
        }

        public double GetAxisSetVelocity(Ax axis)
        {
            var velocity = new double();
            var regimes = new Dictionary<Velocity, double>();
            if (_velRegimes.TryGetValue(axis, out regimes))
            {
                if (!regimes.TryGetValue(VelocityRegime, out velocity))
                {
                    throw new MachineException($"Заданный режим скорости не установлен для оси {axis}");
                }
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
                var axesNums = _axes.Where(a => group.Value.Contains(a.Key)).Select(n=>n.Value.AxisNum).ToArray();
                try
                {
                    _axesGroups.Add(group.Key, (MotionDevice.FormAxesGroup(axesNums), group.Value));
                }
                catch (MotionException)
                {
                    throw;
                }
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
            _spindle.SetSpeed((ushort)frequency);
        }

        public void StartSpindle()
        {
            _spindle.Start();
        }

        public void StopSpindle()
        {
            _spindle.Stop();
        }
    }
}
