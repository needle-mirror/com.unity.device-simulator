using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UIElements;

namespace Unity.DeviceSimulator
{
    [EditorWindowTitle(title = "Simulator", useTypeNameAsIconName = true)]
    internal class SimulatorWindow : PlayModeView, ISimulatorWindow, IHasCustomMenu
    {
        private SimulationState m_State = SimulationState.Enabled;
        private ScreenSimulation m_ScreenSimulation;
        private SystemInfoSimulation m_SystemInfoSimulation;

        private const string kJsonFileName = "SimulatorWindowStatesJsonFile.json";
        private const string kJsonFileEditorPrefKey = "SimulatorWindowStatesJsonFile";

        private InputProvider m_InputProvider;

        private DeviceDatabase m_DeviceDatabase;
        private int CurrentDeviceHandleIndex
        {
            get => m_CurrentDeviceHandleIndex;
            set
            {
                m_CurrentDeviceHandleIndex = value;
                CurrentDeviceInfo = m_DeviceDatabase.GetDevice(m_CurrentDeviceHandleIndex);
            }
        }

        private int m_CurrentDeviceHandleIndex = -1;
        private DeviceInfo CurrentDeviceInfo;

        private SimulatorJsonSerialization m_SimulatorJsonSerialization = null;

        private ToolbarMenu m_DeviceInfoMenu = null;
        private ToolbarButton m_DeviceRestart = null;

        private TwoPaneSplitView m_Splitter = null;
        private SimulatorControlPanel m_ControlPanel = null;
        private SimulatorPreviewPanel m_PreviewPanel = null;

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

        private void LoadRenderDoc()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                RenderDoc.Load();
                ShaderUtil.RecreateGfxDevice();
            }
        }

        public virtual void AddItemsToMenu(GenericMenu menu)
        {
            if (RenderDoc.IsInstalled() && !RenderDoc.IsLoaded())
            {
                menu.AddItem(EditorGUIUtility.TrTextContent(RenderDocUtil.loadRenderDocLabel), false, LoadRenderDoc);
            }
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

            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{kPackagePath}/uxmls/ui_device_simulator.uxml");
            asset.CloneTree(rootVisualElement);

            // We need to initialize SimulatorPlayerSettings before ScreenSimulation.
            var playerSettings = new SimulationPlayerSettings();

            m_InputProvider = new InputProvider();
            if (m_SimulatorJsonSerialization != null)
                m_InputProvider.Rotation = m_SimulatorJsonSerialization.rotation; // We have to set the rotation here as we use it in ScreenSimulation constructor.

            InitSimulation(playerSettings);

            InitToolbar();
            m_Splitter = rootVisualElement.Q<TwoPaneSplitView>("splitter");

            m_ControlPanel = new SimulatorControlPanel(rootVisualElement.Q<VisualElement>("control-panel"), CurrentDeviceInfo, m_SystemInfoSimulation, m_ScreenSimulation, playerSettings);
            m_PreviewPanel = new SimulatorPreviewPanel(rootVisualElement.Q<VisualElement>("preview-panel"), m_InputProvider, CurrentDeviceInfo, m_SimulatorJsonSerialization)
            {
                TargetOrientation = m_ScreenSimulation.orientation,
                IsFullScreen = m_ScreenSimulation.fullScreen,
                OnPreview = this.RenderView,
                OnControlPanelHiddenChanged = HideControlPanel
            };
        }

        private void InitSimulation(SimulationPlayerSettings playerSettings)
        {
            m_ScreenSimulation?.Dispose();

            m_ScreenSimulation = new ScreenSimulation(CurrentDeviceInfo, m_InputProvider, playerSettings, this);
            m_ScreenSimulation.OnFullScreenChanged += OnFullScreenChanged;

            m_SystemInfoSimulation?.Dispose();

            var settings = DeviceSimulatorProjectSettingsProvider.LoadOrCreateSettings();
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
                friendlyName = CurrentDeviceInfo.Meta.friendlyName
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

            return states;
        }

        private void InitDeviceInfoList()
        {
            m_DeviceDatabase = new DeviceDatabase();

            Assert.AreNotEqual(0, m_DeviceDatabase.m_Devices.Count, "No devices found!");
            CurrentDeviceHandleIndex = 0;
        }

        void SetCurrentDeviceIndex()
        {
            if (m_SimulatorJsonSerialization == null)
                return;

            for (int index = 0; index < m_DeviceDatabase.m_Devices.Count; ++index)
            {
                if (m_DeviceDatabase.m_Devices[index].Meta.friendlyName == m_SimulatorJsonSerialization.friendlyName)
                {
                    CurrentDeviceHandleIndex = index;
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

            m_DeviceRestart = rootVisualElement.Q<ToolbarButton>("reload-player-settings");
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

            if (CurrentDeviceHandleIndex == index)
                return;

            CurrentDeviceHandleIndex = index;
            m_DeviceInfoMenu.menu.MenuItems().Clear();
            m_DeviceInfoMenu.text = CurrentDeviceInfo.Meta.friendlyName;
            UpdateDeviceInfoMenu();

            RestartSimulation();
        }

        private void UpdateDeviceInfoMenu()
        {
            foreach (var deviceInfo in m_DeviceDatabase.m_Devices)
            {
                var status = (deviceInfo.Meta.friendlyName == CurrentDeviceInfo.Meta.friendlyName) ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal;
                m_DeviceInfoMenu.menu.AppendAction(deviceInfo.Meta.friendlyName, HandleDeviceSelection, HandleDeviceSelection => status);
            }
        }

        private void RestartSimulation()
        {
            var playerSettings = new SimulationPlayerSettings();

            InitSimulation(playerSettings);

            m_ControlPanel.Update(CurrentDeviceInfo, m_SystemInfoSimulation, m_ScreenSimulation, playerSettings);
            m_PreviewPanel.Update(CurrentDeviceInfo, m_ScreenSimulation.fullScreen);
        }

        private void HideControlPanel(bool hidden)
        {
            m_Splitter.HideLeftPanel(hidden);
        }
    }
}
