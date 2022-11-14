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
        public Directions CutDirection { get; set; }
        /// <summary>
        /// true - действует, false - отключен или выполнен
        /// </summary>
        public bool Status 
        {
            get { return CurrentCut / CutCount==1 ? false : true; }
            private set { }
        }
        public bool NextCut() 
        {
            if (Status)
            {
                CurrentCut++;              
            }
            return Status;
        }
        public int CutCount { get; set; }
        public double CutRatio
        {
            get
            {
                return (double)(CurrentCut + 1) / CutCount;
            }
        }
        private int CurrentCut { get; set; } = 0;
        public double Offset { get; set; }     
        
        public void ResetCut() 
        {
            CurrentCut = 0;
            Offset = 0;
            Status = true;
        }
    }
    public enum Directions
    {
        direct,
        reverse,
        both
    }
}
