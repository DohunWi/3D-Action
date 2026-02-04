using UnityEngine;
using System.Collections;

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance;

    private float _defaultFixedDeltaTime; // 원래 물리 주기 기억

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        _defaultFixedDeltaTime = Time.fixedDeltaTime; // 초기값(보통 0.02) 저장
    }

    // 외부에서 호출할 함수 (느려질 시간, 타임 스케일)
    // 예: DoSlowMotion(0.2f, 0.1f) -> 0.1배속으로 0.2초간 진행 (실제 시간 기준)
    public void DoSlowMotion(float duration, float scale)
    {
        StopAllCoroutines(); // 기존에 돌던 슬로우가 있으면 취소 (중복 방지)
        StartCoroutine(SlowMotionRoutine(duration, scale));
    }

    private IEnumerator SlowMotionRoutine(float duration, float scale)
    {
        // 1. 시간 느리게 설정
        Time.timeScale = scale;
        
        // ★ 중요: 물리 연산 주기도 같이 바꿔줘야 슬로우 중에도 움직임이 부드러움
        Time.fixedDeltaTime = _defaultFixedDeltaTime * scale;

        // 2. 현실 시간(Realtime)으로 대기
        // (Time.timeScale이 0에 가까우면 WaitForSeconds는 영원히 안 끝날 수 있음)
        yield return new WaitForSecondsRealtime(duration);

        // 3. 원래대로 복구
        Time.timeScale = 1.0f;
        Time.fixedDeltaTime = _defaultFixedDeltaTime;
    }
    
    // 팁: 완전히 멈췄다가 풀리는 '히트 스탑' (타격감 강조용)
    public void DoHitStop(float duration)
    {
        // 0.0f로 멈추면 아예 정지, 0.05f 정도면 초고속 카메라 느낌
        DoSlowMotion(duration, 0.0f); 
    }
}