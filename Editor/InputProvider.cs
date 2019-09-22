using System;
using UnityEngine;

namespace Unity.DeviceSimulator
{
    internal struct TouchEvent
    {
        public Vector2 position;
        public TouchPhase phase;
    }

    internal class InputProvider : IInputProvider
    {
        private Quaternion m_Rotation = Quaternion.identity;

        public Action<Quaternion> OnRotation { get; set; }
        public Action<TouchEvent> OnTouchEvent { get; set; }

        public Quaternion Rotation
        {
            get => m_Rotation;
            set
            {
                m_Rotation = value;
                OnRotation?.Invoke(value);
            }
        }

        public void InvokeOnTouchEvent(TouchEvent touchEvent)
        {
            OnTouchEvent?.Invoke(touchEvent);
        }
    }
}
