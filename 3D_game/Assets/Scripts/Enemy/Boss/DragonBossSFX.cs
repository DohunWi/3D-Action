using UnityEngine;

/// <summary>
/// 드래곤 보스 전용 SFX 관리 스크립트.
/// DragonBossAI와 같은 GameObject에 붙이거나 자식 오브젝트에 붙여 사용.
/// Animator 이벤트에서 직접 호출하거나, DragonBossAI에서 GetComponent로 참조하여 호출.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class DragonBossSFX : MonoBehaviour
{
    // --- 오디오 소스 ---
    // _audioSource: 단발성 효과음 (물기, 할퀴기, 날갯짓 등)
    // _loopSource: 루프 효과음 전용 (브레스 루프)
    private AudioSource _audioSource;
    private AudioSource _loopSource;
    private AudioSource _wingFlapSource;

    [Header("--- 포효 ---")]
    public AudioClip roarClip;              // 전투 시작 / 2페이즈 진입 포효

    [Header("--- 지상 공격 ---")]
    public AudioClip[] biteClips;           // 물기 (여러 변형)
    public AudioClip[] clawSwingClips;      // 할퀴기 스윙 (날카로운 소리)
    public AudioClip clawImpactClip;        // 할퀴기 착지 충격음
    public AudioClip backAwayClip;          // 뒤로 물러날 때 발소리/날갯짓

    [Header("--- 브레스 ---")]
    public AudioClip breathStartClip;       // 브레스 시작 (입에서 화염 나오기 직전 차징음)
    public AudioClip breathLoopClip;        // 브레스 루프 (지속 재생)
    public AudioClip breathEndClip;         // 브레스 종료음

    [Header("--- 비행 ---")]
    public AudioClip takeOffClip;           // 이륙 날갯짓
    public AudioClip wingFlapLoopClip;      // 공중 비행 날갯짓 루프 (선택)
    public AudioClip landClip;              // 착지 충격음

    [Header("--- 공중 공격 ---")]
    public AudioClip glideDiveClip;         // 활강 돌진 시 바람 가르는 소리
    public AudioClip spikeSpawnClip;        // 악몽의 쐐기 생성음

    [Header("--- 그로기 / 사망 ---")]
    public AudioClip groggyClip;            // 그로기 쓰러짐
    public AudioClip groggyImpactClip;      // 공중 그로기 낙하 충격
    public AudioClip deathClip;             // 사망

    [Header("--- 피격 ---")]
    public AudioClip[] hitClips;            // 피격음 (여러 변형)

    [Header("--- 볼륨 조절 ---")]
    [Range(0f, 1f)] public float masterVolume = 1.0f;
    [Range(0f, 1f)] public float loopVolume = 0.8f;
    [Range(0f, 0.15f)] public float pitchVariance = 0.07f;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _audioSource.spatialBlend = 1.0f;   // 3D 사운드
        _audioSource.minDistance = 3.0f;
        _audioSource.maxDistance = 40.0f;
        _audioSource.rolloffMode = AudioRolloffMode.Linear;
        _audioSource.playOnAwake = false;

        // 브레스 루프 전용 소스 추가
        _loopSource = gameObject.AddComponent<AudioSource>();
        _loopSource.spatialBlend = 1.0f;
        _loopSource.minDistance = 3.0f;
        _loopSource.maxDistance = 40.0f;
        _loopSource.rolloffMode = AudioRolloffMode.Linear;
        _loopSource.loop = true;
        _loopSource.playOnAwake = false;
        _loopSource.volume = loopVolume;

        _wingFlapSource = gameObject.AddComponent<AudioSource>();
        _wingFlapSource.spatialBlend = 1.0f;
        _wingFlapSource.minDistance = 3.0f;
        _wingFlapSource.maxDistance = 40.0f;
        _wingFlapSource.rolloffMode = AudioRolloffMode.Linear;
        _wingFlapSource.loop = true;
        _wingFlapSource.playOnAwake = false;
        _wingFlapSource.volume = loopVolume;
    }

    // ==========================================================
    // Animator 이벤트 수신용 public 메서드
    // ==========================================================

    // --- 포효 ---
    // doScream 애니메이션 이벤트에서 호출
    public void OnRoar()
    {
        PlayOneShot(roarClip);
    }

    // --- 지상 공격 ---
    // 물기 모션 입이 닫힐 때 호출
    public void OnBiteImpact()
    {
        PlayRandom(biteClips);
    }

    // 할퀴기 발톱 스윙 시 호출
    public void OnClawSwing()
    {
        PlayRandom(clawSwingClips);
    }

    // 할퀴기 돌진 착지 시 호출
    public void OnClawImpact()
    {
        PlayOneShot(clawImpactClip);
    }

    // BackAway 애니메이션 시작 시 호출
    public void OnBackAway()
    {
        PlayOneShot(backAwayClip);
    }

    // --- 브레스 ---
    // OnFireBreathVFX와 동일 타이밍 — 브레스 VFX 켤 때 같이 호출
    public void OnBreathStart()
    {
        PlayOneShot(breathStartClip, 0.9f);
        StartBreathLoop();
    }

    // EndFireBreathVFX와 동일 타이밍 — 브레스 끝날 때 호출
    public void OnBreathEnd()
    {
        StopBreathLoop();
        PlayOneShot(breathEndClip);
    }

    // --- 비행 ---
    // 이륙 날갯짓 프레임에서 호출
    public void OnTakeOff()
    {
        PlayOneShot(takeOffClip);
    }

    // 착지 발이 닿는 프레임에서 호출
    public void OnLand()
    {
        PlayOneShot(landClip, 1.0f, pitchVariance * 0.5f); // 착지는 피치 변화 적게
    }

    // --- 공중 공격 ---
    // 활강 시작 프레임에서 호출
    public void OnGlideDive()
    {
        PlayOneShot(glideDiveClip);
    }

    // DragonBossAI.SpawnSpikeOnGround에서 직접 호출
    public void OnSpikeSpawn()
    {
        PlayOneShot(spikeSpawnClip, 0.7f);
    }

    // --- 그로기 / 사망 ---
    // 그로기 쓰러지는 프레임에서 호출
    public void OnGroggy()
    {
        PlayOneShot(groggyClip);
    }

    // 공중 그로기 낙하 후 바닥 충돌 시 DragonBossAI에서 직접 호출
    public void OnGroggyGroundImpact()
    {
        PlayOneShot(groggyImpactClip, 1.0f, 0f);
    }

    // 사망 애니메이션 시작 프레임에서 호출
    public void OnDeath()
    {
        StopBreathLoop();
        PlayOneShot(deathClip, 1.0f, 0f);
    }

    // --- 피격 ---
    // EnemyStats/CharacterStats의 OnTakeDamage 이벤트에서 호출하거나
    // 피격 애니메이션 이벤트에서 호출
    public void OnHit()
    {
        PlayRandom(hitClips, 0.6f);
    }

    // ==========================================================
    // 브레스 루프 제어 (DragonBossAI에서도 직접 호출 가능)
    // ==========================================================
    public void StartBreathLoop()
    {
        if (breathLoopClip == null || _loopSource.isPlaying) return;
        _loopSource.clip = breathLoopClip;
        _loopSource.volume = loopVolume * masterVolume;
        _loopSource.Play();
    }

    public void StopBreathLoop()
    {
        if (_loopSource.isPlaying)
            _loopSource.Stop();
    }

    public void StartWingFlapLoop()
    {
        if (wingFlapLoopClip == null || _wingFlapSource.isPlaying) return;
        _wingFlapSource.clip = wingFlapLoopClip;
        _wingFlapSource.volume = loopVolume * masterVolume;
        _wingFlapSource.Play();
    }

    public void StopWingFlapLoop()
    {
        if (_wingFlapSource.isPlaying)
            _wingFlapSource.Stop();
    }

    // ==========================================================
    // 내부 헬퍼
    // ==========================================================
    private void PlayOneShot(AudioClip clip, float volume = 1.0f, float customPitchVariance = -1f)
    {
        if (clip == null) return;

        float variance = customPitchVariance >= 0f ? customPitchVariance : pitchVariance;
        _audioSource.pitch = 1.0f + Random.Range(-variance, variance);
        _audioSource.PlayOneShot(clip, volume * masterVolume);
    }

    private void PlayRandom(AudioClip[] clips, float volume = 1.0f)
    {
        if (clips == null || clips.Length == 0) return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip == null) return;

        _audioSource.pitch = 1.0f + Random.Range(-pitchVariance, pitchVariance);
        _audioSource.PlayOneShot(clip, volume * masterVolume);
    }
}
