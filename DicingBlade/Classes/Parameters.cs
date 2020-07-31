using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DicingBlade.Classes
{
    [Serializable]
    public class Parameters
    {
        public Parameters() { }
        public Blade Blade { get; set; }
        public Technology Technology { get; set; }
    }
}
