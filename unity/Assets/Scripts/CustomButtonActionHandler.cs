using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CustomButtonActionHandler : MonoBehaviour
{
    public InputActionReference buttonAction; 
    public GameObject sphere; 
    public Color targetColor = Color.red; 
    private Renderer sphereRenderer;
    private Color originalColor;

    private void Awake()
    {
        if (sphere != null)
        {
            sphereRenderer = sphere.GetComponent<Renderer>();
            originalColor = sphereRenderer.material.color;
        }
    }

    private void OnEnable()
    {
        buttonAction.action.performed += OnButtonPressed;
        buttonAction.action.Enable();
    }

    private void OnDisable()
    {
        buttonAction.action.performed -= OnButtonPressed;
        buttonAction.action.Disable();
    }

    private void OnButtonPressed(InputAction.CallbackContext context)
    {
        if (sphereRenderer != null)
        {
            sphereRenderer.material.color = targetColor;
        }
    }
}