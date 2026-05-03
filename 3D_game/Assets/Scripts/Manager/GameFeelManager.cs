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

    private ChromaticAberration _chromaticAberration;
    private LensDistortion _lensDistortion;
    private float _defaultFOV = 40f;

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
            yield return new WaitForSeconds(interval);
        }
    }

    public void DoParryEffect()
    {
        StopAllCoroutines();
        StartCoroutine(ParryFeelRoutine());
    }

    private IEnumerator ParryFeelRoutine()
    {
        // 1. 현재 활성화된(Live) 가상 카메라 찾기
        // (ICinemachineCamera 인터페이스로 반환되므로 형변환 필요할 수 있음)
        var liveCam = cinemachineBrain.ActiveVirtualCamera as CinemachineCamera; 

        float currentFOV = _defaultFOV; // 기본값
        
        // 카메라가 있다면 현재 FOV 가져오기
        if (liveCam != null)
        {
            currentFOV = liveCam.Lens.FieldOfView; 
        }

        // ====================================================
        // [임팩트] 쾅!
        // ====================================================
        if (_chromaticAberration != null) _chromaticAberration.intensity.value = 1.0f;
        if (_lensDistortion != null) _lensDistortion.intensity.value = -0.5f;

        // ★ 현재 카메라 줌인
        if (liveCam != null) liveCam.Lens.FieldOfView = currentFOV - 15f;

        Time.timeScale = 0.0f;
        yield return new WaitForSecondsRealtime(0.05f); 

        // ====================================================
        // [여운] 복구
        // ====================================================
        Time.timeScale = 0.2f;

        float duration = 0.3f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;

            if (_chromaticAberration != null) 
                _chromaticAberration.intensity.value = Mathf.Lerp(1.0f, 0f, t);
            
            if (_lensDistortion != null) 
                _lensDistortion.intensity.value = Mathf.Lerp(-0.5f, 0f, t);

            // ★ 현재 카메라 FOV 복구
            if (liveCam != null)
                liveCam.Lens.FieldOfView = Mathf.Lerp(currentFOV - 15f, currentFOV, t);

            yield return null;
        }

        // ====================================================
        // [완료] 깔끔하게 정리
        // ====================================================
        Time.timeScale = 1.0f;
        if (_chromaticAberration != null) _chromaticAberration.intensity.value = 0f;
        if (_lensDistortion != null) _lensDistortion.intensity.value = 0f;
        
        // 혹시 그 사이에 카메라가 바뀌었을 수도 있으니 다시 확인 후 복구
        if (liveCam != null) liveCam.Lens.FieldOfView = currentFOV;
    }
}