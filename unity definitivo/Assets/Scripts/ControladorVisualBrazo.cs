using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class ControladorVisualBrazo : MonoBehaviour
{
    private const string EstadoHuesosCerrando = "Armature|CERRAR";

    public bool HuesosAbiertos => huesosAbiertos;

    public bool HuesosEnTransicion
    {
        get
        {
            if (animatorHuesos == null || !animatorHuesos.isActiveAndEnabled)
                return false;

            const int capa = 0;
            if (animatorHuesos.IsInTransition(capa))
                return true;

            AnimatorStateInfo estado = animatorHuesos.GetCurrentAnimatorStateInfo(capa);
            bool animacionRelevante =
                estado.IsName(EstadoHuesosAbiertos) ||
                estado.IsName(EstadoHuesosCerrando);

            return animacionRelevante && estado.normalizedTime < 1f;
        }
    }

    private const string EstadoHuesosAbiertos = "Armature|ABRIR";

    [Header("Huesos")]
    public GameObject huesosAnimacion;
    public Animator animatorHuesos;

    [SerializeField] private PanelInformacionAnatomica panelInformacion;

    [Header("Músculos")]
    public GameObject musculosAnimacion;
    public Animator animatorMusculos;

    private bool huesosVisibles = false;
    private bool huesosAbiertos = false;
    private bool musculosVisibles = false;
    private bool musculosAbiertos = false;

    private XRBaseInteractable[] interactablesHuesos;
    private Coroutine esperaAperturaHuesos;
    private int versionEsperaApertura;

    private void Awake()
    {
        CachearInteractablesHuesos();
        EstablecerInteraccionHuesos(false);
    }

    private void Start()
    {
        PrepararHuesosNoInteractivos();

        if (huesosAnimacion != null)
            huesosAnimacion.SetActive(false);

        if (musculosAnimacion != null)
            musculosAnimacion.SetActive(false);

        huesosVisibles = false;
        musculosVisibles = false;
        huesosAbiertos = false;
        musculosAbiertos = false;
    }

    public void MostrarOcultarHuesos()
    {
        huesosVisibles = !huesosVisibles;

        if (!huesosVisibles)
        {
            huesosAbiertos = false;
            PrepararHuesosNoInteractivos();
        }

        if (huesosAnimacion != null)
            huesosAnimacion.SetActive(huesosVisibles);

        if (huesosVisibles)
        {
            PrepararHuesosNoInteractivos();
            huesosAbiertos = false;

            if (animatorHuesos != null)
                ReiniciarAnimator(animatorHuesos);
        }
    }

    public void AlternarExplosionHuesos()
    {
        if (!huesosVisibles)
        {
            huesosVisibles = true;
            huesosAbiertos = false;

            if (huesosAnimacion != null)
                huesosAnimacion.SetActive(true);

            if (animatorHuesos != null)
                ReiniciarAnimator(animatorHuesos);
        }

        if (animatorHuesos == null)
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
        musculosVisibles = !musculosVisibles;

        if (musculosVisibles)
            PrepararHuesosNoInteractivos();

        if (musculosAnimacion != null)
            musculosAnimacion.SetActive(musculosVisibles);

        if (musculosVisibles && animatorMusculos != null)
        {
            musculosAbiertos = false;
            ReiniciarAnimator(animatorMusculos);
        }
    }

    public void AlternarExplosionMusculos()
    {
        if (!musculosVisibles)
        {
            musculosVisibles = true;
            PrepararHuesosNoInteractivos();

            if (musculosAnimacion != null)
                musculosAnimacion.SetActive(true);

            if (animatorMusculos != null)
                ReiniciarAnimator(animatorMusculos);
        }

        if (animatorMusculos != null)
            AlternarExplosion(animatorMusculos, ref musculosAbiertos);
    }

    private void CachearInteractablesHuesos()
    {
        interactablesHuesos = huesosAnimacion != null
            ? huesosAnimacion.GetComponentsInChildren<XRBaseInteractable>(true)
            : new XRBaseInteractable[0];
    }

    private void EstablecerInteraccionHuesos(bool habilitada)
    {
        if (interactablesHuesos == null)
            return;

        foreach (XRBaseInteractable interactable in interactablesHuesos)
        {
            if (interactable != null)
                interactable.enabled = habilitada;
        }
    }

    private void PrepararHuesosNoInteractivos()
    {
        CancelarEsperaFinAperturaHuesos();
        EstablecerInteraccionHuesos(false);

        if (panelInformacion != null)
            panelInformacion.OcultarPanel();
    }

    private void IniciarEsperaFinAperturaHuesos()
    {
        CancelarEsperaFinAperturaHuesos();
        int versionActual = versionEsperaApertura;
        esperaAperturaHuesos =
            StartCoroutine(EsperarFinAperturaHuesos(versionActual));
    }

    private void CancelarEsperaFinAperturaHuesos()
    {
        versionEsperaApertura++;

        if (esperaAperturaHuesos != null)
        {
            StopCoroutine(esperaAperturaHuesos);
            esperaAperturaHuesos = null;
        }
    }

    private IEnumerator EsperarFinAperturaHuesos(int versionActual)
    {
        while (versionActual == versionEsperaApertura &&
               huesosVisibles &&
               huesosAbiertos)
        {
            if (animatorHuesos != null && animatorHuesos.isActiveAndEnabled)
            {
                AnimatorStateInfo estado =
                    animatorHuesos.GetCurrentAnimatorStateInfo(0);

                if (estado.IsName(EstadoHuesosAbiertos) &&
                    estado.normalizedTime >= 1f &&
                    !animatorHuesos.IsInTransition(0))
                {
                    EstablecerInteraccionHuesos(true);
                    esperaAperturaHuesos = null;
                    yield break;
                }
            }

            yield return null;
        }

        esperaAperturaHuesos = null;
    }

    private void ReiniciarAnimator(Animator animator)
    {
        animator.ResetTrigger("Abrir");
        animator.ResetTrigger("Cerrar");
        animator.Play("Armature|IDLE", 0, 0f);
        animator.Update(0f);
    }

    private void AlternarExplosion(Animator animator, ref bool abierto)
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
