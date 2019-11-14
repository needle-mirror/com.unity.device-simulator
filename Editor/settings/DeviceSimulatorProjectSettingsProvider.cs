using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.DeviceSimulator
{
    internal class DeviceSimulatorProjectSettingsProvider : SettingsProvider
    {
        private DeviceSimulatorProjectSettingsProvider(string path, SettingsScope scopes, IEnumerable<string> keywords = null)
            : base(path, scopes, keywords)
        {
        }

        private static DeviceSimulatorProjectSettings lastSettings;

        private const string k_SettingsPath = "ProjectSettings/DeviceSimulatorSettings.asset";

        private SerializedObject SerializedSettings => new SerializedObject(LoadOrCreateSettings());

        [SettingsProvider]
        public static SettingsProvider CreateDeviceSimulatorSettingsProvider()
        {
            var provider = new DeviceSimulatorProjectSettingsProvider("Project/Device Simulator", SettingsScope.Project);

            provider.activateHandler = (searchContext, rootElement) =>
            {
                var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("packages/com.unity.device-simulator/Editor/uxmls/ui_project_settings.uxml");
                visualTree.CloneTree(rootElement);
                rootElement.Bind(provider.SerializedSettings);
            };

            return provider;
        }

        public static DeviceSimulatorProjectSettings LoadOrCreateSettings()
        {
            if (lastSettings != null)
                return lastSettings;

            DeviceSimulatorProjectSettings settings = ScriptableObject.CreateInstance<DeviceSimulatorProjectSettings>();;
            if (File.Exists(k_SettingsPath))
            {
                var assetJson = File.ReadAllText(k_SettingsPath);
                JsonUtility.FromJsonOverwrite(assetJson, settings);
            }

            lastSettings = settings;
            return settings;
        }

        private void SaveSettings()
        {
            if (lastSettings != null)
                File.WriteAllText(k_SettingsPath, JsonUtility.ToJson(lastSettings, true));
        }

        public override void OnDeactivate()
        {
            base.OnDeactivate();
            SaveSettings();
        }
    }
}
