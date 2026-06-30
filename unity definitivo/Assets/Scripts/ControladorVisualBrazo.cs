using UnityEngine;

public class ControladorVisualBrazo : MonoBehaviour
{
    [Header("Huesos")]
    public GameObject huesosAnimacion;
    public Animator animatorHuesos;

    [Header("Músculos")]
    public GameObject musculosAnimacion;
    public Animator animatorMusculos;

    private bool huesosVisibles = false;
    private bool huesosAbiertos = false;

    private bool musculosVisibles = false;
    private bool musculosAbiertos = false;

    void Start()
    {
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

        if (huesosAnimacion != null)
            huesosAnimacion.SetActive(huesosVisibles);

        if (huesosVisibles && animatorHuesos != null)
        {
            huesosAbiertos = false;
            ReiniciarAnimator(animatorHuesos);
        }
    }

    public void AlternarExplosionHuesos()
    {
        if (!huesosVisibles)
        {
            huesosVisibles = true;

            if (huesosAnimacion != null)
                huesosAnimacion.SetActive(true);

            if (animatorHuesos != null)
                ReiniciarAnimator(animatorHuesos);
        }

        if (animatorHuesos != null)
            AlternarExplosion(animatorHuesos, ref huesosAbiertos);
    }

    public void MostrarOcultarMusculos()
    {
        musculosVisibles = !musculosVisibles;

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

            if (musculosAnimacion != null)
                musculosAnimacion.SetActive(true);

            if (animatorMusculos != null)
                ReiniciarAnimator(animatorMusculos);
        }

        if (animatorMusculos != null)
            AlternarExplosion(animatorMusculos, ref musculosAbiertos);
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