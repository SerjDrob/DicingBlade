using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AForge.Imaging.Filters;
using AForge.Video;
using AForge.Video.DirectShow;
using System.Windows.Media.Imaging;

namespace DicingBlade.Classes
{
    public delegate void BitmapHandler(BitmapImage bitmapImage);
    public interface IVideoCapture
    {               
        /// <summary>
        /// Start video capture device
        /// </summary>
        /// <param name="ind">index of device</param>
        public void StartCamera(int ind);
        public void FreezeCameraImage();
        public void StopCamera();
        public int GetDevicesCount();

        public event BitmapHandler OnBitmapChanged;

    }
}
