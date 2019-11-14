using System.IO;
using Boo.Lang;
using UnityEngine;

namespace Unity.DeviceSimulator
{
    internal class DeviceDatabase
    {
        public readonly List<DeviceInfo> m_Devices = new List<DeviceInfo>();

        public DeviceDatabase()
        {
            Refresh();
        }

        public void Refresh()
        {
            m_Devices.Clear();

            var deviceDirectoryPaths = new[]
            {
                Path.Combine("Packages", "com.unity.device-simulator", ".DeviceDefinitions"),
                DeviceSimulatorUserSettingsProvider.LoadOrCreateSettings().DeviceDirectory
            };

            foreach (var deviceDirectoryPath in deviceDirectoryPaths)
            {
                if (string.IsNullOrEmpty(deviceDirectoryPath) || !Directory.Exists(deviceDirectoryPath))
                    continue;

                var deviceDirectory = new DirectoryInfo(deviceDirectoryPath);
                var deviceDefinitions = deviceDirectory.GetFiles("*.device.json");

                foreach (var deviceDefinition in deviceDefinitions)
                {
                    DeviceInfo deviceInfo;
                    using (StreamReader sr = deviceDefinition.OpenText())
                    {
                        deviceInfo = JsonUtility.FromJson<DeviceInfo>(sr.ReadToEnd());
                    }
                    deviceInfo.Directory = deviceDirectory.FullName;

                    m_Devices.Add(deviceInfo);
                }
            }
            m_Devices.Sort((x, y) => string.CompareOrdinal(x.Meta.friendlyName, y.Meta.friendlyName));
        }

        public DeviceInfo GetDevice(int index)
        {
            var deviceInfo =  m_Devices[index];

            if (!deviceInfo.LoadOverlayImage() && deviceInfo.Meta.overlayOffset == Vector4.zero)
            {
                deviceInfo.Meta.overlayOffset = new Vector4(40, 60, 40, 60);
            }

            return deviceInfo;
        }
    }
}
