using System.Linq;
using NUnit.Framework;
using UnityEditor.DeviceSimulation;

namespace DeviceManagment
{
    internal class DeviceLoading
    {
        [Test]
        public void DeviceOverlayLoad()
        {
            var devices = DeviceLoader.LoadDevices();
            var minimalDevice = devices.First(device => device.deviceInfo.friendlyName == "MinimalTestDevice1");

            var screenOverlay0 = DeviceLoader.LoadOverlay(minimalDevice, 0);
            var screenOverlay1 = DeviceLoader.LoadOverlay(minimalDevice, 1);

            Assert.NotNull(screenOverlay0);
            Assert.NotNull(screenOverlay1);
        }
    }
}
