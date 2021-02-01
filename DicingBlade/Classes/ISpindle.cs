using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DicingBlade.Classes
{
    public delegate void SpindleStateHandler(bool isConnected, double spinCurrent, double spindleFreq);
    public interface ISpindle
    {       
        public void SetSpeed(ushort rpm);
        public void Start();
        public void Stop();
        /// <summary>
        /// Gets frequency, current, spinning state
        /// </summary>
        public event Action<int,double,bool> GetSpindleState;        
    }
}
