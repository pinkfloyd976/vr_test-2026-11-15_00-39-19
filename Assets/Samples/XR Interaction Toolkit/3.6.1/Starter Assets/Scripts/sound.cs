using UnityEngine;

// Вешается на объект, из которого идёт звук.
// Методы PlaySound / StopSound вызываются из события onPressed кнопок.
[RequireComponent(typeof(AudioSource))]
public class SoundSwitch : MonoBehaviour
{
    [Tooltip("Оставь пустым, если клип уже назначен в AudioSource")]
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 0.65f;
    public bool loop = true;
    [Tooltip("Секунд на плавное включение/выключение (0 = мгновенно)")]
    [Min(0f)] public float fadeTime = 0.3f;

    [SerializeField] bool isPlaying;
    AudioSource source;
    float targetVolume;

    public bool IsPlaying => isPlaying;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 1f; // 3D-звук из объекта
        source.volume = 0f;
        source.Stop();
        isPlaying = false;
    }

    // ВЫЗЫВАТЬ С КНОПКИ ВКЛЮЧЕНИЯ
    public void PlaySound()
    {
        if (isPlaying) return;
        if (clip) source.clip = clip;
        if (!source.clip)
        {
            Debug.LogWarning("SoundSwitch: нет аудиоклипа.", this);
            return;
        }
        source.loop = loop;
        if (!source.isPlaying) source.Play();
        targetVolume = volume;
        isPlaying = true;
    }

    // ВЫЗЫВАТЬ С КНОПКИ ВЫКЛЮЧЕНИЯ
    public void StopSound()
    {
        if (!isPlaying) return;
        targetVolume = 0f;
        isPlaying = false;
    }

    // По желанию: одна кнопка включает и выключает
    public void ToggleSound()
    {
        if (isPlaying) StopSound(); else PlaySound();
    }

    void Update()
    {
        if (fadeTime <= 0f)
            source.volume = targetVolume;
        else
            source.volume = Mathf.MoveTowards(source.volume, targetVolume, volume / fadeTime * Time.deltaTime);

        // когда звук затух до нуля — реально останавливаем источник
        if (!isPlaying && source.isPlaying && source.volume <= 0.001f)
            source.Stop();
    }
}
