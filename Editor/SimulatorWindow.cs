using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UIElements;

namespace Unity.DeviceSimulator
{
    [EditorWindowTitle(title = "Simulator", useTypeNameAsIconName = true)]
    internal class SimulatorWindow : PlayModeView, ISimulatorWindow
    {
        private SimulationState m_State = SimulationState.Enabled;
        private ScreenSimulation m_ScreenSimulation;
        private SystemInfoSimulation m_SystemInfoSimulation;

        private const string kJsonFileName = "SimulatorWindowStatesJsonFile.json";
        private const string kJsonFileEditorPrefKey = "SimulatorWindowStatesJsonFile";

        private InputProvider m_InputProvider;

        private DeviceDatabase m_DeviceDatabase;
        private DeviceInfo[] m_Devices;
        private int m_CurrentDeviceInfoIndex = -1;
        private DeviceInfo CurrentDeviceInfo => m_Devices[m_CurrentDeviceInfoIndex];

        private SimulatorJsonSerialization m_SimulatorJsonSerialization = null;

        private ToolbarMenu m_DeviceInfoMenu = null;
        private ToolbarButton m_DeviceRestart = null;

        private SimulatorControlPanel m_ControlPanel = null;
        private SimulatorPreviewPanel m_PreviewPanel = null;
        private SimulatorPlayerSettingsUI m_PlayerSettings = null;

        public Action OnWindowFocus { get; set; }

        public Vector2 TargetSize
        {
            set
            {
                targetSize = value;
                OnStateChanged();
            }
        }

        public ScreenOrientation TargetOrientation
        {
            set
            {
                if (m_PreviewPanel != null)
                    m_PreviewPanel.TargetOrientation = value;
                OnStateChanged();
            }
        }

        [MenuItem("Window/General/Device Simulator", false, 2000)]
        public static void ShowWindow()
        {
            SimulatorWindow window = GetWindow<SimulatorWindow>();
            window.Show();
        }

        void OnEnable()
        {
            autoRepaintOnSceneChange = true;

            var tileImage = AssetDatabase.LoadAssetAtPath<Texture2D>("packages/com.unity.device-simulator/Editor/icons/title.png");
            this.titleContent = new GUIContent("Simulator", tileImage);

            DeviceSimulatorInterfaces.InitializeDeviceSimulatorCallbacks();
            InitDeviceInfoList();

            m_SimulatorJsonSerialization = LoadStates();
            SetCurrentDeviceIndex();

            this.clearColor = Color.black;
            this.playModeViewName = "Device Simulator";
            this.showGizmos = false;
            this.targetDisplay = 0;
            this.renderIMGUI = true;
            this.targetSize = new Vector2(CurrentDeviceInfo.Screens[0].width, CurrentDeviceInfo.Screens[0].height);

            const string kPackagePath = "packages/com.unity.device-simulator/Editor";
            rootVisualElement.AddStyleSheetPath($"{kPackagePath}/stylesheets/styles_common.uss");
            rootVisualElement.AddStyleSheetPath($"{kPackagePath}/stylesheets/styles_{(EditorGUIUtility.isProSkin ? "dark" : "light")}.uss");

            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{kPackagePath}/uxmls/device_simulator_ui.uxml");
            asset.CloneTree(rootVisualElement);

            // We need to initialize SimulatorPlayerSettings before ScreenSimulation.
            m_PlayerSettings = new SimulatorPlayerSettingsUI(rootVisualElement.Q<Foldout>("player-settings"), CurrentDeviceInfo, m_SimulatorJsonSerialization);

            m_InputProvider = new InputProvider();
            if (m_SimulatorJsonSerialization != null)
                m_InputProvider.Rotation = m_SimulatorJsonSerialization.rotation; // We have to set the rotation here as we use it in ScreenSimulation constructor.

            InitSimulation();

            InitToolbar();
            m_ControlPanel = new
                SimulatorControlPanel(rootVisualElement.Q<VisualElement>("control-panel"), CurrentDeviceInfo, m_SystemInfoSimulation, m_ScreenSimulation, m_PlayerSettings.CurrentPlayerSettings);
            m_PreviewPanel = new SimulatorPreviewPanel(rootVisualElement.Q<VisualElement>("preview-panel"), m_InputProvider, CurrentDeviceInfo, m_SimulatorJsonSerialization)
            {
                TargetOrientation = m_ScreenSimulation.orientation,
                IsFullScreen = m_ScreenSimulation.fullScreen,
                OnPreview = this.RenderView
            };
        }

        private void InitSimulation()
        {
            m_ScreenSimulation?.Dispose();

            var playerSettings = m_PlayerSettings.CurrentPlayerSettings;

            m_ScreenSimulation = new ScreenSimulation(CurrentDeviceInfo, m_InputProvider, playerSettings, this);
            m_ScreenSimulation.OnFullScreenChanged += OnFullScreenChanged;

            m_SystemInfoSimulation?.Dispose();

            var settings = DeviceSimulatorSettingsProvider.LoadOrCreateSettings();
            var whitelistedAssemblies = new List<string>(settings.SystemInfoAssemblies);

            if (settings.SystemInfoDefaultAssembly)
                whitelistedAssemblies.Add("Assembly-CSharp.dll");

            m_SystemInfoSimulation = new SystemInfoSimulation(CurrentDeviceInfo, playerSettings, whitelistedAssemblies);

            DeviceSimulatorCallbacks.InvokeOnDeviceChange();
        }

        void Update()
        {
            bool simulationStateChanged = false;
            if (m_State == SimulationState.Disabled && GetMainPlayModeView() == this)
            {
                m_State = SimulationState.Enabled;
                simulationStateChanged = true;
                m_ScreenSimulation.Enable();
                m_SystemInfoSimulation.Enable();
                DeviceSimulatorCallbacks.InvokeOnDeviceChange();
            }
            else if (m_State == SimulationState.Enabled && GetMainPlayModeView() != this)
            {
                m_State = SimulationState.Disabled;
                simulationStateChanged = true;
                m_ScreenSimulation.Disable();
                m_SystemInfoSimulation.Disable();

                // Assumption here is that another Simulator instance will call OnDeviceChange event when it becomes MainPlayModeView
                // so we don't need to call it here, but if it's not another Simulator window then we need to call the event.
                if (GetMainPlayModeView().GetType() != typeof(SimulatorWindow))
                    DeviceSimulatorCallbacks.InvokeOnDeviceChange();
            }

            if (simulationStateChanged)
                m_PreviewPanel.OnSimulationStateChanged(m_State);
        }

        private void OnFocus()
        {
            this.SetFocus(true);
            OnWindowFocus?.Invoke();
        }

        private void OnDisable()
        {
            SaveStates();
            m_ScreenSimulation.Dispose();
            m_SystemInfoSimulation.Dispose();
        }

        private void OnStateChanged()
        {
            m_PreviewPanel?.OnStateChanged();
        }

        private void OnFullScreenChanged(bool fullScreen)
        {
            if (m_PreviewPanel != null)
            {
                m_PreviewPanel.IsFullScreen = fullScreen;
                m_PreviewPanel.OnStateChanged();
            }
        }

        private void SaveStates()
        {
            SimulatorJsonSerialization states = new SimulatorJsonSerialization()
            {
                scale = m_PreviewPanel.Scale,
                fitToScreenEnabled = m_PreviewPanel.FitToScreenEnabled,
                rotationDegree = m_PreviewPanel.RotationDegree,
                highlightSafeAreaEnabled = m_PreviewPanel.HighlightSafeAre,
                friendlyName = CurrentDeviceInfo.Meta.friendlyName,
                overrideDefaultPlayerSettings = m_PlayerSettings.OverrideDefaultPlayerSettings,
                customizedPlayerSettings = m_PlayerSettings.CustomizedPlayerSettings
            };

            var jsonString = JsonUtility.ToJson(states);
            if (string.IsNullOrEmpty(jsonString))
                return;

            var jsonFilePath = Path.Combine(Application.persistentDataPath, kJsonFileName);
            if (File.Exists(jsonFilePath))
                File.Delete(jsonFilePath);
            File.WriteAllText(jsonFilePath, jsonString);

            EditorPrefs.SetString(kJsonFileEditorPrefKey, jsonFilePath);
        }

        private SimulatorJsonSerialization LoadStates()
        {
            if (!EditorPrefs.HasKey(kJsonFileEditorPrefKey))
                return null;

            var jsonFilePath = EditorPrefs.GetString(kJsonFileEditorPrefKey);
            if (string.IsNullOrEmpty(jsonFilePath) || !File.Exists(jsonFilePath))
                return null;

            var jsonString = File.ReadAllText(jsonFilePath);
            if (string.IsNullOrEmpty(jsonString))
                return null;

            SimulatorJsonSerialization states = null;
            try
            {
                states = JsonUtility.FromJson<SimulatorJsonSerialization>(jsonString);
            }
            catch (Exception)
            {
                return null;
            }

            states.rotation = Quaternion.Euler(0, 0, 360 - states.rotationDegree);

            if (states.customizedPlayerSettings.androidGraphicsAPIs.Length == 0 || states.customizedPlayerSettings.iOSGraphicsAPIs.Length == 0)
                return null;

            return states;
        }

        private void InitDeviceInfoList()
        {
            m_DeviceDatabase = new DeviceDatabase();
            m_Devices = m_DeviceDatabase.GetDevices();

            Assert.AreNotEqual(0, m_Devices.Length, "No devices found!");
            m_CurrentDeviceInfoIndex = 0;
        }

        void SetCurrentDeviceIndex()
        {
            if (m_SimulatorJsonSerialization == null)
                return;

            for (int index = 0; index < m_Devices.Length; ++index)
            {
                if (m_Devices[index].Meta.friendlyName == m_SimulatorJsonSerialization.friendlyName)
                {
                    m_CurrentDeviceInfoIndex = index;
                    break;
                }
            }
        }

        private void InitToolbar()
        {
            var playModeViewTypeMenu = rootVisualElement.Q<ToolbarMenu>("playmode-view-menu");
            playModeViewTypeMenu.text = GetWindowTitle(GetType());

            var types = GetAvailableWindowTypes();
            foreach (var type in types)
            {
                var status = type.Key == GetType() ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal;
                playModeViewTypeMenu.menu.AppendAction(type.Value, HandleWindowSelection, HandleWindowSelection => status, type.Key);
            }

            m_DeviceInfoMenu = rootVisualElement.Q<ToolbarMenu>("device-info-menu");
            m_DeviceInfoMenu.text = CurrentDeviceInfo.Meta.friendlyName;
            UpdateDeviceInfoMenu();

            m_DeviceRestart = rootVisualElement.Q<ToolbarButton>("device-restart");
            m_DeviceRestart.clickable = new Clickable(RestartSimulation);
        }

        private void HandleWindowSelection(object typeData)
        {
            var type = (Type)((DropdownMenuAction)typeData).userData;
            if (type != null)
                SwapMainWindow(type);
        }

        public void HandleDeviceSelection(object userdata)
        {
            DropdownMenuAction action = (DropdownMenuAction)userdata;
            int index = m_DeviceInfoMenu.menu.MenuItems().IndexOf(action);
            if (index < 0)
                return;

            if (m_CurrentDeviceInfoIndex == index)
                return;

            m_CurrentDeviceInfoIndex = index;
            m_DeviceInfoMenu.menu.MenuItems().Clear();
            m_DeviceInfoMenu.text = CurrentDeviceInfo.Meta.friendlyName;
            UpdateDeviceInfoMenu();

            RestartSimulation();
            m_PlayerSettings.Update(CurrentDeviceInfo);
        }

        private void UpdateDeviceInfoMenu()
        {
            foreach (var deviceInfo in m_Devices)
            {
                var status = (deviceInfo.Meta.friendlyName == CurrentDeviceInfo.Meta.friendlyName) ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal;
                m_DeviceInfoMenu.menu.AppendAction(deviceInfo.Meta.friendlyName, HandleDeviceSelection, HandleDeviceSelection => status);
            }
        }

        private void RestartSimulation()
        {
            InitSimulation();

            m_ControlPanel.Update(CurrentDeviceInfo, m_SystemInfoSimulation, m_ScreenSimulation, m_PlayerSettings.CurrentPlayerSettings);
            m_PreviewPanel.Update(CurrentDeviceInfo, m_ScreenSimulation.fullScreen);
            m_PlayerSettings.Update(CurrentDeviceInfo);
        }
    }
}
