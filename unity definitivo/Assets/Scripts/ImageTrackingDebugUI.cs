using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Muestra mensajes en pantalla sobre el estado del image tracking
/// </summary>
public class ImageTrackingDebugUI : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField]
    private ARTrackedImageManager trackedImageManager;

    [Header("UI Text")]
    [SerializeField]
    private TextMeshProUGUI statusText;

    private string currentStatus = "Buscando imagen...";
    private int trackedImagesCount = 0;

    void Start()
    {
        if (trackedImageManager == null)
        {
            Debug.LogError("ARTrackedImageManager no asignado!");
            enabled = false;
            return;
        }

        if (statusText == null)
        {
            Debug.LogError("Status Text no asignado!");
            enabled = false;
            return;
        }

        UpdateUI();
    }

    void OnEnable()
    {
        if (trackedImageManager != null)
        {
            trackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
        }
    }

    void OnDisable()
    {
        if (trackedImageManager != null)
        {
            trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
        }
    }

    void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        // Imágenes recién detectadas
        foreach (ARTrackedImage trackedImage in eventArgs.added)
        {
            trackedImagesCount++;
            currentStatus = $"✅ IMAGEN DETECTADA: {trackedImage.referenceImage.name}\n" +
                          $"Estado: Tracking activo";
            UpdateUI();
        }

        // Imágenes actualizadas
        foreach (ARTrackedImage trackedImage in eventArgs.updated)
        {
            string trackingState = GetTrackingStateText(trackedImage.trackingState);
            
            if (trackedImage.trackingState == TrackingState.Tracking)
            {
                currentStatus = $"🟢 TRACKING: {trackedImage.referenceImage.name}\n" +
                              $"Estado: {trackingState}";
            }
            else if (trackedImage.trackingState == TrackingState.Limited)
            {
                currentStatus = $"🟡 TRACKING LIMITADO: {trackedImage.referenceImage.name}\n" +
                              $"Estado: {trackingState}\n" +
                              $"Consejo: Acércate más a la imagen";
            }
            else
            {
                currentStatus = $"⚪ SIN TRACKING: {trackedImage.referenceImage.name}\n" +
                              $"Estado: {trackingState}";
            }
            
            UpdateUI();
        }

        // Imágenes que ya no se ven
        foreach (ARTrackedImage trackedImage in eventArgs.removed)
        {
            trackedImagesCount--;
            currentStatus = $"❌ IMAGEN PERDIDA: {trackedImage.referenceImage.name}\n" +
                          $"Buscando de nuevo...";
            UpdateUI();
        }

        // Si no hay imágenes trackeadas
        if (trackedImagesCount == 0 && eventArgs.removed.Count > 0)
        {
            currentStatus = "🔍 Buscando imagen...\n" +
                          "Apunta la cámara a la imagen impresa";
            UpdateUI();
        }
    }

    string GetTrackingStateText(TrackingState state)
    {
        switch (state)
        {
            case TrackingState.Tracking:
                return "Tracking activo";
            case TrackingState.Limited:
                return "Tracking limitado";
            case TrackingState.None:
                return "Sin tracking";
            default:
                return "Estado desconocido";
        }
    }

    void UpdateUI()
    {
        if (statusText != null)
        {
            statusText.text = currentStatus;
        }
    }

    void Update()
    {
        // Actualizar contador de imágenes
        if (trackedImageManager != null)
        {
            int count = 0;
            foreach (var image in trackedImageManager.trackables)
            {
                if (image.trackingState == TrackingState.Tracking)
                    count++;
            }

            if (count == 0 && trackedImagesCount > 0)
            {
                currentStatus = "🔍 Buscando imagen...\n" +
                              "Apunta la cámara a la imagen impresa";
                UpdateUI();
            }
        }
    }
}
