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
        private ApplicationSimulation m_ApplicationSimulation;

        private const string kJsonFileName = "SimulatorWindowStatesJsonFile.json";
        private const string kJsonFileEditorPrefKey = "SimulatorWindowStatesJsonFile";

        private InputProvider m_InputProvider;

        private DeviceDatabase m_DeviceDatabase;

        private int CurrentDeviceIndex
        {
            get => m_CurrentDeviceIndex;
            set
            {
                m_CurrentDeviceIndex = value;
                CurrentDeviceInfo = m_DeviceDatabase.GetDevice(m_CurrentDeviceIndex);
            }
        }
        private int m_CurrentDeviceIndex = -1;
        private DeviceInfo CurrentDeviceInfo;

        private string m_DeviceSearchContent = string.Empty;

        private SimulatorJsonSerialization m_SimulatorJsonSerialization = null;

        private VisualElement m_DeviceListMenu = null;
        private TextElement m_SelectedDeviceName = null;

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
            if (m_SimulatorJsonSerialization != null)
                m_Splitter.LeftPanelHidden = m_SimulatorJsonSerialization.controlPanelHidden;

            m_ControlPanel = new SimulatorControlPanel(rootVisualElement.Q<VisualElement>("control-panel"), CurrentDeviceInfo, m_SystemInfoSimulation,
                m_ScreenSimulation, m_ApplicationSimulation, playerSettings);

            m_PreviewPanel = new SimulatorPreviewPanel(rootVisualElement.Q<VisualElement>("preview-panel"), m_InputProvider, CurrentDeviceInfo, m_SimulatorJsonSerialization)
            {
                TargetOrientation = m_ScreenSimulation.orientation,
                IsFullScreen = m_ScreenSimulation.fullScreen,
                OnControlPanelHiddenChanged = HideControlPanel
            };
        }

        private void OnGUI()
        {
            if (Event.current.type == EventType.Repaint && GetMainPlayModeView() == this)
                m_PreviewPanel.PreviewTexture = RenderView(Event.current.mousePosition, false);
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

            SimulatorUtilities.CheckShimmedAssemblies(whitelistedAssemblies);

            m_SystemInfoSimulation = new SystemInfoSimulation(CurrentDeviceInfo, playerSettings, whitelistedAssemblies);

            // No need to reinitialize ApplicationSimulation.
            if (m_ApplicationSimulation == null)
                m_ApplicationSimulation = new ApplicationSimulation(CurrentDeviceInfo, whitelistedAssemblies);

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
                controlPanelHidden = m_PreviewPanel.ControlPanelHidden,
                scale = m_PreviewPanel.Scale,
                fitToScreenEnabled = m_PreviewPanel.FitToScreenEnabled,
                rotationDegree = m_PreviewPanel.RotationDegree,
                highlightSafeAreaEnabled = m_PreviewPanel.HighlightSafeAre,
                friendlyName = CurrentDeviceInfo.friendlyName
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
            CurrentDeviceIndex = 0;
        }

        void SetCurrentDeviceIndex()
        {
            if (m_SimulatorJsonSerialization == null)
                return;

            for (int index = 0; index < m_DeviceDatabase.m_Devices.Count; ++index)
            {
                if (m_DeviceDatabase.m_Devices[index].friendlyName == m_SimulatorJsonSerialization.friendlyName)
                {
                    CurrentDeviceIndex = index;
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

            m_DeviceRestart = rootVisualElement.Q<ToolbarButton>("reload-player-settings");
            m_DeviceRestart.clickable = new Clickable(RestartSimulation);

            m_DeviceListMenu = rootVisualElement.Q<VisualElement>("device-list-menu");
            m_DeviceListMenu.AddManipulator(new Clickable(ShowDeviceInfoList));

            m_SelectedDeviceName = m_DeviceListMenu.Q<TextElement>("selected-device-name");
            m_SelectedDeviceName.text = CurrentDeviceInfo.friendlyName;
        }

        private void HandleWindowSelection(object typeData)
        {
            var type = (Type)((DropdownMenuAction)typeData).userData;
            if (type != null)
                SwapMainWindow(type);
        }

        private void RestartSimulation()
        {
            var playerSettings = new SimulationPlayerSettings();

            InitSimulation(playerSettings);

            m_ControlPanel.Update(CurrentDeviceInfo, m_SystemInfoSimulation, m_ScreenSimulation, playerSettings);
            m_PreviewPanel.Update(CurrentDeviceInfo, m_ScreenSimulation.fullScreen);
        }

        private void ShowDeviceInfoList()
        {
            var rect = new Rect(m_DeviceListMenu.worldBound.position + new Vector2(1, m_DeviceListMenu.worldBound.height), new Vector2());
            var maximumVisibleDeviceCount = DeviceSimulatorUserSettingsProvider.LoadOrCreateSettings().MaximumVisibleDeviceCount;

            var deviceListPopup = new DeviceListPopup(m_DeviceDatabase.m_Devices, m_CurrentDeviceIndex, maximumVisibleDeviceCount, m_DeviceSearchContent);
            deviceListPopup.OnDeviceSelected += OnDeviceSelected;
            deviceListPopup.OnSearchInput += OnSearchInput;

            UnityEditor.PopupWindow.Show(rect, deviceListPopup);
        }

        private void OnDeviceSelected(int selectedDeviceIndex)
        {
            if (CurrentDeviceIndex == selectedDeviceIndex)
                return;

            CurrentDeviceIndex = selectedDeviceIndex;
            m_SelectedDeviceName.text = CurrentDeviceInfo.friendlyName;

            RestartSimulation();
        }

        private void OnSearchInput(string searchContent)
        {
            m_DeviceSearchContent = searchContent;
        }

        private void HideControlPanel(bool hidden)
        {
            m_Splitter.HideLeftPanel(hidden);
        }
    }
}
