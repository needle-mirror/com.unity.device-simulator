using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Unity.DeviceSimulator
{
    internal class ScreenSimulation : ScreenShimBase, ISimulatorEvents
    {
        private DeviceInfo m_DeviceInfo;
        private ScreenData m_Screen;

        private IInputProvider m_InputProvider;
        private ISimulatorWindow m_Window;

        private bool m_AutoRotation;
        public bool AutoRotation => m_AutoRotation;

        private ScreenOrientation m_RenderedOrientation = ScreenOrientation.Portrait;
        private Dictionary<ScreenOrientation, bool> m_AllowedAutoRotation;
        private Dictionary<ScreenOrientation, OrientationData> m_SupportedOrientations;

        private float m_DpiRatio = 1;

        private Rect m_CurrentSafeArea;
        private Rect[] m_CurrentCutouts;

        private bool m_WasResolutionSet = false;
        private int m_CurrentWidth;
        private int m_CurrentHeight;

        public int Width => m_CurrentWidth;
        public int Height => m_CurrentHeight;

        private bool m_IsFullScreen;

        public bool IsRenderingLandscape => SimulatorUtilities.IsLandscape(m_RenderedOrientation);

        public Action<bool> OnOrientationChanged  { get; set; }
        public Action OnAllowedOrientationChanged { get; set; }
        public Action<int, int> OnResolutionChanged { get; set; }
        public Action<bool> OnFullScreenChanged { get; set; }

        public ScreenSimulation(DeviceInfo device, IInputProvider inputProvider, SimulationPlayerSettings playerSettings, ISimulatorWindow window)
        {
            this.m_DeviceInfo = device;
            m_Screen = device.Screens[0];

            m_SupportedOrientations = new Dictionary<ScreenOrientation, OrientationData>();
            foreach (var o in m_Screen.orientations)
            {
                m_SupportedOrientations.Add(o.orientation, o);
            }

            m_Window = window;

            m_InputProvider = inputProvider;
            m_InputProvider.OnRotation += Rotate;
            m_InputProvider.OnTouchEvent += HandleTouch;

            m_AllowedAutoRotation = new Dictionary<ScreenOrientation, bool>();
            m_AllowedAutoRotation.Add(ScreenOrientation.Portrait, playerSettings.allowedPortrait);
            m_AllowedAutoRotation.Add(ScreenOrientation.PortraitUpsideDown, playerSettings.allowedPortraitUpsideDown);
            m_AllowedAutoRotation.Add(ScreenOrientation.LandscapeLeft, playerSettings.allowedLandscapeLeft);
            m_AllowedAutoRotation.Add(ScreenOrientation.LandscapeRight, playerSettings.allowedLandscapeRight);

            // Calculate the right orientation.
            var settingOrientation = SimulatorUtilities.ToScreenOrientation(playerSettings.defaultOrientation);
            if (settingOrientation == ScreenOrientation.AutoRotation)
            {
                m_AutoRotation = true;
                var newOrientation = SimulatorUtilities.RotationToScreenOrientation(m_InputProvider.Rotation);
                if (m_SupportedOrientations.ContainsKey(newOrientation) && m_AllowedAutoRotation[newOrientation])
                    ForceNewOrientation(newOrientation);
                else
                    SetFirstAvailableAutoOrientation();
            }
            else if (m_SupportedOrientations.ContainsKey(settingOrientation))
            {
                m_AutoRotation = false;
                ForceNewOrientation(settingOrientation);
            }
            else
            {
                // At least iPhone X responds to this absolute corner case by crashing, we will not do that.
                m_AutoRotation = false;
                ForceNewOrientation(m_SupportedOrientations.Keys.ToArray()[0]);
            }

            // Calculate the right resolution.
            var initWidth = m_Screen.width;
            var initHeight = m_Screen.height;
            if (playerSettings.resolutionScalingMode == ResolutionScalingMode.FixedDpi && playerSettings.targetDpi < m_Screen.dpi)
            {
                m_DpiRatio = playerSettings.targetDpi / m_Screen.dpi;
                initWidth = (int)(initWidth * m_DpiRatio);
                initHeight = (int)(initHeight * m_DpiRatio);
            }
            m_CurrentWidth = IsRenderingLandscape ? initHeight : initWidth;
            m_CurrentHeight = IsRenderingLandscape ? initWidth : initHeight;

            // Set the full screen mode.
            m_IsFullScreen = !m_DeviceInfo.IsAndroidDevice() || playerSettings.androidStartInFullscreen;
            if (!m_IsFullScreen)
                CalculateScreenResolutionForScreenMode(out m_CurrentWidth, out m_CurrentHeight);

            CalculateSafeAreaAndCutouts();

            m_Window.TargetOrientation = m_RenderedOrientation;
            m_Window.TargetSize = new Vector2(m_CurrentWidth, m_CurrentHeight);

            ShimManager.UseShim(this);
        }

        private void Rotate(Quaternion rotation)
        {
            if (m_AutoRotation)
            {
                var newOrientation = SimulatorUtilities.RotationToScreenOrientation(rotation);
                if (m_SupportedOrientations.ContainsKey(newOrientation) && m_AllowedAutoRotation[newOrientation])
                    ForceNewOrientation(newOrientation);
                OnOrientationChanged?.Invoke(m_AutoRotation);
            }
        }

        private void ForceNewOrientation(ScreenOrientation orientation)
        {
            // Swap resolution Width and Height if changing from Portrait to Landscape or vice versa
            if ((orientation == ScreenOrientation.Portrait || orientation == ScreenOrientation.PortraitUpsideDown) && IsRenderingLandscape ||
                (orientation == ScreenOrientation.LandscapeLeft || orientation == ScreenOrientation.LandscapeRight) && !IsRenderingLandscape)
            {
                var temp = m_CurrentHeight;
                m_CurrentHeight = m_CurrentWidth;
                m_CurrentWidth = temp;
                m_Window.TargetSize = new Vector2(m_CurrentWidth, m_CurrentHeight);
                OnResolutionChanged?.Invoke(m_CurrentWidth, m_CurrentHeight);
            }
            m_RenderedOrientation = orientation;
            m_Window.TargetOrientation = orientation;
            CalculateSafeAreaAndCutouts();
        }

        private void CalculateSafeAreaAndCutouts()
        {
            int scaledHeight = 0;
            var scaledNavigationBarHeight = Mathf.RoundToInt(m_DpiRatio * m_Screen.navigationBarHeight);
            if (!m_WasResolutionSet)
            {
                scaledHeight = scaledNavigationBarHeight;
                if (m_SupportedOrientations.ContainsKey(ScreenOrientation.Portrait))
                    scaledHeight += Mathf.RoundToInt(m_DpiRatio * (m_Screen.height - m_SupportedOrientations[ScreenOrientation.Portrait].safeArea.height));
            }

            // Always consider the full screen mode width/height to scale the safe area & cutouts.
            float xScale, yScale;
            if (IsRenderingLandscape)
            {
                xScale = (float)(m_CurrentWidth + (m_IsFullScreen ? 0 : scaledHeight)) / m_Screen.height;
                yScale = (float)m_CurrentHeight / m_Screen.width;
            }
            else
            {
                xScale = (float)m_CurrentWidth / m_Screen.width;
                yScale = (float)(m_CurrentHeight + (m_IsFullScreen ? 0 : scaledHeight)) / m_Screen.height;
            }

            // Scale safe area.
            var odd = m_SupportedOrientations[m_RenderedOrientation];
            var sa = odd.safeArea;
            if (m_IsFullScreen)
            {
                m_CurrentSafeArea = new Rect(Mathf.Round(sa.x * xScale), Mathf.Round(sa.y * yScale), Mathf.Round(sa.width * xScale), Mathf.Round(sa.height * yScale));
            }
            else
            {
                if (m_WasResolutionSet)
                    m_CurrentSafeArea = new Rect(0, 0, m_CurrentWidth, m_CurrentHeight); // Set the safe area to current resolution in windowed mode with resolution set.
                else
                    m_CurrentSafeArea = new Rect(0, 0, Mathf.Round(sa.width * xScale), Mathf.Round(sa.height * yScale));
            }

            // Consider the navigation bar height if it's windowed mode without resolution set.
            if (!m_IsFullScreen && !m_WasResolutionSet)
            {
                switch (m_RenderedOrientation)
                {
                    case ScreenOrientation.Portrait:
                    case ScreenOrientation.PortraitUpsideDown:
                        m_CurrentSafeArea.height -= scaledNavigationBarHeight;
                        break;
                    case ScreenOrientation.LandscapeLeft:
                    case ScreenOrientation.LandscapeRight:
                        m_CurrentSafeArea.width -= scaledNavigationBarHeight;
                        break;
                }
            }

            // For windowed mode, let's return empty cutouts for now.
            if (!m_IsFullScreen)
            {
                m_CurrentCutouts = new Rect[0];
                return;
            }

            // Scale cutouts.
            if (odd.cutouts?.Length > 0)
            {
                m_CurrentCutouts = new Rect[odd.cutouts.Length];
                for (int i = 0; i < odd.cutouts.Length; ++i)
                {
                    var cutout = odd.cutouts[i];
                    m_CurrentCutouts[i] = new Rect(Mathf.Round(cutout.x * xScale), Mathf.Round(cutout.y * yScale), Mathf.Round(cutout.width * xScale), Mathf.Round(cutout.height * yScale));
                }
            }
            else
                m_CurrentCutouts = new Rect[0];
        }

        private void HandleTouch(TouchEvent evt)
        {
            var deviceScreen = new Vector2(m_DeviceInfo.Screens[0].width, m_DeviceInfo.Screens[0].height);
            var actualPosition = Vector2.zero;
            float scaleX;
            float scaleY;

            // TODO should be moved to recalculate on orientation, resolution, full screen mode change, no need to calculate every touch

            if (m_IsFullScreen)
            {
                switch (m_RenderedOrientation)
                {
                    case ScreenOrientation.Portrait:
                        scaleX = (float)m_CurrentWidth / m_DeviceInfo.Screens[0].width;
                        scaleY = (float)m_CurrentHeight / m_DeviceInfo.Screens[0].height;
                        actualPosition = new Vector2(evt.position.x, -evt.position.y + deviceScreen.y);
                        actualPosition *= new Vector2(scaleX, scaleY);
                        break;
                    case ScreenOrientation.PortraitUpsideDown:
                        scaleX = (float)m_CurrentWidth / m_DeviceInfo.Screens[0].width;
                        scaleY = (float)m_CurrentHeight / m_DeviceInfo.Screens[0].height;
                        actualPosition = new Vector2(-evt.position.x + deviceScreen.x, evt.position.y);
                        actualPosition *= new Vector2(scaleX, scaleY);
                        break;
                    case ScreenOrientation.LandscapeLeft:
                        scaleX = (float)m_CurrentHeight / m_DeviceInfo.Screens[0].width;
                        scaleY = (float)m_CurrentWidth / m_DeviceInfo.Screens[0].height;
                        actualPosition = new Vector2(evt.position.y, evt.position.x);
                        actualPosition *= new Vector2(scaleY, scaleX);
                        break;
                    case ScreenOrientation.LandscapeRight:
                        scaleX = (float)m_CurrentHeight / m_DeviceInfo.Screens[0].width;
                        scaleY = (float)m_CurrentWidth / m_DeviceInfo.Screens[0].height;
                        actualPosition = new Vector2(-evt.position.y + deviceScreen.y, -evt.position.x + deviceScreen.x);
                        actualPosition *= new Vector2(scaleY, scaleX);
                        break;
                }
            }
            else
            {
                var renderedAreaSize = m_SupportedOrientations[ScreenOrientation.Portrait].safeArea.size - new Vector2(0, m_Screen.navigationBarHeight);
                var headerHeight = m_Screen.height - m_SupportedOrientations[ScreenOrientation.Portrait].safeArea.height;
                switch (m_RenderedOrientation)
                {
                    case ScreenOrientation.Portrait:
                        scaleX = (float)m_CurrentWidth / renderedAreaSize.x;
                        scaleY = (float)m_CurrentHeight / renderedAreaSize.y;
                        actualPosition = new Vector2(evt.position.x, -evt.position.y + deviceScreen.y - m_Screen.navigationBarHeight);
                        actualPosition *= new Vector2(scaleX, scaleY);
                        break;
                    case ScreenOrientation.PortraitUpsideDown:
                        scaleX = (float)m_CurrentWidth / renderedAreaSize.x;
                        scaleY = (float)m_CurrentHeight / renderedAreaSize.y;
                        actualPosition = new Vector2(-evt.position.x + deviceScreen.x, evt.position.y - headerHeight - m_Screen.navigationBarHeight);
                        actualPosition *= new Vector2(scaleX, scaleY);
                        break;
                    case ScreenOrientation.LandscapeLeft:
                        scaleX = (float)m_CurrentHeight / renderedAreaSize.x;
                        scaleY = (float)m_CurrentWidth / renderedAreaSize.y;
                        actualPosition = new Vector2(evt.position.y - headerHeight, evt.position.x);
                        actualPosition *= new Vector2(scaleY, scaleX);
                        break;
                    case ScreenOrientation.LandscapeRight:
                        scaleX = (float)m_CurrentHeight / renderedAreaSize.x;
                        scaleY = (float)m_CurrentWidth / renderedAreaSize.y;
                        actualPosition = new Vector2(-evt.position.y + deviceScreen.y - m_Screen.navigationBarHeight, -evt.position.x + deviceScreen.x);
                        actualPosition *= new Vector2(scaleY, scaleX);
                        break;
                }
            }

            actualPosition = new Vector2(Mathf.Round(actualPosition.x), Mathf.Round(actualPosition.y));
            Input.SimulateTouch(0, actualPosition, evt.phase);
        }

        private void SetAutoRotationOrientation(ScreenOrientation orientation, bool value)
        {
            m_AllowedAutoRotation[orientation] = value;

            if (!m_AutoRotation)
            {
                OnAllowedOrientationChanged?.Invoke();
                return;
            }

            // If the current auto rotation is disabled we need to rotate to another allowed orientation
            if (!value && orientation == m_RenderedOrientation)
            {
                SetFirstAvailableAutoOrientation();
            }
            else if (value)
            {
                Rotate(m_InputProvider.Rotation);
            }

            OnAllowedOrientationChanged?.Invoke();
        }

        private void SetFirstAvailableAutoOrientation()
        {
            foreach (var newOrientation in m_SupportedOrientations.Keys)
            {
                if (m_AllowedAutoRotation[newOrientation])
                {
                    ForceNewOrientation(newOrientation);
                }
            }
        }

        private void SetResolution(int width, int height)
        {
            m_CurrentWidth = width;
            m_CurrentHeight = height;
            m_Window.TargetSize = new Vector2(width, height);
            CalculateSafeAreaAndCutouts();

            OnResolutionChanged?.Invoke(m_CurrentWidth, m_CurrentHeight);
        }

        private void CalculateScreenResolutionForScreenMode(out int width, out int height)
        {
            width = m_CurrentWidth;
            height = m_CurrentHeight;

            var noFullScreenHeight = m_SupportedOrientations[ScreenOrientation.Portrait].safeArea.height - m_Screen.navigationBarHeight;
            float scale = m_IsFullScreen ? m_Screen.height / noFullScreenHeight : noFullScreenHeight / m_Screen.height;

            switch (m_RenderedOrientation)
            {
                case ScreenOrientation.Portrait:
                case ScreenOrientation.PortraitUpsideDown:
                    height = Convert.ToInt32(Math.Round(height * scale));
                    break;
                case ScreenOrientation.LandscapeLeft:
                case ScreenOrientation.LandscapeRight:
                    width = Convert.ToInt32(Math.Round(width * scale));
                    break;
            }
        }

        public void Enable()
        {
            ShimManager.UseShim(this);
        }

        public void Disable()
        {
            ShimManager.RemoveShim(this);
        }

        public new void Dispose()
        {
            m_InputProvider.OnRotation -= Rotate;
            m_InputProvider.OnTouchEvent -= HandleTouch;
            Disable();
        }

        #region ShimBase Overrides
        public override Rect safeArea => m_CurrentSafeArea;

        public override Rect[] cutouts => m_CurrentCutouts;

        public override float dpi => m_Screen.dpi;

        public override Resolution currentResolution => new Resolution() { width = m_CurrentWidth, height = m_CurrentHeight };

        public override Resolution[] resolutions => new[] { currentResolution };

        public override ScreenOrientation orientation
        {
            get => m_RenderedOrientation;
            set
            {
                if (value == ScreenOrientation.AutoRotation)
                {
                    m_AutoRotation = true;
                    Rotate(m_InputProvider.Rotation);
                }
                else if (m_SupportedOrientations.ContainsKey(value))
                {
                    m_AutoRotation = false;
                    ForceNewOrientation(value);
                }

                OnOrientationChanged?.Invoke(m_AutoRotation);
            }
        }

        public override bool autorotateToPortrait
        {
            get => m_AllowedAutoRotation[ScreenOrientation.Portrait];
            set => SetAutoRotationOrientation(ScreenOrientation.Portrait, value);
        }

        public override bool autorotateToPortraitUpsideDown
        {
            get => m_AllowedAutoRotation[ScreenOrientation.PortraitUpsideDown];
            set => SetAutoRotationOrientation(ScreenOrientation.PortraitUpsideDown, value);
        }

        public override bool autorotateToLandscapeLeft
        {
            get => m_AllowedAutoRotation[ScreenOrientation.LandscapeLeft];
            set => SetAutoRotationOrientation(ScreenOrientation.LandscapeLeft, value);
        }

        public override bool autorotateToLandscapeRight
        {
            get => m_AllowedAutoRotation[ScreenOrientation.LandscapeRight];
            set => SetAutoRotationOrientation(ScreenOrientation.LandscapeRight, value);
        }

        public override void SetResolution(int width, int height, FullScreenMode fullScreenMode, int refreshRate)
        {
            m_WasResolutionSet = true;
            SetResolution(width, height);
            fullScreen = (fullScreenMode != FullScreenMode.Windowed); // Tested on Pixel 2 that all other three types go into full screen mode.
        }

        public override bool fullScreen
        {
            get => m_IsFullScreen;
            set
            {
                if (!m_DeviceInfo.IsAndroidDevice() || m_IsFullScreen == value)
                    return;

                m_IsFullScreen = value;

                // We only change the resolution if we never set the resolution by calling Screen.SetResolution().
                if (!m_WasResolutionSet)
                {
                    CalculateScreenResolutionForScreenMode(out int tempWidth, out int tempHeight);
                    SetResolution(tempWidth, tempHeight);
                }
                else
                {
                    CalculateSafeAreaAndCutouts();
                }

                OnFullScreenChanged?.Invoke(m_IsFullScreen);
            }
        }

        public override FullScreenMode fullScreenMode
        {
            get => fullScreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            set => fullScreen = (value != FullScreenMode.Windowed);
        }

        #endregion
    }
}
