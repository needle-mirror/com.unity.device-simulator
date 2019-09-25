using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.DeviceSimulator
{
    internal class SimulatorPreviewPanel : IDisposable
    {
        private VisualElement m_RootElement = null;
        private InputProvider m_InputProvider = null;
        private DeviceInfo m_DeviceInfo = null;

        public Func<Vector2, bool, RenderTexture> OnPreview { get; set; }

        private int m_Scale = 20; // Value from (0, 100].
        private const int kScaleMin = 10;
        private const int kScaleMax = 100;
        private bool m_FitToScreenEnabled = true;

        private int m_RotationDegree = 0; // Value from [0, 360), counted as CCW(convert to CW in the future?).
        private Quaternion m_Rotation = Quaternion.identity;

        private bool m_HighlightSafeArea = false;

        public int Scale => m_Scale;
        public bool FitToScreenEnabled => m_FitToScreenEnabled;
        public int RotationDegree => m_RotationDegree;

        public bool HighlightSafeAre => m_HighlightSafeArea;

        // Controls for preview toolbar.
        private SliderInt m_ScaleSlider = null;
        private Label m_ScaleValueLabel = null;
        private ToolbarToggle m_FitToScreenToggle = null;

        // Controls for inactive message.
        private VisualElement m_InactiveMsgContainer = null;

        // Controls for preview.
        private VisualElement m_ScrollViewContainer = null;
        private VisualElement m_PreviewImageRenderer = null;
        private RenderTexture m_PreviewImage = null;
        private Material m_PreviewMaterial = null;

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
            EditorApplication.playModeStateChanged += OnEditorPlayModeStateChanged;

            Init(states);
        }

        public void Dispose()
        {
            EditorApplication.playModeStateChanged -= OnEditorPlayModeStateChanged;
        }

        private void OnEditorPlayModeStateChanged(PlayModeStateChange state)
        {
            // Workaround for issue https://github.com/Unity-Technologies/com.unity.device-simulator/issues/35.
            // Here we register a callback for play mode state change to reinitialize the preview material and trigger a repaint.
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                m_PreviewMaterial = new Material(Shader.Find("Hidden/DeviceSimulator/Preview"));
                m_PreviewImageRenderer.MarkDirtyRepaint();
            }
        }

        private void Init(SimulatorJsonSerialization states)
        {
            if (states != null)
            {
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

            var imguiContainer = m_RootElement.Q<IMGUIContainer>("preview-imgui-renderer");
            imguiContainer.onGUIHandler = OnIMGUIRendered;

            m_PreviewImageRenderer = m_RootElement.Q<VisualElement>("preview-image-renderer");
            m_PreviewImageRenderer.generateVisualContent += DrawPreviewImage;
            m_PreviewImageRenderer.generateVisualContent += DrawDeviceImage;
            m_PreviewImageRenderer.generateVisualContent += DrawHighlightSafeArea;

            m_PreviewImageRenderer.AddManipulator(m_TouchEventManipulator = new TouchEventManipulator(m_InputProvider));

            m_PreviewMaterial = new Material(Shader.Find("Hidden/DeviceSimulator/Preview"));
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

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            if (m_FitToScreenEnabled)
                FitToScreenScale();
        }

        private void DrawPreviewImage(MeshGenerationContext mgc)
        {
            if (m_PreviewImage == null)
                return;

            ComputePreviewImageHalfSizeAndOffset(out Vector2 halfSize, out Vector2 offset);

            var meshWriteData = mgc.Allocate(4, 6, m_PreviewImage, m_PreviewMaterial, MeshGenerationContext.MeshFlags.None);

            var vertices = new Vertex[4];
            vertices[0].position = new Vector3(-halfSize.x, -halfSize.y, Vertex.nearZ);
            vertices[0].tint = Color.white;

            vertices[1].position = new Vector3(halfSize.x, -halfSize.y, Vertex.nearZ);
            vertices[1].tint = Color.white;

            vertices[2].position = new Vector3(halfSize.x, halfSize.y, Vertex.nearZ);
            vertices[2].tint = Color.white;

            vertices[3].position = new Vector3(-halfSize.x, halfSize.y, Vertex.nearZ);
            vertices[3].tint = Color.white;

            SimulatorUtilities.SetTextureCoordinates(TargetOrientation, vertices);
            SimulatorUtilities.TransformVertices(m_Rotation, offset, vertices);

            meshWriteData.SetAllVertices(vertices);

            var indices = new ushort[] { 0, 3, 1, 1, 3, 2 };
            meshWriteData.SetAllIndices(indices);
        }

        private void ComputePreviewImageHalfSizeAndOffset(out Vector2 halfSize, out Vector2 offset)
        {
            var imageWidth = m_DeviceInfo.Screens[0].width;
            var imageHeight = m_DeviceInfo.Screens[0].height;

            if (!IsFullScreen)
                imageHeight = (int)m_DeviceInfo.Screens[0].orientations[ScreenOrientation.Portrait].safeArea.height - m_DeviceInfo.Screens[0].navigationBarHeight;

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

            var safeArea = m_DeviceInfo.Screens[0].orientations[TargetOrientation].safeArea;
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

        private void DrawDeviceImage(MeshGenerationContext mgc)
        {
            if (m_DeviceInfo.Meta.overlayImage == null)
            {
                DrawDeviceBorder(mgc);
                return;
            }

            var scale = m_Scale / 100f;
            var halfImageWidth = scale * m_DeviceInfo.Screens[0].width / 2;
            var halfImageHeight = scale * m_DeviceInfo.Screens[0].height / 2;

            var leftWidth = halfImageWidth + scale * m_DeviceInfo.Meta.overlayOffset.x;
            var topWidth = halfImageHeight + scale * m_DeviceInfo.Meta.overlayOffset.y;
            var rightWidth = halfImageWidth + scale * m_DeviceInfo.Meta.overlayOffset.z;
            var bottomWidth = halfImageHeight + scale * m_DeviceInfo.Meta.overlayOffset.w;

            var meshWriteData = mgc.Allocate(4, 6, m_DeviceInfo.Meta.overlayImage);

            var vertices = new Vertex[4];
            vertices[0].position = new Vector3(-leftWidth, -topWidth, Vertex.nearZ);
            vertices[0].tint = Color.white;
            vertices[0].uv = new Vector2(0, 1);

            vertices[1].position = new Vector3(rightWidth, -topWidth, Vertex.nearZ);
            vertices[1].tint = Color.white;
            vertices[1].uv = new Vector2(1, 1);

            vertices[2].position = new Vector3(rightWidth, bottomWidth, Vertex.nearZ);
            vertices[2].tint = Color.white;
            vertices[2].uv = new Vector2(1, 0);

            vertices[3].position = new Vector3(-leftWidth, bottomWidth, Vertex.nearZ);
            vertices[3].tint = Color.white;
            vertices[3].uv = new Vector2(0, 0);

            SimulatorUtilities.TransformVertices(m_Rotation, m_Offset, vertices);

            meshWriteData.SetAllVertices(vertices);

            var indices = new ushort[] { 0, 3, 1, 1, 3, 2 };
            meshWriteData.SetAllIndices(indices);
        }

        private void DrawDeviceBorder(MeshGenerationContext mgc)
        {
            // For now, we draw device as borders. We can draw device image in the future.
            var scale = m_Scale / 100f;
            var halfImageWidth = scale * m_DeviceInfo.Screens[0].width / 2;
            var halfImageHeight = scale * m_DeviceInfo.Screens[0].height / 2;

            var leftOuterWidth = halfImageWidth + scale * m_DeviceInfo.Meta.overlayOffset.x;
            var topOuterWidth = halfImageHeight + scale * m_DeviceInfo.Meta.overlayOffset.y;
            var rightOuterWidth = halfImageWidth + scale * m_DeviceInfo.Meta.overlayOffset.z;
            var bottomOuterWidth = halfImageHeight + scale * m_DeviceInfo.Meta.overlayOffset.w;

            var padding = 20 * scale;
            var leftInnerWidth = leftOuterWidth - padding;
            var topInnerWidth = topOuterWidth - padding;
            var rightInnerWidth = rightOuterWidth - padding;
            var bottomInnerWidth = bottomOuterWidth - padding;

            var outerColor = EditorGUIUtility.isProSkin ? new Color(217f / 255, 217f / 255, 217f / 255) : new Color(100f / 255, 100f / 255, 100f / 255);
            var innerColor = new Color(41f / 255, 41f / 255, 41f / 255);

            const int vertexCount = 16, indexCount = 48;
            var meshWriteData = mgc.Allocate(vertexCount, indexCount);
            var vertices = new Vertex[vertexCount];

            // Outer border.
            vertices[0].position = new Vector3(-leftOuterWidth, -topOuterWidth, Vertex.nearZ);
            vertices[0].tint = outerColor;

            vertices[1].position = new Vector3(rightOuterWidth, -topOuterWidth, Vertex.nearZ);
            vertices[1].tint = outerColor;

            vertices[2].position = new Vector3(rightOuterWidth, bottomOuterWidth, Vertex.nearZ);
            vertices[2].tint = outerColor;

            vertices[3].position = new Vector3(-leftOuterWidth, bottomOuterWidth, Vertex.nearZ);
            vertices[3].tint = outerColor;

            vertices[4].position = new Vector3(-leftInnerWidth, -topInnerWidth, Vertex.nearZ);
            vertices[4].tint = outerColor;

            vertices[5].position = new Vector3(rightInnerWidth, -topInnerWidth, Vertex.nearZ);
            vertices[5].tint = outerColor;

            vertices[6].position = new Vector3(rightInnerWidth, bottomInnerWidth, Vertex.nearZ);
            vertices[6].tint = outerColor;

            vertices[7].position = new Vector3(-leftInnerWidth, bottomInnerWidth, Vertex.nearZ);
            vertices[7].tint = outerColor;

            //Inner border.
            vertices[8].position = vertices[4].position;
            vertices[8].tint = innerColor;

            vertices[9].position = vertices[5].position;
            vertices[9].tint = innerColor;

            vertices[10].position = vertices[6].position;
            vertices[10].tint = innerColor;

            vertices[11].position = vertices[7].position;
            vertices[11].tint = innerColor;

            vertices[12].position = new Vector3(-halfImageWidth, -halfImageHeight, Vertex.nearZ);
            vertices[12].tint = innerColor;

            vertices[13].position = new Vector3(halfImageWidth, -halfImageHeight, Vertex.nearZ);
            vertices[13].tint = innerColor;

            vertices[14].position = new Vector3(halfImageWidth, halfImageHeight, Vertex.nearZ);
            vertices[14].tint = innerColor;

            vertices[15].position = new Vector3(-halfImageWidth, halfImageHeight, Vertex.nearZ);
            vertices[15].tint = innerColor;

            SimulatorUtilities.TransformVertices(m_Rotation, m_Offset, vertices);

            meshWriteData.SetAllVertices(vertices);

            var indices = new ushort[]
            {
                0, 4, 1,  1, 4, 5,   1, 5, 6,   1, 6, 2,   6, 7, 2,    7, 3, 2,    0, 3, 4,   3, 7, 4, // Outer
                8, 12, 9, 9, 12, 13, 9, 13, 14, 9, 14, 10, 14, 15, 10, 15, 11, 10, 8, 11, 12, 11, 15, 12 // Inner
            };
            meshWriteData.SetAllIndices(indices);
        }

        private Rect GetSafeAreaInScreen()
        {
            var sa = m_DeviceInfo.Screens[0].orientations[TargetOrientation].safeArea;
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

        private void DrawHighlightSafeArea(MeshGenerationContext mgc)
        {
            if (!m_HighlightSafeArea)
                return;

            var safeAreaInScreen = GetSafeAreaInScreen();
            var halfImageWidth = safeAreaInScreen.width / 2;
            var halfImageHeight = safeAreaInScreen.height / 2;

            const int vertexCount = 8, indexCount = 24;
            var meshWriteData = mgc.Allocate(vertexCount, indexCount);
            var vertices = new Vertex[vertexCount];

            var highlightColor = Color.green;
            const int highlightLineWidth = 2;

            vertices[0].position = new Vector3(-halfImageWidth, -halfImageHeight, Vertex.nearZ);
            vertices[0].tint = highlightColor;

            vertices[1].position = new Vector3(halfImageWidth, -halfImageHeight, Vertex.nearZ);
            vertices[1].tint = highlightColor;

            vertices[2].position = new Vector3(halfImageWidth, halfImageHeight, Vertex.nearZ);
            vertices[2].tint = highlightColor;

            vertices[3].position = new Vector3(-halfImageWidth, halfImageHeight, Vertex.nearZ);
            vertices[3].tint = highlightColor;

            vertices[4].position = new Vector3(-halfImageWidth + highlightLineWidth, -halfImageHeight + highlightLineWidth, Vertex.nearZ);
            vertices[4].tint = highlightColor;

            vertices[5].position = new Vector3(halfImageWidth - highlightLineWidth, -halfImageHeight + highlightLineWidth, Vertex.nearZ);
            vertices[5].tint = highlightColor;

            vertices[6].position = new Vector3(halfImageWidth - highlightLineWidth, halfImageHeight - highlightLineWidth, Vertex.nearZ);
            vertices[6].tint = highlightColor;

            vertices[7].position = new Vector3(-halfImageWidth + highlightLineWidth, halfImageHeight - highlightLineWidth, Vertex.nearZ);
            vertices[7].tint = highlightColor;

            var offset = new Vector2(m_Offset.x + safeAreaInScreen.x / 2, m_Offset.y + safeAreaInScreen.y / 2);
            offset = ComputeOffsetForScreenMode(offset, true);

            SimulatorUtilities.TransformVertices(ComputeRotationForHighlightSafeArea(), offset, vertices);

            meshWriteData.SetAllVertices(vertices);

            var indices = new ushort[]
            {
                0, 4, 1, 1, 4, 5, 1, 5, 6, 1, 6, 2, 6, 7, 2, 7, 3, 2, 0, 3, 4, 3, 7, 4
            };
            meshWriteData.SetAllIndices(indices);
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
            if (type == EventType.Repaint || m_PreviewImage == null)
            {
                var renderTexture = OnPreview(Event.current.mousePosition, false);
                if (renderTexture.IsCreated())
                    m_PreviewImage = renderTexture;
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

        // Only gets called during switching device.
        public void Update(DeviceInfo deviceInfo, bool fullScreen)
        {
            m_DeviceInfo = deviceInfo;
            IsFullScreen = fullScreen;

            OnStateChanged();

            if (m_FitToScreenEnabled)
                FitToScreenScale();
        }

        public void OnStateChanged()
        {
            m_PreviewImageRenderer.MarkDirtyRepaint();
            ComputeBoundingBox();
        }

        public void OnSimulationStateChanged(SimulationState simulationState)
        {
            SetInactiveMsgState(simulationState == SimulationState.Disabled);
        }

        private void ComputeBoundingBox()
        {
            var overlayOffset = m_DeviceInfo.Meta.overlayOffset;
            var width = m_DeviceInfo.Screens[0].width + overlayOffset.x + overlayOffset.z;
            var height = m_DeviceInfo.Screens[0].height + overlayOffset.y + overlayOffset.w;
            var toScreenCenter = new Vector2(m_DeviceInfo.Screens[0].width / 2 + overlayOffset.x, m_DeviceInfo.Screens[0].height / 2 + overlayOffset.y);

            var vertices = new Vector3[4];
            vertices[0] = new Vector3(0, 0, Vertex.nearZ);
            vertices[1] = new Vector3(width, 0, Vertex.nearZ);
            vertices[2] = new Vector3(width, height, Vertex.nearZ);
            vertices[3] = new Vector3(0, height, Vertex.nearZ);

            var scale = m_Scale / 100f;
            Matrix4x4 transformMatrix = Matrix4x4.TRS(
                new Vector3(0, 0, 0), m_Rotation, new Vector3(scale, scale));

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

            m_BoundingBox = max - min;
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

            m_PreviewImageRenderer.style.width = m_BoundingBox.x;
            m_PreviewImageRenderer.style.height = m_BoundingBox.y;
        }
    }
}
