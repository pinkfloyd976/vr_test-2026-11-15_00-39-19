using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(AudioSource))]
public class CrusherMotorController : MonoBehaviour
{
    public AudioClip motorSound;
    [Range(0f, 1f)] public float volume = 0.65f;
    [Min(0.01f)] public float minDistance = 1f;
    [Min(0.02f)] public float maxDistance = 20f;
    public UnityEvent onStarted = new UnityEvent();
    public UnityEvent onStopped = new UnityEvent();
    [SerializeField] bool isRunning;
    AudioSource motorSource;
    int lastStopFrame = -1;
    public bool IsRunning => isRunning;

    void Reset()
    {
        motorSource = GetComponent<AudioSource>();
        ConfigureSource();
    }
    void Awake()
    {
        motorSource = GetComponent<AudioSource>();
        ConfigureSource();
        motorSource.Stop();
        isRunning = false;
    }
    void ConfigureSource()
    {
        if (!motorSource) return;
        motorSource.playOnAwake = false;
        motorSource.loop = true;
        motorSource.spatialBlend = 1f;
        motorSource.volume = volume;
        motorSource.minDistance = minDistance;
        motorSource.maxDistance = Mathf.Max(minDistance, maxDistance);
        motorSource.rolloffMode = AudioRolloffMode.Logarithmic;
        motorSource.dopplerLevel = 0f;
    }

    public void StartMotor()
    {
        // STOP wins if both buttons are touched in the same frame.
        if (!isActiveAndEnabled || isRunning || lastStopFrame == Time.frameCount) return;
        if (!motorSound)
        {
            Debug.LogWarning("Crusher: assign Motor Sound in the Inspector.", this);
            return;
        }
        ConfigureSource();
        motorSource.clip = motorSound;
        motorSource.Play();
        isRunning = true;
        onStarted.Invoke();
    }

    public void StopMotor()
    {
        lastStopFrame = Time.frameCount;
        if (motorSource) motorSource.Stop();
        bool wasRunning = isRunning;
        isRunning = false;
        if (wasRunning) onStopped.Invoke();
    }

    void OnDisable() => StopMotor();
}
