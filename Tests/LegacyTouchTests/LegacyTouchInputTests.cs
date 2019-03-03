using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.DeviceSimulator;

namespace Tests
{
    public class LegacyTouchInputTests
    {
        private DeviceInfo m_Device;
        private IInputProvider m_InputProvider;
        private ScreenSimulation m_ScreenSimulation;

        private class TestSimulatorWindow
        {
            public Action OnWindowFocus { get; set; }
            public Vector2 TargetSize { get; set; }
            public ScreenOrientation TargetOrientation { get; set; }
        }

        private class TestInput : IInputProvider
        {
            public Action<Quaternion> OnRotation { get; set; }
            public Action<TouchEvent> OnTouchEvent { get; set; }
            public Quaternion Rotation { get; set; }
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            var screen = new ScreenData()
            {
                dpi = 200,
                width = 500,
                height = 1000,
                orientations = new[]
                {
                    new OrientationData()
                    {
                        orientation = ScreenOrientation.Portrait,
                        safeArea = new Rect(0, 0, 500, 1000)
                    }
                    ,
                    new OrientationData()
                    {
                        orientation = ScreenOrientation.LandscapeLeft,
                        safeArea = new Rect(0, 0, 1000, 500)
                    },
                    new OrientationData()
                    {
                        orientation = ScreenOrientation.LandscapeRight,
                        safeArea = new Rect(0, 0, 1000, 500)
                    },
                    new OrientationData()
                    {
                        orientation = ScreenOrientation.PortraitUpsideDown,
                        safeArea = new Rect(0, 0, 500, 1000)
                    }
                }
            };

            m_Device = new DeviceInfo()
            {
                Screens = new[] {screen}
            };
        }

        [SetUp]
        public void SetUp()
        {
            m_InputProvider = new TestInput();
            m_ScreenSimulation = new ScreenSimulation(m_Device, m_InputProvider, new SimulationPlayerSettings());
        }

        [TearDown]
        public void TearDown()
        {
            // Making sure current touch is ended
            m_InputProvider.OnTouchEvent.Invoke(
                new TouchEvent()
                {
                    phase = TouchPhase.Ended,
                    position = Vector2.zero
                });
            m_ScreenSimulation.Dispose();
        }

        [UnityTest]
        [TestCase(ScreenOrientation.Portrait, 100f, 800f, ExpectedResult = null)]
        [TestCase(ScreenOrientation.PortraitUpsideDown, 400f , 200f, ExpectedResult = null)]
        [TestCase(ScreenOrientation.Landscape, 200f, 100f, ExpectedResult = null)]
        [TestCase(ScreenOrientation.LandscapeLeft, 200f, 100f, ExpectedResult = null)]
        [TestCase(ScreenOrientation.LandscapeRight, 800f, 400f, ExpectedResult = null)]
        public IEnumerator TouchSimulatedCorrectlyWithDifferentOrientations(ScreenOrientation orientation, float expectedPositionX, float expectedPositionY)
        {
            var position = new Vector2(100, 200);

            Screen.orientation = orientation;
            m_InputProvider.OnTouchEvent.Invoke(
                new TouchEvent()
                {
                    phase = TouchPhase.Began,
                    position = position
                });

            yield return null;

            Assert.AreEqual(1, Input.touchCount);
            Assert.AreEqual(new Vector2(expectedPositionX, expectedPositionY), Input.GetTouch(0).position);
        }

        [UnityTest]
        [TestCase(ScreenOrientation.Portrait, 120f, 1120f, ExpectedResult = null)]
        [TestCase(ScreenOrientation.PortraitUpsideDown, 480f, 280f, ExpectedResult = null)]
        [TestCase(ScreenOrientation.Landscape, 220f, 180f, ExpectedResult = null)]
        [TestCase(ScreenOrientation.LandscapeLeft, 220f, 180f, ExpectedResult = null)]
        [TestCase(ScreenOrientation.LandscapeRight, 880f, 720f, ExpectedResult = null)]
        public IEnumerator TouchSimulatedCorrectlyAfterChangingResolution(ScreenOrientation orientation, float expectedPositionX, float expectedPositionY)
        {
            var position = new Vector2(100, 200);

            Screen.orientation = orientation;
            Screen.SetResolution(Screen.currentResolution.width + 100, Screen.currentResolution.height + 400, false);

            m_InputProvider.OnTouchEvent.Invoke(
                new TouchEvent()
                {
                    phase = TouchPhase.Began,
                    position = position
                });

            yield return null;

            Assert.AreEqual(1, Input.touchCount);
            Assert.AreEqual(new Vector2(expectedPositionX, expectedPositionY), Input.GetTouch(0).position);
        }

        [UnityTest]
        [TestCase(ScreenOrientation.Portrait, 120f, 1120f, ExpectedResult = null)]
        [TestCase(ScreenOrientation.PortraitUpsideDown, 480f, 280f, ExpectedResult = null)]
        [TestCase(ScreenOrientation.Landscape, 280f, 120f, ExpectedResult = null)]
        [TestCase(ScreenOrientation.LandscapeLeft, 280f, 120f, ExpectedResult = null)]
        [TestCase(ScreenOrientation.LandscapeRight, 1120f, 480f, ExpectedResult = null)]
        public IEnumerator TouchSimulatedCorrectlyChangingOrientationAfterResolution(ScreenOrientation orientation, float expectedPositionX, float expectedPositionY)
        {
            var position = new Vector2(100, 200);

            Screen.SetResolution(Screen.currentResolution.width + 100, Screen.currentResolution.height + 400, false);
            Screen.orientation = orientation;

            m_InputProvider.OnTouchEvent.Invoke(
                new TouchEvent()
                {
                    phase = TouchPhase.Began,
                    position = position
                });

            yield return null;

            Assert.AreEqual(1, Input.touchCount);
            Assert.AreEqual(new Vector2(expectedPositionX, expectedPositionY), Input.GetTouch(0).position);
        }

        [UnityTest]
        public IEnumerator TouchSimulatedCorrectlyWhenChangingPosition()
        {
            var position = new Vector2(100, 200);

            Screen.orientation = ScreenOrientation.Portrait;
            m_InputProvider.OnTouchEvent.Invoke(
                new TouchEvent()
                {
                    phase = TouchPhase.Began,
                    position = position
                });

            yield return null;

            Assert.AreEqual(1, Input.touchCount);
            Assert.AreEqual(TouchPhase.Began, Input.GetTouch(0).phase);
            Assert.AreEqual(new Vector2(100f, 800f), Input.GetTouch(0).position);

            // Now move touch position
            position = new Vector2(150, 250);

            m_InputProvider.OnTouchEvent.Invoke(
                new TouchEvent()
                {
                    phase = TouchPhase.Moved,
                    position = position
                });

            yield return null;

            Assert.AreEqual(1, Input.touchCount);
            Assert.AreEqual(TouchPhase.Moved, Input.GetTouch(0).phase);
            Assert.AreEqual(new Vector2(150f, 750f), Input.GetTouch(0).position);
        }

        [UnityTest]
        public IEnumerator TouchBecomesStationaryWhenNotMoved()
        {
            var position = new Vector2(100, 200);

            Screen.orientation = ScreenOrientation.Portrait;
            m_InputProvider.OnTouchEvent.Invoke(
                new TouchEvent()
                {
                    phase = TouchPhase.Began,
                    position = position
                });

            yield return null;

            Assert.AreEqual(1, Input.touchCount);
            Assert.AreEqual(TouchPhase.Began, Input.GetTouch(0).phase);

            yield return null;

            Assert.AreEqual(1, Input.touchCount);
            Assert.AreEqual(TouchPhase.Stationary, Input.GetTouch(0).phase);
        }
    }
}
