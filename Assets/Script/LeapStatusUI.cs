using System.Threading.Tasks;
using Leap;
using TMPro;
using UnityEngine;

// Attach this to a GameObject in the Main Menu scene.
// Assign a LeapProvider (e.g., LeapServiceProvider) and a TMP_Text.
// It displays whether Leap frames are streaming, an approximate FPS,
// and which hands are currently detected.
// It also shows whether the TTL TriggerBox is connected and on which COM port.
public class LeapStatusUI : MonoBehaviour
{
    [Header("Leap")]
    [SerializeField] public LeapProvider leapProvider; // assign in Inspector

    [Header("UI")]
    [SerializeField] TMP_Text statusText; // assign in Inspector
    [Tooltip("Optional. If empty, a copy of statusText is created just below it.")]
    [SerializeField] TMP_Text triggerBoxText;
    [SerializeField] float triggerBoxLineOffset = 30f; // px below statusText for the auto-created line

    [Header("TriggerBox")]
    [Tooltip("Matched against the COM device name (Device Manager → Ports), same as TrialGameController_RWR.")]
    [SerializeField] string triggerBoxDeviceName = "TriggerBox";
    [SerializeField] float triggerBoxPollSeconds = 2f;

    [Header("Appearance")]
    [SerializeField] Color okColor    = new Color(0.2f, 0.85f, 0.2f);
    [SerializeField] Color warnColor  = new Color(0.95f, 0.75f, 0.2f);
    [SerializeField] Color errorColor = new Color(0.9f, 0.25f, 0.25f);
    [SerializeField] float staleSeconds = 1.0f; // no new device frames within this → stale

    // Time (unscaled) when we last observed a new device frame (based on device timestamp)
    float lastGoodFrameTime = -1f;
    long  lastFrameTimestamp = 0;

    // TriggerBox detection runs off the main thread (registry scan + open test); results land here.
    struct TriggerBoxResult { public string port; public string openError; }
    Task<TriggerBoxResult> triggerBoxScan;
    float  nextTriggerBoxPoll = 0f;
    bool   triggerBoxChecked  = false;
    string triggerBoxPort     = null;
    string triggerBoxError    = null;
    string verifiedPort       = null;   // port that last passed the open test; skip re-opening it

    void Awake()
    {
        if (!triggerBoxText && statusText)
        {
            // Clone the Leap line so the new line inherits its font, size and alignment.
            triggerBoxText = Instantiate(statusText, statusText.transform.parent);
            triggerBoxText.name = "TriggerBoxStatus";
            var rt = (RectTransform)triggerBoxText.transform;
            rt.anchoredPosition = ((RectTransform)statusText.transform).anchoredPosition + new Vector2(0f, -triggerBoxLineOffset);
        }
    }

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
        UpdateTriggerBox();
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

    void UpdateTriggerBox()
    {
        if (triggerBoxScan != null && triggerBoxScan.IsCompleted)
        {
            var r = triggerBoxScan.Status == TaskStatus.RanToCompletion ? triggerBoxScan.Result : default;
            triggerBoxPort    = r.port;
            triggerBoxError   = r.openError;
            verifiedPort      = (r.port != null && r.openError == null) ? r.port : null;
            triggerBoxChecked = true;
            triggerBoxScan    = null;
        }

        // Re-poll so plugging/unplugging the box while on the menu is reflected.
        if (triggerBoxScan == null && Time.unscaledTime >= nextTriggerBoxPoll)
        {
            string match    = triggerBoxDeviceName;
            string verified = verifiedPort;
            triggerBoxScan = Task.Run(() =>
            {
                string port = TtlPortFinder.Find(match);
                // Open-test only a newly seen or previously failing port, so a working box
                // isn't opened every poll.
                string err = (port == null || port == verified) ? null : TtlPortFinder.TryOpen(port);
                return new TriggerBoxResult { port = port, openError = err };
            });
            nextTriggerBoxPoll = Time.unscaledTime + triggerBoxPollSeconds;
        }

        if (!triggerBoxText)
            return;

        if (!triggerBoxChecked)
        {
            triggerBoxText.text  = "TriggerBox: checking...";
            triggerBoxText.color = warnColor;
        }
        else if (string.IsNullOrEmpty(triggerBoxPort))
        {
            triggerBoxText.text  = "TriggerBox: not connected";
            triggerBoxText.color = errorColor;
        }
        else if (triggerBoxError != null)
        {
            triggerBoxText.text  = $"TriggerBox: {triggerBoxPort} not responding — replug USB";
            triggerBoxText.color = errorColor;
        }
        else
        {
            triggerBoxText.text  = $"TriggerBox: connected ({triggerBoxPort})";
            triggerBoxText.color = okColor;
        }
    }
}
