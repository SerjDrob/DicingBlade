using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DicingBlade.Utility
{
    internal class MachineConfiguration
    {
        private const string PCI1240U = "PCI1240U";
        private const string PCI1245E = "PCI1245E";
        private const string MOCKBOARD = "MOCKBOARD";

        private const string EM225 = "EM225";
        private const string O4PP100 = "O4PP100";
        private const string DR150 = "DR150";

        public string MotionBoardNote { get => $"Choose from following boards: {PCI1240U}, {PCI1245E}, {MOCKBOARD}"; }
        public string MotionBoard { get; set; }
        public string DicingDevTypeNote { get => $"Choose from following types: {EM225}, {O4PP100}, {DR150}"; }
        public string DicingDevType { get; set; }
        public bool IsPCI1240U { get => MotionBoard == PCI1240U; }
        public bool IsPCI1245E { get => MotionBoard == PCI1245E; }
        public bool IsMOCKBOARD { get => MotionBoard == MOCKBOARD; }
        public bool IsEM225 { get => DicingDevType == EM225; }
        public bool IsO4PP100 { get => DicingDevType == O4PP100; }
        public bool IsDR150 { get => DicingDevType == DR150; }

    }
}
