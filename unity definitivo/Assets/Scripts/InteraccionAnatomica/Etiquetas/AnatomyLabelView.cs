using TMPro;
using UnityEngine;

public sealed class AnatomyLabelView : MonoBehaviour
{
    [SerializeField] private TextMeshPro textoNombre;
    [SerializeField] private Transform fondo;
    [SerializeField] private LineRenderer linea;
    [SerializeField] private Transform puntoAnchorVisual;

    public void Asignar(string nombre)
    {
        textoNombre.text = nombre;
        gameObject.SetActive(true);
    }
    public void ActualizarVisual(Vector3 position, Vector3 anchorWorldPosition, Camera camara, bool visible)
    {
        if (!visible || camara == null) { Ocultar(); return; }
        gameObject.SetActive(true);
        transform.position = position;
        // TMP front is local -Z, so -forward points toward the camera.
        Vector3 away = position - camara.transform.position;
        if (away.sqrMagnitude > 0.000001f)
            transform.rotation = Quaternion.LookRotation(away, camara.transform.up);
        linea.useWorldSpace = true;
        linea.positionCount = 2;
        Vector3 localAnchor = transform.InverseTransformPoint(anchorWorldPosition);
        Vector3 edge = new Vector3(Mathf.Clamp(localAnchor.x, -0.095f, 0.095f),
            Mathf.Clamp(localAnchor.y, -0.023f, 0.023f), 0.001f);
        linea.SetPosition(0, transform.TransformPoint(edge));
        linea.SetPosition(1, anchorWorldPosition);
        if (puntoAnchorVisual != null)
        {
            puntoAnchorVisual.position = anchorWorldPosition;
            puntoAnchorVisual.rotation = transform.rotation;
        }
    }
    public void Ocultar() { gameObject.SetActive(false); }
}
