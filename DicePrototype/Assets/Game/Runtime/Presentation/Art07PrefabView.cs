using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceCraft.Presentation
{
    /// <summary>Reusable visual-only view. Its caller owns receipt identity, timing and pool budget.</summary>
    public sealed class Art07PrefabView : MonoBehaviour
    {
        public enum Motion { None, Punch, Ticket, Bracket, Wipe, Link, Expand, Probability, Cash, Reveal, Shatter, Burst, Stamp, Acquire }
        public string assetId;
        public Motion motion;
        public bool isEffect, autoTick = true, deactivateWhenFinished = true;
        public float defaultDuration = .26f;
        public RectTransform visualRoot;
        public CanvasGroup group;
        public TMP_Text title, value, detail;
        public TMP_Text compactTitle, compactValue;
        public RectTransform compactRoot;
        public GameObject[] fullOnly = Array.Empty<GameObject>();
        public Image compactIcon, badge, compactBadge;
        public GameObject[] burstLayers = Array.Empty<GameObject>(), burstStaticStars = Array.Empty<GameObject>();
        public Sprite scoreRing071,scoreArc071;
        public string[] fieldKeys = Array.Empty<string>();
        public TMP_Text[] fieldValues = Array.Empty<TMP_Text>();
        public UnityEngine.UI.Image icon, fill;
        public UnityEngine.UI.Button action;
        public RectTransform[] lineSegments = Array.Empty<RectTransform>();
        public RectTransform[] uprightWheelLabels = Array.Empty<RectTransform>();
        public Transform[] particles = Array.Empty<Transform>();
        public MeshRenderer diceBody;
        public MeshFilter[] physicalFaces = Array.Empty<MeshFilter>();
        public Transform faceOrientation;
        public string BoundInstanceId { get; private set; }
        public bool IsPlaying { get; private set; }
        public bool IsPaused { get; private set; }
        public bool ReducedMotion { get; private set; }
        public float NormalizedTime { get; private set; }
        private Vector3 origin, target, baseScale;
        private Quaternion baseRotation;
        private Vector3[] particlePositions, particleScales;
        private Quaternion[] particleRotations;
        private Graphic[] graphics;
        private Color[] graphicColors;
        private Sprite defaultIcon;
        private bool defaultIconEnabled;
        private Quaternion iconRotation;
        private Material defaultBodyMaterial;
        private Quaternion defaultFaceOrientation;
        private Mesh[] defaultFaceMeshes;
        private float elapsed, duration;
        private bool initialized;
        private Action finished;
        private int particleBudget = int.MaxValue, burstLevel;
        private float revealAngle;
        private bool committedRevealFadeOnly;
        private float committedRevealFadeFraction=1;
        public bool CommittedRevealFadeOnly=>committedRevealFadeOnly;
        public void ConfigureCommittedReveal(bool resultOnly,float phaseSeconds){committedRevealFadeOnly=resultOnly;committedRevealFadeFraction=Mathf.Clamp01(.08f/Mathf.Max(.001f,phaseSeconds));if(icon!=null&&resultOnly)icon.rectTransform.localRotation=Quaternion.Euler(0,0,revealAngle);Evaluate(0);}
        private Art07ScoreBurstLayout scoreBurstLayout;
        public int BurstLevel=>burstLevel;
        public void ConfigureScoreBurst(Vector2 size,bool lowQuality=false) { if(motion!=Motion.Burst)throw new InvalidOperationException("Not a Combo view."); Initialize(); if(scoreBurstLayout==null){scoreBurstLayout=GetComponent<Art07ScoreBurstLayout>();if(scoreBurstLayout==null)scoreBurstLayout=gameObject.AddComponent<Art07ScoreBurstLayout>();scoreBurstLayout.Initialize(this);} scoreBurstLayout.Configure(size,lowQuality); }

        private void Awake() { Initialize(); if (motion == Motion.Burst) SetBurstLevel(0); }

        public void Bind(string instanceId, string heading, string exactValue, string description, Sprite picture = null)
        {
            Initialize();
            BoundInstanceId = instanceId;
            if (title != null) title.text = heading ?? "";
            if (value != null) value.text = exactValue ?? "";
            if (detail != null) detail.text = description ?? "";
            if (compactTitle != null) compactTitle.text = heading ?? "";
            if (compactValue != null) compactValue.text = exactValue ?? "";
            if (icon != null && picture != null) { icon.sprite = picture; icon.enabled = true; }
            if (compactIcon != null && picture != null) { compactIcon.sprite = picture; compactIcon.enabled = true; }
        }
        public void SetCompact(bool compact)
        {
            if (compactRoot == null) return;
            foreach (var item in fullOnly) if (item != null) item.SetActive(!compact);
            compactRoot.gameObject.SetActive(compact);
            var size = compact ? new Vector2(208, 104) : new Vector2(160, 224);
            ((RectTransform)transform).sizeDelta = size; visualRoot.sizeDelta = size;
            if (action != null) action.targetGraphic = (compact ? compactRoot : visualRoot).Find("Surface").GetComponent<Image>();
        }
        public void BindBadge(Sprite picture)
        {
            if (badge != null) { badge.sprite = picture; badge.enabled = picture != null; }
            if (compactBadge != null) { compactBadge.sprite = picture; compactBadge.enabled = picture != null; }
        }
        /// <summary>The caller maps the committed receipt to the authored nine-slice wheel; no draw occurs here.</summary>
        public void SetRevealSlice(int index)
        {
            if (index < 0 || index > 8) throw new ArgumentOutOfRangeException(nameof(index));
            revealAngle = -index * 40;
        }
        /// <summary>Caller supplies the already resolved highest tier. This view never computes a gameplay multiplier.</summary>
        public void SetBurstLevel(int level)
        {
            if (level < 0 || level > 5) throw new ArgumentOutOfRangeException(nameof(level)); burstLevel = level;
            defaultDuration = level == 0 ? .18f : level == 1 ? .26f : level == 2 ? .34f : level == 3 ? .42f : level == 4 ? .52f : .60f;
            SetParticleBudget(level == 0 ? 0 : level == 1 ? 4 : level == 2 ? 8 : level == 3 ? 16 : level == 4 ? 24 : 36);
            ApplyBurstLayers();
        }
        private void ApplyBurstLayers()
        {
            for (int i = 0; i < burstLayers.Length; i++) if (burstLayers[i] != null)
                burstLayers[i].SetActive(i == 0 || i == 1 && burstLevel >= 2 || i == 2 && burstLevel == 3 || (i == 3 || i == 4) && burstLevel >= 4 || i == 5 && burstLevel == 5);
            for (int i = 0; i < burstStaticStars.Length; i++) if (burstStaticStars[i] != null) burstStaticStars[i].SetActive(ReducedMotion && i < burstLevel);
        }
        public void SetField(string key, string exactText)
        {
            int i = Array.IndexOf(fieldKeys, key);
            if (i < 0 || i >= fieldValues.Length || fieldValues[i] == null) throw new ArgumentException("Unknown view field: " + key);
            fieldValues[i].text = exactText ?? "";
        }
        public void SetFraction(float displayFraction) { if (fill != null) fill.fillAmount = Mathf.Clamp01(displayFraction); }
        public void BindAction(Action callback)
        {
            if (action == null) throw new InvalidOperationException(assetId + " has no action button.");
            action.onClick.RemoveAllListeners(); action.onClick.AddListener(() => callback?.Invoke());
        }
        public void BindDie(string instanceId, Mesh[] sixFaceMeshes, Quaternion resultOrientation, Material bodyMaterial = null)
        {
            if (physicalFaces.Length != 6 || sixFaceMeshes == null || sixFaceMeshes.Length != 6) throw new ArgumentException("Exactly six physical face meshes are required.");
            Initialize();
            BoundInstanceId = instanceId;
            for (int i = 0; i < 6; i++) physicalFaces[i].sharedMesh = sixFaceMeshes[i];
            if (faceOrientation != null) faceOrientation.localRotation = resultOrientation;
            if (bodyMaterial != null && diceBody != null) diceBody.sharedMaterial = bodyMaterial;
        }
        /// <summary>Coordinates belong to the owning UI canvas. For cross-row links use an outside elbow.</summary>
        public void SetLink(Vector2 from, Vector2 to, float outsideX = float.NaN)
        {
            SetPolyline(float.IsNaN(outsideX) ? new[] { from, to } : new[] { from, new Vector2(outsideX, from.y), new Vector2(outsideX, to.y), to });
        }
        /// <summary>At most five prewarmed segments; gaps around card rows keep the path outside card bodies.</summary>
        public void SetPolyline(Vector2[] points)
        {
            Initialize();
            if (points == null || points.Length < 2 || points.Length > lineSegments.Length + 1) throw new ArgumentException("Polyline exceeds the prewarmed segment budget.");
            foreach (var segment in lineSegments) segment.gameObject.SetActive(false);
            for (int i = 0; i < points.Length - 1; i++) Segment(lineSegments[i], points[i], points[i + 1]);
            if (icon != null)
            {
                Vector2 last = points[points.Length - 2], to = points[points.Length - 1];
                icon.rectTransform.pivot = new Vector2(.5f, .5f); icon.rectTransform.anchoredPosition = to;
                icon.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(to.y - last.y, to.x - last.x) * Mathf.Rad2Deg);
            }
        }
        private static void Segment(RectTransform r, Vector2 from, Vector2 to)
        {
            r.gameObject.SetActive(true); r.pivot = new Vector2(0, .5f); r.anchoredPosition = from;
            Vector2 delta = to - from; r.sizeDelta = new Vector2(delta.magnitude, 3);
            r.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }
        public void Play(Vector3 destination, bool reducedMotion = false, float seconds = -1, Action onFinished = null)
        {
            Initialize();
            // A pool caller places the view before Play. Never restore the preceding receipt's position.
            origin = visualRoot != null ? visualRoot.anchoredPosition3D : transform.localPosition;
            if (icon != null) iconRotation = icon.rectTransform.localRotation;
            RestoreVisuals(); finished = onFinished; target = destination;
            ReducedMotion = reducedMotion; duration = reducedMotion ? .12f : Mathf.Max(.001f, seconds < 0 ? defaultDuration : seconds);
            elapsed = 0; NormalizedTime = 0; IsPaused = false; IsPlaying = true;
            gameObject.SetActive(true); if (group != null) group.alpha = 1;
            SetParticleBudget(particleBudget); ApplyBurstLayers(); Evaluate(0);
        }
        public void SetPaused(bool pause) { IsPaused = pause; }
        public void SetReducedMotion(bool reduced)
        {
            ReducedMotion = reduced;
            SetParticleBudget(particleBudget); ApplyBurstLayers();
            if (reduced && IsPlaying) { Evaluate(1); Complete(); }
        }
        public void SetParticleBudget(int maximum)
        { particleBudget = Math.Max(0, maximum); for (int i = 0; i < particles.Length; i++) if (particles[i] != null) particles[i].gameObject.SetActive(!ReducedMotion && i < particleBudget); }
        private void Update()
        {
            if (!autoTick || !IsPlaying || IsPaused) return;
            elapsed += Time.unscaledDeltaTime; Evaluate(Mathf.Clamp01(elapsed / duration));
            if (elapsed >= duration) Complete();
        }
        /// <summary>External PresentationClock may call this with autoTick=false. No numeric score interpolation.</summary>
        public void Evaluate(float normalized)
        {
            Initialize(); float t = Mathf.Clamp01(normalized); NormalizedTime = t;
            var r = visualRoot != null ? (Transform)visualRoot : transform;
            r.localScale = baseScale; r.localRotation = baseRotation;
            if (visualRoot != null) visualRoot.anchoredPosition3D = origin; else r.localPosition = origin;
            float eased = 1 - Mathf.Pow(1 - t, 3);
            if (!ReducedMotion)
            {
                if (motion == Motion.Punch || motion == Motion.Stamp) r.localScale = baseScale * (1 + Mathf.Sin(t * Mathf.PI * 2) * .04f);
                else if (motion == Motion.Expand) r.localScale = new Vector3(baseScale.x * Mathf.Lerp(.9f, 1, eased), baseScale.y, baseScale.z);
                else if (motion == Motion.Acquire) r.localScale = baseScale * Mathf.Lerp(.85f, 1, eased);
                else if (motion == Motion.Ticket || motion == Motion.Cash)
                {
                    Vector3 position = Vector3.Lerp(origin, target, eased) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 16;
                    if (visualRoot != null) visualRoot.anchoredPosition3D = position; else r.localPosition = position;
                }
                else if (motion == Motion.Reveal && icon != null) icon.rectTransform.localRotation = Quaternion.Euler(0, 0, revealAngle + (committedRevealFadeOnly?0:360 * (1 - eased)));
                if (motion == Motion.Shatter || motion == Motion.Burst)
                    for (int i = 0; i < particles.Length; i++)
                    {
                        if (particles[i] == null) continue;
                        float a = i * 2.399963f;
                        float distance = particles[i] is RectTransform ? 36 : .65f;
                        particles[i].localPosition = particlePositions[i] + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * distance * eased;
                        particles[i].localRotation = particleRotations[i] * Quaternion.Euler(0, 0, (i % 2 == 0 ? 1 : -1) * 40 * t);
                        particles[i].localScale = particleScales[i] * Mathf.Lerp(1, .15f, t);
                    }
            }
            if (fill != null && (motion == Motion.Wipe || motion == Motion.Probability)) fill.fillAmount = ReducedMotion ? 1 : eased;
            if (group != null && isEffect) group.alpha = motion==Motion.Reveal&&committedRevealFadeOnly?Mathf.Clamp01(t/Mathf.Max(.001f,committedRevealFadeFraction)):t < .7f ? 1 : 1 - (t - .7f) / .3f;
            if (ReducedMotion) { for (int i = 0; i < particles.Length; i++) if (particles[i] != null) particles[i].gameObject.SetActive(false); if (motion == Motion.Reveal && icon != null) icon.rectTransform.localRotation = Quaternion.Euler(0, 0, revealAngle); }
            if(motion==Motion.Reveal&&icon!=null)foreach(var label in uprightWheelLabels)if(label!=null)label.localRotation=Quaternion.Inverse(icon.rectTransform.localRotation);
            scoreBurstLayout?.Evaluate(t);
        }
        public void Complete()
        {
            if (!IsPlaying) return;
            IsPlaying = false; IsPaused = false; var callback = finished; finished = null;
            RestoreVisuals(); if (deactivateWhenFinished && isEffect) gameObject.SetActive(false); callback?.Invoke();
        }
        public void ResetForPool()
        {
            scoreBurstLayout?.ResetLayout();
            IsPlaying = false; IsPaused = false; finished = null; BoundInstanceId = null; elapsed = 0; NormalizedTime = 0; ReducedMotion = false;
            Initialize(); RestoreVisuals(); if (group != null) group.alpha = 1;
            if (title != null) title.text = ""; if (value != null) value.text = ""; if (detail != null) detail.text = "";
            if (compactTitle != null) compactTitle.text = ""; if (compactValue != null) compactValue.text = "";
            if (compactIcon != null) compactIcon.sprite = defaultIcon;
            BindBadge(null); SetCompact(false); revealAngle = 0;committedRevealFadeOnly=false;committedRevealFadeFraction=1; particleBudget = int.MaxValue;
            if (motion == Motion.Burst) SetBurstLevel(0);
            foreach (var field in fieldValues) if (field != null) field.text = "";
            for (int i = 0; i < graphics.Length; i++) if (graphics[i] != null) graphics[i].color = graphicColors[i];
            if (icon != null) { icon.sprite = defaultIcon; icon.enabled = defaultIconEnabled; icon.rectTransform.localRotation = Quaternion.identity; }
            if (fill != null) fill.fillAmount = 0;
            if (diceBody != null) diceBody.sharedMaterial = defaultBodyMaterial;
            if (faceOrientation != null) faceOrientation.localRotation = defaultFaceOrientation;
            for (int i = 0; i < physicalFaces.Length; i++) if (physicalFaces[i] != null) physicalFaces[i].sharedMesh = defaultFaceMeshes[i];
            foreach (var segment in lineSegments) if (segment != null) segment.gameObject.SetActive(false);
            if (action != null) action.onClick.RemoveAllListeners(); gameObject.SetActive(false);
        }
        private void Initialize()
        {
            if (initialized) return; initialized = true;
            if (motion == Motion.Link && lineSegments.Length > 0 && lineSegments.Length < 5)
            {
                int oldCount = lineSegments.Length; Array.Resize(ref lineSegments, 5);
                for (int i = oldCount; i < 5; i++) { lineSegments[i] = Instantiate(lineSegments[0], lineSegments[0].parent, false); lineSegments[i].name = "RouteSegment" + i; lineSegments[i].gameObject.SetActive(false); }
            }
            var r = visualRoot != null ? (Transform)visualRoot : transform;
            origin = visualRoot != null ? visualRoot.anchoredPosition3D : r.localPosition; baseScale = r.localScale; baseRotation = r.localRotation;
            particlePositions = new Vector3[particles.Length]; particleScales = new Vector3[particles.Length]; particleRotations = new Quaternion[particles.Length];
            for (int i = 0; i < particles.Length; i++) if (particles[i] != null) { particlePositions[i] = particles[i].localPosition; particleScales[i] = particles[i].localScale; particleRotations[i] = particles[i].localRotation; }
            graphics = GetComponentsInChildren<Graphic>(true); graphicColors = new Color[graphics.Length];
            for (int i = 0; i < graphics.Length; i++) graphicColors[i] = graphics[i].color;
            if (icon != null) { defaultIcon = icon.sprite; defaultIconEnabled = icon.enabled; iconRotation = icon.rectTransform.localRotation; }
            if (diceBody != null) defaultBodyMaterial = diceBody.sharedMaterial;
            if (faceOrientation != null) defaultFaceOrientation = faceOrientation.localRotation;
            defaultFaceMeshes = new Mesh[physicalFaces.Length];
            for (int i = 0; i < physicalFaces.Length; i++) if (physicalFaces[i] != null) defaultFaceMeshes[i] = physicalFaces[i].sharedMesh;
            if(motion==Motion.Burst){scoreBurstLayout=GetComponent<Art07ScoreBurstLayout>();if(scoreBurstLayout==null)scoreBurstLayout=gameObject.AddComponent<Art07ScoreBurstLayout>();scoreBurstLayout.Initialize(this);}
        }
        private void RestoreVisuals()
        {
            if (!initialized) return;
            var r = visualRoot != null ? (Transform)visualRoot : transform; r.localScale = baseScale; r.localRotation = baseRotation;
            if (visualRoot != null) visualRoot.anchoredPosition3D = origin; else r.localPosition = origin;
            if (icon != null) icon.rectTransform.localRotation = iconRotation;
            for (int i = 0; i < particles.Length; i++) if (particles[i] != null) { particles[i].localPosition = particlePositions[i]; particles[i].localScale = particleScales[i]; particles[i].localRotation = particleRotations[i]; particles[i].gameObject.SetActive(!ReducedMotion && i < particleBudget); }
        }
    }
}
