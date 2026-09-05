using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[DefaultExecutionOrder(1000)]
public sealed class AnatomyLabelManager : MonoBehaviour
{
    [SerializeField] private ControladorVisualBrazo controladorVisual;
    [SerializeField] private Camera camaraPrincipal;
    [SerializeField] private Transform huesosRoot;
    [SerializeField] private AnatomyLabelView[] labelPool;
    [SerializeField] private bool labelsEnabled = true;
    [SerializeField, Range(1, 6)] private int maxLabels = 6;
    [SerializeField, Min(0.05f)] private float refreshInterval = 0.12f;
    [SerializeField, Range(0f, 0.25f)] private float viewportMargin = 0.05f;
    [SerializeField] private float offsetHorizontal = 0.14f;
    [SerializeField] private float offsetVertical = 0.035f;
    [SerializeField, Min(0f)] private float smoothing = 14f;
    [SerializeField, Min(0f)] private float retentionBias = 0.004f;

    private sealed class Bone
    {
        public ElementoAnatomicoInteractivo element;
        public string name;
        public XRSimpleInteractable interactable;
        public Transform proxy;
        public Collider[] colliders;
        public float score, distance;
        public int index;
    }
    private readonly List<Bone> bones = new List<Bone>(30);
    private readonly List<Bone> candidates = new List<Bone>(30);
    private Bone[] assigned;
    private Vector3[] positions, targets, anchors;
    private bool[] positioned;
    private float nextRefresh;
    private bool wasRunning, wasTransition;
    public bool LabelsEnabled => labelsEnabled;
    public int BoneCount => bones.Count;
    public int ActiveLabelCount
    {
        get
        {
            int count = 0;
            if (labelPool != null)
                foreach (var view in labelPool)
                    if (view != null && view.gameObject.activeSelf) count++;
            return count;
        }
    }
    private void Awake()
    {
        int count = labelPool == null ? 0 : Mathf.Min(6, labelPool.Length);
        assigned = new Bone[count];
        positions = new Vector3[count]; targets = new Vector3[count]; anchors = new Vector3[count];
        positioned = new bool[count];
        if (huesosRoot != null)
            foreach (var element in huesosRoot.GetComponentsInChildren<ElementoAnatomicoInteractivo>(true))
            {
                var interactable = element.GetComponent<XRSimpleInteractable>();
                if (interactable == null || interactable.colliders.Count == 0) continue;
                var colliders = interactable.colliders.ToArray();
                Transform proxy = null;
                foreach (var collider in colliders)
                    if (collider != null) { proxy = collider.transform; break; }
                if (proxy == null) continue;
                bones.Add(new Bone { element = element, name = element.NombreAnatomico,
                    interactable = interactable, proxy = proxy, colliders = colliders, index = bones.Count });
            }
        if (bones.Count != 30)
            Debug.LogWarning("AnatomyLabelManager: se esperaban 30 huesos; encontrados " + bones.Count, this);
        HideAll();
    }
    public void ToggleLabels() { SetLabelsEnabled(!labelsEnabled); }
    public void SetLabelsEnabled(bool value)
    {
        labelsEnabled = value;
        if (!value) HideAll();
        else nextRefresh = 0f;
    }
    private void OnDisable() { HideAll(); wasRunning = false; }
    private void HideAll()
    {
        if (labelPool != null)
            foreach (var view in labelPool) if (view != null) view.Ocultar();
        if (assigned != null)
            for (int i = 0; i < assigned.Length; i++) { assigned[i] = null; positioned[i] = false; }
    }
    private static bool Finite(Vector3 v)
    {
        return !float.IsNaN(v.x) && !float.IsInfinity(v.x) &&
            !float.IsNaN(v.y) && !float.IsInfinity(v.y) && !float.IsNaN(v.z) && !float.IsInfinity(v.z);
    }
    private static bool TryAnchor(Bone bone, out Vector3 anchor)
    {
        Bounds combined = default;
        bool found = false;
        foreach (var collider in bone.colliders)
        {
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy) continue;
            Bounds bounds = collider.bounds;
            if (!Finite(bounds.center) || !Finite(bounds.size) || bounds.size.sqrMagnitude <= 0f) continue;
            if (!found) { combined = bounds; found = true; } else combined.Encapsulate(bounds);
        }
        anchor = combined.center;
        return found;
    }
    private bool InView(Vector3 anchor, out Vector3 viewport)
    {
        viewport = camaraPrincipal.WorldToViewportPoint(anchor);
        return Finite(viewport) && viewport.z > camaraPrincipal.nearClipPlane &&
            viewport.z < camaraPrincipal.farClipPlane && viewport.x >= viewportMargin &&
            viewport.x <= 1f - viewportMargin && viewport.y >= viewportMargin &&
            viewport.y <= 1f - viewportMargin;
    }
    private bool IsAssigned(Bone bone)
    {
        foreach (var item in assigned) if (item == bone) return true;
        return false;
    }
    private int Compare(Bone a, Bone b)
    {
        int score = a.score.CompareTo(b.score);
        if (score != 0) return score;
        int distance = a.distance.CompareTo(b.distance);
        return distance != 0 ? distance : a.index.CompareTo(b.index);
    }
    private void Refresh(bool transition)
    {
        candidates.Clear();
        foreach (var bone in bones)
        {
            Vector3 anchor, viewport;
            if (!TryAnchor(bone, out anchor) || !InView(anchor, out viewport)) continue;
            float dx = viewport.x - 0.5f, dy = viewport.y - 0.5f;
            bone.score = dx * dx + dy * dy - (IsAssigned(bone) ? retentionBias : 0f);
            bone.distance = (anchor - camaraPrincipal.transform.position).sqrMagnitude;
            candidates.Add(bone);
        }
        candidates.Sort(Compare);
        int limit = Mathf.Clamp(maxLabels, 0, assigned.Length);
        if (!transition)
            for (int i = 0; i < assigned.Length; i++)
            {
                int rank = assigned[i] == null ? -1 : candidates.IndexOf(assigned[i]);
                if (i >= limit || rank < 0 || rank >= limit) ClearSlot(i);
            }
        for (int i = 0; i < limit; i++)
        {
            if (assigned[i] != null || labelPool[i] == null) continue;
            foreach (var bone in candidates)
            {
                if (IsAssigned(bone)) continue;
                assigned[i] = bone; positioned[i] = false;
                labelPool[i].Asignar(bone.name);
                break;
            }
        }
    }
    private void ClearSlot(int i)
    {
        assigned[i] = null; positioned[i] = false;
        if (labelPool[i] != null) labelPool[i].Ocultar();
    }
    private void LateUpdate()
    {
        bool running = labelsEnabled && controladorVisual != null && controladorVisual.HuesosVisibles &&
            camaraPrincipal != null && camaraPrincipal.isActiveAndEnabled;
        if (!running) { HideAll(); wasRunning = false; return; }
        bool transition = controladorVisual.HuesosEnTransicion;
        if (!wasRunning || (wasTransition && !transition)) nextRefresh = 0f;
        wasRunning = true; wasTransition = transition;
        for (int i = 0; i < assigned.Length; i++)
        {
            Vector3 viewport;
            if (assigned[i] != null && (i >= maxLabels ||
                !TryAnchor(assigned[i], out anchors[i]) || !InView(anchors[i], out viewport))) ClearSlot(i);
        }
        if (Time.unscaledTime >= nextRefresh)
        {
            Refresh(transition);
            nextRefresh = Time.unscaledTime + Mathf.Max(0.05f, refreshInterval);
        }
        for (int i = 0; i < assigned.Length; i++)
        {
            if (assigned[i] == null) continue;
            Vector3 viewport;
            if (!TryAnchor(assigned[i], out anchors[i]) || !InView(anchors[i], out viewport))
            { ClearSlot(i); continue; }
            float side = i % 2 == 0 ? -1f : 1f;
            targets[i] = anchors[i] + camaraPrincipal.transform.right * (side * offsetHorizontal) +
                camaraPrincipal.transform.up * (offsetVertical + (i / 2 - 1) * 0.055f);
            // Bounded pairwise separation for at most six projected rectangles.
            for (int pass = 0; pass < 6; pass++)
                for (int j = 0; j < i; j++)
                {
                    if (assigned[j] == null) continue;
                    Vector3 a = camaraPrincipal.transform.InverseTransformPoint(targets[i]);
                    Vector3 b = camaraPrincipal.transform.InverseTransformPoint(targets[j]);
                    if (a.z <= 0f || b.z <= 0f) continue;
                    if (Mathf.Abs(a.x / a.z - b.x / b.z) < 0.105f / a.z + 0.105f / b.z &&
                        Mathf.Abs(a.y / a.z - b.y / b.z) < 0.027f / a.z + 0.027f / b.z)
                        targets[i] += camaraPrincipal.transform.up * 0.058f;
                }
            if (!positioned[i]) { positions[i] = targets[i]; positioned[i] = true; }
            else positions[i] = Vector3.Lerp(positions[i], targets[i],
                1f - Mathf.Exp(-Mathf.Max(0f, smoothing) * Time.unscaledDeltaTime));
            labelPool[i].ActualizarVisual(positions[i], anchors[i], camaraPrincipal, true);
        }
    }
}
