using UnityEngine;
using System.Collections;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

    [Header("Settings")]
    [Range(0f, 0.2f)] public float pitchRandomness = 0.1f;

    [Header("BGM")]
    [Range(0f, 1f)] public float bgmVolume = 0.7f;
    public float defaultFadeDuration = 1.5f;

    private AudioSource _oneShotSource;
    private AudioSource _bgmSourceA;
    private AudioSource _bgmSourceB;
    private bool _isSourceAActive = true;
    private AudioClip _fieldBGM; // 씬 기본 BGM (보스 전투 후 복귀용)

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        _oneShotSource = gameObject.AddComponent<AudioSource>();

        _bgmSourceA = gameObject.AddComponent<AudioSource>();
        _bgmSourceA.loop = true;
        _bgmSourceA.playOnAwake = false;
        _bgmSourceA.volume = 0f;

        _bgmSourceB = gameObject.AddComponent<AudioSource>();
        _bgmSourceB.loop = true;
        _bgmSourceB.playOnAwake = false;
        _bgmSourceB.volume = 0f;
    }

    // 씬 기본 BGM 설정 및 재생 (보스 전투 후 복귀 기준점)
    public void PlayFieldBGM(AudioClip clip, float fadeDuration = -1f)
    {
        if (clip == null) return;
        _fieldBGM = clip;
        PlayBGM(clip, fadeDuration);
    }

    // 보스 전투 종료 후 필드 BGM 복귀
    public void RestoreFieldBGM(float fadeDuration = -1f)
    {
        if (_fieldBGM == null) return;
        CrossFadeBGM(_fieldBGM, fadeDuration);
    }

    // BGM 재생 (현재 BGM이 없을 때 또는 페이드인)
    public void PlayBGM(AudioClip clip, float fadeDuration = -1f)
    {
        if (clip == null) return;
        float fade = fadeDuration < 0f ? defaultFadeDuration : fadeDuration;

        AudioSource incoming = _isSourceAActive ? _bgmSourceA : _bgmSourceB;
        incoming.clip = clip;
        incoming.volume = 0f;
        incoming.Play();
        StartCoroutine(FadeIn(incoming, fade));
    }

    // 현재 BGM 중단 (페이드아웃)
    public void StopBGM(float fadeDuration = -1f)
    {
        float fade = fadeDuration < 0f ? defaultFadeDuration : fadeDuration;
        AudioSource active = _isSourceAActive ? _bgmSourceA : _bgmSourceB;
        StartCoroutine(FadeOut(active, fade));
    }

    // BGM 교체 (크로스페이드)
    public void CrossFadeBGM(AudioClip clip, float fadeDuration = -1f)
    {
        if (clip == null) return;
        float fade = fadeDuration < 0f ? defaultFadeDuration : fadeDuration;

        AudioSource outgoing = _isSourceAActive ? _bgmSourceA : _bgmSourceB;
        AudioSource incoming = _isSourceAActive ? _bgmSourceB : _bgmSourceA;
        _isSourceAActive = !_isSourceAActive;

        incoming.clip = clip;
        incoming.volume = 0f;
        incoming.Play();
        StartCoroutine(FadeOut(outgoing, fade));
        StartCoroutine(FadeIn(incoming, fade));
    }

    private IEnumerator FadeIn(AudioSource source, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            source.volume = Mathf.Lerp(0f, bgmVolume, elapsed / duration);
            yield return null;
        }
        source.volume = bgmVolume;
    }

    private IEnumerator FadeOut(AudioSource source, float duration)
    {
        float startVolume = source.volume;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            source.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
            yield return null;
        }
        source.volume = 0f;
        source.Stop();
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

    public void PlayRandomSFX(AudioClip[] clips, float volume = 1.0f)
    {
        // 배열이 비어있거나 오디오 소스가 없으면 패스
        if (clips == null || clips.Length == 0 || _oneShotSource == null) return;

        // 랜덤하게 하나 뽑기
        int index = Random.Range(0, clips.Length);
        
        if (clips[index] != null)
        {
            _oneShotSource.pitch = Random.Range(0.9f, 1.1f);
            _oneShotSource.PlayOneShot(clips[index], volume);
        }
    }
}