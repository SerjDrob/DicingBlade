using System;
using System.Collections.Generic;
using System.Linq;
using Advantech.Motion;

namespace DicingBlade.Classes
{
    internal class MotionDevice : IDisposable
    {
        public MotionDevice()
        {
            var device = GetAvailableDevs().First();
            DeviceHandle = OpenDevice(device);
        }

        public static IntPtr DeviceHandle { get; private set; }

        private static IntPtr OpenDevice(in DEV_LIST device)
        {
            var deviceHandle = IntPtr.Zero;
            var result = Motion.mAcm_DevOpen(device.DeviceNum, ref deviceHandle);

            if (!Success(result))
            {
                throw new MotionException($"Open Device Failed With Error Code: [0x{result:X}]");
            }

            return deviceHandle;
        }

        private static IEnumerable<DEV_LIST> GetAvailableDevs()
        {
            var availableDevs = new DEV_LIST[Motion.MAX_DEVICES];
            uint deviceCount = default;
            var result = Motion.mAcm_GetAvailableDevs(availableDevs, Motion.MAX_DEVICES, ref deviceCount);

            if (!Success(result))
            {
                throw new MotionException($"Get Device Numbers Failed With Error Code: [{result:X}]");
            }

            return availableDevs.Take((int)deviceCount);
        }

        public int GetAxisCount()
        {
            uint axesPerDev = default;
            var result = Motion.mAcm_GetU32Property(DeviceHandle, (uint)PropertyID.FT_DevAxesCount, ref axesPerDev);

            if (!Success(result))
            {
                throw new MotionException($"Get Axis Number Failed With Error Code: [0x{result:X}]");
            }

            return (int)axesPerDev;
        }

        private static bool Success(uint result)
        {
            return result == (uint)ErrorCode.SUCCESS;
        }

        private static bool Success(int result)
        {
            return result == (int)ErrorCode.SUCCESS;
        }

        private void ReleaseUnmanagedResources()
        {
            //var copy = DeviceHandle;

            //if (copy != IntPtr.Zero)
            //{
            //    Motion.mAcm_DevClose(ref copy);
            //}
        }

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        ~MotionDevice()
        {
            ReleaseUnmanagedResources();
        }
    }
}