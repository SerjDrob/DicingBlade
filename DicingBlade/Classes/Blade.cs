using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DicingBlade.Classes
{
    class Blade
    {
        public double Diameter { get; set; }
        public double Thickness { get; set; }
        public string Type { get; set; }
        /// <summary>
        /// Расстояние между точкой входа диска на рабочую высоту и началом материала
        /// </summary>
        public double XGap(double h)
        {
            return Math.Sqrt(Diameter * h - Math.Pow(h, 2));
        }
    }
}
