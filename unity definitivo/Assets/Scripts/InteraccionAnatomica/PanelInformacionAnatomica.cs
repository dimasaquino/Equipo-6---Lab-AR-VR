using TMPro;
using UnityEngine;

public class PanelInformacionAnatomica : MonoBehaviour
{
    [Header("Referencias del panel")]
    [SerializeField] private GameObject panelInformacion;
    [SerializeField] private TMP_Text textoNombre;
    [SerializeField] private TMP_Text textoDescripcion;

    private void Start()
    {
        if (panelInformacion != null)
        {
            panelInformacion.SetActive(false);
        }
    }

    public void MostrarInformacion(
        string nombre,
        string descripcion)
    {
        if (!ReferenciasValidas())
        {
            return;
        }

        textoNombre.text = nombre;
        textoDescripcion.text = descripcion;

        panelInformacion.SetActive(true);
    }

    public void OcultarPanel()
    {
        if (panelInformacion != null)
        {
            panelInformacion.SetActive(false);
        }

        // Quita la selección azul del hueso o músculo actual.
        ElementoAnatomicoInteractivo.LimpiarSeleccionActual();
    }

    [ContextMenu("Mostrar panel de prueba")]
    private void MostrarPanelDePrueba()
    {
        MostrarInformacion(
            "Nombre anatómico",
            "Aquí aparecerá una breve descripción de la estructura anatómica seleccionada."
        );
    }

    [ContextMenu("Ocultar panel")]
    private void OcultarPanelDePrueba()
    {
        OcultarPanel();
    }

    private bool ReferenciasValidas()
    {
        if (panelInformacion == null)
        {
            Debug.LogError(
                "Falta asignar Panel Informacion.",
                gameObject
            );

            return false;
        }

        if (textoNombre == null)
        {
            Debug.LogError(
                "Falta asignar Texto Nombre.",
                gameObject
            );

            return false;
        }

        if (textoDescripcion == null)
        {
            Debug.LogError(
                "Falta asignar Texto Descripcion.",
                gameObject
            );

            return false;
        }

        return true;
    }
}