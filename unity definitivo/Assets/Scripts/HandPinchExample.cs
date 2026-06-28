using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;


public class HandPinchExample : MonoBehaviour
{
    public InputActionReference leftPinchAction;
    public InputActionReference rightPinchAction;

    private void OnEnable()
    {
        leftPinchAction.action.Enable();
        leftPinchAction.action.performed += OnLeftPinch;

        rightPinchAction.action.Enable();
        rightPinchAction.action.performed += OnRightPinch;
    }

    private void OnDisable()
    {
        leftPinchAction.action.performed -= OnLeftPinch;
        leftPinchAction.action.Disable();

        rightPinchAction.action.performed -= OnRightPinch;
        rightPinchAction.action.Disable();
    }

    void OnLeftPinch(InputAction.CallbackContext ctx)
    {
        Debug.Log("Left hand pinch detected!");
    }

    void OnRightPinch(InputAction.CallbackContext ctx)
    {
        Debug.Log("Right hand pinch detected!");
    }
}
