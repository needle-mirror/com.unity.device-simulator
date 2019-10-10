using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.DeviceSimulator
{
    internal class DeviceSimulatorUserSettingsProvider : SettingsProvider
    {
        private TextField m_CustomizedDeviceDirectoryField = null;

        private static DeviceSimulatorUserSettings s_Settings;

        private static DeviceSimulatorUserSettingsProvider s_Provider = null;

        private const string k_DeviceDirectoryPreferenceKey = "DeviceSimulatorDeviceDirectory";

        private SerializedObject SerializedSettings => new SerializedObject(LoadOrCreateSettings());

        public DeviceSimulatorUserSettingsProvider(string path, SettingsScope scopes, IEnumerable<string> keywords = null)
            : base(path, scopes, keywords)
        {
        }

        [SettingsProvider]
        public static SettingsProvider CreateDeviceSimulatorSettingsProvider()
        {
            var provider = new DeviceSimulatorUserSettingsProvider("Preferences/Device Simulator", SettingsScope.User);

            provider.activateHandler = (searchContext, rootElement) =>
            {
                var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("packages/com.unity.device-simulator/Editor/uxmls/user_settings.uxml");
                visualTree.CloneTree(rootElement);

                var textField = rootElement.Q<TextField>("customized-device-directory");
                textField.isDelayed = true;
                textField.SetValueWithoutNotify(LoadOrCreateSettings().DeviceDirectory);
                textField.RegisterValueChangedCallback(SetCustomizedDeviceDirectory);
                provider.m_CustomizedDeviceDirectoryField = textField;

                var button = rootElement.Q<Button>("set-customized-device-directory");
                button.clickable = new Clickable(SetCustomizedDeviceDirectory);
            };

            s_Provider = provider;
            return provider;
        }

        private static void SetCustomizedDeviceDirectory(ChangeEvent<string> evt)
        {
            // We allow users to set the directory to empty.
            var directory = evt.newValue;
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Debug.LogWarning($"Input device directory '{directory}' is invalid.");
                return;
            }

            LoadOrCreateSettings().DeviceDirectory = directory;
        }

        private static void SetCustomizedDeviceDirectory()
        {
            var settings = LoadOrCreateSettings();

            var directory = EditorUtility.OpenFolderPanel("Select directory", settings.DeviceDirectory, String.Empty);
            if (string.IsNullOrEmpty(directory))
                return;

            settings.DeviceDirectory = directory;
            s_Provider.m_CustomizedDeviceDirectoryField.SetValueWithoutNotify(directory);
        }

        public static DeviceSimulatorUserSettings LoadOrCreateSettings()
        {
            if (s_Settings != null)
                return s_Settings;

            DeviceSimulatorUserSettings settings = ScriptableObject.CreateInstance<DeviceSimulatorUserSettings>();

            var directory = EditorPrefs.GetString(k_DeviceDirectoryPreferenceKey, "");
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                settings.DeviceDirectory = directory;

            s_Settings = settings;
            return settings;
        }

        private void SaveSettings()
        {
            if (s_Settings != null)
                EditorPrefs.SetString(k_DeviceDirectoryPreferenceKey, s_Settings.DeviceDirectory);
        }

        public override void OnDeactivate()
        {
            base.OnDeactivate();
            SaveSettings();
        }
    }
}
