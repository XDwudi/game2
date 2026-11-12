using System;
using System.Collections.Generic;
using UnityEngine;

namespace DiceCraft
{
    // v0.73 presentation only: per-face finishes, interface-layer dice and face picking.
    // Nothing here reads or writes rule state; a finish is derived from the face data it is given.
    public sealed partial class DiceTable
    {
        private Material[] plainMaterials073, styledMaterials073;
        private Material[] PlainMaterials073()
        {
            if (plainMaterials073 == null || plainMaterials073[0] != faceMaterial) plainMaterials073 = new[] { faceMaterial };
            return plainMaterials073;
        }
        private Material[] StyledMaterials073()
        {
            if (styledMaterials073 == null || styledMaterials073[0] != faceMaterial || styledMaterials073[1] != styleMaterial)
                styledMaterials073 = new[] { faceMaterial, styleMaterial };
            return styledMaterials073;
        }

        private void ApplyLayer073(Visual v)
        {
            foreach (var t in v.root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = visualLayer;
            if (castShadows) return;
            foreach (var r in v.root.GetComponentsInChildren<MeshRenderer>(true))
            { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
        }

        /// <summary>Number of dice this table is currently drawing.</summary>
        public int ActiveVisualCount
        {
            get { int n = 0; foreach (var v in visuals.Values) if (v.root != null && v.root.activeSelf) n++; return n; }
        }

        /// <summary>Hides one named static preview without touching any other die.</summary>
        public void HideStaticPreview073(string slot)
        {
            if (visuals.TryGetValue(StaticPreviewPrefix07 + slot, out Visual v) && v.root != null) v.root.SetActive(false);
        }

        /// <summary>
        /// Pose that turns one physical face toward the viewer, tilted so two neighbours stay readable.
        /// </summary>
        public Quaternion FaceToCamera073(int face, float pitch = -17f, float yaw = 21f)
        {
            if (face < 0 || face > 5) throw new ArgumentOutOfRangeException(nameof(face));
            var faceFrame = Quaternion.FromToRotation(Vector3.forward, Normals[face]);
            return viewCamera.transform.rotation * Quaternion.Euler(pitch, yaw, 0) * Quaternion.Euler(0, 180, 0) * Quaternion.Inverse(faceFrame);
        }

        /// <summary>Face of the large dice-sheet preview under a screen point.</summary>
        public bool TryPickPreviewFace073(Vector2 screenPoint, out int face) => TryPickFaceOf073(PreviewId, screenPoint, out face);
        /// <summary>Face of the large dice-sheet preview that points most directly at the viewer, or -1.</summary>
        public int PreviewFacingFace073() => FacingFaceOf073(PreviewId);
        /// <summary>The physical face of a static preview that points most directly at the viewer, or -1.</summary>
        public int FacingFace073(string slot) => FacingFaceOf073(StaticPreviewPrefix07 + slot);
        /// <summary>The physical face of a static preview under a screen point, if the point hits the die.</summary>
        public bool TryPickFace073(string slot, Vector2 screenPoint, out int face) => TryPickFaceOf073(StaticPreviewPrefix07 + slot, screenPoint, out face);

        private int FacingFaceOf073(string id)
        {
            if (viewCamera == null || !visuals.TryGetValue(id, out Visual v) || v.pivot == null) return -1;
            Vector3 toward = -viewCamera.transform.forward; int best = -1; float bestDot = float.NegativeInfinity;
            for (int f = 0; f < 6; f++)
            {
                float dot = Vector3.Dot(v.pivot.TransformDirection(Normals[f]), toward);
                if (dot > bestDot) { bestDot = dot; best = f; }
            }
            return best;
        }

        private bool TryPickFaceOf073(string id, Vector2 screenPoint, out int face)
        {
            face = -1;
            if (viewCamera == null || !visuals.TryGetValue(id, out Visual v) || v.pivot == null || !v.root.activeInHierarchy) return false;
            Ray ray = viewCamera.ScreenPointToRay(screenPoint);
            Vector3 origin = v.pivot.InverseTransformPoint(ray.origin), direction = v.pivot.InverseTransformVector(ray.direction);
            float enter = float.NegativeInfinity, exit = float.PositiveInfinity; int axis = -1; float sign = 0;
            for (int a = 0; a < 3; a++)
            {
                float o = origin[a], d = direction[a];
                if (Mathf.Abs(d) < 1e-7f) { if (o < -.5f || o > .5f) return false; continue; }
                float t1 = (-.5f - o) / d, t2 = (.5f - o) / d;
                float near = Mathf.Min(t1, t2), far = Mathf.Max(t1, t2);
                if (near > enter) { enter = near; axis = a; sign = t1 < t2 ? -1 : 1; }
                exit = Mathf.Min(exit, far);
            }
            if (axis < 0 || enter > exit) return false;
            Vector3 normal = Vector3.zero; normal[axis] = sign;
            for (int f = 0; f < 6; f++) if (Vector3.Dot(Normals[f], normal) > .9f) { face = f; return true; }
            return false;
        }

        /// <summary>
        /// Appends the finish of one face as submesh 1: glaze for smooth, grain for rough, a lit rim
        /// for gold. Submesh 0 (pips and the legacy corner marks) is left exactly as it was built.
        /// The finish sits between the body and the pip layer, so it can never cover a pip.
        /// </summary>
        private static void AddFinish073(Mesh mesh, bool gold, string texture)
        {
            bool smooth = texture == "smooth", rough = texture == "rough";
            if (!gold && !smooth && !rough) return;
            var vertices = new List<Vector3>(); mesh.GetVertices(vertices);
            var colors = new List<Color>(); mesh.GetColors(colors);
            var weights = new List<Vector2>(vertices.Count); for (int i = 0; i < vertices.Count; i++) weights.Add(Vector2.zero);
            var finish = new List<int>();
            const float z = -.0012f;
            Action<Vector2[], Color, float> fan = (points, color, weight) =>
            {
                int start = vertices.Count; Vector2 centre = Vector2.zero;
                foreach (var p in points) centre += p; centre /= points.Length;
                vertices.Add(new Vector3(centre.x, centre.y, z)); colors.Add(color); weights.Add(new Vector2(weight, 0));
                foreach (var p in points) { vertices.Add(new Vector3(p.x, p.y, z)); colors.Add(color); weights.Add(new Vector2(weight, 0)); }
                for (int i = 0; i < points.Length; i++) { finish.Add(start); finish.Add(start + 1 + i); finish.Add(start + 1 + (i + 1) % points.Length); }
            };
            Func<float, float, Vector2[]> sheet = (half, cut) => new[]
            {
                new Vector2(-half + cut, -half), new Vector2(half - cut, -half), new Vector2(half, -half + cut), new Vector2(half, half - cut),
                new Vector2(half - cut, half), new Vector2(-half + cut, half), new Vector2(-half, half - cut), new Vector2(-half, -half + cut)
            };
            if (smooth)
            {
                // Glaze: a faint bright sheet that takes the key highlight, crossed by two sheen bands.
                fan(sheet(.40f, .05f), new Color(1, 1, 1, .14f), 1f);
                const float m = .39f;
                foreach (var b in new[] { new Vector3(.30f, .56f, .24f), new Vector3(.63f, .67f, .5f) })
                    fan(new[] { new Vector2(-m, -m + b.x), new Vector2(m - b.x, m), new Vector2(m - b.y, m), new Vector2(-m, -m + b.y) }, new Color(1, 1, 1, b.z), .2f);
            }
            if (rough)
            {
                // Grain: a slightly dulled sheet and fixed specks. One seed, so a rough face is always the same face.
                fan(sheet(.40f, .05f), new Color(.45f, .38f, .28f, .09f), 0f);
                uint seed = 0x73A5C1E9u;
                Func<float> next = () => { seed = seed * 1664525u + 1013904223u; return (seed >> 8) / 16777216f; };
                for (int i = 0; i < 52; i++)
                {
                    float x = (next() * 2 - 1) * .37f, y = (next() * 2 - 1) * .37f, r = .008f + next() * .013f; bool dark = next() < .68f;
                    Color speck = dark ? new Color(.33f, .27f, .19f, .34f + next() * .3f) : new Color(1f, .99f, .94f, .5f);
                    fan(new[] { new Vector2(x - r, y), new Vector2(x - r * .4f, y - r * .8f), new Vector2(x + r * .6f, y - r * .7f), new Vector2(x + r, y + r * .1f), new Vector2(x + r * .3f, y + r * .9f), new Vector2(x - r * .6f, y + r * .7f) }, speck, 0f);
                }
            }
            if (gold)
            {
                // Lit rim around the legacy flat gold line: bright outside, deep inside, a glint in each corner.
                Color bright = new Color(1f, .84f, .42f, 1f), deep = new Color(.74f, .46f, .16f, 1f);
                Action<float, float, Color, float> ring = (outer, inner, color, weight) =>
                {
                    for (int side = 0; side < 4; side++)
                    {
                        Func<Vector2, Vector2> turn = p => side == 0 ? p : side == 1 ? new Vector2(-p.y, p.x) : side == 2 ? new Vector2(-p.x, -p.y) : new Vector2(p.y, -p.x);
                        fan(new[] { turn(new Vector2(-outer, outer)), turn(new Vector2(outer, outer)), turn(new Vector2(inner, inner)), turn(new Vector2(-inner, inner)) }, color, weight);
                    }
                };
                ring(.445f, .40f, bright, 1f); ring(.40f, .365f, deep, .45f);
                foreach (var corner in new[] { new Vector2(-.42f, .42f), new Vector2(.42f, .42f), new Vector2(.42f, -.42f), new Vector2(-.42f, -.42f) })
                    fan(new[] { corner + new Vector2(0, .022f), corner + new Vector2(.022f, 0), corner + new Vector2(0, -.022f), corner + new Vector2(-.022f, 0) }, new Color(1, 1, .9f, .85f), 0f);
            }
            var pips = mesh.GetTriangles(0);
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetUVs(0, weights);
            mesh.subMeshCount = 2; mesh.SetTriangles(pips, 0); mesh.SetTriangles(finish, 1);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
        }
    }
}
