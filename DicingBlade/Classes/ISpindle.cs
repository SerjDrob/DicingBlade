using System;

namespace DicingBlade.Classes
{
    public delegate void SpindleStateHandler(bool isConnected, double spinCurrent, double spindleFreq);

    public interface ISpindle : IDisposable
    {
        public void SetSpeed(ushort rpm);
        public void Start();
        public void Stop();

        /// <summary>
        ///     Gets frequency, current, spinning state
        /// </summary>
        public event Action<int, double, bool> GetSpindleState;
    }
}