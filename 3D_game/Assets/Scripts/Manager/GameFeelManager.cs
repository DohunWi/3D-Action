using UnityEngine;
using UnityEngine.Rendering; 
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine; 
using System.Collections;

public class GameFeelManager : MonoBehaviour
{
    public static GameFeelManager Instance;

    [Header("Components")]
    public Volume globalVolume;
    public CinemachineBrain cinemachineBrain;

    [Header("Camera Shake")]
    // Virtual Camera GameObject에 붙인 CinemachineImpulseSource를 여기에 연결
    public CinemachineImpulseSource impulseSource;

    [Header("Parry Feel")]
    [Tooltip("히트스톱(완전 정지) 길이 — real time 초")]
    [SerializeField] private float _parryFreezeDuration = 0.04f;
    [Tooltip("프리즈 직후 슬로우모 배속 (0~1)")]
    [SerializeField] private float _parrySlowScale = 0.2f;
    [Tooltip("슬로우모 지속 시간 — real time 초")]
    [SerializeField] private float _parrySlowDuration = 0.1f;
    [Tooltip("임팩트 시 줌인 양 (FOV 감소값)")]
    [SerializeField] private float _parryZoomIn = 15f;

    private ChromaticAberration _chromaticAberration;
    private LensDistortion _lensDistortion;
    private float _defaultFOV = 40f;
    private Coroutine _parryCoroutine;

    private void Awake()
    {
        if (Instance == null) Instance = this;

        if (globalVolume != null && globalVolume.profile != null)
        {
            globalVolume.profile.TryGet(out _chromaticAberration);
            globalVolume.profile.TryGet(out _lensDistortion);
        }

        if (cinemachineBrain == null)
            cinemachineBrain = Camera.main.GetComponent<CinemachineBrain>();
    }

    // force: 흔들림 세기 / direction: ImpulseSource DefaultVelocity 방향 배율
    public void ShakeCamera(float force, Vector3 direction)
    {
        if (impulseSource == null) return;
        impulseSource.GenerateImpulse(direction.normalized * force);
    }

    // 브레스처럼 지속되는 잔흔들림 — 시작/중지 쌍으로 사용
    private Coroutine _loopShakeCoroutine;

    public void StartLoopShake(float force = 0.3f, float interval = 0.12f)
    {
        StopLoopShake();
        _loopShakeCoroutine = StartCoroutine(LoopShakeRoutine(force, interval));
    }

    public void StopLoopShake()
    {
        if (_loopShakeCoroutine != null)
        {
            StopCoroutine(_loopShakeCoroutine);
            _loopShakeCoroutine = null;
        }
    }

    private IEnumerator LoopShakeRoutine(float force, float interval)
    {
        var wait = new WaitForSeconds(interval);
        while (true)
        {
            if (impulseSource != null)
            {
                // 매번 방향을 랜덤하게 줘서 부르르 떨리는 느낌
                Vector3 randomDir = new Vector3(
                    Random.Range(-1f, 1f),
                    Random.Range(-0.3f, 0.3f),
                    Random.Range(-1f, 1f)
                );
                impulseSource.GenerateImpulse(randomDir.normalized * force);
            }
            yield return wait;
        }
    }

    public void DoParryEffect()
    {
        // 전역 StopAllCoroutines 대신 패링 전용 핸들만 정지 → 카메라 셰이크 등 다른 코루틴 보호.
        // 이전 패링 루틴이 진행 중이면 Dispose되며 finally가 실행돼 상태가 원복된 뒤 새로 시작된다.
        if (_parryCoroutine != null) StopCoroutine(_parryCoroutine);
        _parryCoroutine = StartCoroutine(ParryFeelRoutine());
    }

    private IEnumerator ParryFeelRoutine()
    {
        // 현재 활성화된(Live) 가상 카메라 찾기
        var liveCam = cinemachineBrain != null
            ? cinemachineBrain.ActiveVirtualCamera as CinemachineCamera
            : null;
        float currentFOV = liveCam != null ? liveCam.Lens.FieldOfView : _defaultFOV;

        // try/finally: 정상 종료뿐 아니라 StopCoroutine·오브젝트 파괴로 코루틴이 Dispose될 때도
        // finally가 실행되어 timeScale/FOV/post-processing을 반드시 원복 → 슬로우 상태로 멈추는 소프트락 방지.
        try
        {
            // [임팩트] 쾅! — 색수차·렌즈왜곡·줌인 + 히트스톱(완전 정지)
            if (_chromaticAberration != null) _chromaticAberration.intensity.value = 1.0f;
            if (_lensDistortion != null) _lensDistortion.intensity.value = -0.5f;
            if (liveCam != null) liveCam.Lens.FieldOfView = currentFOV - _parryZoomIn;

            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(_parryFreezeDuration);

            // [여운] 짧은 슬로우모로 풀면서 이펙트 복구
            Time.timeScale = _parrySlowScale;

            float elapsed = 0f;
            while (elapsed < _parrySlowDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / _parrySlowDuration;

                if (_chromaticAberration != null)
                    _chromaticAberration.intensity.value = Mathf.Lerp(1.0f, 0f, t);
                if (_lensDistortion != null)
                    _lensDistortion.intensity.value = Mathf.Lerp(-0.5f, 0f, t);
                if (liveCam != null)
                    liveCam.Lens.FieldOfView = Mathf.Lerp(currentFOV - _parryZoomIn, currentFOV, t);

                yield return null;
            }
        }
        finally
        {
            // [복구] 어떤 경로로 종료되든 상태 원복
            Time.timeScale = 1.0f;
            if (_chromaticAberration != null) _chromaticAberration.intensity.value = 0f;
            if (_lensDistortion != null) _lensDistortion.intensity.value = 0f;
            if (liveCam != null) liveCam.Lens.FieldOfView = currentFOV;
            _parryCoroutine = null;
        }
    }
}