using System;
using DiceCraft.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DiceCraft
{
    /// <summary>
    /// v0.73: one real die inside an interface rectangle. Drag turns it, a click on a face can be
    /// reported, and it can turn itself to show a given face. It only ever draws the permanent face
    /// data it was given: no roll, no rule state, no random numbers.
    /// </summary>
    public sealed class Art073DieView : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public const float TurnSeconds = .18f, DragDegreesPerPixel = .55f, SpinDegreesPerSecond = 30f;
        public Action clicked;
        public Action<int> faceClicked;
        /// <summary>Raised after any drag step, with the face now pointing at the viewer.</summary>
        public Action<int> turned;
        private DiceTable table;
        private RectTransform rect, viewport;
        private DieInstance die;
        private string slot, identity;
        private float size;
        private Func<bool> canShow;
        private bool draggable, spin, dragging, userTurned, hasPose;
        private Quaternion rotation = Quaternion.identity, turnFrom, turnTo, lastRotation;
        private float turnElapsed = -1;
        private CanvasGroup[] groups;
        private Matrix4x4 lastMatrix;
        private Vector2 lastScreen;
        private readonly Vector3[] corners = new Vector3[4];

        public string Slot => slot;
        public bool IsShown => hasPose;
        public bool IsTurning => turnElapsed >= 0;
        public Quaternion Rotation => rotation;
        public int FacingFace => table != null && slot != null ? table.FacingFace073(slot) : -1;

        public void Bind(DiceTable owner, string key, DieInstance snapshot, float scale, Quaternion initial, Func<bool> visible,
            RectTransform clip = null, bool drag = true, bool idleSpin = false)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (table != null && (table != owner || slot != key)) HideOwn();
            string next = key + "|" + snapshot.dieId;
            table = owner; rect = (RectTransform)transform; viewport = clip; die = snapshot; size = scale; canShow = visible; draggable = drag; spin = idleSpin;
            // The same die in the same place keeps the pose the player left it in across redraws.
            if (identity != next) { rotation = initial; userTurned = false; turnElapsed = -1; dragging = false; }
            identity = next; slot = key; hasPose = false; groups = null; enabled = true;
            var hit = UiKit.Component<UnityEngine.UI.Image>(gameObject); hit.sprite = null; hit.color = Color.clear; hit.raycastTarget = true;
        }

        /// <summary>Turns the die so a physical face (0..5) looks at the viewer.</summary>
        public void TurnToFace(int face, bool instant = false)
        {
            if (table == null) return;
            turnTo = table.FaceToCamera073(face); userTurned = true;
            if (instant || UiKit.ReducedMotion) { rotation = turnTo; turnElapsed = -1; }
            else { turnFrom = rotation; turnElapsed = 0; }
        }

        /// <summary>Applies a drag of the given screen delta. Public so input can be driven without a pointer.</summary>
        public void Turn(Vector2 screenDelta)
        {
            if (table == null || table.viewCamera == null) return;
            var eye = table.viewCamera.transform; float k = DragDegreesPerPixel * 1080f / Mathf.Max(1, Screen.height);
            rotation = Quaternion.AngleAxis(-screenDelta.x * k, eye.up) * Quaternion.AngleAxis(screenDelta.y * k, eye.right) * rotation;
            userTurned = true; turnElapsed = -1;
        }

        private void LateUpdate()
        {
            if (table == null || die == null || canShow != null && !canShow() || !Ready()) { HideOwn(); return; }
            float dt = Time.unscaledDeltaTime;
            if (turnElapsed >= 0)
            {
                turnElapsed += dt; float t = Mathf.Clamp01(turnElapsed / TurnSeconds);
                rotation = Quaternion.Slerp(turnFrom, turnTo, t * t * (3 - 2 * t));
                if (t >= 1) { rotation = turnTo; turnElapsed = -1; }
            }
            else if (spin && !userTurned && !dragging && !UiKit.ReducedMotion)
                rotation = Quaternion.AngleAxis(SpinDegreesPerSecond * dt, Vector3.up) * rotation;
            var screen = new Vector2(Screen.width, Screen.height); var matrix = rect.localToWorldMatrix;
            var pose = table.GetFaceOrientation("$static-preview:" + slot);
            if (hasPose && matrix == lastMatrix && screen == lastScreen && rotation == lastRotation && pose != null && pose.gameObject.activeInHierarchy) return;
            Vector3 local = table.designRoot.InverseTransformPoint(rect.TransformPoint(rect.rect.center));
            table.ShowStaticPreview07(slot, die, new Vector2(local.x, -local.y), rotation, size);
            lastMatrix = matrix; lastScreen = screen; lastRotation = rotation; hasPose = true;
        }

        private bool Ready()
        {
            if (!rect.gameObject.activeInHierarchy || table.designRoot == null || table.viewCamera == null) return false;
            // Wait for a fading panel: a die drawn at full strength over a half-faded card reads as a glitch.
            if (groups == null) groups = rect.GetComponentsInParent<CanvasGroup>();
            foreach (var group in groups) if (group != null && group.alpha < .999f) return false;
            if (viewport == null) return true;
            // A canvas mask cannot clip the dice texture: hide the die unless its whole cell is inside the viewport.
            rect.GetWorldCorners(corners); var area = viewport.rect;
            foreach (var corner in corners)
            {
                var p = viewport.InverseTransformPoint(corner);
                if (p.x < area.xMin - .5f || p.x > area.xMax + .5f || p.y < area.yMin - .5f || p.y > area.yMax + .5f) return false;
            }
            return true;
        }

        private void HideOwn()
        {
            hasPose = false;
            if (table != null && !string.IsNullOrEmpty(slot)) table.HideStaticPreview073(slot);
        }

        public void OnBeginDrag(PointerEventData e) { dragging = draggable; }
        public void OnDrag(PointerEventData e)
        {
            if (!dragging) return;
            Turn(e.delta); turned?.Invoke(FacingFaceAfter());
        }
        public void OnEndDrag(PointerEventData e) { dragging = false; }
        public void OnPointerClick(PointerEventData e)
        {
            if (e.dragging) return;
            if (faceClicked != null && table != null && table.TryPickFace073(slot, e.position, out int face)) { faceClicked(face); return; }
            clicked?.Invoke();
        }
        // The visual is posed in LateUpdate; during a drag report the face from the pending rotation.
        private int FacingFaceAfter()
        {
            Vector3 toward = -table.viewCamera.transform.forward; int best = 0; float bestDot = float.NegativeInfinity;
            for (int f = 0; f < 6; f++) { float dot = Vector3.Dot(rotation * DiceTable.Normals[f], toward); if (dot > bestDot) { bestDot = dot; best = f; } }
            return best;
        }
        /// <summary>The face that will point at the viewer once the current pose is drawn.</summary>
        public int PendingFacingFace => table == null || table.viewCamera == null ? -1 : FacingFaceAfter();
        private void OnDisable() { dragging = false; HideOwn(); }
        private void OnDestroy() { HideOwn(); }
    }
}
