using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.DeviceSimulator
{
    internal class DeviceSimulatorSettingsProvider : SettingsProvider
    {
        public DeviceSimulatorSettingsProvider(string path, SettingsScope scopes, IEnumerable<string> keywords = null) : base(path, scopes, keywords)
        {
        }

        private static DeviceSimulatorSettings lastSettings;

        private const string k_SettingsPath = "ProjectSettings/DeviceSimulatorSettings.json";

        public SerializedObject SerializedSettings => new SerializedObject(LoadOrCreateSettings());

        [SettingsProvider]
        public static SettingsProvider CreateDeviceSimulatorSettingsProvider()
        {
            var provider = new DeviceSimulatorSettingsProvider("Project/Device Simulator", SettingsScope.Project);

            provider.activateHandler = (searchContext, rootElement) =>
            {
                const string kPackagePath = "packages/com.unity.device-simulator/Editor";
                rootElement.AddStyleSheetPath($"{kPackagePath}/stylesheets/project_settings.uss");
                var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{kPackagePath}/uxmls/project_settings.uxml");
                visualTree.CloneTree(rootElement);
                rootElement.Bind(provider.SerializedSettings);
            };

            return provider;
        }

        public static DeviceSimulatorSettings LoadOrCreateSettings()
        {
            if (lastSettings != null)
                return lastSettings;

            DeviceSimulatorSettings settings = ScriptableObject.CreateInstance<DeviceSimulatorSettings>();;
            if (File.Exists(k_SettingsPath))
            {
                var assetJson = File.ReadAllText(k_SettingsPath);
                JsonUtility.FromJsonOverwrite(assetJson, settings);
            }
            else
            {
                settings.SystemInfoDefaultAssembly = true;
                settings.SystemInfoAssemblies = new string[0];
            }
            lastSettings = settings;
            return settings;
        }

        private static void SaveSettings()
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
