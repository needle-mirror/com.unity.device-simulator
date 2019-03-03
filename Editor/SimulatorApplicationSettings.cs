using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.DeviceSimulator
{
    internal class SimulatorApplicationSettingsUI
    {
        internal Foldout m_RootElement = null;

        private ApplicationSimulation m_ApplicationSimulation = null;

        public SimulatorApplicationSettingsUI(Foldout rootElement, ApplicationSimulation applicationSimulation)
        {
            m_RootElement = rootElement;
            m_ApplicationSimulation = applicationSimulation;

            InitUI();
        }

        private void InitUI()
        {
            var systemLanguageEnumField = m_RootElement.Q<EnumField>("application-system-language");
            systemLanguageEnumField.Init(SystemLanguage.Unknown);
            systemLanguageEnumField.SetValueWithoutNotify(m_ApplicationSimulation.ShimmedSystemLanguage);
            systemLanguageEnumField.RegisterValueChangedCallback((evt) => { m_ApplicationSimulation.ShimmedSystemLanguage = (SystemLanguage)evt.newValue; });

            var internetReachabilityEnumField = m_RootElement.Q<EnumField>("application-internet-reachability");
            internetReachabilityEnumField.Init(NetworkReachability.NotReachable);
            internetReachabilityEnumField.SetValueWithoutNotify(m_ApplicationSimulation.ShimmedInternetReachability);
            internetReachabilityEnumField.RegisterValueChangedCallback((evt) => { m_ApplicationSimulation.ShimmedInternetReachability = (NetworkReachability)evt.newValue; });

            var onLowMemoryButton = m_RootElement.Q<Button>("application-low-memory");
            onLowMemoryButton.clickable = new Clickable(() => m_ApplicationSimulation.OnLowMemory());
        }
    }
}
