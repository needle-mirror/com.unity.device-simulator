using NUnit.Framework;
using Unity.DeviceSimulator;
using UnityEngine;

namespace Tests
{
    public class ScreenResolutionTests
    {
        internal ScreenSimulation m_Simulation;
        internal TestInput m_InputTest;

        [SetUp]
        public void SetUp()
        {
            m_InputTest = new TestInput();
        }

        [TearDown]
        public void TearDown()
        {
            m_Simulation?.Dispose();
        }

        [Test]
        [TestCase(ScreenOrientation.Portrait, ScreenOrientation.LandscapeLeft, 500, 1000)]
        [TestCase(ScreenOrientation.LandscapeLeft, ScreenOrientation.PortraitUpsideDown, 500, 1000)]
        [TestCase(ScreenOrientation.PortraitUpsideDown, ScreenOrientation.LandscapeRight, 500, 1000)]
        [TestCase(ScreenOrientation.LandscapeRight, ScreenOrientation.Portrait, 500, 1000)]
        [TestCase(ScreenOrientation.PortraitUpsideDown, ScreenOrientation.Portrait, 500, 1000)]
        [TestCase(ScreenOrientation.LandscapeRight, ScreenOrientation.LandscapeLeft, 500, 1000)]
        [TestCase(ScreenOrientation.Portrait, ScreenOrientation.LandscapeLeft, 100, 10)]
        [TestCase(ScreenOrientation.LandscapeLeft, ScreenOrientation.PortraitUpsideDown, 100, 10)]
        [TestCase(ScreenOrientation.PortraitUpsideDown, ScreenOrientation.LandscapeRight, 100, 10)]
        [TestCase(ScreenOrientation.LandscapeRight, ScreenOrientation.Portrait, 100, 10)]
        [TestCase(ScreenOrientation.PortraitUpsideDown, ScreenOrientation.Portrait, 100, 10)]
        [TestCase(ScreenOrientation.LandscapeRight, ScreenOrientation.LandscapeLeft, 100, 10)]
        public void ScreenResolutionChangesCorrectlyWhenChangingOrientation(ScreenOrientation initOrientation, ScreenOrientation newOrientation, int screenWidth, int screenHeight)
        {
            var portraitResolution = new Vector2(screenWidth, screenHeight);
            var landscapeResolution = new Vector2(screenHeight, screenWidth);

            var testDevice = DeviceInfoLibrary.GetDeviceWithSupportedOrientations(ScreenTestUtilities.ExplicitOrientations, screenWidth, screenHeight);
            var testWindow = new TestWindow();
            m_Simulation = new ScreenSimulation(testDevice, m_InputTest, new SimulationPlayerSettings(), testWindow);

            Screen.orientation = initOrientation;
            Assert.AreEqual(initOrientation.IsLandscape() ? landscapeResolution : portraitResolution, testWindow.TargetSize);

            Screen.orientation = newOrientation;
            Assert.AreEqual(newOrientation.IsLandscape() ? landscapeResolution : portraitResolution, testWindow.TargetSize);
        }

        [Test]
        [TestCase(ScreenOrientation.Portrait, ScreenOrientation.LandscapeLeft, 32, 16)]
        [TestCase(ScreenOrientation.LandscapeLeft, ScreenOrientation.PortraitUpsideDown, 32, 16)]
        [TestCase(ScreenOrientation.PortraitUpsideDown, ScreenOrientation.LandscapeRight, 32, 16)]
        [TestCase(ScreenOrientation.LandscapeRight, ScreenOrientation.Portrait, 32, 16)]
        [TestCase(ScreenOrientation.PortraitUpsideDown, ScreenOrientation.Portrait, 32, 16)]
        [TestCase(ScreenOrientation.LandscapeRight, ScreenOrientation.LandscapeLeft, 32, 16)]
        [TestCase(ScreenOrientation.Portrait, ScreenOrientation.LandscapeLeft, 500, 1000)]
        [TestCase(ScreenOrientation.LandscapeLeft, ScreenOrientation.PortraitUpsideDown, 500, 1000)]
        [TestCase(ScreenOrientation.PortraitUpsideDown, ScreenOrientation.LandscapeRight, 500, 1000)]
        [TestCase(ScreenOrientation.LandscapeRight, ScreenOrientation.Portrait, 500, 1000)]
        [TestCase(ScreenOrientation.PortraitUpsideDown, ScreenOrientation.Portrait, 500, 1000)]
        [TestCase(ScreenOrientation.LandscapeRight, ScreenOrientation.LandscapeLeft, 500, 1000)]
        public void ScreenResolutionChangesCorrectlyWhenChangingResolutionAndOrientation(ScreenOrientation initOrientation, ScreenOrientation newOrientation, int newWidth, int newHeight)
        {
            var initResolution = new Vector2(newWidth, newHeight);
            var flippedResolution = new Vector2(newHeight, newWidth);
            var isFlipped = initOrientation.IsLandscape() ^ newOrientation.IsLandscape();

            var testDevice = DeviceInfoLibrary.GetDeviceWithSupportedOrientations(ScreenTestUtilities.ExplicitOrientations);
            var testWindow = new TestWindow();
            m_Simulation = new ScreenSimulation(testDevice, m_InputTest, new SimulationPlayerSettings(), testWindow);

            Screen.orientation = initOrientation;
            Screen.SetResolution(newWidth, newHeight, true);
            Assert.AreEqual(initResolution, testWindow.TargetSize);

            Screen.orientation = newOrientation;
            Assert.AreEqual(isFlipped ? flippedResolution : initResolution, testWindow.TargetSize);
        }
    }
}
