using System;
using UnityEngine;

namespace Unity.DeviceSimulator
{
    internal class LegacyInputBackend : IInputBackend
    {
        public void Touch(int id, Vector2 position, SimulatorTouchPhase phase)
        {
            Input.SimulateTouch(id, position, ToLegacy(phase));
        }

        private static TouchPhase ToLegacy(SimulatorTouchPhase original)
        {
            switch (original)
            {
                case SimulatorTouchPhase.Began:
                    return TouchPhase.Began;
                case SimulatorTouchPhase.Moved:
                    return TouchPhase.Moved;
                case SimulatorTouchPhase.Ended:
                    return TouchPhase.Ended;
                case SimulatorTouchPhase.Canceled:
                    return TouchPhase.Canceled;
                case SimulatorTouchPhase.Stationary:
                    return TouchPhase.Stationary;
                default:
                    throw new ArgumentOutOfRangeException(nameof(original), original, "None is not a supported phase with legacy input system");
            }
        }

        public void Dispose()
        {
        }
    }
}
