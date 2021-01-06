using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DicingBlade.Classes
{
    class SquareWafer : IWafer2
    {

        //public Line this[int angle, int lineNum] 
        //{
        //    get
        //    {
        //        return _grid.Lines[angle][lineNum];
        //    }
        //}

        public double Width { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public double Height { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public double IndexW { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public double IndexH { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public double Thickness { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }


        private Grid _grid;
    }
}
