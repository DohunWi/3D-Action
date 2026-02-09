using UnityEngine;
using System.Collections;

public class EliteEnemy : Enemy
{
    [Header("--- Elite Spec Settings ---")]
    public float eliteMaxHealth = 500f; // 일반 몹(100)의 8배
    public float elitePoise = 100f;     // 강인도 증가

    [Header("--- Elite Skill: Fire Breath ---")]
    public float fireBreathCooldown = 10.0f; 
    public float fireBreathRange = 6.0f;     
    public float rotationSpeedWhileBreathing = 1.0f; // 불 뿜을 때 회전 속도
    private bool _isBreathActive = false; // 파이어 브레스 플래그
    
    [Header("--- Fire Breath Visuals ---")]
    public GameObject fireBreathVFX; // 불 이펙트 (입 앞)
    public Transform mouthPosition;  // (선택) 발사 위치 보정용

    [Header("--- Enrage Settings (Phase 2) ---")]
    public float enrageHealthRatio = 0.4f; // 체력 40% 이하일 때 광폭화
    public float speedBuff = 1.3f;         // 이동 속도 증가 배율
    public ParticleSystem auraParticle;    // 붉은 오라 파티클

    // 내부 상태 변수
    private float _lastFireBreathTime; 
    private bool _isFireBreathing = false;
    private bool _isEnraged = false;

    protected override void Start()
    {
        base.Start(); // 부모(Enemy)의 기본 초기화 실행
        // 1. 엘리트 스펙 적용
        if (_stats != null)
        {
            _stats.maxHealth = eliteMaxHealth;
            _stats.currentHealth = eliteMaxHealth;
            _stats.maxPoise = elitePoise;
            
            // 체력 변경 이벤트 구독 (광폭화 체크용)
            _stats.OnHealthChanged += CheckEnrage;
            // 콤보 시퀀스 변경
            _comboSequence = new int[] { 3, 2, 0, 2, 1, 3 };
        }

        // 2. 쿨타임 초기화 (시작하자마자 쏘지 않게 5초 여유)
        _lastFireBreathTime = -fireBreathCooldown + 5.0f; 
        
        // 3. 덩치에 맞게 사거리 등 자동 보정
        _agent.stoppingDistance += 0.5f; 
        attackRange += 0.5f;

        // 4. 아우라 파티클 초기화 (꺼두기)
        if (auraParticle != null)
        {
            auraParticle.Stop();
        }
    }

    // ★ 부모(Enemy)가 "특수 공격 할 거 있어?" 라고 물어볼 때 실행
    protected override bool TrySpecialAttack()
    {
        // 이미 뿜고 있으면 중복 실행 방지
        if (_isFireBreathing) return true; 

        // 1. 쿨타임 체크
        if (Time.time < _lastFireBreathTime + fireBreathCooldown) return false;

        // 2. 거리 체크 (너무 멀면 안 쏨)
        float dist = Vector3.Distance(transform.position, _target.position);
        if (dist > fireBreathRange) return false;

        // 3. 조건 만족 시 불뿜기 시작!
        StartCoroutine(ProcessFireBreath());
        return true; // "나 특수 공격 했다!"고 알림 (평타 캔슬)
    }
    // 애니메이션 이벤트에서 호출할 함수 (불 켜기)
    public void OnFireBreathStart()
    {
        if(!_isFireBreathing) return;
        _isBreathActive = true; // 플래그 ON
        if (fireBreathVFX != null) 
        {
            fireBreathVFX.SetActive(true);
            // 오디오도 여기서 재생하면 입 벌릴 때 딱 소리 남
            // audioSource.PlayOneShot(fireSound); 
        }
    }

    // ★ 애니메이션 이벤트 or 코루틴 종료 시 호출할 함수 (불 끄기)
    public void OnFireBreathEnd()
    {
        _isBreathActive = false; 
        if (fireBreathVFX != null) 
        {
            fireBreathVFX.SetActive(false);
        }
    }
    private IEnumerator ProcessFireBreath()
    {
        void CleanupAndExit()
        {
            OnFireBreathEnd();
            _isFireBreathing = false;
            _lastFireBreathTime = Time.time;
        }
        _isFireBreathing = true;
        _isBreathActive = false; 

        ChangeState(EnemyState.Attack);
        _agent.isStopped = true;
        _agent.velocity = Vector3.zero;

        // 1. 애니메이션 재생 (이제 얘가 이벤트를 통해 OnFireBreathStart를 부를 겁니다)
        _animator.SetTrigger("doRoar"); 

        // 2. [준비 단계] 불 뿜기 시작할 때까지 대기
        // (안전장치: 2초가 지나도 이벤트가 안 불리면 강제 종료 -> 버그 방지)
        float safetyTimer = 0f;
        while (_isFireBreathing && !_isBreathActive && safetyTimer < 2.0f)
        {
            safetyTimer += Time.deltaTime;
            
            // 준비 동작 중에도 타겟팅을 하고 싶다면 여기서 LookAtTarget 호출
            LookAtTarget(5.0f); 

            // 피격 등으로 상태가 바뀌면 중단
            if (currentState != EnemyState.Attack)
            {
                CleanupAndExit();
                yield break;
            }
            
            
            yield return null;
        }

       // 3. [불 뿜는 단계] Start 이벤트 ~ End 이벤트 사이
        // ★ 시간(Timer)이 아니라, 이 변수가 true인 동안만 돕니다!
        while (_isBreathActive && _isFireBreathing)
        {
            // 불 뿜는 동안 천천히 회전
            // LookAtTarget(rotationSpeedWhileBreathing);

            // 상태 체크 (맞아서 끊기거나 죽으면 탈출)
            if (currentState != EnemyState.Attack)
            {
                CleanupAndExit();
                yield break;
            }

            yield return null;
        }
        // 4. [마무리 단계] End 이벤트 이후 ~ 애니메이션 완전 종료까지
        // (불은 꺼졌지만, 몬스터가 자세를 바로잡는 시간)
        
        // 애니메이션이 끝날 때까지 대기 (NormalizedTime >= 1.0f)
        // "Roar" 태그나 이름을 정확히 확인해야 함
        while (IsPlayingAnimation("Mutant_roar") && _isFireBreathing) 
        {
             if (currentState != EnemyState.Attack)
            {
                CleanupAndExit();
                yield break;
            }
             yield return null;
        }

        // 5. 복귀
        CleanupAndExit();
        ChangeState(EnemyState.Chase);
    }
    private bool IsPlayingAnimation(string stateName)
    {
        if (_animator.GetCurrentAnimatorStateInfo(0).IsName(stateName))
        {
            return _animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1.0f;
        }
        return false;
    }

    // 광폭화 체크 함수
    private void CheckEnrage(float current, float max)
    {
        if (_isEnraged) return;

        // 체력이 설정 비율 이하로 떨어지면 광폭화
        if (current <= max * enrageHealthRatio)
        {
            ActivateEnrage();
        }
    }

    private void ActivateEnrage()
    {
        _isEnraged = true;
        
        // 1. 아우라 파티클 켜기
        if (auraParticle != null) auraParticle.Play();

        // 2. 능력치 강화 (속도 증가, 쿨타임 감소)
        _agent.speed *= speedBuff; 
        attackCooldown /= speedBuff; 

        // 3. 강인도 즉시 회복 (마지막 기회)
        // if (_stats != null) _stats.ResetPoise(); // ResetPoise 함수가 있다면 사용

        Debug.Log($"⚠️ ELITE ENRAGED! Speed x{speedBuff}");
    }

    // 엘리트 사망 연출 (슬로우 모션)
    protected override void OnDie()
    {
        if (auraParticle != null) auraParticle.Stop(); // 아우라 끄기
        
        // 죽는 순간 임팩트 (시간 느리게)
        Time.timeScale = 0.5f; 
        Invoke("ResetTimeScale", 0.5f); // 0.5초(실제시간 1초) 뒤 복구

        base.OnDie(); // 부모의 사망 처리 실행
    }

    private void ResetTimeScale()
    {
        Time.timeScale = 1.0f;
    }

    // 회전 보조 함수 (속도 조절 가능)
    private void LookAtTarget(float speed)
    {
        if (_target == null) return;
        Vector3 dir = (_target.position - transform.position).normalized;
        dir.y = 0;
        if (dir != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * speed);
        }
    }
}