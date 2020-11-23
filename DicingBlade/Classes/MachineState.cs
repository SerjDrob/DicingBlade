using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace DicingBlade.Classes
{
    enum States 
    {
        A,
        B,
        C,
        D
    }
    enum Func
    {
        F1,
        F2,
        F3,
        F4
    }
    class MachineState
    {
        public MachineState() 
        {
            Graph = new Dictionary<States, Dictionary<Func, States>>(5);
            Graph.Add(States.A, new Dictionary<Func, States>()
            {
                [Func.F1] = States.B,
                [Func.F2] = States.C
            }
            );
        }
        public Dictionary<States,Dictionary<Func,States>> Graph { get; set; }

        //EnumFunc.Action
    }
}
