﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DicingBlade.Classes
{
    public interface IWafer
    {
        bool IsRound { get; set; }
        double Width { get; set; }
        double Height { get; set; }
        double Thickness { get; set; }
        double IndexW { get; set; }
        double IndexH { get; set; }
        double Diameter { get; set; }
        string FileName { get; set; }
    }

    public class TempWafer : IWafer
    {
        public bool IsRound { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Thickness { get; set; }
        public double IndexW { get; set; }
        public double IndexH { get; set; }
        public double Diameter { get; set; }
        public string FileName { get; set; }
        public TempWafer(IWafer wafer)
        {
            wafer.CopyPropertiesTo(this);
        }
        public TempWafer(){}
    }
}
