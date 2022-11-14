using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Globalization;

namespace DicingBlade.Classes
{
    class Map : INotifyPropertyChanged
    {
        private Int32 mask;
        public Int32 Mask 
        {
            get { return mask; }
            set 
            {
                mask = value;
                OnPropertyChanged();
            }
        }
        public void Set(int bit) => Mask |= 1 << bit;
        public void UnSet(int bit) => Mask &= ~(1 << bit);
        public void ApplyMask(params int[] bits)
        {
            foreach (var item in bits)
            {
                Mask |= 1 << item;
            }
        }
        public bool GetCondition(int bit) => (Mask & (1 << bit)) != 0 ? true : false;
        
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([CallerMemberName]string prop = "") 
        {
            if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }

    }
}
