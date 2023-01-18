using NUnit.Framework;
using UnityEditor.DeviceSimulation;
using UnityEngine;
using Unity.DeviceSimulator.Editor.Tests.Utilities;

namespace Unity.DeviceSimulator.Editor.Tests.ScreenFunctionality
{
    internal class ScreenSimulationEvents
    {
        private DeviceInfo m_TestDevice;
        private ScreenSimulation m_Simulation;

        private int m_EventCounter;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            m_TestDevice = DeviceInfoLibrary.GetDeviceWithSupportedOrientations(new[]
            {
                ScreenOrientation.Portrait,
                ScreenOrientation.LandscapeLeft,
                ScreenOrientation.LandscapeRight,
                ScreenOrientation.PortraitUpsideDown
            });
        }

        [TearDown]
        public void TearDown()
        {
            m_Simulation?.Dispose();
        }

        [Test]
        public void OnOrientationChangedTest()
        {
            m_Simulation = new ScreenSimulation(m_TestDevice, new SimulationPlayerSettings());

            void Reset()
            {
                m_EventCounter = 0;
            }

            m_Simulation.OnOrientationChanged += () =>
            {
                m_EventCounter++;
            };

            // ScreenOrientation.Portrait is default

            Reset();
            Screen.orientation = ScreenOrientation.PortraitUpsideDown;
            m_Simulation.ApplyChanges();
            Assert.AreEqual(1, m_EventCounter);

            Reset();
            Screen.orientation = ScreenOrientation.Portrait;
            m_Simulation.ApplyChanges();
            Assert.AreEqual(1, m_EventCounter);

            Reset();
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            m_Simulation.ApplyChanges();
            Assert.AreEqual(1, m_EventCounter);

            Reset();
            Screen.orientation = ScreenOrientation.LandscapeRight;
            m_Simulation.ApplyChanges();
            Assert.AreEqual(1, m_EventCounter);

            // Screen.orientation = ScreenOrientation.Autorotation does not fire an event, nor should it.
        }
    }
}
