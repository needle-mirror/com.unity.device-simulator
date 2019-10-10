using System.IO;
using Boo.Lang;
using UnityEngine;

namespace Unity.DeviceSimulator
{
    internal class DeviceHandle
    {
        public DeviceHandle(int id, string name, string directory)
        {
            Id = id;
            Name = name;
            Directory = directory;
        }

        public int Id { get; }
        public string Name { get; }
        public string Directory { get; }
    }

    internal class DeviceDatabase
    {
        private List<DeviceInfo> m_Devices = new List<DeviceInfo>();
        private readonly List<DeviceHandle> m_DeviceHandles = new List<DeviceHandle>();

        public DeviceDatabase()
        {
            Refresh();
        }

        public void Refresh()
        {
            m_Devices.Clear();
            m_DeviceHandles.Clear();

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

                    m_Devices.Add(deviceInfo);
                    m_DeviceHandles.Add(new DeviceHandle(m_Devices.Count - 1, deviceInfo.Meta.friendlyName, deviceDirectory.FullName));
                }
            }
            m_DeviceHandles.Sort((x, y) => string.CompareOrdinal(x.Name, y.Name));
        }

        public DeviceInfo GetDevice(DeviceHandle handle)
        {
            var deviceInfo =  m_Devices[handle.Id];

            if (!deviceInfo.LoadOverlayImage(handle) && deviceInfo.Meta.overlayOffset == Vector4.zero)
            {
                deviceInfo.Meta.overlayOffset = new Vector4(40, 60, 40, 60);
            }

            return deviceInfo;
        }

        public DeviceHandle[] GetDeviceHandles()
        {
            return m_DeviceHandles.ToArray();
        }
    }
}
