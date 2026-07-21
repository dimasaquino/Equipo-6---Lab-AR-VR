using TMPro;
using UnityEngine;

public class ControladorInfoHueso : MonoBehaviour
{
    public GameObject panelInfo;
    public TMP_Text tituloHueso;
    public TMP_Text descripcionHueso;

    public void MostrarInfo(string titulo, string descripcion)
    {
        if (panelInfo != null)
            panelInfo.SetActive(true);

        if (tituloHueso != null)
            tituloHueso.text = titulo;

        if (descripcionHueso != null)
            descripcionHueso.text = descripcion;
    }

    public void OcultarInfo()
    {
        if (panelInfo != null)
            panelInfo.SetActive(false);
    }
}