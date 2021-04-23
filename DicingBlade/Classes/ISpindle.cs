using System;

namespace DicingBlade.Classes
{
    public delegate void SpindleStateHandler(bool isConnected, double spinCurrent, double spindleFreq);

    public interface ISpindle : IDisposable
    {
        public bool IsConnected { get; set; }          
        public void SetSpeed(ushort rpm);
        public void Start();
        public void Stop();

        /// <summary>
        ///     Gets frequency, current, spinning state
        /// </summary>
        
        public event EventHandler<SpindleEventArgs> GetSpindleState;
    }

    public class SpindleEventArgs : EventArgs
    {
        public int Rpm { get; set; } = 0;
        public double Current { get; set; } = 0;
        public bool OnFreq { get; set; } = false;
        public bool Accelerating { get; set; } = false;
        public bool Deccelarating { get; set; } = false;
        public bool Stop { get; set; } = true;
    }
}