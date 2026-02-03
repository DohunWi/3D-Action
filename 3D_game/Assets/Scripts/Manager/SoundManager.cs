using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

    [Header("Settings")]
    [Range(0f, 0.2f)] public float pitchRandomness = 0.1f;

    // PlayOneShot을 위한 전용 소스 (플레이어 몸에 붙어있음)
    private AudioSource _oneShotSource;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // PlayOneShot용 소스 하나 추가
        _oneShotSource = gameObject.AddComponent<AudioSource>();
    }

    // 1. [적 타격용] 3D 위치 + 독립적인 피치 (기존 방식 유지)
    public void PlaySFX(AudioClip clip, Vector3 position, float volume = 1.0f)
    {
        if (clip == null) return;

        GameObject audioObj = new GameObject("TempAudio");
        audioObj.transform.position = position;

        AudioSource source = audioObj.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = volume;
        // ★ 독립적인 피치 조절 가능 (다른 소리에 영향 안 줌)
        source.pitch = 1.0f + Random.Range(-pitchRandomness, pitchRandomness);
        
        source.spatialBlend = 1.0f; // 3D 사운드
        source.minDistance = 2.0f;
        source.maxDistance = 20.0f;

        source.Play();
        Destroy(audioObj, clip.length + 0.1f);
    }

    // 2. [플레이어용] 가벼운 PlayOneShot (성능 최적화)
    public void PlayPlayerSFX(AudioClip clip, float volume = 1.0f)
    {
        if (clip == null) return;

        // ★ 피치 랜덤을 주면 겹친 소리가 다 변하므로, 
        // 여기서는 미세하게만 주거나 아예 안 주는 게 안전함.
        _oneShotSource.pitch = 1.0f + Random.Range(-0.05f, 0.05f); 
        _oneShotSource.PlayOneShot(clip, volume);
    }
}