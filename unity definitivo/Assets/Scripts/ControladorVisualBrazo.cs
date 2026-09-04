using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class ControladorVisualBrazo : MonoBehaviour
{
    private const string EstadoAbierto = "Armature|ABRIR";
    private const string EstadoCerrando = "Armature|CERRAR";

    public bool HuesosVisibles => huesosVisibles;
    public bool MusculosVisibles => musculosVisibles;
    public bool HuesosAbiertos => huesosAbiertos;
    public bool MusculosAbiertos => musculosAbiertos;
    public bool HuesosEnTransicion => AnimatorEnTransicion(animatorHuesos);
    public bool MusculosEnTransicion => AnimatorEnTransicion(animatorMusculos);

    [Header("Huesos")]
    public GameObject huesosAnimacion;
    public Animator animatorHuesos;

    [SerializeField] private PanelInformacionAnatomica panelInformacion;

    [Header("Músculos")]
    public GameObject musculosAnimacion;
    public Animator animatorMusculos;

    private bool huesosVisibles;
    private bool huesosAbiertos;
    private bool musculosVisibles;
    private bool musculosAbiertos;

    private XRBaseInteractable[] interactablesHuesos;
    private XRBaseInteractable[] interactablesMusculos;

    private Coroutine esperaAperturaHuesos;
    private Coroutine esperaAperturaMusculos;
    private int versionEsperaAperturaHuesos;
    private int versionEsperaAperturaMusculos;

    private void Awake()
    {
        CachearInteractables();
        EstablecerInteraccionHuesos(false);
        EstablecerInteraccionMusculos(false);
    }

    private void Start()
    {
        PrepararHuesosNoInteractivos();
        PrepararMusculosNoInteractivos();

        if (huesosAnimacion != null)
            huesosAnimacion.SetActive(false);

        if (musculosAnimacion != null)
            musculosAnimacion.SetActive(false);

        huesosVisibles = false;
        huesosAbiertos = false;
        musculosVisibles = false;
        musculosAbiertos = false;
    }

    public void MostrarOcultarHuesos()
    {
        if (huesosVisibles)
        {
            OcultarHuesos();
            return;
        }

        MostrarHuesosEnEstadoNormal();
    }

public void AlternarExplosionHuesos()
    {
        if (!huesosVisibles || animatorHuesos == null)
            return;

        if (!huesosAbiertos)
        {
            PrepararHuesosNoInteractivos();
            AlternarExplosion(animatorHuesos, ref huesosAbiertos);
            IniciarEsperaFinAperturaHuesos();
        }
        else
        {
            PrepararHuesosNoInteractivos();
            AlternarExplosion(animatorHuesos, ref huesosAbiertos);
        }
    }

    public void MostrarOcultarMusculos()
    {
        if (musculosVisibles)
        {
            OcultarMusculos();
            return;
        }

        MostrarMusculosEnEstadoNormal();
    }

public void AlternarExplosionMusculos()
    {
        if (!musculosVisibles || animatorMusculos == null)
            return;

        if (!musculosAbiertos)
        {
            PrepararMusculosNoInteractivos();
            AlternarExplosion(animatorMusculos, ref musculosAbiertos);
            IniciarEsperaFinAperturaMusculos();
        }
        else
        {
            PrepararMusculosNoInteractivos();
            AlternarExplosion(animatorMusculos, ref musculosAbiertos);
        }
    }

    private void MostrarHuesosEnEstadoNormal()
    {
        OcultarMusculos();
        PrepararHuesosNoInteractivos();

        huesosVisibles = true;
        huesosAbiertos = false;

        if (huesosAnimacion != null)
            huesosAnimacion.SetActive(true);

        if (animatorHuesos != null)
            ReiniciarAnimator(animatorHuesos);
    }

    private void MostrarMusculosEnEstadoNormal()
    {
        OcultarHuesos();
        PrepararMusculosNoInteractivos();

        musculosVisibles = true;
        musculosAbiertos = false;

        if (musculosAnimacion != null)
            musculosAnimacion.SetActive(true);

        if (animatorMusculos != null)
            ReiniciarAnimator(animatorMusculos);
    }

    private void OcultarHuesos()
    {
        PrepararHuesosNoInteractivos();

        if (animatorHuesos != null && animatorHuesos.isActiveAndEnabled)
            ReiniciarAnimator(animatorHuesos);

        if (huesosAnimacion != null)
            huesosAnimacion.SetActive(false);

        huesosVisibles = false;
        huesosAbiertos = false;
    }

    private void OcultarMusculos()
    {
        PrepararMusculosNoInteractivos();

        if (animatorMusculos != null && animatorMusculos.isActiveAndEnabled)
            ReiniciarAnimator(animatorMusculos);

        if (musculosAnimacion != null)
            musculosAnimacion.SetActive(false);

        musculosVisibles = false;
        musculosAbiertos = false;
    }

    private void CachearInteractables()
    {
        interactablesHuesos = huesosAnimacion != null
            ? huesosAnimacion.GetComponentsInChildren<XRBaseInteractable>(true)
            : new XRBaseInteractable[0];

        interactablesMusculos = musculosAnimacion != null
            ? musculosAnimacion.GetComponentsInChildren<XRBaseInteractable>(true)
            : new XRBaseInteractable[0];
    }

    private void EstablecerInteraccionHuesos(bool habilitada)
    {
        EstablecerInteraccion(interactablesHuesos, habilitada);
    }

    private void EstablecerInteraccionMusculos(bool habilitada)
    {
        EstablecerInteraccion(interactablesMusculos, habilitada);
    }

    private static void EstablecerInteraccion(
        XRBaseInteractable[] interactables,
        bool habilitada)
    {
        if (interactables == null)
            return;

        foreach (XRBaseInteractable interactable in interactables)
        {
            if (interactable != null)
                interactable.enabled = habilitada;
        }
    }

    private void PrepararHuesosNoInteractivos()
    {
        CancelarEsperaFinAperturaHuesos();
        EstablecerInteraccionHuesos(false);
        CerrarPanelYLimpiarSeleccion();
    }

    private void PrepararMusculosNoInteractivos()
    {
        CancelarEsperaFinAperturaMusculos();
        EstablecerInteraccionMusculos(false);
        CerrarPanelYLimpiarSeleccion();
    }

    private void CerrarPanelYLimpiarSeleccion()
    {
        if (panelInformacion != null)
            panelInformacion.OcultarPanel();
        else
            ElementoAnatomicoInteractivo.LimpiarSeleccionActual();
    }

    private void IniciarEsperaFinAperturaHuesos()
    {
        CancelarEsperaFinAperturaHuesos();
        int versionActual = versionEsperaAperturaHuesos;
        esperaAperturaHuesos =
            StartCoroutine(EsperarFinAperturaHuesos(versionActual));
    }

    private void IniciarEsperaFinAperturaMusculos()
    {
        CancelarEsperaFinAperturaMusculos();
        int versionActual = versionEsperaAperturaMusculos;
        esperaAperturaMusculos =
            StartCoroutine(EsperarFinAperturaMusculos(versionActual));
    }

    private void CancelarEsperaFinAperturaHuesos()
    {
        versionEsperaAperturaHuesos++;

        if (esperaAperturaHuesos == null)
            return;

        StopCoroutine(esperaAperturaHuesos);
        esperaAperturaHuesos = null;
    }

    private void CancelarEsperaFinAperturaMusculos()
    {
        versionEsperaAperturaMusculos++;

        if (esperaAperturaMusculos == null)
            return;

        StopCoroutine(esperaAperturaMusculos);
        esperaAperturaMusculos = null;
    }

    private IEnumerator EsperarFinAperturaHuesos(int versionActual)
    {
        while (versionActual == versionEsperaAperturaHuesos &&
               huesosVisibles &&
               huesosAbiertos)
        {
            if (AperturaTerminada(animatorHuesos))
            {
                EstablecerInteraccionHuesos(true);
                esperaAperturaHuesos = null;
                yield break;
            }

            yield return null;
        }

        esperaAperturaHuesos = null;
    }

    private IEnumerator EsperarFinAperturaMusculos(int versionActual)
    {
        while (versionActual == versionEsperaAperturaMusculos &&
               musculosVisibles &&
               musculosAbiertos)
        {
            if (AperturaTerminada(animatorMusculos))
            {
                EstablecerInteraccionMusculos(true);
                esperaAperturaMusculos = null;
                yield break;
            }

            yield return null;
        }

        esperaAperturaMusculos = null;
    }

    private static bool AperturaTerminada(Animator animator)
    {
        if (animator == null || !animator.isActiveAndEnabled)
            return false;

        const int capa = 0;
        AnimatorStateInfo estado = animator.GetCurrentAnimatorStateInfo(capa);
        return estado.IsName(EstadoAbierto) &&
               estado.normalizedTime >= 1f &&
               !animator.IsInTransition(capa);
    }

    private static bool AnimatorEnTransicion(Animator animator)
    {
        if (animator == null || !animator.isActiveAndEnabled)
            return false;

        const int capa = 0;
        if (animator.IsInTransition(capa))
            return true;

        AnimatorStateInfo estado = animator.GetCurrentAnimatorStateInfo(capa);
        bool animacionRelevante =
            estado.IsName(EstadoAbierto) ||
            estado.IsName(EstadoCerrando);

        return animacionRelevante && estado.normalizedTime < 1f;
    }

    private static void ReiniciarAnimator(Animator animator)
    {
        animator.ResetTrigger("Abrir");
        animator.ResetTrigger("Cerrar");
        animator.Play("Armature|IDLE", 0, 0f);
        animator.Update(0f);
    }

    private static void AlternarExplosion(
        Animator animator,
        ref bool abierto)
    {
        if (!abierto)
        {
            animator.ResetTrigger("Cerrar");
            animator.SetTrigger("Abrir");
            abierto = true;
        }
        else
        {
            animator.ResetTrigger("Abrir");
            animator.SetTrigger("Cerrar");
            abierto = false;
        }
    }
}
