using NUnit.Framework;
using Unity.DeviceSimulator.Editor.Tests.Utilities;
using UnityEditor;
using UnityEditor.DeviceSimulation;
using UnityEngine;

namespace Unity.DeviceSimulator.Editor.Tests.ScreenFunctionality
{
    public class ChangeFrameDelayTests
    {
        private ScreenSimulation m_Simulation;

        private Resolution m_OriginalResolution = new Resolution { width = 2688, height = 1242 };
        private Rect m_OriginalSafeArea = new Rect(132, 63, 2424, 1179);
        private Rect m_OriginalCutout = new Rect(2598, 308, 90, 626);
        private ScreenOrientation m_OriginalOrientation = ScreenOrientation.LandscapeRight;

        [SetUp]
        public void SetUp()
        {
            var playerSettings = new SimulationPlayerSettings();
            playerSettings.defaultOrientation = UIOrientation.LandscapeRight;

            m_Simulation = new ScreenSimulation(DeviceInfoLibrary.GetIphoneXMax(), playerSettings);
            m_Simulation.ApplyChanges();
        }

        [TearDown]
        public void TearDown()
        {
            m_Simulation?.Dispose();
        }

        [Test]
        public void OrientationChangeFrameDelayTest()
        {
            Screen.orientation = ScreenOrientation.LandscapeRight;

            Assert.AreEqual(m_OriginalResolution, Screen.currentResolution);
            Assert.AreEqual(m_OriginalSafeArea, Screen.safeArea);
            Assert.AreEqual(m_OriginalCutout, Screen.cutouts[0]);
            Assert.AreEqual(m_OriginalOrientation, Screen.orientation);

            Screen.orientation = ScreenOrientation.Portrait;
            m_Simulation.ApplyChanges();

            Assert.AreNotEqual(m_OriginalResolution, Screen.currentResolution);
            Assert.AreNotEqual(m_OriginalSafeArea, Screen.safeArea);
            Assert.AreNotEqual(m_OriginalCutout, Screen.cutouts[0]);
            Assert.AreNotEqual(m_OriginalOrientation, Screen.orientation);
        }

        [Test]
        public void ResolutionChangeFrameDelayTest()
        {
            Assert.AreEqual(m_OriginalResolution, Screen.currentResolution);
            Assert.AreEqual(m_OriginalSafeArea, Screen.safeArea);
            Assert.AreEqual(m_OriginalCutout, Screen.cutouts[0]);
            Assert.AreEqual(m_OriginalOrientation, Screen.orientation);

            Screen.SetResolution(500, 500, true);
            m_Simulation.ApplyChanges();

            Assert.AreNotEqual(m_OriginalResolution, Screen.currentResolution);
            Assert.AreNotEqual(m_OriginalSafeArea, Screen.safeArea);
            Assert.AreNotEqual(m_OriginalCutout, Screen.cutouts[0]);
        }
    }
}
