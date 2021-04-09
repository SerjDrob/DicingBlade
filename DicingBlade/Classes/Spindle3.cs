using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Modbus.Device;
using System.IO.Ports;

namespace DicingBlade.Classes
{
    class Spindle3:ISpindle
    {
        public Spindle3()
        {
            if (EstablishConnection("COM1"))
            {
                WatchingStateAsync();
                if (!SetParams())
                {
                    throw new SpindleException("SetParams is failed");
                }
            }
        }
        private ModbusSerialMaster _client;
        private SerialPort _serialPort;
        private readonly object _modbusLock = new object();
        /// <summary>
        /// 300 Hz = 18000 rpm
        /// </summary>
        private const ushort LowFreqLimit = 3000;
        /// <summary>
        /// 550 Hz = 33000 rpm
        /// </summary>
        private const ushort HighFreqLimit = 5500;
        private bool EstablishConnection(string com)
        {
            _serialPort = new SerialPort();

            _serialPort.PortName = com;
            _serialPort.BaudRate = 9600;
            //serialPort.DataBits = 8;
            _serialPort.Parity = Parity.Even;
            //serialPort.StopBits = StopBits.One;
            _serialPort.WriteTimeout = 1000;
            _serialPort.ReadTimeout = 1000;            
            _serialPort.Open();           
            if (_serialPort.IsOpen)
            {
                _client = ModbusSerialMaster.CreateRtu(_serialPort);
            }
            else
            {
                return false;
            }

            return true;
        }      

        private async Task WatchingStateAsync()
        {
            Task.Run(() =>
            {
                while (true)
                {
                    try
                    {
                        lock (_modbusLock)
                        {
                            var data = _client.ReadHoldingRegisters(1, 0xD000, 2);
                            int current = data[1];
                            int freq = data[0];
                            GetSpindleState?.Invoke(freq * 6, (double)current / 10, freq > 0);
                        }

                    }
                    catch (FluentModbus.ModbusException)
                    {
                        //throw;                       
                    }

                    Task.Delay(100).Wait();
                }
            });
        }

        public event Action<int, double, bool> GetSpindleState;

        private bool SetParams()
        {
            lock (_modbusLock)
            {
                _client.WriteMultipleRegisters(1, 0xF000, new ushort[]
                {
                    0,
                    5000,
                    2,
                    LowFreqLimit,//500,//lower limiting frequency/10
                    HighFreqLimit,//upper limiting frequency/10
                    900//acceleration time/10                
                });
                
                _client.WriteMultipleRegisters(1, 0xF00B, new ushort[]
                {
                    60,//torque boost/10, 0.0 - 20.0%
                    5200,//basic running frequency/10
                    50//maximum output voltage 50 - 500V                            
                });

                _client.WriteMultipleRegisters(1, 0xF20F, new ushort[] 
                {                    
                    4999,//f3/10
                    30//V3
                });

                _client.WriteMultipleRegisters(1, 0xF20D, new ushort[]
                {
                    1200,//f2/10
                    20//V2
                });

                _client.WriteMultipleRegisters(1, 0xF20B, new ushort[]
                {
                    800,//f1/10
                    10//V1
                });
            }
            return true;
        }
        public void SetSpeed(ushort rpm)
        {
            if (!((rpm / 6 > LowFreqLimit) && (rpm / 6 < HighFreqLimit)))
            {
                throw new SpindleException($"{rpm}rpm is out of ({LowFreqLimit*6},{HighFreqLimit*6}) rpm range");
            }
            rpm = (ushort)Math.Abs(rpm / 6);
            lock (_modbusLock)
            {
                _client.WriteSingleRegister(1, 0xF001, rpm);
            }            
        }
        public void Start()
        {
            lock (_modbusLock)
            {
                _client.WriteSingleRegister(1, 0x1001, 0x0001);                
            }
        }

        public void Stop()
        {
            lock (_modbusLock)
            {                
                _client.WriteSingleRegister(1, 0x1001, 0x0003);
            }
        }

        ~Spindle3()
        {
            _serialPort.Dispose();
            _client.Dispose();
        }
    }
}

