using System;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.DeviceSimulator
{
    internal class SimulatorPreviewPanel
    {
        private VisualElement m_RootElement = null;
        private InputProvider m_InputProvider = null;
        private DeviceInfo m_DeviceInfo = null;

        public RenderTexture PreviewTexture { private get; set; }

        public Action<bool> OnControlPanelHiddenChanged { get; set; }

        private bool m_ControlPanelHidden = false;
        public bool ControlPanelHidden => m_ControlPanelHidden;

        private int m_Scale = 20; // Value from (0, 100].
        private const int kScaleMin = 10;
        private const int kScaleMax = 100;
        private bool m_FitToScreenEnabled = true;

        private int m_RotationDegree = 0; // Value from [0, 360), counted as CCW(convert to CW in the future?).
        private Quaternion m_Rotation = Quaternion.identity;

        private bool m_HighlightSafeArea = false;
        private Color m_HighlightSafeAreaColor = Color.green;
        private int m_HighlightSafeAreaLineWidth = 2;

        public int Scale => m_Scale;
        public bool FitToScreenEnabled => m_FitToScreenEnabled;
        public int RotationDegree => m_RotationDegree;

        public bool HighlightSafeAre => m_HighlightSafeArea;

        // Controls for preview toolbar.
        private ToolbarButton m_HideControlPanel = null;
        private SliderInt m_ScaleSlider = null;
        private Label m_ScaleValueLabel = null;
        private ToolbarToggle m_FitToScreenToggle = null;

        // Controls for inactive message.
        private VisualElement m_InactiveMsgContainer = null;

        // Controls for preview.
        private VisualElement m_ScrollViewContainer = null;
        private VisualElement m_ScrollView = null;
        private IMGUIContainer m_PreviewRenderer = null;
        private Material m_PreviewMaterial = null;
        private Material m_DeviceMaterial = null;

        private Vector2 m_BoundingBox = Vector2.zero;
        private Vector2 m_Offset = Vector2.zero;

        private TouchEventManipulator m_TouchEventManipulator = null;

        public ScreenOrientation TargetOrientation { set; get; }

        public bool IsFullScreen { set; get; }

        public SimulatorPreviewPanel(VisualElement rootElement, InputProvider inputProvider, DeviceInfo deviceInfo, SimulatorJsonSerialization states)
        {
            m_RootElement = rootElement;
            m_InputProvider = inputProvider;
            m_DeviceInfo = deviceInfo;

            var userSettings = DeviceSimulatorUserSettingsProvider.LoadOrCreateSettings();
            m_HighlightSafeAreaColor = userSettings.SafeAreaHighlightColor;
            m_HighlightSafeAreaLineWidth = userSettings.SafeAreaHighlightLineWidth;

            Init(states);
        }

        private void Init(SimulatorJsonSerialization states)
        {
            if (states != null)
            {
                m_ControlPanelHidden = states.controlPanelHidden;
                m_Scale = states.scale;
                m_FitToScreenEnabled = states.fitToScreenEnabled;
                m_RotationDegree = states.rotationDegree;
                m_Rotation = states.rotation;
                m_HighlightSafeArea = states.highlightSafeAreaEnabled;
            }

            InitPreviewToolbar();
            InitInactiveMsg();
            InitPreview();

            ComputeBoundingBox();
        }

        private void InitPreviewToolbar()
        {
            m_HideControlPanel = m_RootElement.Q<ToolbarButton>("hide-control-panel");
            m_HideControlPanel.style.backgroundImage = (Texture2D)EditorGUIUtility.Load($"Icons/d_tab_{(m_ControlPanelHidden ? "next" : "prev")}@2x.png");
            m_HideControlPanel.clickable = new Clickable(HideControlPanel);

            #region Scale
            m_ScaleSlider = m_RootElement.Q<SliderInt>("scale-slider");
            m_ScaleSlider.lowValue = kScaleMin;
            m_ScaleSlider.highValue = kScaleMax;
            m_ScaleSlider.value = m_Scale;
            m_ScaleSlider.RegisterCallback<ChangeEvent<int>>(SetScale);

            m_ScaleValueLabel = m_RootElement.Q<Label>("scale-value-label");
            m_ScaleValueLabel.text = m_Scale.ToString();

            m_FitToScreenToggle = m_RootElement.Q<ToolbarToggle>("fit-to-screen");
            m_FitToScreenToggle.RegisterValueChangedCallback(FitToScreen);
            m_FitToScreenToggle.SetValueWithoutNotify(m_FitToScreenEnabled);
            #endregion

            #region Rotate
            var namePostfix = EditorGUIUtility.isProSkin ? "_dark" : "_light";
            const string iconPath = "packages/com.unity.device-simulator/Editor/icons";

            m_RootElement.Q<Image>("rotate-cw-image").image = AssetDatabase.LoadAssetAtPath<Texture2D>($"{iconPath}/rotate_cw{namePostfix}.png");
            m_RootElement.Q<VisualElement>("rotate-cw").AddManipulator(new Clickable(RotateDeviceCW));

            m_RootElement.Q<Image>("rotate-ccw-image").image = AssetDatabase.LoadAssetAtPath<Texture2D>($"{iconPath}/rotate_ccw{namePostfix}.png");
            m_RootElement.Q<VisualElement>("rotate-ccw").AddManipulator(new Clickable(RotateDeviceCCW));
            #endregion

            // Highlight safe area.
            var highlightSafeAreaToggle = m_RootElement.Q<Toggle>("highlight-safe-area");
            highlightSafeAreaToggle.RegisterValueChangedCallback((evt) => { m_HighlightSafeArea = evt.newValue; OnStateChanged(); });
            highlightSafeAreaToggle.SetValueWithoutNotify(m_HighlightSafeArea);
        }

        private void HideControlPanel()
        {
            m_ControlPanelHidden = !m_ControlPanelHidden;

            m_HideControlPanel.style.backgroundImage = (Texture2D)EditorGUIUtility.Load($"Icons/d_tab_{(m_ControlPanelHidden ? "next" : "prev")}@2x.png");
            OnControlPanelHiddenChanged?.Invoke(m_ControlPanelHidden);
        }

        private void InitInactiveMsg()
        {
            m_InactiveMsgContainer = m_RootElement.Q<VisualElement>("inactive-msg-container");
            var closeInactiveMsg = m_RootElement.Q<Image>("close-inactive-msg");
            closeInactiveMsg.image = AssetDatabase.LoadAssetAtPath<Texture2D>($"packages/com.unity.device-simulator/Editor/icons/close_button.png");
            closeInactiveMsg.AddManipulator(new Clickable(CloseInactiveMsg));

            SetInactiveMsgState(false);
        }

        private void InitPreview()
        {
            m_ScrollViewContainer = m_RootElement.Q<VisualElement>("scrollview-container");
            m_ScrollViewContainer.RegisterCallback<WheelEvent>(OnScrollWheel, TrickleDown.TrickleDown);
            m_ScrollViewContainer.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);

            m_ScrollView = m_RootElement.Q<ScrollView>("preview-scroll-view");

            m_PreviewRenderer = m_RootElement.Q<IMGUIContainer>("preview-imgui-renderer");
            m_PreviewRenderer.onGUIHandler = OnIMGUIRendered;
            m_PreviewRenderer.AddManipulator(m_TouchEventManipulator = new TouchEventManipulator(m_InputProvider));
        }

        private void SetScale(ChangeEvent<int> e)
        {
            UpdateScale(e.newValue);

            m_FitToScreenEnabled = false;
            m_FitToScreenToggle.SetValueWithoutNotify(m_FitToScreenEnabled);
        }

        private void FitToScreen(ChangeEvent<bool> evt)
        {
            m_FitToScreenEnabled = evt.newValue;
            if (m_FitToScreenEnabled)
                FitToScreenScale();
        }

        private void FitToScreenScale()
        {
            Vector2 screenSize = m_ScrollViewContainer.worldBound.size;
            var x = screenSize.x / m_BoundingBox.x;
            var y = screenSize.y / m_BoundingBox.y;

            UpdateScale(ClampScale(Mathf.FloorToInt(m_Scale * Math.Min(x, y))));
        }

        private void UpdateScale(int newScale)
        {
            m_Scale = newScale;
            m_ScaleValueLabel.text = newScale.ToString();
            m_ScaleSlider.SetValueWithoutNotify(newScale);

            OnStateChanged();
        }

        private void SetRotationDegree(int newValue)
        {
            m_RotationDegree = newValue;
            m_Rotation = Quaternion.Euler(0, 0, 360 - m_RotationDegree);
            m_InputProvider.Rotation = m_Rotation; // Update the orientations in ScreenSimulation.

            OnStateChanged();

            if (m_FitToScreenEnabled)
                FitToScreenScale();
        }

        private void RotateDeviceCW()
        {
            // Always rotate to 0/90/180/270 degrees if clicking rotation buttons.
            if (m_RotationDegree % 90 != 0)
                m_RotationDegree = System.Convert.ToInt32(System.Math.Ceiling(m_RotationDegree / 90f)) * 90;
            m_RotationDegree = (m_RotationDegree + 270) % 360;
            m_Rotation = Quaternion.Euler(0, 0, 360 - m_RotationDegree);

            SetRotationDegree(m_RotationDegree);
        }

        private void RotateDeviceCCW()
        {
            // Always rotate to 0/90/180/270 degrees if clicking rotation buttons.
            if (m_RotationDegree % 90 != 0)
                m_RotationDegree = System.Convert.ToInt32(System.Math.Floor(m_RotationDegree / 90f)) * 90;
            m_RotationDegree = (m_RotationDegree + 90) % 360;
            m_Rotation = Quaternion.Euler(0, 0, 360 - m_RotationDegree);

            SetRotationDegree(m_RotationDegree);
        }

        private void CloseInactiveMsg()
        {
            SetInactiveMsgState(false);
        }

        private void SetInactiveMsgState(bool shown)
        {
            m_InactiveMsgContainer.style.visibility = shown ? Visibility.Visible : Visibility.Hidden;
            m_InactiveMsgContainer.style.position = shown ? Position.Relative : Position.Absolute;
        }

        private void OnScrollWheel(WheelEvent evt)
        {
            var newScale = (int)(m_Scale - evt.delta.y);
            UpdateScale(ClampScale(newScale));
            evt.StopPropagation();

            m_FitToScreenEnabled = false;
            m_FitToScreenToggle.SetValueWithoutNotify(m_FitToScreenEnabled);
        }

        private int ClampScale(int scale)
        {
            if (scale < kScaleMin)
                return kScaleMin;
            if (scale > kScaleMax)
                return kScaleMax;

            return scale;
        }

        public void OnStateChanged()
        {
            m_PreviewRenderer.MarkDirtyRepaint();

            ComputeBoundingBox();
            SetScrollViewTopPadding();
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            if (m_FitToScreenEnabled)
                FitToScreenScale();

            SetScrollViewTopPadding();
        }

        // This is a workaround to fix https://github.com/Unity-Technologies/com.unity.device-simulator/issues/79.
        private void SetScrollViewTopPadding()
        {
            var scrollViewHeight = m_ScrollView.worldBound.height;
            if (float.IsNaN(scrollViewHeight))
                return;

            m_ScrollView.style.paddingTop = scrollViewHeight > m_BoundingBox.y ? (scrollViewHeight - m_BoundingBox.y) / 2 : 0;
        }

        private void ComputePreviewImageHalfSizeAndOffset(out Vector2 halfSize, out Vector2 offset)
        {
            var imageWidth = m_DeviceInfo.Screens[0].width;
            var imageHeight = m_DeviceInfo.Screens[0].height;

            if (!IsFullScreen)
                imageHeight = (int)(from o in m_DeviceInfo.Screens[0].orientations where o.orientation == ScreenOrientation.Portrait select o.safeArea.height).First() - m_DeviceInfo.Screens[0].navigationBarHeight;

            var scale = m_Scale / 100f;
            halfSize.x = scale * imageWidth / 2;
            halfSize.y = scale * imageHeight / 2;

            offset = ComputeOffsetForScreenMode(m_Offset);
        }

        private Vector2 ComputeOffsetForScreenMode(Vector2 offset, bool isHighlightingSafeArea = false)
        {
            // If we're rendering the preview image in full screen mode, no extra offset.
            if (IsFullScreen && !isHighlightingSafeArea)
                return offset;

            var scale = m_Scale / 100f;
            var scaledNavigationBarOffset = (IsFullScreen && isHighlightingSafeArea) ? 0 : m_DeviceInfo.Screens[0].navigationBarHeight * scale / 2.0f;

            var safeArea = (from o in m_DeviceInfo.Screens[0].orientations where o.orientation == TargetOrientation select o.safeArea).First();
            var safeAreaOffset = new Vector2(safeArea.x + safeArea.width / 2.0f, safeArea.y + safeArea.height / 2.0f);
            if (SimulatorUtilities.IsLandscape(TargetOrientation))
            {
                safeAreaOffset.x -= m_DeviceInfo.Screens[0].height / 2.0f;
                safeAreaOffset.y -= m_DeviceInfo.Screens[0].width / 2.0f;
            }
            else
            {
                safeAreaOffset.x -= m_DeviceInfo.Screens[0].width / 2.0f;
                safeAreaOffset.y -= m_DeviceInfo.Screens[0].height / 2.0f;
            }

            var scaledSafeAreaOffset = scale * safeAreaOffset;
            var tempOffset = offset;

            // We have to consider the case that the rendering orientation is not same as the physical orientation.
            var physicalOrientation = SimulatorUtilities.RotationToScreenOrientation(m_Rotation);
            switch (TargetOrientation)
            {
                case ScreenOrientation.Portrait:
                    switch (physicalOrientation)
                    {
                        case ScreenOrientation.Portrait:
                            tempOffset.x = tempOffset.x + scaledSafeAreaOffset.x;
                            tempOffset.y = tempOffset.y - scaledSafeAreaOffset.y - scaledNavigationBarOffset;
                            break;
                        case ScreenOrientation.PortraitUpsideDown:
                            tempOffset.x = tempOffset.x + scaledSafeAreaOffset.x;
                            tempOffset.y = tempOffset.y + scaledSafeAreaOffset.y + scaledNavigationBarOffset;
                            break;
                        case ScreenOrientation.LandscapeLeft:
                            tempOffset.x = tempOffset.x - scaledSafeAreaOffset.y - scaledNavigationBarOffset;
                            tempOffset.y = tempOffset.y + scaledSafeAreaOffset.x;
                            break;
                        case ScreenOrientation.LandscapeRight:
                            tempOffset.x = tempOffset.x + scaledSafeAreaOffset.y + scaledNavigationBarOffset;
                            tempOffset.y = tempOffset.y + scaledSafeAreaOffset.x;
                            break;
                    }
                    break;
                case ScreenOrientation.PortraitUpsideDown:
                    switch (physicalOrientation)
                    {
                        case ScreenOrientation.Portrait:
                            tempOffset.x = tempOffset.x + scaledSafeAreaOffset.x;
                            tempOffset.y = tempOffset.y + scaledSafeAreaOffset.y + scaledNavigationBarOffset;
                            break;
                        case ScreenOrientation.PortraitUpsideDown:
                            tempOffset.x = tempOffset.x + scaledSafeAreaOffset.x;
                            tempOffset.y = tempOffset.y - scaledSafeAreaOffset.y - scaledNavigationBarOffset;
                            break;
                        case ScreenOrientation.LandscapeLeft:
                            tempOffset.x = tempOffset.x + scaledSafeAreaOffset.y + scaledNavigationBarOffset;
                            tempOffset.y = tempOffset.y + scaledSafeAreaOffset.x;
                            break;
                        case ScreenOrientation.LandscapeRight:
                            tempOffset.x = tempOffset.x - scaledSafeAreaOffset.y - scaledNavigationBarOffset;
                            tempOffset.y = tempOffset.y + scaledSafeAreaOffset.x;
                            break;
                    }
                    break;
                case ScreenOrientation.LandscapeLeft:
                    switch (physicalOrientation)
                    {
                        case ScreenOrientation.Portrait:
                            tempOffset.x = tempOffset.x + scaledSafeAreaOffset.y;
                            tempOffset.y = tempOffset.y + scaledSafeAreaOffset.x - scaledNavigationBarOffset;
                            break;
                        case ScreenOrientation.PortraitUpsideDown:
                            tempOffset.x = tempOffset.x - scaledSafeAreaOffset.y;
                            tempOffset.y = tempOffset.y - scaledSafeAreaOffset.x + scaledNavigationBarOffset;
                            break;
                        case ScreenOrientation.LandscapeLeft:
                            tempOffset.x = tempOffset.x + scaledSafeAreaOffset.x - scaledNavigationBarOffset;
                            tempOffset.y = tempOffset.y - scaledSafeAreaOffset.y;
                            break;
                        case ScreenOrientation.LandscapeRight:
                            tempOffset.x = tempOffset.x - scaledSafeAreaOffset.x + scaledNavigationBarOffset;
                            tempOffset.y = tempOffset.y + scaledSafeAreaOffset.y;
                            break;
                    }
                    break;
                case ScreenOrientation.LandscapeRight:
                    switch (physicalOrientation)
                    {
                        case ScreenOrientation.Portrait:
                            tempOffset.x = tempOffset.x - scaledSafeAreaOffset.y;
                            tempOffset.y = tempOffset.y - scaledSafeAreaOffset.x - scaledNavigationBarOffset;
                            break;
                        case ScreenOrientation.PortraitUpsideDown:
                            tempOffset.x = tempOffset.x + scaledSafeAreaOffset.y;
                            tempOffset.y = tempOffset.y + scaledSafeAreaOffset.x + scaledNavigationBarOffset;
                            break;
                        case ScreenOrientation.LandscapeLeft:
                            tempOffset.x = tempOffset.x - scaledSafeAreaOffset.x - scaledNavigationBarOffset;
                            tempOffset.y = tempOffset.y + scaledSafeAreaOffset.y;
                            break;
                        case ScreenOrientation.LandscapeRight:
                            tempOffset.x = tempOffset.x + scaledSafeAreaOffset.x + scaledNavigationBarOffset;
                            tempOffset.y = tempOffset.y - scaledSafeAreaOffset.y;
                            break;
                    }

                    break;
            }

            return tempOffset;
        }

        private Rect GetSafeAreaInScreen()
        {
            var sa = (from o in m_DeviceInfo.Screens[0].orientations where o.orientation == TargetOrientation select o.safeArea).First();
            if (!IsFullScreen)
            {
                if (SimulatorUtilities.IsLandscape(TargetOrientation))
                    sa.width -= m_DeviceInfo.Screens[0].navigationBarHeight;
                else
                    sa.height -= m_DeviceInfo.Screens[0].navigationBarHeight;
            }

            var scale = m_Scale / 100f;
            return new Rect(0, 0, sa.width * scale, sa.height * scale);
        }

        private Quaternion ComputeRotationForHighlightSafeArea()
        {
            var rotation = Quaternion.identity;

            // We have to consider the case that the rendering orientation is not same as the physical orientation.
            var physicalOrientation = SimulatorUtilities.RotationToScreenOrientation(m_Rotation);
            switch (TargetOrientation)
            {
                case ScreenOrientation.Portrait:
                case ScreenOrientation.PortraitUpsideDown:
                    if (SimulatorUtilities.IsLandscape(physicalOrientation))
                        rotation = Quaternion.Euler(0, 0, 90);
                    break;
                case ScreenOrientation.LandscapeLeft:
                case ScreenOrientation.LandscapeRight:
                    if (!SimulatorUtilities.IsLandscape(physicalOrientation))
                        rotation = Quaternion.Euler(0, 0, 90);
                    break;
            }

            return rotation;
        }

        private void OnIMGUIRendered()
        {
            if (EditorApplication.isPlaying && !EditorApplication.isPaused)
                EditorGUIUtility.keyboardControl = 0;

            var type = Event.current.type;
            if (type == EventType.Repaint)
            {
                if (PreviewTexture != null && PreviewTexture.IsCreated())
                {
                    LoadResources();
                    RenderPreviewImage();
                    RenderDeviceImage();
                    RenderSafeArea();
                }
            }

            if (type != EventType.Repaint && type != EventType.Layout && type != EventType.Used)
            {
                var useEvent = true;
                if (!Event.current.isKey || (!EditorApplication.isPlaying || EditorApplication.isPaused))
                    return;

                EditorGUIUtility.QueueGameViewInputEvent(Event.current);

                // Don't use command events, or they won't be sent to other views.
                if (type == EventType.ExecuteCommand || type == EventType.ValidateCommand)
                    useEvent = false;

                if (useEvent)
                    Event.current.Use();
            }
        }

        private void LoadResources()
        {
            if (m_PreviewMaterial == null)
                m_PreviewMaterial = GUI.blitMaterial;
            if (m_DeviceMaterial == null)
                m_DeviceMaterial = new Material(Shader.Find("Hidden/Internal-GUITextureClip"));
            m_DeviceInfo.LoadOverlayImage();
        }

        private static readonly float zValue = Vertex.nearZ;

        private void RenderPreviewImage()
        {
            if (PreviewTexture == null)
                return;

            ComputePreviewImageHalfSizeAndOffset(out Vector2 halfSize, out Vector2 offset);

            var vertices = new Vector3[4];
            vertices[0] = new Vector3(-halfSize.x, -halfSize.y, zValue);
            vertices[1] = new Vector3(halfSize.x, -halfSize.y, zValue);
            vertices[2] = new Vector3(halfSize.x, halfSize.y, zValue);
            vertices[3] = new Vector3(-halfSize.x, halfSize.y, zValue);

            var uvs = new Vector2[4];
            SimulatorUtilities.SetTextureCoordinates(TargetOrientation, uvs);

            var mesh = new Mesh()
            {
                vertices = vertices,
                uv = uvs,
                triangles = new[] { 0, 1, 3, 1, 2, 3 }
            };

            m_PreviewMaterial.mainTexture = PreviewTexture;
            m_PreviewMaterial.SetPass(0);

            var transformMatrix = Matrix4x4.TRS(new Vector3(offset.x, offset.y), m_Rotation, Vector3.one);
            Graphics.DrawMeshNow(mesh, transformMatrix);
        }

        private void RenderDeviceImage()
        {
            if (m_DeviceInfo.Screens[0].presentation.overlay == null)
            {
                RenderDeviceBorder();
                return;
            }

            var rect = new Vector4(m_DeviceInfo.Screens[0].width, m_DeviceInfo.Screens[0].height, m_DeviceInfo.Screens[0].width, m_DeviceInfo.Screens[0].height);
            rect = (rect / 2 + m_DeviceInfo.Screens[0].presentation.borderSize);

            var vertices = new Vector3[4];
            vertices[0] = new Vector3(-rect.x, -rect.y, zValue);
            vertices[1] = new Vector3(rect.z, -rect.y, zValue);
            vertices[2] = new Vector3(rect.z, rect.w, zValue);
            vertices[3] = new Vector3(-rect.x, rect.w, zValue);

            var mesh = new Mesh()
            {
                vertices = vertices,
                uv = new[] { new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(0, 0) },
                triangles = new[] { 0, 1, 3, 1, 2, 3 }
            };

            m_DeviceMaterial.mainTexture = m_DeviceInfo.Screens[0].presentation.overlay;
            m_DeviceMaterial.SetPass(0);

            var transformMatrix = Matrix4x4.TRS(new Vector3(m_Offset.x, m_Offset.y), m_Rotation, new Vector3(m_Scale / 100f, m_Scale / 100f));

            Graphics.DrawMeshNow(mesh, transformMatrix);
        }

        private void RenderDeviceBorder()
        {
            // Fallback to draw borders if no overlay image presents.
            var deviceRect = new Vector4(m_DeviceInfo.Screens[0].width, m_DeviceInfo.Screens[0].height, m_DeviceInfo.Screens[0].width, m_DeviceInfo.Screens[0].height) / 2;
            var outerRect = (deviceRect + m_DeviceInfo.Screens[0].presentation.borderSize);

            const float padding = 20;
            var innerRect = outerRect - new Vector4(padding, padding, padding, padding);

            var vertices = new Vector3[16];
            // Outer border.
            vertices[0] = new Vector3(-outerRect.x, -outerRect.y, zValue);
            vertices[1] = new Vector3(outerRect.z, -outerRect.y, zValue);
            vertices[2] = new Vector3(outerRect.z, outerRect.w, zValue);
            vertices[3] = new Vector3(-outerRect.x, outerRect.w, zValue);
            vertices[4] = new Vector3(-innerRect.x, -innerRect.y, zValue);
            vertices[5] = new Vector3(innerRect.z, -innerRect.y, zValue);
            vertices[6] = new Vector3(innerRect.z, innerRect.w, zValue);
            vertices[7] = new Vector3(-innerRect.x, innerRect.w, zValue);

            //Inner border.
            vertices[8] = vertices[4];
            vertices[9] = vertices[5];
            vertices[10] = vertices[6];
            vertices[11] = vertices[7];
            vertices[12] = new Vector3(-deviceRect.x, -deviceRect.y, zValue);
            vertices[13] = new Vector3(deviceRect.z, -deviceRect.y, zValue);
            vertices[14] = new Vector3(deviceRect.z, deviceRect.w, zValue);
            vertices[15] = new Vector3(-deviceRect.x, deviceRect.w, zValue);

            var outerColor = EditorGUIUtility.isProSkin ? new Color(217f / 255, 217f / 255, 217f / 255) : new Color(100f / 255, 100f / 255, 100f / 255);
            var innerColor = new Color(41f / 255, 41f / 255, 41f / 255);

            var mesh = new Mesh()
            {
                vertices = vertices,
                colors = new[]
                {
                    outerColor, outerColor, outerColor, outerColor, outerColor, outerColor, outerColor, outerColor,
                    innerColor, innerColor, innerColor, innerColor, innerColor, innerColor, innerColor, innerColor,
                },
                triangles = new[]
                {
                    0, 4, 1,  1, 4, 5,   1, 5, 6,   1, 6, 2,   6, 7, 2,    7, 3, 2,    0, 3, 4,   3, 7, 4, // Outer
                    8, 12, 9, 9, 12, 13, 9, 13, 14, 9, 14, 10, 14, 15, 10, 15, 11, 10, 8, 11, 12, 11, 15, 12 // Inner
                }
            };

            m_PreviewMaterial.mainTexture = null;
            m_PreviewMaterial.SetPass(0);

            var transformMatrix = Matrix4x4.TRS(new Vector3(m_Offset.x, m_Offset.y), m_Rotation, new Vector3(m_Scale / 100f, m_Scale / 100f));

            Graphics.DrawMeshNow(mesh, transformMatrix);
        }

        private void RenderSafeArea()
        {
            if (!m_HighlightSafeArea)
                return;

            var safeAreaInScreen = GetSafeAreaInScreen();
            var halfImageWidth = safeAreaInScreen.width / 2;
            var halfImageHeight = safeAreaInScreen.height / 2;

            var vertices = new Vector3[8];
            vertices[0] = new Vector3(-halfImageWidth, -halfImageHeight, zValue);
            vertices[1] = new Vector3(halfImageWidth, -halfImageHeight, zValue);
            vertices[2] = new Vector3(halfImageWidth, halfImageHeight, zValue);
            vertices[3] = new Vector3(-halfImageWidth, halfImageHeight, zValue);
            vertices[4] = new Vector3(-halfImageWidth + m_HighlightSafeAreaLineWidth, -halfImageHeight + m_HighlightSafeAreaLineWidth, zValue);
            vertices[5] = new Vector3(halfImageWidth - m_HighlightSafeAreaLineWidth, -halfImageHeight + m_HighlightSafeAreaLineWidth, zValue);
            vertices[6] = new Vector3(halfImageWidth - m_HighlightSafeAreaLineWidth, halfImageHeight - m_HighlightSafeAreaLineWidth, zValue);
            vertices[7] = new Vector3(-halfImageWidth + m_HighlightSafeAreaLineWidth, halfImageHeight - m_HighlightSafeAreaLineWidth, zValue);

            var offset = new Vector2(m_Offset.x + safeAreaInScreen.x / 2, m_Offset.y + safeAreaInScreen.y / 2);
            offset = ComputeOffsetForScreenMode(offset, true);

            var color = m_HighlightSafeAreaColor;
            var mesh = new Mesh()
            {
                vertices = vertices,
                colors = new[] { color, color, color, color, color, color, color, color },
                triangles = new[] { 0, 4, 1, 1, 4, 5, 1, 5, 6, 1, 6, 2, 6, 7, 2, 7, 3, 2, 0, 3, 4, 3, 7, 4 }
            };

            m_PreviewMaterial.mainTexture = null;
            m_PreviewMaterial.SetPass(0);

            var transformMatrix = Matrix4x4.TRS(new Vector3(offset.x, offset.y), ComputeRotationForHighlightSafeArea(), Vector3.one);
            Graphics.DrawMeshNow(mesh, transformMatrix);
        }

        // Only gets called during switching device.
        public void Update(DeviceInfo deviceInfo, bool fullScreen)
        {
            m_DeviceInfo = deviceInfo;
            IsFullScreen = fullScreen;

            OnStateChanged();

            if (m_FitToScreenEnabled)
                FitToScreenScale();
        }

        public void OnSimulationStateChanged(SimulationState simulationState)
        {
            SetInactiveMsgState(simulationState == SimulationState.Disabled);
        }

        private void ComputeBoundingBox()
        {
            var overlayOffset = m_DeviceInfo.Screens[0].presentation.borderSize;
            var width = m_DeviceInfo.Screens[0].width + overlayOffset.x + overlayOffset.z;
            var height = m_DeviceInfo.Screens[0].height + overlayOffset.y + overlayOffset.w;
            var toScreenCenter = new Vector2(m_DeviceInfo.Screens[0].width / 2f + overlayOffset.x, m_DeviceInfo.Screens[0].height / 2f + overlayOffset.y);

            var vertices = new Vector3[4];
            vertices[0] = new Vector3(0, 0, Vertex.nearZ);
            vertices[1] = new Vector3(width, 0, Vertex.nearZ);
            vertices[2] = new Vector3(width, height, Vertex.nearZ);
            vertices[3] = new Vector3(0, height, Vertex.nearZ);

            var scale = m_Scale / 100f;
            Matrix4x4 transformMatrix = Matrix4x4.TRS(new Vector3(0, 0, 0), m_Rotation, new Vector3(scale, scale));

            for (int index = 0; index < vertices.Length; ++index)
            {
                vertices[index] = transformMatrix.MultiplyPoint(vertices[index]);
            }

            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);

            foreach (var vertex in vertices)
            {
                if (vertex.x < min.x)
                    min.x = vertex.x;
                if (vertex.x > max.x)
                    max.x = vertex.x;

                if (vertex.y < min.y)
                    min.y = vertex.y;
                if (vertex.y > max.y)
                    max.y = vertex.y;
            }

            m_BoundingBox = max - min + new Vector2(2, 2);
            m_Offset = m_BoundingBox / 2;

            // We need to consider the case that overlay offset is not symmetrical.
            var physicalOrientation = SimulatorUtilities.RotationToScreenOrientation(m_Rotation);
            switch (physicalOrientation)
            {
                case ScreenOrientation.Portrait:
                    m_Offset.x -= (overlayOffset.z - overlayOffset.x) * scale / 2;
                    m_Offset.y -= (overlayOffset.w - overlayOffset.y) * scale / 2;
                    break;
                case ScreenOrientation.PortraitUpsideDown:
                    m_Offset.x += (overlayOffset.z - overlayOffset.x) * scale / 2;
                    m_Offset.y += (overlayOffset.w - overlayOffset.y) * scale / 2;
                    break;
                case ScreenOrientation.Landscape:
                    m_Offset.x -= (overlayOffset.w - overlayOffset.y) * scale / 2;
                    m_Offset.y += (overlayOffset.z - overlayOffset.x) * scale / 2;
                    break;
                case ScreenOrientation.LandscapeRight:
                    m_Offset.x += (overlayOffset.w - overlayOffset.y) * scale / 2;
                    m_Offset.y -= (overlayOffset.z - overlayOffset.x) * scale / 2;
                    break;
            }

            // Device space: (0,0) at the left top corner of the device in portrait orientation, 1 unit is 1 screen pixel of the device screen
            var deviceSpaceToPreviewImageRendererSpace =
                Matrix4x4.TRS(new Vector3(m_Offset.x, m_Offset.y), m_Rotation, new Vector3(scale, scale, 1)) *
                Matrix4x4.Translate(new Vector3(-toScreenCenter.x, -toScreenCenter.y, 0));
            var deviceSpaceToScreenSpace = Matrix4x4.Translate(new Vector3(-overlayOffset.x, -overlayOffset.y));
            m_TouchEventManipulator.PreviewImageRendererSpaceToScreenSpace = deviceSpaceToScreenSpace * deviceSpaceToPreviewImageRendererSpace.inverse;

            m_PreviewRenderer.style.width = m_BoundingBox.x;
            m_PreviewRenderer.style.height = m_BoundingBox.y;
        }
    }
}
