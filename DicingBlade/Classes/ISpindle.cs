using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DicingBlade.Classes
{
    public delegate void SpindleStateHandler(bool isConnected, double spinCurrent, double spindleFreq);
    interface ISpindle
    {
        //private double SpindleFreq { get; set; }
        //private double SpindleCurrent { get; set; }
        ////public bool EstablishConnectionModbus(string com);
        //private bool IsConnected { get; set; }
        public void SetSpeed(double rpm);
        public void Start();
        public void Stop();
        public event SpindleStateHandler GetSpindleState;
        //{
        //    //ModbusClient modbusClient = new ModbusClient("COM1");
        //    //modbusClient.UnitIdentifier = 1; Not necessary since default slaveID = 1;
        //    //modbusClient.Baudrate = 9600;	// Not necessary since default baudrate = 9600
        //    //modbusClient.Parity = System.IO.Ports.Parity.None;
        //    //modbusClient.StopBits = System.IO.Ports.StopBits.Two;
        //    //modbusClient.ConnectionTimeout = 500;
        //    //modbusClient.Connect();
        //    //modbusClient.ConnectionTimeout = 100;
        //    //Console.WriteLine("Value of Discr. Input #1: " + modbusClient.ReadHoldingRegisters(0xF004, 1)[0].ToString());  //Reads Discrete Input #1
        //    _spindleModbus.WriteSingleRegister(0x1001, 0x0001);
        //    //Console.WriteLine("Value of Input Reg. #10: " + modbusClient.ReadInputRegisters(9, 1)[0].ToString());   //Reads Inp. Reg. #10

        //    //modbusClient.WriteSingleCoil(4, true);      //Writes Coil #5
        //    //modbusClient.WriteSingleRegister(19, 4711); //Writes Holding Reg. #20

        //    //Console.WriteLine("Value of Coil #5: " + modbusClient.ReadCoils(4, 1)[0].ToString());   //Reads Discrete Input #1
        //    //Console.WriteLine("Value of Holding Reg.. #20: " + modbusClient.ReadHoldingRegisters(19, 1)[0].ToString()); //Reads Inp. Reg. #10
        //    //modbusClient.WriteMultipleRegisters(49, new int[10] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
        //    //modbusClient.WriteMultipleCoils(29, new bool[10] { true, true, true, true, true, true, true, true, true, true, });

        //    //Console.Write("Press any key to continue . . . ");
        //    //Console.ReadKey(true);
        //}
    }
}
