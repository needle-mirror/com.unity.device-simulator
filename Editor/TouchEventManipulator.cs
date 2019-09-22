using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.DeviceSimulator
{
    internal class TouchEventManipulator : MouseManipulator
    {
        public Matrix4x4 PreviewImageRendererSpaceToScreenSpace { get; set; }

        private bool m_IsActive = false;
        private InputProvider m_InputProvider = null;

        public TouchEventManipulator(InputProvider inputProvider)
        {
            m_InputProvider = inputProvider;
            activators.Add(new ManipulatorActivationFilter() { button = MouseButton.LeftMouse});
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<MouseDownEvent>(OnMouseDown);
            target.RegisterCallback<MouseMoveEvent>(OnMouseMove);
            target.RegisterCallback<MouseUpEvent>(OnMouseUp);
            target.RegisterCallback<MouseLeaveEvent>(OnMouseLeave);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<MouseDownEvent>(OnMouseDown);
            target.UnregisterCallback<MouseMoveEvent>(OnMouseMove);
            target.UnregisterCallback<MouseUpEvent>(OnMouseUp);
            target.UnregisterCallback<MouseLeaveEvent>(OnMouseLeave);
        }

        private void OnMouseDown(MouseDownEvent evt)
        {
            TiggerTouchEvent(evt.localMousePosition, TouchPhase.Began);
            m_IsActive = true;
        }

        private void OnMouseMove(MouseMoveEvent evt)
        {
            if (m_IsActive)
                TiggerTouchEvent(evt.localMousePosition, TouchPhase.Moved);
        }

        private void OnMouseUp(MouseUpEvent evt)
        {
            if (m_IsActive)
            {
                TiggerTouchEvent(evt.localMousePosition, TouchPhase.Ended);
                m_IsActive = false;
            }
        }

        private void OnMouseLeave(MouseLeaveEvent evt)
        {
            if (m_IsActive)
            {
                TiggerTouchEvent(evt.localMousePosition, TouchPhase.Canceled);
                m_IsActive = false;
            }
        }

        private void TiggerTouchEvent(Vector2 mousePosition, TouchPhase touchPhase)
        {
            var touchEvent = new TouchEvent()
            {
                position = PreviewImageRendererSpaceToScreenSpace.MultiplyPoint(mousePosition),
                phase = touchPhase
            };
            m_InputProvider.InvokeOnTouchEvent(touchEvent);
        }
    }
}
