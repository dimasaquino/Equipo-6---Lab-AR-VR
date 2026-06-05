using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CancelParentScale : MonoBehaviour
{
     [Tooltip("Escala final que tú quieres para tu contenido, independiente del tracking.")]
    public Vector3 desiredLocalScale = Vector3.one;

    void LateUpdate()
    {
        if (!transform.parent) return;

        Vector3 ps = transform.parent.localScale;

        // Evita división por cero
        ps.x = Mathf.Abs(ps.x) < 1e-6f ? 1f : ps.x;
        ps.y = Mathf.Abs(ps.y) < 1e-6f ? 1f : ps.y;
        ps.z = Mathf.Abs(ps.z) < 1e-6f ? 1f : ps.z;

        // Compensa la escala NO uniforme del padre
        transform.localScale = new Vector3(
            desiredLocalScale.x / ps.x,
            desiredLocalScale.y / ps.y,
            desiredLocalScale.z / ps.z
        );
    }
}
