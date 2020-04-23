using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DicingBlade.Classes
{
    public class TrigVar
    {
        private bool trig;
        public void trigger(bool variable,DIEventArgs eventArgs, System.Action<DIEventArgs> func) 
        {
            if (variable == true) func(eventArgs);
        }
    }
}
