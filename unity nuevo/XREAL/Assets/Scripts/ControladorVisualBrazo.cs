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

    public void MostrarOcultarHuesos()
    {
        huesosVisibles = !huesosVisibles;
        huesosAnimacion.SetActive(huesosVisibles);

        if (huesosVisibles)
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
            huesosAnimacion.SetActive(true);
            ReiniciarAnimator(animatorHuesos);
        }

        AlternarExplosion(animatorHuesos, ref huesosAbiertos);
    }

    public void MostrarOcultarMusculos()
    {
        musculosVisibles = !musculosVisibles;
        musculosAnimacion.SetActive(musculosVisibles);

        if (musculosVisibles)
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
            musculosAnimacion.SetActive(true);
            ReiniciarAnimator(animatorMusculos);
        }

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