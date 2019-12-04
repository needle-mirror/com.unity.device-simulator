using UnityEngine.UIElements;

namespace Unity.DeviceSimulator
{
    internal class SimulatorControlPanel
    {
        private VisualElement m_RootElement = null;

        // Controls for device specifications.
        private Label m_OS = null;
        private Label m_Chipset = null;
        private Label m_CPU = null;
        private Label m_GPU = null;
        private Label m_Resolution = null;

        private SimulatorScreenSettingsUI m_SimulatorScreenSettings = null;

        private SimulatorApplicationSettingsUI m_SimulatorApplicationSettings = null;

        public SimulatorControlPanel(VisualElement rootElement, DeviceInfo deviceInfo, SystemInfoSimulation systemInfoSimulation, ScreenSimulation screenSimulation,
                                     ApplicationSimulation applicationSimulation,
                                     SimulationPlayerSettings playerSettings)
        {
            m_RootElement = rootElement;

            InitDeviceSpecifications();
            UpdateDeviceSpecifications(deviceInfo, systemInfoSimulation);

            m_SimulatorScreenSettings = new SimulatorScreenSettingsUI(m_RootElement.Q<VisualElement>("screen-settings"), deviceInfo, screenSimulation, playerSettings);
            m_SimulatorApplicationSettings = new SimulatorApplicationSettingsUI(m_RootElement, applicationSimulation);

            InitDeviceSimulatorExtensions();
        }

        private void InitDeviceSpecifications()
        {
            m_OS = m_RootElement.Q<Label>("device_os");
            m_Chipset = m_RootElement.Q<Label>("device_chipset");
            m_CPU = m_RootElement.Q<Label>("device_cpu");
            m_GPU = m_RootElement.Q<Label>("device_gpu");
            m_Resolution = m_RootElement.Q<Label>("device_resolution");
        }

        private void InitDeviceSimulatorExtensions()
        {
            foreach (var extension in DeviceSimulatorInterfaces.s_DeviceSimulatorExtensions)
            {
                var foldout = new Foldout()
                {
                    text = extension.extensionTitle,
                    value = false
                };
                foldout.AddToClassList("unity-device-simulator__control-panel_foldout");

                m_RootElement.Add(foldout);
                extension.OnExtendDeviceSimulator(foldout);
            }
        }

        // Only gets called during initialization and switching device.
        public void Update(DeviceInfo deviceInfo, SystemInfoSimulation systemInfoSimulation, ScreenSimulation screenSimulation, SimulationPlayerSettings playerSettings)
        {
            if (deviceInfo == null)
                return;

            UpdateDeviceSpecifications(deviceInfo, systemInfoSimulation);
            m_SimulatorScreenSettings.Update(deviceInfo, screenSimulation, playerSettings);
        }

        private void UpdateDeviceSpecifications(DeviceInfo deviceInfo, SystemInfoSimulation systemInfoSimulation)
        {
            m_OS.text = "OS: " + (string.IsNullOrEmpty(deviceInfo.SystemInfo.operatingSystem) ? "N/A" : deviceInfo.SystemInfo.operatingSystem);
            m_CPU.text = "CPU: " + (string.IsNullOrEmpty(deviceInfo.SystemInfo.processorType) ? "N/A" : deviceInfo.SystemInfo.processorType);
            m_GPU.text = "GPU: " + (systemInfoSimulation.GraphicsDependentData == null ? "N/A" : systemInfoSimulation.GraphicsDependentData.graphicsDeviceType.ToString());
            m_Resolution.text = $"Resolution: {deviceInfo.Screens[0].width} x {deviceInfo.Screens[0].height}";
        }
    }
}
