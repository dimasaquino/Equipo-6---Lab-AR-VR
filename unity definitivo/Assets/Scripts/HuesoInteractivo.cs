using UnityEngine;

public class HuesoInteractivo : MonoBehaviour
{
    public string nombreHueso;

    [TextArea(3, 6)]
    public string descripcion;

    public ControladorInfoHueso controladorInfo;

    public Color colorSeleccionado = Color.yellow;

    private Renderer rend;
    private Color colorOriginal;

    void Start()
    {
        rend = GetComponent<Renderer>();

        if (rend != null)
            colorOriginal = rend.material.color;
    }

    void OnMouseDown()
    {
        SeleccionarHueso();
    }

    public void SeleccionarHueso()
    {
        if (rend != null)
            rend.material.color = colorSeleccionado;

        if (controladorInfo != null)
            controladorInfo.MostrarInfo(nombreHueso, descripcion);
    }

    public void DeseleccionarHueso()
    {
        if (rend != null)
            rend.material.color = colorOriginal;
    }
}