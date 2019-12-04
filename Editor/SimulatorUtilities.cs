using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.DeviceSimulator
{
    internal enum RenderedScreenOrientation
    {
        Portrait = 1,
        PortraitUpsideDown = 2,
        LandscapeLeft = 3,
        LandscapeRight = 4,
    }

    internal enum ResolutionScalingMode
    {
        Disabled = 0,
        FixedDpi = 1
    }

    internal enum SimulationState{ Enabled, Disabled }

    internal class SimulatorJsonSerialization
    {
        public bool controlPanelHidden = false;
        public int scale = 20;
        public bool fitToScreenEnabled = true;
        public int rotationDegree = 0;
        public bool highlightSafeAreaEnabled = false;
        [NonSerialized]
        public Quaternion rotation = Quaternion.identity;
        public string friendlyName = string.Empty;
    }

    internal static class SimulatorUtilities
    {
        public static void SetTextureCoordinates(ScreenOrientation orientation, Vector2[] vertices)
        {
            Assert.IsTrue(vertices.Length == 4);

            // Check the orientation to set the UVs correctly.
            var uvs = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };

            int startPos = 0;
            switch (orientation)
            {
                case ScreenOrientation.Portrait:
                    startPos = 0;
                    break;
                case ScreenOrientation.LandscapeRight:
                    startPos = 1;
                    break;
                case ScreenOrientation.PortraitUpsideDown:
                    startPos = 2;
                    break;
                case ScreenOrientation.LandscapeLeft:
                    startPos = 3;
                    break;
            }

            for (int index = 0; index < 4; ++index)
            {
                var uvIndex = (index + startPos) % 4;
                vertices[index] = uvs[uvIndex];
            }
        }

        public static ScreenOrientation ToScreenOrientation(UIOrientation original)
        {
            switch (original)
            {
                case UIOrientation.Portrait:
                    return ScreenOrientation.Portrait;
                case UIOrientation.PortraitUpsideDown:
                    return ScreenOrientation.PortraitUpsideDown;
                case UIOrientation.LandscapeLeft:
                    return ScreenOrientation.LandscapeLeft;
                case UIOrientation.LandscapeRight:
                    return ScreenOrientation.LandscapeRight;
                case UIOrientation.AutoRotation:
                    return ScreenOrientation.AutoRotation;
            }
            throw new ArgumentException($"Unexpected value of UIOrientation {original}");
        }

        public static ScreenOrientation RotationToScreenOrientation(Quaternion rotation)
        {
            var angle = rotation.eulerAngles.z;
            ScreenOrientation orientation = ScreenOrientation.Portrait;
            if (angle > 315 || angle <= 45)
            {
                orientation = ScreenOrientation.Portrait;
            }
            else if (angle > 45 && angle <= 135)
            {
                orientation = ScreenOrientation.LandscapeRight;
            }
            else if (angle > 135 && angle <= 225)
            {
                orientation = ScreenOrientation.PortraitUpsideDown;
            }
            else if (angle > 225 && angle <= 315)
            {
                orientation = ScreenOrientation.LandscapeLeft;
            }
            return orientation;
        }

        public static bool IsLandscape(ScreenOrientation orientation)
        {
            if (orientation == ScreenOrientation.Landscape || orientation == ScreenOrientation.LandscapeLeft ||
                orientation == ScreenOrientation.LandscapeRight)
                return true;

            return false;
        }

        public static void CheckShimmedAssemblies(List<string> shimmedAssemblies)
        {
            if (shimmedAssemblies == null || shimmedAssemblies.Count == 0)
                return;

            shimmedAssemblies.RemoveAll(string.IsNullOrEmpty);

            const string dll = ".dll";
            for (int i = 0; i < shimmedAssemblies.Count; i++)
            {
                shimmedAssemblies[i] = shimmedAssemblies[i].ToLower();
                if (!shimmedAssemblies[i].EndsWith(dll))
                {
                    shimmedAssemblies[i] += dll;
                }
            }
        }

        public static bool ShouldShim(List<string> shimmedAssemblies)
        {
            if (shimmedAssemblies == null || shimmedAssemblies.Count == 0)
                return false;

            // Here we use StackTrace to trace where the call comes from, only shim if it comes from the white listed assemblies.
            // 4 in StackTrace stands for the frames that we want to trace back up from here, as below:
            // SimulatorUtilities.ShouldShim() <-- SystemInfoSimulation/ApplicationSimulation.ShouldShim() <-- ApplicationSimulation <-- Application <-- Where the APIs are called.
            var callingAssembly = new StackTrace(4).GetFrame(0).GetMethod().Module.ToString().ToLower();
            foreach (var assembly in shimmedAssemblies)
            {
                if (callingAssembly == assembly)
                    return true;
            }
            return false;
        }
    }
}
