using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.DeviceSimulator
{
    internal class TouchEventManipulator : MouseManipulator
    {
        public Matrix4x4 PreviewImageRendererSpaceToScreenSpace { get; set; }
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
            var position = PreviewImageRendererSpaceToScreenSpace.MultiplyPoint(evt.localMousePosition);
            m_InputProvider.TouchFromMouse(position, MouseEvent.Start);
        }

        private void OnMouseMove(MouseMoveEvent evt)
        {
            var position = PreviewImageRendererSpaceToScreenSpace.MultiplyPoint(evt.localMousePosition);
            m_InputProvider.TouchFromMouse(position, MouseEvent.Move);
        }

        private void OnMouseUp(MouseUpEvent evt)
        {
            var position = PreviewImageRendererSpaceToScreenSpace.MultiplyPoint(evt.localMousePosition);
            m_InputProvider.TouchFromMouse(position, MouseEvent.End);
        }

        private void OnMouseLeave(MouseLeaveEvent evt)
        {
            var position = PreviewImageRendererSpaceToScreenSpace.MultiplyPoint(evt.localMousePosition);
            m_InputProvider.TouchFromMouse(position, MouseEvent.End);
        }
    }
}
