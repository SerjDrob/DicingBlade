using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using AForge.Imaging.Filters;
using AForge.Video;
using AForge.Video.DirectShow;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;


namespace DicingBlade.Classes
{    
    class USBCamera : IVideoCapture
    {  
        public void FreezeCameraImage()
        {
            _localWebCam.SignalToStop();            
        }
        public void StartCamera(int ind)
        {
            _localWebCam = GetCamera(ind);

            //while (_localWebCam is null)
            //{
            //    MessageBox.Show("Включите питание видеокамеры !");
            //    _localWebCam = GetCamera();
            //}

            _localWebCam.VideoResolution = _localWebCam.VideoCapabilities[1]; //8
            _localWebCam.NewFrame += HandleNewFrame;
            _localWebCam.Start();
        }
        public void StopCamera()
        {
            _localWebCam.Stop();
        }
        
        private VideoCaptureDevice _localWebCam;

        public event BitmapHandler OnBitmapChanged;
        private static VideoCaptureDevice GetCamera(int ind)
        {
            var webCams = new FilterInfoCollection(FilterCategory.VideoInputDevice);

            return (webCams.Count != 0)&(ind<=webCams.Count) ? new VideoCaptureDevice(webCams[ind].MonikerString) : default;
        }
        public async void HandleNewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            try
            {
                var filter = new Mirror(false, true);
                using var img = (Bitmap)eventArgs.Frame.Clone();
                filter.ApplyInPlace(img);

                var ms = new MemoryStream();
                img.Save(ms, ImageFormat.Bmp);

                ms.Seek(0, SeekOrigin.Begin);

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze();

                //var tmp = BitmapImage;
                //BitmapImage = bitmap;
                OnBitmapChanged(bitmap);
                //if (tmp?.StreamSource != null)
                //{
                //    await tmp.StreamSource.DisposeAsync().ConfigureAwait(false);
                //}
            }
            catch (Exception ex)
            {
            }

            await Task.Delay(1).ConfigureAwait(false);
        }

        public int GetDevicesCount()
        {
            return new FilterInfoCollection(FilterCategory.VideoInputDevice).Count;
        }
    }
}
