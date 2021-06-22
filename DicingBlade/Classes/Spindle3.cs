﻿using System;
using System.IO.Ports;
using System.Threading.Tasks;
using System.Windows.Documents;
using FluentModbus;
using Modbus.Device;

namespace DicingBlade.Classes
{
    internal class Spindle3 : ISpindle, IDisposable
    {
        /// <summary>
        ///     300 Hz = 18000 rpm
        /// </summary>
        private const ushort LowFreqLimit = 3000;

        /// <summary>
        ///     550 Hz = 33000 rpm
        /// </summary>
        private const ushort HighFreqLimit = 5500;

        private readonly object _modbusLock = new();
        private ModbusSerialMaster _client;
        private SerialPort _serialPort;

        // TODO wait o cancel in the end, NEVER forget Tasks
        private Task _watchingStateTask;

        public Spindle3()
        {
            if (EstablishConnection("COM1"))
            {
                _watchingStateTask = WatchingStateAsync();
                if (CheckSpindleWorking())
                {
                    return;
                }
                if (!SetParams()) throw new SpindleException("SetParams is failed");
            }
        }

        private bool CheckSpindleWorking()
        {
            lock (_modbusLock)
            {
                var data = _client.ReadHoldingRegisters(1, 0xD000, 1);
                
                return data[0]!=0;
            }
        }

       
        public event EventHandler<SpindleEventArgs> GetSpindleState;
       

        public bool IsConnected { get; set; } = false;

        public void SetSpeed(ushort rpm)
        {
            if (!(rpm / 6 > LowFreqLimit && rpm / 6 < HighFreqLimit))
            {
                throw new SpindleException($"{rpm}rpm is out of ({LowFreqLimit * 6},{HighFreqLimit * 6}) rpm range");
            }
            rpm = (ushort) Math.Abs(rpm / 6);
            lock (_modbusLock)
            {
                _client.WriteSingleRegister(1, 0x0001, rpm);
            }
        }

        public void Start()
        {
            lock (_modbusLock)
            {
               // _client.WriteSingleRegister(1, 0x1001, 0x0020);
                _client.WriteSingleRegister(1, 0x1001, 0x0001);
                _hasStarted = true;
            }
        }

        private bool _hasStarted = false;
        public void Stop()
        {
            lock (_modbusLock)
            {
                _client.WriteSingleRegister(1, 0x1001, 0x0003);
                _hasStarted = false;
            }
        }

        private bool EstablishConnection(string com)
        {
            _serialPort = new SerialPort
            {
                PortName = com,
                BaudRate = 9600,
                Parity = Parity.Even,
                WriteTimeout = 1000,
                ReadTimeout = 100
            };
            
            _serialPort.Open();
            if (_serialPort.IsOpen)
            {
                _client = ModbusSerialMaster.CreateRtu(_serialPort);
            }
            else
            {
                return false;
            }

            return IsConnected = true;
        }

        private async Task WatchingStateAsync()
        {
            ushort[] data = default;
            bool onFreq = false;
            bool acc = false;
            bool dec = false;
            bool stop = false;

            while (true)
            {
                try
                {
                    int current;
                    int freq;
                    lock (_modbusLock)
                    {
                        data = _client.ReadHoldingRegisters(1, 0xD000, 2);
                        current = data[1];
                        freq = data[0];
                        data = _client.ReadHoldingRegisters(1, 0x2000, 1);
                        onFreq = ((data[0] == 0x0001) | (data[0] == 0x0002));
                        acc = ((data[0] == 0x0011) | (data[0] == 0x0012));
                        dec = ((data[0] == 0x0014) | (data[0] == 0x0015));
                        stop = (data[0] == 0x0003);
                    }
                    //GetSpindleState?.Invoke(freq * 6, (double)current / 10, onFreq);
                    GetSpindleState?.Invoke(
                        null,
                        new SpindleEventArgs()
                        {
                            Rpm=freq*6,
                            Current = (double)current/10,
                            Accelerating=acc,
                            Deccelarating=dec,
                            OnFreq=onFreq,
                            Stop=stop
                        }
                        );
                }
                catch (ModbusException)
                {
                    //throw;
                }

                await Task.Delay(100).ConfigureAwait(false);
            }

        }

        private bool SetParams()
        {
            lock (_modbusLock)
            {
                _client.WriteMultipleRegisters(1, 0x0000, new ushort[]
                {
                    0,
                    5000,
                    2,
                    LowFreqLimit, //500,//lower limiting frequency/10
                    HighFreqLimit, //upper limiting frequency/10
                    900 //acceleration time/10
                });

                _client.WriteMultipleRegisters(1, 0x000B, new ushort[]
                {
                    60, //torque boost/10, 0.0 - 20.0%
                    5200, //basic running frequency/10
                    50 //maximum output voltage 50 - 500V
                });

                _client.WriteMultipleRegisters(1, 0x020F, new ushort[]
                {
                    4999, //f3/10
                    30 //V3
                });

                _client.WriteMultipleRegisters(1, 0x020D, new ushort[]
                {
                    1200, //f2/10
                    20 //V2
                });

                _client.WriteMultipleRegisters(1, 0x020B, new ushort[]
                {
                    800, //f1/10
                    10 //V1
                });
            }

            return true;
        }

        public void Dispose()
        {
            _serialPort.Dispose();
            _client.Dispose();
        }
    }
}