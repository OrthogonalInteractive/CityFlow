#nullable enable

using System.Linq;
using CityFlow.Application.Connections;
using CityFlow.Application.UseCases;
using CityFlow.Domain.Spatial;
using CityFlow.Presentation.Connections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace CityFlow.Presentation.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class NodeMinimapView : MonoBehaviour
    {
        private const int TextureSize = 512;
        private ConnectionSession? session;
        private NodeConnectionController? controller;
        private FlowSimulation? simulation;
        private Camera? sceneCamera, miniCamera;
        private UIDocument? document;
        private RenderTexture? texture;
        private VisualElement? root, panel, heading;
        private Image? image;
        private float groundHeight, cameraHeight;

        public void Initialize(ConnectionSession connection, NodeConnectionController cameraController,
            FlowSimulation clock, StageDefinition stage, Camera mainCamera)
        {
            session = connection;
            controller = cameraController;
            simulation = clock;
            sceneCamera = mainCamera;
            groundHeight = stage.GroundHeight;
            cameraHeight = Mathf.Max(groundHeight, stage.Buildings.Select(b => b.max.y).DefaultIfEmpty(groundHeight).Max()) + 50;
            document = GetComponent<UIDocument>();
            if (isActiveAndEnabled) Bind();
        }

        private void OnEnable() => Bind();

        private void Bind()
        {
            Unbind();
            if (document == null) return;
            root = document.rootVisualElement;
            if (root == null) return;
            panel = root.Q("node-minimap");
            image = root.Q<Image>("node-minimap-image");
            heading = root.Q("node-minimap-heading");
            if (image != null) image.image = texture;
            if (heading != null) heading.generateVisualContent += DrawHeading;
        }

        private void LateUpdate()
        {
            if (document == null || controller == null || session == null || simulation == null || sceneCamera == null) return;
            if (root != document.rootVisualElement) Bind();
            if (root == null || panel == null || image == null)
            {
                if (miniCamera != null) miniCamera.enabled = false;
                return;
            }
            bool visible = controller.IsNode360 && !controller.IsEditing && simulation.Result == null;
            panel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible)
            {
                if (miniCamera != null) miniCamera.enabled = false;
                return;
            }
            var source = session.Nodes.FirstOrDefault(n => n.Id == session.SourceId);
            if (source == null) return;
            EnsureCamera();
            if (miniCamera == null) return;
            // Keep the source centered even when it lies near a stage boundary.
            miniCamera.transform.SetPositionAndRotation(new Vector3(source.Position.x, cameraHeight, source.Position.z),
                Quaternion.Euler(90, 0, 0));
            float rangeScale = OverlayLayout.Value(panel, "--minimap-range-scale", 1.5f);
            miniCamera.orthographicSize = session.NearLimit * rangeScale;
            miniCamera.enabled = true;
            image.image = texture;
            root.Q<Label>("node-minimap-title").text = source.Id + " · LOCAL VIEW";
            root.Q<Label>("node-minimap-scale").text = $"{miniCamera.orthographicSize * 2:0} m across";
            heading?.MarkDirtyRepaint();
        }

        private void EnsureCamera()
        {
            if (miniCamera != null || sceneCamera == null) return;
            texture = new RenderTexture(TextureSize, TextureSize, 24, RenderTextureFormat.ARGB32)
            {
                name = "Node 360 Mini View",
                hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Bilinear,
                antiAliasing = 1
            };
            texture.Create();
            var cameraObject = new GameObject("Node 360 Mini Camera") { hideFlags = HideFlags.DontSave };
            cameraObject.transform.SetParent(transform, false);
            miniCamera = cameraObject.AddComponent<Camera>();
            miniCamera.enabled = false;
            miniCamera.orthographic = true;
            miniCamera.aspect = 1;
            miniCamera.nearClipPlane = 0.1f;
            miniCamera.farClipPlane = cameraHeight - groundHeight + 10;
            miniCamera.clearFlags = CameraClearFlags.SolidColor;
            miniCamera.backgroundColor = sceneCamera.backgroundColor;
            miniCamera.cullingMask = sceneCamera.cullingMask;
            miniCamera.allowHDR = false;
            miniCamera.allowMSAA = false;
            miniCamera.depth = sceneCamera.depth - 1;
            miniCamera.targetTexture = texture;
            var rendering = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            rendering.renderShadows = false;
            rendering.renderPostProcessing = false;
            rendering.requiresColorOption = CameraOverrideOption.Off;
            rendering.requiresDepthOption = CameraOverrideOption.Off;
        }

        private void DrawHeading(MeshGenerationContext context)
        {
            if (heading == null || sceneCamera == null || miniCamera == null || !miniCamera.enabled) return;
            Vector2 center = heading.contentRect.center;
            float radius = OverlayLayout.Value(heading, "--minimap-heading-radius", 70);
            Vector3 forward = Vector3.ProjectOnPlane(sceneCamera.transform.forward, Vector3.up).normalized;
            float halfAngle = Mathf.Atan(Mathf.Tan(sceneCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * sceneCamera.aspect) * Mathf.Rad2Deg;
            Vector2 left = Direction(Quaternion.AngleAxis(-halfAngle, Vector3.up) * forward);
            Vector2 right = Direction(Quaternion.AngleAxis(halfAngle, Vector3.up) * forward);
            var painter = context.painter2D;
            Color color = heading.resolvedStyle.color;
            painter.fillColor = new Color(color.r, color.g, color.b, OverlayLayout.Value(heading, "--minimap-cone-opacity", 0.12f));
            painter.strokeColor = color;
            painter.lineWidth = OverlayLayout.Value(heading, "--minimap-stroke-width", 1.5f);
            painter.BeginPath();
            painter.MoveTo(center);
            painter.LineTo(center + left * radius);
            painter.LineTo(center + right * radius);
            painter.ClosePath();
            painter.Fill();
            painter.Stroke();
            painter.BeginPath();
            painter.MoveTo(center);
            painter.LineTo(center + Direction(forward) * radius);
            painter.Stroke();

            Vector2 Direction(Vector3 world) => new Vector2(Vector3.Dot(world, miniCamera.transform.right),
                -Vector3.Dot(world, miniCamera.transform.up)).normalized;
        }

        private void Unbind()
        {
            if (heading != null) heading.generateVisualContent -= DrawHeading;
            if (image != null) image.image = null;
            if (panel != null) panel.style.display = DisplayStyle.None;
            root = panel = heading = null;
            image = null;
        }

        private void OnDisable()
        {
            Unbind();
            if (miniCamera != null)
            {
                miniCamera.enabled = false;
                miniCamera.targetTexture = null;
                Destroy(miniCamera.gameObject);
            }
            miniCamera = null;
            if (texture != null)
            {
                texture.Release();
                Destroy(texture);
            }
            texture = null;
        }
    }
}
