using System;
using System.Collections.Generic;
using Unity.DeviceSimulator;
using UnityEngine;

namespace Tests
{
    internal class DeviceInfoLibrary
    {
        public static DeviceInfo GetDeviceWithSupportedOrientations(ScreenOrientation[] orientations, int screenWidth = 500, int screenHeight = 1000, float screenDpi = 200)
        {
            if (orientations.Length > 4)
                throw new ArgumentException("There are 4 possible screen orientations");

            var screen = new ScreenData()
            {
                dpi = screenDpi,
                width = screenWidth,
                height = screenHeight,
                orientations = new Dictionary<ScreenOrientation, OrientationDependentData>()
            };

            for (int i = 0; i < orientations.Length; i++)
            {
                screen.orientations.Add(orientations[i], Orientations[i]);
            }

            var device = new DeviceInfo()
            {
                Screens = new[] {screen}
            };
            return device;
        }

        public static OrientationDependentData[] Orientations =
        {
            new OrientationDependentData() {safeArea = new Rect(100, 100, 100, 100)},
            new OrientationDependentData() {safeArea = new Rect(200, 200, 200, 200)},
            new OrientationDependentData() {safeArea = new Rect(300, 300, 300, 300)},
            new OrientationDependentData() {safeArea = new Rect(400, 400, 400, 400)}
        };
    }
}
