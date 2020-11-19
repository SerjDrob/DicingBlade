using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DicingBlade.Classes
{
    internal class Map : INotifyPropertyChanged
    {
        private Int32 _mask;
        public Int32 Mask
        {
            get => _mask;
            set
            {
                _mask = value;
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
        public bool GetCondition(int bit) => (Mask & (1 << bit)) != 0;

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }

    }
}
