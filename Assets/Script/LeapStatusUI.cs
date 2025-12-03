using Leap;
using TMPro;
using UnityEngine;

// Attach this to a GameObject in the Main Menu scene.
// Assign a LeapProvider (e.g., LeapServiceProvider) and a TMP_Text.
// It displays whether Leap frames are streaming, an approximate FPS,
// and which hands are currently detected.
public class LeapStatusUI : MonoBehaviour
{
    [Header("Leap")]
    [SerializeField] public LeapProvider leapProvider; // assign in Inspector

    [Header("UI")]
    [SerializeField] TMP_Text statusText; // assign in Inspector

    [Header("Appearance")]
    [SerializeField] Color okColor    = new Color(0.2f, 0.85f, 0.2f);
    [SerializeField] Color warnColor  = new Color(0.95f, 0.75f, 0.2f);
    [SerializeField] Color errorColor = new Color(0.9f, 0.25f, 0.25f);
    [SerializeField] float staleSeconds = 1.0f; // no new device frames within this → stale

    // Time (unscaled) when we last observed a new device frame (based on device timestamp)
    float lastGoodFrameTime = -1f;
    long  lastFrameTimestamp = 0;

    void OnEnable()
    {
        if (leapProvider != null)
            leapProvider.OnUpdateFrame += OnUpdateFrame;
    }

    void OnDisable()
    {
        if (leapProvider != null)
            leapProvider.OnUpdateFrame -= OnUpdateFrame;
    }

    void Update()
    {
        UpdateUi();
    }

    void OnUpdateFrame(Frame frame)
    {
        // Only treat as streaming when device timestamp advances
        if (frame != null)
        {
            long ts = frame.Timestamp; // microseconds, increases per device frame
            if (ts > 0 && ts != lastFrameTimestamp)
            {
                lastGoodFrameTime = Time.unscaledTime;
                lastFrameTimestamp = ts;
            }
        }
    }

    void UpdateUi()
    {
        if (!statusText)
            return;

        if (leapProvider == null)
        {
            statusText.text = "Leap: provider not assigned";
            statusText.color = errorColor;
            return;
        }

        float age = (lastGoodFrameTime < 0f) ? float.PositiveInfinity : (Time.unscaledTime - lastGoodFrameTime);
        if (age > staleSeconds)
        {
            statusText.text = "Leap: no data";
            statusText.color = warnColor;
        }
        else
        {
            statusText.text = "Leap: streaming";
            statusText.color = okColor;
        }
    }
}
