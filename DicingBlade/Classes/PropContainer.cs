using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DicingBlade.Classes
{
    public static class PropContainer
    {
        public static int counter { get; set; }
        public static bool IsRound { get; set; }
        public static Wafer Wafer { get; set; }
        public static ITechnology Technology { get; set; }
    }
}
