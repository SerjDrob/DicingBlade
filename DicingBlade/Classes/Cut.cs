using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using netDxf.Entities;
using netDxf;

namespace DicingBlade.Classes
{
    public class Cut:Line
    {
        public Cut(Cut cut) 
        {
            //this.Clone = cut;
        }
        public Cut(Vector3 startpoint, Vector3 endpoint) 
        {
            StartPoint = startpoint;
            EndPoint = endpoint;
            Status = true;
            CutCount = 1;
            Offset = 0;
            CutDirection = Directions.direct;
        }
        
        /// <summary>
        /// направление резки - встречная, попутная, встречно-попутная        
        /// </summary>
        public static Directions CutDirection { get; set; }
        /// <summary>
        /// true - действует, false - отключен или выполнен
        /// </summary>
        public bool Status { get; set; }
        public int CutCount { get; set; }
        public int CurrentCut { get; set; }
        public double Offset { get; set; }
        public static double CommonOffset { get; set; }
    
    }
    public enum Directions
    {
        direct,
        reverse,
        both
    }
}
