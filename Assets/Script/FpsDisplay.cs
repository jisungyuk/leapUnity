using UnityEngine;
using TMPro;
using Leap;   // for Frame + LeapProvider

public class FpsDisplay : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text label;

    [Header("Leap Provider (Optional)")]
    [SerializeField] private LeapProvider leapProvider;

    // Unity FPS 계산용
    private float unityAccum = 0f;
    private int   unityFrames = 0;

    // Leap FPS 저장
    private float leapFps = 0f;

    void Awake()
    {
        if (!label)
            label = GetComponent<TMP_Text>();
    }

    void Update()
    {
        // ---------- Unity FPS ----------
        unityAccum += Time.unscaledDeltaTime;
        unityFrames++;

        // ---------- Leap FPS ----------
        if (leapProvider != null)
        {
            Frame f = leapProvider.CurrentFrame;
            if (f != null)
                leapFps = f.CurrentFramesPerSecond; // Leap 자체 FPS
        }

        // 1초마다 텍스트 업데이트
        if (unityAccum >= 1f)
        {
            float unityFps = unityFrames / unityAccum;

            string text = $"Unity: {unityFps:0.0} fps";

            if (leapProvider != null)
                text += $"\nLeap:  {leapFps:0.0} fps";

            if (label != null)
                label.text = text;

            unityAccum = 0f;
            unityFrames = 0;
        }
    }
}
