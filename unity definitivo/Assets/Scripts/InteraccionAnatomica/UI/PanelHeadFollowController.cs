using UnityEngine;

[DefaultExecutionOrder(1100)]
[DisallowMultipleComponent]
public sealed class PanelHeadFollowController : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform panelTransform;
    [SerializeField] private bool followEnabled = true;
    [SerializeField, Min(0.1f)] private float distanceFromCamera = 0.60f;
    [SerializeField] private Vector2 viewportPosition = new Vector2(0.76f, 0.72f);
    [SerializeField, Min(0.01f)] private float positionSmoothTime = 0.10f;
    [SerializeField, Min(0f)] private float rotationSmoothSpeed = 22f;
    private Vector3 velocity;
    private bool initialized;
    private readonly Vector3[] corners = new Vector3[4];

    private void OnEnable() { initialized = false; velocity = Vector3.zero; }
    private void LateUpdate() { Follow(Time.unscaledDeltaTime); }

    private void Follow(float deltaTime)
    {
        if (!followEnabled || targetCamera == null || panelTransform == null ||
            !targetCamera.isActiveAndEnabled) { initialized = false; return; }
        float depth = Mathf.Clamp(distanceFromCamera,
            targetCamera.nearClipPlane + 0.05f, targetCamera.farClipPlane - 0.05f);
        Vector3 target = targetCamera.ViewportToWorldPoint(
            new Vector3(viewportPosition.x, viewportPosition.y, depth));
        Vector3 position = initialized ? Vector3.SmoothDamp(panelTransform.position, target,
            ref velocity, Mathf.Max(0.01f, positionSmoothTime), Mathf.Infinity, deltaTime) : target;
        // Canvas graphics are read from local -Z: -forward points toward the viewer.
        Quaternion rotation = Quaternion.LookRotation(
            position - targetCamera.transform.position, targetCamera.transform.up);
        panelTransform.SetPositionAndRotation(position, initialized ?
            Quaternion.Slerp(panelTransform.rotation, rotation,
                1f - Mathf.Exp(-rotationSmoothSpeed * deltaTime)) : rotation);
        // Fast head turns must not leave the HUD behind the viewer. Only snap when clipped.
        if (!InsideViewport())
        {
            velocity = Vector3.zero;
            panelTransform.SetPositionAndRotation(target, Quaternion.LookRotation(
                target - targetCamera.transform.position, targetCamera.transform.up));
        }
        initialized = true;
    }

    private bool InsideViewport()
    {
        var rect = panelTransform as RectTransform;
        if (rect == null) return true;
        rect.GetWorldCorners(corners);
        foreach (Vector3 corner in corners)
        {
            Vector3 point = targetCamera.WorldToViewportPoint(corner);
            if (point.z <= targetCamera.nearClipPlane || point.z >= targetCamera.farClipPlane ||
                point.x < 0.02f || point.x > 0.98f || point.y < 0.02f || point.y > 0.98f)
                return false;
        }
        return true;
    }
}
