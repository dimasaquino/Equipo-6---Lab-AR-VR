using UnityEngine;

public class ElementoAnatomicoInteractivo : MonoBehaviour
{
    [Header("Información anatómica")]
    [SerializeField] private string nombreAnatomico;

    [SerializeField]
    [TextArea(3, 6)]
    private string descripcion;

    [Header("Referencias")]
    [SerializeField]
    private PanelInformacionAnatomica panelInformacion;

    [SerializeField]
    private Renderer rendererObjetivo;

    [Header("Colores de interacción")]
    [SerializeField]
    private Color colorHover =
        new Color(1f, 0.75f, 0.15f, 1f);

    [SerializeField]
    private Color colorSeleccionado =
        new Color(0.15f, 0.75f, 1f, 1f);

    private static ElementoAnatomicoInteractivo seleccionadoActual;

    private MaterialPropertyBlock propertyBlock;
    private Color colorOriginal;

    private bool estaSeleccionado;
    private bool punteroEncima;

    private static readonly int ColorId =
        Shader.PropertyToID("_Color");

    private void Awake()
    {
        if (rendererObjetivo == null)
        {
            rendererObjetivo = GetComponent<Renderer>();
        }

        propertyBlock = new MaterialPropertyBlock();

        if (rendererObjetivo == null)
        {
            Debug.LogError(
                $"No se encontró un Renderer en {gameObject.name}.",
                gameObject
            );

            return;
        }

        if (rendererObjetivo.sharedMaterial != null &&
            rendererObjetivo.sharedMaterial.HasProperty(ColorId))
        {
            colorOriginal =
                rendererObjetivo.sharedMaterial.GetColor(ColorId);
        }
        else
        {
            colorOriginal = Color.white;
        }

        AplicarColor(colorOriginal);
    }

    public void EntrarHover()
    {
        Debug.Log(
            $"HOVER ENTERED: {gameObject.name}",
            gameObject
        );

        punteroEncima = true;

        if (!estaSeleccionado)
        {
            AplicarColor(colorHover);
        }
    }

    public void SalirHover()
    {
        Debug.Log(
            $"HOVER EXITED: {gameObject.name}",
            gameObject
        );

        punteroEncima = false;

        if (!estaSeleccionado)
        {
            AplicarColor(colorOriginal);
        }
    }

    public void Seleccionar()
    {
        Debug.Log(
            $"CLICK XR RECIBIDO: {gameObject.name}",
            gameObject
        );

        if (seleccionadoActual != null &&
            seleccionadoActual != this)
        {
            seleccionadoActual.Deseleccionar();
        }

        seleccionadoActual = this;
        estaSeleccionado = true;

        AplicarColor(colorSeleccionado);

        if (panelInformacion == null)
        {
            Debug.LogError(
                $"No se asignó PanelInformacionAnatomica en " +
                $"{gameObject.name}.",
                gameObject
            );

            return;
        }

        panelInformacion.MostrarInformacion(
            nombreAnatomico,
            descripcion
        );

        Debug.Log(
            $"Información enviada al panel. Nombre: {nombreAnatomico}",
            gameObject
        );
    }

    public void Deseleccionar()
    {
        estaSeleccionado = false;

        if (punteroEncima)
        {
            AplicarColor(colorHover);
        }
        else
        {
            AplicarColor(colorOriginal);
        }

        if (seleccionadoActual == this)
        {
            seleccionadoActual = null;
        }
    }

    public static void LimpiarSeleccionActual()
    {
        if (seleccionadoActual != null)
        {
            seleccionadoActual.Deseleccionar();
        }
    }

    [ContextMenu("Probar selección y panel")]
    private void ProbarSeleccionYPanel()
    {
        Debug.Log(
            $"PRUEBA MANUAL DE SELECCIÓN: {gameObject.name}",
            gameObject
        );

        Seleccionar();
    }

    private void AplicarColor(Color nuevoColor)
    {
        if (rendererObjetivo == null)
        {
            return;
        }

        rendererObjetivo.GetPropertyBlock(propertyBlock);

        propertyBlock.SetColor(
            ColorId,
            nuevoColor
        );

        rendererObjetivo.SetPropertyBlock(propertyBlock);
    }
}