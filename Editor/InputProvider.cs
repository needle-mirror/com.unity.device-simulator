using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.DeviceSimulator
{
    internal enum MouseEvent { Start, Move, End }

    internal class InputProvider : IInputProvider, IDisposable
    {
        private bool m_TouchFromMouseActive;
        private int m_ScreenWidth;
        private int m_ScreenHeight;
        private ScreenSimulation m_ScreenSimulation;
        private List<IInputBackend> m_InputBackends;

        private Quaternion m_Rotation = Quaternion.identity;

        public Action<Quaternion> OnRotation { get; set; }

        public Quaternion Rotation
        {
            get => m_Rotation;
            set
            {
                m_Rotation = value;
                OnRotation?.Invoke(value);
            }
        }

        public InputProvider()
        {
            m_InputBackends = new List<IInputBackend>();
#if INPUT_SYSTEM_INSTALLED
            var playerSettings = Resources.FindObjectsOfTypeAll<PlayerSettings>()[0];
            var so = new SerializedObject(playerSettings);
            var enableNativePlatformBackendsForNewInputSystem = so.FindProperty("enableNativePlatformBackendsForNewInputSystem");
            var disableOldInputManagerSupport = so.FindProperty("disableOldInputManagerSupport");

            if (enableNativePlatformBackendsForNewInputSystem.boolValue)
                m_InputBackends.Add(new InputSystemBackend());
            if (!disableOldInputManagerSupport.boolValue)
                m_InputBackends.Add(new LegacyInputBackend());
#else
            m_InputBackends.Add(new LegacyInputBackend());
#endif
        }

        public void InitTouchInput(int screenWidth, int screenHeight, ScreenSimulation screenSimulation)
        {
            m_ScreenWidth = screenWidth;
            m_ScreenHeight = screenHeight;
            m_ScreenSimulation = screenSimulation;
            CancelAllTouches();
        }

        public void TouchFromMouse(Vector2 position, MouseEvent mouseEvent)
        {
            if (!Application.isPlaying) return;

            if (!m_TouchFromMouseActive && mouseEvent != MouseEvent.Start)
                return;

            var phase = SimulatorTouchPhase.None;

            // Case when we are not actually hitting the screen
            if (position.x < 0 || position.y < 0 || position.x > m_ScreenWidth || position.y > m_ScreenHeight)
            {
                if (mouseEvent == MouseEvent.Start) return;
                else if (mouseEvent == MouseEvent.Move || mouseEvent == MouseEvent.End)
                {
                    phase = SimulatorTouchPhase.Ended;
                    m_TouchFromMouseActive = false;
                }

                if (position.x < 0)
                    position.x = 0;
                else if (position.x > m_ScreenWidth)
                    position.x = m_ScreenWidth;

                if (position.y < 0)
                    position.y = 0;
                else if (position.y > m_ScreenHeight)
                    position.y = m_ScreenHeight;
            }
            else
            {
                switch (mouseEvent)
                {
                    case MouseEvent.Start:
                        phase = SimulatorTouchPhase.Began;
                        m_TouchFromMouseActive = true;
                        break;
                    case MouseEvent.Move:
                        phase = SimulatorTouchPhase.Moved;
                        break;
                    case MouseEvent.End:
                        phase = SimulatorTouchPhase.Ended;
                        m_TouchFromMouseActive = false;
                        break;
                }
            }

            // First calculating which pixel is being touched inside the pixel rect where game is rendered in portrait orientation, due to insets this might not be full screen
            var renderedAreaPortraitWidth = m_ScreenWidth - m_ScreenSimulation.Insets.x - m_ScreenSimulation.Insets.z;
            var renderedAreaPortraitHeight = m_ScreenHeight - m_ScreenSimulation.Insets.y - m_ScreenSimulation.Insets.w;

            var touchedPixelPortraitX = position.x - m_ScreenSimulation.Insets.x;
            var touchedPixelPortraitY = position.y - m_ScreenSimulation.Insets.y;

            // Converting touch so that no matter the orientation origin would be at the bottom left corner
            float touchedPixelX = 0;
            float touchedPixelY = 0;
            switch (m_ScreenSimulation.orientation)
            {
                case ScreenOrientation.Portrait:
                    touchedPixelX = touchedPixelPortraitX;
                    touchedPixelY = renderedAreaPortraitHeight - touchedPixelPortraitY;
                    break;
                case ScreenOrientation.PortraitUpsideDown:
                    touchedPixelX = renderedAreaPortraitWidth - touchedPixelPortraitX;
                    touchedPixelY = touchedPixelPortraitY;
                    break;
                case ScreenOrientation.LandscapeLeft:
                    touchedPixelX = touchedPixelPortraitY;
                    touchedPixelY = touchedPixelPortraitX;
                    break;
                case ScreenOrientation.LandscapeRight:
                    touchedPixelX = renderedAreaPortraitHeight - touchedPixelPortraitY;
                    touchedPixelY = renderedAreaPortraitWidth - touchedPixelPortraitX;
                    break;
            }

            // Scaling in case rendering resolution does not match screen pixels
            float scaleX;
            float scaleY;
            if (m_ScreenSimulation.IsRenderingLandscape)
            {
                scaleX = m_ScreenSimulation.Width / renderedAreaPortraitHeight;
                scaleY = m_ScreenSimulation.Height / renderedAreaPortraitWidth;
            }
            else
            {
                scaleX = m_ScreenSimulation.Width / renderedAreaPortraitWidth;
                scaleY = m_ScreenSimulation.Height / renderedAreaPortraitHeight;
            }

            var actualPosition = new Vector2(touchedPixelX * scaleX, touchedPixelY * scaleY);
            foreach (var inputBackend in m_InputBackends)
            {
                inputBackend.Touch(0, actualPosition, phase);
            }
        }

        public void CancelAllTouches()
        {
            if (m_TouchFromMouseActive)
            {
                m_TouchFromMouseActive = false;
                foreach (var inputBackend in m_InputBackends)
                {
                    inputBackend.Touch(0, Vector2.zero, SimulatorTouchPhase.Canceled);
                }
            }
        }

        public void Dispose()
        {
            CancelAllTouches();
            foreach (var inputBackend in m_InputBackends)
            {
                inputBackend.Dispose();
            }
        }
    }
}
