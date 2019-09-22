using System;
using UnityEngine;

namespace Unity.DeviceSimulator
{
    internal interface ISimulatorWindow
    {
        Action OnWindowFocus { get; set; }
        Vector2 TargetSize { set; }
        ScreenOrientation TargetOrientation { set; }
    }
}
