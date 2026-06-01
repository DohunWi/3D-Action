using UnityEngine;
using System.Collections;

public class EliteEnemy : Enemy
{
    [Header("--- Elite Spec Settings ---")]
    public float eliteMaxEgo = 500f; // 일반 몹(100)의 8배
    public float eliteComposure = 100f;     // 강인도 증가

    [Header("--- Elite Skill: Fire Breath ---")]
    public float fireBreathCooldown = 10.0f; 
    public float fireBreathRange = 6.0f;     
    public float rotationSpeedWhileBreathing = 1.0f; // 불 뿜을 때 회전 속도
    private bool _isBreathActive = false; // 파이어 브레스 플래그
    
    [Header("--- Fire Breath Visuals ---")]
    public GameObject fireBreathVFX; // 불 이펙트 (입 앞)
    public Transform mouthPosition;  // (선택) 발사 위치 보정용

    [Header("--- Enrage Settings (Phase 2) ---")]
    public float enrageEgoRatio = 0.4f; // 체력 40% 이하일 때 광폭화
    public float speedBuff = 1.3f;         // 이동 속도 증가 배율
    public ParticleSystem auraParticle;    // 붉은 오라 파티클

    [Header("Audio Clip")]
    public AudioClip breathSound;

    [Header("--- UI Settings ---")]
    public string bossName = "Mutant Overlord"; // 보스 이름
    // 씬에 있는 UI를 직접 연결하거나, 프리팹이면 Find로 찾음
    public BossEgoBar bossEgoBar;
    [Header("--- Door ---")]
    public FloorTeleporter doorScript;

    // 내부 상태 변수
    private float _lastFireBreathTime; 
    private bool _isFireBreathing = false;
    private bool _isEnraged = false;
    private bool _hasBossFightStarted = false;

    protected override void Start()
    {
        base.Start(); // 부모(Enemy)의 기본 초기화 실행
        // 1. 엘리트 스펙 적용
        if (_stats != null)
        {
            _stats.maxEgo = eliteMaxEgo;
            _stats.currentEgo = eliteMaxEgo;
            _stats.maxComposure = eliteComposure;
            
            // 체력 변경 이벤트 구독 (광폭화 체크용)
            _stats.OnEgoChanged += CheckEnrage;
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
        // ★ [추가] 보스 체력바 초기화 및 활성화
        if (bossEgoBar == null)
        {
            // 만약 인스펙터 연결 안 했으면 찾아서라도 연결
            //bossHealthBar = FindObjectOfType<BossHealthBar>();
        }
    }

    public override void ChangeState(EnemyState newState)
    {
        // 1. 부모의 원래 기능(애니메이션, 변수 초기화 등) 먼저 실행
        base.ChangeState(newState);

        // 2. 보스전 시작 체크
        // 아직 전투 시작 안 함 AND (추격하거나, 공격하거나, 맞았거나)
        // 1. 전투 시작 체크 (기존 로직)
        if (!_hasBossFightStarted)
        {
            // 추격, 공격, 피격 상태가 되면 전투 시작으로 간주
            if (newState == EnemyState.Chase || newState == EnemyState.Attack || newState == EnemyState.Hit)
            {
                StartBossFight();
            }
        }
        // 2. ★ 전투 종료 체크 (어그로 풀림)
        else 
        {
            // 이미 전투 중이었는데, '순찰(Patrol)'이나 '대기(Idle)'로 상태가 변했다면?
            // (= 플레이어가 멀어져서 추격을 포기하고 돌아감)
            if (newState == EnemyState.Patrol)
            {
                EndBossFight();
            }
        }
    }
    // 보스전 시작 함수
    private void StartBossFight()
    {
        _hasBossFightStarted = true; // 이제 중복 실행 안 됨
        Debug.Log("⚔️ BOSS FIGHT STARTED! ⚔️");

        // 1. UI 켜기 (페이드 인)
        if (bossEgoBar != null && _stats != null)
        {
            bossEgoBar.Initialize(_stats, bossName);
        }

        // 2. (선택) 보스전 배경음악(BGM)으로 교체
        // SoundManager.Instance.PlayBGM(bossBattleMusic);

        // 3. (선택) 포효 한번 지르기
        if (!_isFireBreathing) // 이미 공격 중이 아니라면
        {
            _animator.SetTrigger("doRoar"); // 등장 포효!
            // ChangeState(EnemyState.Attack); // 강제로 공격 상태로 전환할 수도 있음
        }
    }
    private void EndBossFight()
    {
        _hasBossFightStarted = false; // 플래그 리셋 (다시 마주치면 UI 띄우기 위해)
        Debug.Log("💤 BOSS FIGHT ENDED (Player ran away)");

        // 1. UI 숨기기 (페이드 아웃)
        if (bossEgoBar != null)
        {
            bossEgoBar.Hide(); 
        }

        // 2. (선택) 보스 체력 리셋?
        // 다크소울처럼 도망가면 보스 체력을 다시 꽉 채우고 싶다면 주석 해제
        if (_stats != null) 
        {
            _stats.currentEgo = _stats.maxEgo;
            // UI도 다시 꽉 찬 상태로 갱신해줘야 함 (Initialize 재호출 등)
        }

        // 3. (선택) BGM 끄기 또는 원래 배경음으로 복귀
        // SoundManager.Instance.StopBGM(); 
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
    public void OnFireBreathSFX()
    {
        //Sound
        SoundManager.Instance.PlaySFX(breathSound, transform.position, 1.0f);
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
        if (current <= max * enrageEgoRatio)
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
        if (auraParticle != null) auraParticle.Stop();

        Time.timeScale = 0.5f;
        Invoke("ResetTimeScale", 0.5f);
        doorScript.isLocked = false;
        if (GameManager.Instance != null) GameManager.Instance.eliteDefeated = true;

        // 세계가 반응하는 월드 내레이션 (unscaledDeltaTime 기반 — 슬로우모션 중에도 동작)
        AreaNameUI.Instance?.Show("The ward has broken.\nThe Tower remembers who you are.");

        base.OnDie();
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