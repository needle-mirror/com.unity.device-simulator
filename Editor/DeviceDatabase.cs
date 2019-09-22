using System.IO;
using Boo.Lang;
using UnityEditor;
using UnityEngine;

namespace Unity.DeviceSimulator
{
    internal class DeviceDatabase
    {
        private List<DeviceInfo> m_Devices = new List<DeviceInfo>();

        public DeviceDatabase()
        {
            Refresh();
        }

        public void Refresh()
        {
            m_Devices.Clear();

            var deviceInfoFilePath = Path.Combine("Packages", "com.unity.device-simulator", "DeviceDefinitions");
            foreach (var deviceInfoFiles in Directory.GetFiles(deviceInfoFilePath, "*.json", SearchOption.TopDirectoryOnly))
            {
                var deviceInfo = JsonUtility.FromJson<DeviceInfo>(File.ReadAllText(deviceInfoFiles));
                // TODO could improve in the future that only loads the texture while it's the current device.
                if (!string.IsNullOrEmpty(deviceInfo.Meta.overlay))
                {
                    deviceInfo.Meta.overlayImage = AssetDatabase.LoadAssetAtPath<Texture>(Path.Combine(deviceInfoFilePath, deviceInfo.Meta.overlay));
                }
                else
                {
                    deviceInfo.Meta.overlayOffset = new Vector4(40, 60, 40, 60);
                }

                m_Devices.Add(deviceInfo);
            }
        }

        public DeviceInfo[] GetDevices()
        {
            return m_Devices.ToArray();
        }
    }
}
