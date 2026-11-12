using UnityEngine;
using UnityEngine.UI;

namespace DiceCraft
{
    /// <summary>
    /// v0.73: draws interface dice above interface panels. A second camera with the table camera's
    /// pose renders only the interface-dice layer into a transparent screen-sized texture, which one
    /// full-screen image shows between the screen layer and the modal layer (or above the modal
    /// layer while a modal owns the dice). Idle when no interface die is visible.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class Art073DiceLayer : MonoBehaviour
    {
        public const int Layer = 30;
        private DiceTable dice;
        private Camera source, view;
        private RenderTexture texture;
        private RawImage image;
        private RectTransform screenLayer, modalLayer;
        private System.Func<bool> modalOpen;
        public DiceTable Dice => dice;
        public Camera View => view;
        public RawImage Image => image;
        public RenderTexture Texture => texture;

        public void Configure(DiceTable table, Canvas canvas, RectTransform screen, RectTransform modal, Material imageMaterial, System.Func<bool> isModal)
        {
            source = table.viewCamera; screenLayer = screen; modalLayer = modal; modalOpen = isModal;
            source.cullingMask &= ~(1 << Layer);

            var cameraObject = new GameObject("UiDiceCamera073");
            cameraObject.transform.SetParent(source.transform, false);
            view = cameraObject.AddComponent<Camera>();
            view.CopyFrom(source);
            view.cullingMask = 1 << Layer; view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = new Color(0, 0, 0, 0);
            view.depth = source.depth - 1; view.allowHDR = false; view.allowMSAA = true; view.enabled = false;

            var tableObject = new GameObject("UiDice073");
            tableObject.transform.SetParent(table.transform.parent, false);
            tableObject.transform.SetPositionAndRotation(table.transform.position, table.transform.rotation);
            dice = tableObject.AddComponent<DiceTable>();
            dice.viewCamera = view; dice.bodyMesh = table.bodyMesh; dice.bodyMaterial = table.bodyMaterial; dice.faceMaterial = table.faceMaterial;
            dice.glassMaterial = table.glassMaterial; dice.outlineMaterial = table.outlineMaterial; dice.styleMaterial = table.styleMaterial;
            dice.designRoot = table.designRoot; dice.visualLayer = Layer; dice.castShadows = false;

            var imageObject = new GameObject("UiDiceLayer073", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            var rect = (RectTransform)imageObject.transform; rect.SetParent(canvas.transform, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            image = imageObject.GetComponent<RawImage>(); image.raycastTarget = false; image.enabled = false;
            if (imageMaterial == null)
            {
                var shader = Shader.Find("DiceCraft/Art073/UiPremultiplied");
                if (shader != null) imageMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
            image.material = imageMaterial;
        }

        private void LateUpdate()
        {
            if (dice == null || view == null) return;
            bool any = dice.ActiveVisualCount > 0;
            if (any && (texture == null || texture.width != Screen.width || texture.height != Screen.height))
            {
                if (texture != null) { view.targetTexture = null; texture.Release(); Destroy(texture); }
                texture = new RenderTexture(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height), 24, RenderTextureFormat.ARGB32) { name = "UiDice073", antiAliasing = 4, useMipMap = false };
                view.targetTexture = texture; image.texture = texture;
            }
            // Same pose and projection as the table camera every frame (resolution or safe-area changes).
            if (any) { view.orthographic = source.orthographic; view.orthographicSize = source.orthographicSize; view.aspect = source.aspect; }
            view.enabled = any; image.enabled = any && texture != null;
            var anchor = modalOpen != null && modalOpen() ? modalLayer : screenLayer;
            if (anchor == null) return;
            int at = anchor.GetSiblingIndex(), mine = image.transform.GetSiblingIndex();
            if (mine < at) image.transform.SetSiblingIndex(at);
            else if (mine > at + 1) image.transform.SetSiblingIndex(at + 1);
        }

        private void OnDestroy()
        {
            if (view != null) view.targetTexture = null;
            if (texture != null) { texture.Release(); Destroy(texture); }
        }
    }
}
