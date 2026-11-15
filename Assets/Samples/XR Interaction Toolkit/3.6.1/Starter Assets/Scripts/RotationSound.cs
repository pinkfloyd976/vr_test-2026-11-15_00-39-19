using UnityEngine;

// Вешается на объект, из которого идёт звук.
// Сам следит за скриптом FlipAndSpin: звук играет, пока объект вращается,
// и затихает, когда вращение закончилось. Кнопки для звука подключать не нужно.
[RequireComponent(typeof(AudioSource))]
public class RotationSound : MonoBehaviour
{
    [Header("Что слушаем")]
    [Tooltip("Скрипт вращения. Если пусто — ищется на этом же объекте")]
    public FlipAndSpin rotator;

    [Header("Звук")]
    [Tooltip("Оставь пустым, если клип уже назначен в AudioSource")]
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 0.65f;
    [Tooltip("Секунд на плавное включение (0 = мгновенно)")]
    [Min(0f)] public float fadeInTime = 0.3f;
    [Tooltip("Секунд на плавное затухание (0 = мгновенно)")]
    [Min(0f)] public float fadeOutTime = 0.6f;

    [Header("Когда играть")]
    public bool playDuringFlip = true;    // во время переворота на 180°
    public bool playDuringSpin = true;    // во время постоянного вращения
    public bool playDuringSettle = true;  // во время доворота после остановки

    [SerializeField] bool isPlaying;
    AudioSource source;

    public bool IsPlaying => isPlaying;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 1f; // 3D-звук из объекта
        source.volume = 0f;
        source.Stop();

        if (!rotator) rotator = GetComponent<FlipAndSpin>();
        if (!rotator) Debug.LogWarning("RotationSound: назначь Rotator (объект со скриптом FlipAndSpin).", this);
    }

    bool ShouldPlay()
    {
        if (!rotator) return false;

        switch (rotator.state)
        {
            case FlipAndSpin.State.Переворот: return playDuringFlip;
            case FlipAndSpin.State.Вращение:  return playDuringSpin;
            case FlipAndSpin.State.Доворот:   return playDuringSettle;
            default: return false; // Ожидание и Перевёрнут — объект стоит
        }
    }

    void Update()
    {
        bool want = ShouldPlay();

        if (want)
        {
            if (clip) source.clip = clip;
            if (!source.clip)
            {
                Debug.LogWarning("RotationSound: нет аудиоклипа.", this);
                return;
            }
            if (!source.isPlaying) source.Play();

            source.volume = fadeInTime <= 0f
                ? volume
                : Mathf.MoveTowards(source.volume, volume, volume / fadeInTime * Time.deltaTime);
        }
        else
        {
            source.volume = fadeOutTime <= 0f
                ? 0f
                : Mathf.MoveTowards(source.volume, 0f, volume / fadeOutTime * Time.deltaTime);

            if (source.isPlaying && source.volume <= 0.001f) source.Stop();
        }

        isPlaying = want;
    }
}
