using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Unity.DeviceSimulator
{
    internal class SimulatorPlayerSettingsUI
    {
        private VisualElement m_RootElement = null;

        private DeviceInfo m_DeviceInfo = null;

        private Toggle m_OverrideDefaultPlayerSettings = null;
        private VisualElement m_CustomizedPlayerSettingsElement = null;

        private Toggle m_StartInFullscreen = null;

        private EnumField m_ResolutionScalingMode = null;
        private VisualElement m_DpiContainer = null;
        private SliderInt m_DpiSlider = null;
        private IntegerField m_DpiField = null;

        private EnumField m_DefaultOrientation = null;

        private Foldout m_AllowedOrienations = null;
        private Toggle m_AllowedPortrait = null;
        private Toggle m_AllowedPortraitUpsideDown = null;
        private Toggle m_AllowedLandscapeLeft = null;
        private Toggle m_AllowedLandscapeRight = null;

        private Toggle m_AutoGraphicsAPI = null;
        private VisualElement m_GraphicsAPIPlaceholder = null;
        private PopupField<GraphicsDeviceType> m_GraphicsAPIField = null;

        public bool OverrideDefaultPlayerSettings => m_OverrideDefaultPlayerSettings.value;

        private SimulationPlayerSettings m_CustomizedPlayerSettings = null;

        public SimulationPlayerSettings CustomizedPlayerSettings => m_CustomizedPlayerSettings;
        public SimulationPlayerSettings CurrentPlayerSettings => OverrideDefaultPlayerSettings ? m_CustomizedPlayerSettings : InitDefaultPlayerSettings();

        public SimulatorPlayerSettingsUI(VisualElement rootElement, DeviceInfo deviceInfo, SimulatorJsonSerialization states)
        {
            m_RootElement = rootElement;
            m_DeviceInfo = deviceInfo;
            Init(states);
        }

        private void Init(SimulatorJsonSerialization states)
        {
            InitPlayerSettings(states);

            bool overrideDefaultPlayerSettings = (states != null) ? states.overrideDefaultPlayerSettings : false;
            m_OverrideDefaultPlayerSettings = m_RootElement.Q<Toggle>("override-default-player-settings");
            m_OverrideDefaultPlayerSettings.RegisterValueChangedCallback(SetOverridePlayerSettings);
            m_OverrideDefaultPlayerSettings.SetValueWithoutNotify(overrideDefaultPlayerSettings);
            UpdateOverridePlayerSettingsStatus();

            m_CustomizedPlayerSettingsElement = m_RootElement.Q<VisualElement>("customized-player-settings");

            m_StartInFullscreen = m_RootElement.Q<Toggle>("android-start-in-fullscreen");
            m_StartInFullscreen.SetValueWithoutNotify(m_CustomizedPlayerSettings.androidStartInFullscreen);
            m_StartInFullscreen.RegisterValueChangedCallback((evt) => { m_CustomizedPlayerSettings.androidStartInFullscreen = evt.newValue; });

            m_ResolutionScalingMode = m_RootElement.Q<EnumField>("resolution-scaling-mode");
            m_ResolutionScalingMode.Init(ResolutionScalingMode.Disabled);
            m_ResolutionScalingMode.RegisterValueChangedCallback(SetResolutionScalingMode);
            m_ResolutionScalingMode.SetValueWithoutNotify(m_CustomizedPlayerSettings.resolutionScalingMode);

            #region DPI
            m_DpiContainer = m_RootElement.Q<VisualElement>("dpi-container");

            m_DpiSlider = m_DpiContainer.Q<SliderInt>("dpi-slider");
            m_DpiSlider.RegisterValueChangedCallback(SyncDpiField);
            m_DpiSlider.SetValueWithoutNotify(m_CustomizedPlayerSettings.targetDpi);

            m_DpiField = m_DpiContainer.Q<IntegerField>("dpi-field");
            m_DpiField.RegisterValueChangedCallback(SyncDpiSlider);
            m_DpiField.RegisterCallback<KeyDownEvent>(OnDpiFieldKeyDown);
            m_DpiField.SetValueWithoutNotify(m_CustomizedPlayerSettings.targetDpi);
            #endregion

            #region Orientation
            m_DefaultOrientation = m_RootElement.Q<EnumField>("default-screen-orientation");
            m_DefaultOrientation.Init(UIOrientation.AutoRotation);
            m_DefaultOrientation.RegisterValueChangedCallback(SetDefaultOrientation);
            m_DefaultOrientation.SetValueWithoutNotify(m_CustomizedPlayerSettings.defaultOrientation);

            m_AllowedOrienations = m_RootElement.Q<Foldout>("allowed-orientations");

            m_AllowedPortrait = m_AllowedOrienations.Q<Toggle>("orientation-allow-portrait");
            m_AllowedPortrait.SetValueWithoutNotify(m_CustomizedPlayerSettings.allowedPortrait);
            m_AllowedPortrait.RegisterValueChangedCallback((evt) => { m_CustomizedPlayerSettings.allowedPortrait = evt.newValue; });

            m_AllowedPortraitUpsideDown = m_AllowedOrienations.Q<Toggle>("orientation-allow-portrait-upside-down");
            m_AllowedPortraitUpsideDown.SetValueWithoutNotify(m_CustomizedPlayerSettings.allowedPortraitUpsideDown);
            m_AllowedPortraitUpsideDown.RegisterValueChangedCallback((evt) => { m_CustomizedPlayerSettings.allowedPortraitUpsideDown = evt.newValue; });

            m_AllowedLandscapeLeft = m_AllowedOrienations.Q<Toggle>("orientation-allow-landscape-left");
            m_AllowedLandscapeLeft.SetValueWithoutNotify(m_CustomizedPlayerSettings.allowedLandscapeLeft);
            m_AllowedLandscapeLeft.RegisterValueChangedCallback((evt) => { m_CustomizedPlayerSettings.allowedLandscapeLeft = evt.newValue; });

            m_AllowedLandscapeRight = m_AllowedOrienations.Q<Toggle>("orientation-allow-landscape-right");
            m_AllowedLandscapeRight.SetValueWithoutNotify(m_CustomizedPlayerSettings.allowedLandscapeRight);
            m_AllowedLandscapeRight.RegisterValueChangedCallback((evt) => { m_CustomizedPlayerSettings.allowedLandscapeRight = evt.newValue; });
            #endregion

            #region Graphics API
            m_AutoGraphicsAPI = m_RootElement.Q<Toggle>("auto-graphics-api");
            m_AutoGraphicsAPI.SetValueWithoutNotify(m_CustomizedPlayerSettings.autoGraphicsAPI);
            m_AutoGraphicsAPI.RegisterValueChangedCallback(SetAutoGraphicsAPI);

            m_GraphicsAPIPlaceholder = m_RootElement.Q<VisualElement>("graphics-api-placeholder");
            m_GraphicsAPIPlaceholder.SetEnabled(!m_CustomizedPlayerSettings.autoGraphicsAPI);
            #endregion

            UpdateCustomizedPlayerSettings(m_OverrideDefaultPlayerSettings.value);
            UpdateStartInFullScreen();
            UpdateResolutionScalingMode(m_CustomizedPlayerSettings.resolutionScalingMode);
            UpdateAllowedOrientations(m_CustomizedPlayerSettings.defaultOrientation);
            UpdateGraphicsAPI();
        }

        private void InitPlayerSettings(SimulatorJsonSerialization states)
        {
            // Initialize customized player settings.
            if (states != null)
            {
                m_CustomizedPlayerSettings = states.customizedPlayerSettings;
            }
            else
            {
                m_CustomizedPlayerSettings = new SimulationPlayerSettings()
                {
                    targetDpi = (int)m_DeviceInfo.Screens[0].dpi
                };
            }
        }

        internal static SimulationPlayerSettings InitDefaultPlayerSettings()
        {
            var defaultPlayerSettings = new SimulationPlayerSettings();

            var serializedSettings = PlayerSettings.GetSerializedObject();
            serializedSettings.Update();

            defaultPlayerSettings.resolutionScalingMode = (ResolutionScalingMode)serializedSettings.FindProperty("resolutionScalingMode").intValue;
            defaultPlayerSettings.targetDpi = serializedSettings.FindProperty("targetPixelDensity").intValue;
            defaultPlayerSettings.androidStartInFullscreen = serializedSettings.FindProperty("androidStartInFullscreen").boolValue;

            defaultPlayerSettings.defaultOrientation = PlayerSettings.defaultInterfaceOrientation;
            defaultPlayerSettings.allowedPortrait = PlayerSettings.allowedAutorotateToPortrait;
            defaultPlayerSettings.allowedPortraitUpsideDown = PlayerSettings.allowedAutorotateToPortraitUpsideDown;
            defaultPlayerSettings.allowedLandscapeLeft = PlayerSettings.allowedAutorotateToLandscapeLeft;
            defaultPlayerSettings.allowedLandscapeRight = PlayerSettings.allowedAutorotateToLandscapeRight;

            if (!PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android))
                defaultPlayerSettings.androidGraphicsAPIs = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            if (!PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.iOS))
                defaultPlayerSettings.iOSGraphicsAPIs =  PlayerSettings.GetGraphicsAPIs(BuildTarget.iOS);

            return defaultPlayerSettings;
        }

        private void UpdateOverridePlayerSettingsStatus()
        {
            var buildTargetGroup = BuildTargetGroup.Unknown;
            var buildTarget = BuildTarget.NoTarget;

            if (m_DeviceInfo.IsAndroidDevice())
            {
                buildTargetGroup = BuildTargetGroup.Android;
                buildTarget = BuildTarget.Android;
            }
            else if (m_DeviceInfo.IsiOSDevice())
            {
                buildTargetGroup = BuildTargetGroup.iOS;
                buildTarget = BuildTarget.iOS;
            }

            bool isTargetSupported = BuildPipeline.IsBuildTargetSupported(buildTargetGroup, buildTarget);
            if (isTargetSupported)
            {
                m_OverrideDefaultPlayerSettings.SetEnabled(true);
            }
            else
            {
                m_OverrideDefaultPlayerSettings.SetEnabled(false);
                m_OverrideDefaultPlayerSettings.SetValueWithoutNotify(true);
            }
        }

        private void SetOverridePlayerSettings(ChangeEvent<bool> evt)
        {
            UpdateCustomizedPlayerSettings(evt.newValue);
        }

        private void UpdateCustomizedPlayerSettings(bool overrideDefaultPlayerSettings)
        {
            m_CustomizedPlayerSettingsElement.SetEnabled(overrideDefaultPlayerSettings);
        }

        private void UpdateStartInFullScreen()
        {
            if (m_DeviceInfo.IsAndroidDevice())
            {
                m_StartInFullscreen.style.visibility = Visibility.Visible;
                m_StartInFullscreen.style.position = Position.Relative;
            }
            else
            {
                m_StartInFullscreen.style.visibility = Visibility.Hidden;
                m_StartInFullscreen.style.position = Position.Absolute;
            }
        }

        private void SetResolutionScalingMode(ChangeEvent<Enum> evt)
        {
            m_CustomizedPlayerSettings.resolutionScalingMode = (ResolutionScalingMode)evt.newValue;
            UpdateResolutionScalingMode(m_CustomizedPlayerSettings.resolutionScalingMode);
        }

        private void UpdateResolutionScalingMode(ResolutionScalingMode scalingMode)
        {
            bool isScalingDisabled = scalingMode == ResolutionScalingMode.Disabled;
            m_DpiContainer.style.visibility = isScalingDisabled ? Visibility.Hidden : Visibility.Visible;
            m_DpiContainer.style.position = isScalingDisabled ? Position.Absolute : Position.Relative;
        }

        private void SyncDpiField(ChangeEvent<int> evt)
        {
            m_CustomizedPlayerSettings.targetDpi = evt.newValue;
            m_DpiField.SetValueWithoutNotify(m_CustomizedPlayerSettings.targetDpi);
        }

        private void SyncDpiSlider(ChangeEvent<int> evt)
        {
            m_CustomizedPlayerSettings.targetDpi = GetValidDpi(evt.newValue);
            m_DpiSlider.SetValueWithoutNotify(m_CustomizedPlayerSettings.targetDpi);
        }

        private void OnDpiFieldKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Return)
                return;

            var validValue = GetValidDpi(m_DpiField.value);
            if (validValue != m_DpiField.value)
                m_DpiField.SetValueWithoutNotify(validValue);
        }

        private int GetValidDpi(int dpi)
        {
            return Math.Max(30, Math.Min(1000, dpi));
        }

        private void SetDefaultOrientation(ChangeEvent<Enum> evt)
        {
            m_CustomizedPlayerSettings.defaultOrientation = (UIOrientation)evt.newValue;
            UpdateAllowedOrientations(m_CustomizedPlayerSettings.defaultOrientation);
        }

        private void UpdateAllowedOrientations(UIOrientation orientation)
        {
            bool isAutoRotation = (orientation == UIOrientation.AutoRotation);
            m_AllowedOrienations.style.visibility = isAutoRotation ? Visibility.Visible : Visibility.Hidden;
            m_AllowedOrienations.style.position = isAutoRotation ? Position.Relative : Position.Absolute;
        }

        private void SetAutoGraphicsAPI(ChangeEvent<bool> evt)
        {
            m_CustomizedPlayerSettings.autoGraphicsAPI = evt.newValue;
            m_GraphicsAPIPlaceholder.SetEnabled(!m_CustomizedPlayerSettings.autoGraphicsAPI);

            // Update the CustomizedPlayerSettings.
            if (m_DeviceInfo.IsAndroidDevice())
            {
                m_CustomizedPlayerSettings.androidGraphicsAPIs = m_CustomizedPlayerSettings.autoGraphicsAPI ?
                    new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 } :
                new[] { m_GraphicsAPIField.value };
            }
            else if (m_DeviceInfo.IsiOSDevice())
            {
                m_CustomizedPlayerSettings.iOSGraphicsAPIs = m_CustomizedPlayerSettings.autoGraphicsAPI ?
                    new[] { GraphicsDeviceType.Metal } :
                new[] { m_GraphicsAPIField.value };
            }
        }

        void UpdateGraphicsAPI()
        {
            m_GraphicsAPIPlaceholder.Clear();

            List<GraphicsDeviceType> graphicsAPIList = null;
            GraphicsDeviceType initGraphicsDeviceType;
            if (m_DeviceInfo.IsAndroidDevice())
            {
                graphicsAPIList = new List<GraphicsDeviceType> { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3, GraphicsDeviceType.OpenGLES2 };
                initGraphicsDeviceType = m_CustomizedPlayerSettings.androidGraphicsAPIs[0];
            }
            else if (m_DeviceInfo.IsiOSDevice())
            {
                graphicsAPIList = new List<GraphicsDeviceType> { GraphicsDeviceType.Metal, GraphicsDeviceType.OpenGLES3, GraphicsDeviceType.OpenGLES2 };
                initGraphicsDeviceType = m_CustomizedPlayerSettings.iOSGraphicsAPIs[0];
            }
            else
                return;

            // We have to use PopupField to only add a subset of GraphicsDeviceType, and PopupField has no UXML support.
            // Also we can't modify the values of PopupField, so we have to create it every time when we're switching devices.
            m_GraphicsAPIField = new PopupField<GraphicsDeviceType>("Graphics API", graphicsAPIList, initGraphicsDeviceType)
            {
                style = { width = 270 }
            };
            m_GraphicsAPIField.RegisterCallback<ChangeEvent<GraphicsDeviceType>>(SetGraphicsAPI);

            m_GraphicsAPIPlaceholder.Add(m_GraphicsAPIField);
        }

        private void SetGraphicsAPI(ChangeEvent<GraphicsDeviceType> evt)
        {
            if (m_DeviceInfo.IsAndroidDevice())
                m_CustomizedPlayerSettings.androidGraphicsAPIs = new[] { evt.newValue };
            else if (m_DeviceInfo.IsiOSDevice())
                m_CustomizedPlayerSettings.iOSGraphicsAPIs = new[] { evt.newValue };
        }

        public void Update(DeviceInfo deviceInfo)
        {
            m_DeviceInfo = deviceInfo;
            UpdateOverridePlayerSettingsStatus();
            UpdateCustomizedPlayerSettings(m_OverrideDefaultPlayerSettings.value);
            UpdateStartInFullScreen();
            UpdateGraphicsAPI();
        }
    }
}
