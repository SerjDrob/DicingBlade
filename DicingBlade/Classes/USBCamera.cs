﻿using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using AForge.Imaging.Filters;
using AForge.Video;
using AForge.Video.DirectShow;

namespace DicingBlade.Classes
{
    internal class USBCamera : IVideoCapture
    {
        private VideoCaptureDevice _localWebCam;

        public void FreezeCameraImage()
        {
            _localWebCam.SignalToStop();
        }

        public void StartCamera(int ind)
        {
            if (_localWebCam is null)
            {
                _localWebCam = GetCamera(ind);

                //while (_localWebCam is null)
                //{
                //    MessageBox.Show("Включите питание видеокамеры !");
                //    _localWebCam = GetCamera();
                //}

                try
                {
                    _localWebCam.VideoResolution = _localWebCam.VideoCapabilities[1]; //8
                    _localWebCam.NewFrame += HandleNewFrame;
                }
                catch (IndexOutOfRangeException)
                {
                    MessageBox.Show("Не подключена видеокамера !");
                }
            }

            _localWebCam.Start();
        }

        public void StopCamera()
        {
            _localWebCam.Stop();
        }

        public event BitmapHandler OnBitmapChanged;

        public int GetDevicesCount()
        {
            return new FilterInfoCollection(FilterCategory.VideoInputDevice).Count;
        }

        private static VideoCaptureDevice GetCamera(int ind)
        {
            var webCams = new FilterInfoCollection(FilterCategory.VideoInputDevice);

            return (webCams.Count != 0) & (ind <= webCams.Count)
                ? new VideoCaptureDevice(webCams[ind].MonikerString)
                : default;
        }

        public async void HandleNewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            try
            {
                var filter = new Mirror(false, false);
                using var img = (Bitmap) eventArgs.Frame.Clone();
                filter.ApplyInPlace(img);

                var ms = new MemoryStream();
                img.Save(ms, ImageFormat.Bmp);

                ms.Seek(0, SeekOrigin.Begin);

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze();
                OnBitmapChanged?.Invoke(bitmap);
            }
            catch (Exception ex)
            {
            }

            await Task.Delay(40).ConfigureAwait(false);
        }
    }
}