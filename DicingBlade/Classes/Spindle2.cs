using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using FluentModbus;

namespace DicingBlade.Classes
{
    class Spindle2 : ISpindle
    {
        public Spindle2()
        {
            if (EstablishConnection("COM1"))
            {
                WatchingStateAsync();
            }
        }
        private ModbusRtuClient _client;
        private readonly object _modbusLock = new object();
        private bool EstablishConnection(string com)
        {
            _client = new ModbusRtuClient()
            {
                BaudRate = 9600,
                Parity = Parity.Even,
                StopBits = StopBits.Two,                
            };
            _client.Connect(com);
            return _client.IsConnected;
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
                            //int current = (data[2] << 8) | data[3];
                            //int freq = (data[0] << 8) | data[1];
                            //GetSpindleState?.Invoke(freq * 6, current / 10, true);
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

        public void SetSpeed(ushort rpm)
        {
            throw new NotImplementedException();
        }

        public void Start()
        {
            lock (_modbusLock)
            {
                _client.WriteSingleRegister(1, 0x1001, 0x0001);
               // _client.WriteMultipleRegisters(1, 0x1001, new short[] { 0x0001 });
            }
            
        }

        public void Stop()
        {
            lock (_modbusLock)
            {
                _client.WriteSingleRegister(1, 0x1001, 0x0003);
            }
        }
    }
}
