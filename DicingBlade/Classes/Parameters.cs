using System;

namespace DicingBlade.Classes
{
    [Serializable]
    public class Parameters
    {
        //public Parameters() { }
        public Parameters(Blade blade, ITechnology technology)
        {
            Blade = blade;
            Technology = technology;
        }
        public Blade Blade { get; set; }
        public ITechnology Technology { get; set; }
    }
}
