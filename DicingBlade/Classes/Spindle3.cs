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
        private SerialPort serialPort;
        private readonly object modbusLock = new object();
        /// <summary>
        /// 300 Hz = 18000 rpm
        /// </summary>
        private const ushort _lowFreqLimit = 3000;
        /// <summary>
        /// 550 Hz = 33000 rpm
        /// </summary>
        private const ushort _highFreqLimit = 5500;
        private bool EstablishConnection(string com)
        {
            serialPort = new SerialPort();

            serialPort.PortName = com;
            serialPort.BaudRate = 9600;
            //serialPort.DataBits = 8;
            serialPort.Parity = Parity.Even;
            //serialPort.StopBits = StopBits.One;
            serialPort.WriteTimeout = 1000;
            serialPort.ReadTimeout = 1000;            
            serialPort.Open();           
            if (serialPort.IsOpen)
            {
                _client = ModbusSerialMaster.CreateRtu(serialPort);
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
                        lock (modbusLock)
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
            lock (modbusLock)
            {
                _client.WriteMultipleRegisters(1, 0xF000, new ushort[]
                {
                    0,
                    5000,
                    2,
                    _lowFreqLimit,//500,//lower limiting frequency/10
                    _highFreqLimit,//upper limiting frequency/10
                    500//acceleration time/10                
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
                    25//V3
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
            if (!((rpm / 6 > _lowFreqLimit) && (rpm / 6 < _highFreqLimit)))
            {
                throw new SpindleException($"{rpm}rpm is out of ({_lowFreqLimit*6},{_highFreqLimit*6}) rpm range");
            }
            rpm = (ushort)Math.Abs(rpm / 6);
            lock (modbusLock)
            {
                _client.WriteSingleRegister(1, 0xF001, rpm);
            }            
        }
        public void Start()
        {
            lock (modbusLock)
            {
                _client.WriteSingleRegister(1, 0x1001, 0x0001);                
            }
        }

        public void Stop()
        {
            lock (modbusLock)
            {                
                _client.WriteSingleRegister(1, 0x1001, 0x0003);
            }
        }

        ~Spindle3()
        {
            serialPort.Dispose();
            _client.Dispose();
        }
    }
}

