using UnityEngine;
using TMPro;
using Leap;   // for Frame + LeapProvider

public class FpsDisplay : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text label;

    [Header("Leap Provider (Optional)")]
    [SerializeField] private LeapProvider leapProvider;

    [Header("LabChart (Optional)")]
    [SerializeField] private LabChartStatusChecker    labChartStatus;
    [SerializeField] private LabChartFro              labChartFro;
    [SerializeField] private GameSessionController_RWR sessionController; // for kinematic-only (SHIFT+SPACE bypass) status

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

            if (labChartStatus != null)
            {
                string labChartLine;
                if (sessionController != null && sessionController.LabChartBypassed)
                    labChartLine = "<color=#888888>LabChart: OFF (kinematic-only)</color>";
                else if (!labChartStatus.IsOpen)
                    labChartLine = "<color=#FF4444>LabChart: OFF</color>";
                else if (labChartFro != null && labChartFro.IsRecording)
                    labChartLine = "<color=#44FF44>LabChart: Recording</color>";
                else if (labChartFro != null && labChartFro.IsArming)
                    labChartLine = "<color=#FFFF44>LabChart: Arming...</color>";
                else
                    labChartLine = "<color=#FFFF44>LabChart: idling</color>";

                text += $"\n{labChartLine}";
            }

            if (label != null)
                label.text = text;

            unityAccum = 0f;
            unityFrames = 0;
        }
    }
}
