using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public enum DragonState
{
    // 지상 상태
    GroundIdle,
    GroundChase,
    GroundAttack,
    
    // 지상 특수 행동
    BackAway,     // 너무 가까울 때 뒤로 물러나는 애니메이션 재생 후 공격

    // 비행 전환 상태
    TakeOff,      // 이륙 (이동 불가, 애니메이션만 재생)
    Land,         // 착륙
    
    // 공중 상태
    FlyHover,     // 공중 체공 및 플레이어 추적 (Z축, Y축 이동)
    FlyAttack,    // 공중 브레스 폭격
    
    // 특수 상태
    Groggy,       // 특정 데미지 누적 시 추락하여 무방비
    Die
}

public class DragonBossAI : MonoBehaviour
{
    [Header("--- State ---")]
    public DragonState currentState = DragonState.GroundIdle;

    [Header("--- Components ---")]
    public Animator _animator;
    public NavMeshAgent _agent;
    public CharacterStats _stats; // 보스 체력/강인도 관리용
    public Transform _target;     // 플레이어

    [Header("--- UI & Specs ---")]
    public string bossName = "The Oblivion Dragon";
    public BossEgoBar bossEgoBar;

    [Header("--- Phase & Flight Loop ---")]
    public float phaseTwoEgoRatio = 0.5f; // 체력 50% 이하 시 2페이즈 진입
    public float flightCooldown = 20.0f;  // 지상 패턴 유지 시간 (2페이즈 전용)
    // 공중 패턴 제어용 변수
    public float flightDuration = 15.0f;  // 공중 유지 시간 (이 시간이 지나면 착륙)
    public float airAttackCooldown = 4.0f;// 공중 공격 사이의 쿨타임
    public bool isPhaseTwo = false;
    private float lastFlightTime;         // 마지막으로 착륙한 시간 (지상 쿨타임용)
    private float currentFlightStartTime; // 이륙 완료한 시간 (공중 지속시간 체크용)
    private float lastAirAttackTime;      // 마지막 공중 공격 끝난 시간
    
    [Header("--- Flight Settings ---")]
    public float flySpeed = 15.0f;         // 비행 이동 속도
    public float flySmoothTime = 1.5f;    // 목적지에 도달하는 데 걸리는 시간 (클수록 무겁게 움직임)
    private Vector3 targetFlyPosition;    // 비행 시 목표 위치
    private Vector3 currentFlyVelocity;   // SmoothDamp가 내부적으로 사용하는 현재 속도
    private float currentFlyAltitude; //  이륙 후 유지할 실제 높이
    [Header("--- Ground Attack Settings ---")]
    public float groundAttackCooldown = 3.0f; // 공격 사이의 대기 시간
    private float lastGroundAttackTime;       // 마지막 지상 공격이 끝난 시점
    public float walkSpeed = 4.0f;            // 쿨타임 중 걷기 속도 (blend tree walk 기준값)
    public float runSpeed = 8.0f;             // 공격 접근 / 원거리 추격 속도 (blend tree run 기준값)
    private float _walkSpeed;                 // 런타임 walkSpeed (2페이즈 배율 반영)
    private float _runSpeed;                  // 런타임 runSpeed  (2페이즈 배율 반영)

    [Header("--- Body Dive Attack Settings ---")]
    private float glideSpeed ;      // 돌진 속도
    private Vector3 glideStartPosition;   // 돌진 시작 위치
    private Vector3 glideEndPosition;     // 돌진 도착 위치
    private float glideTimer;             // 돌진 진행 시간
    public float glideDuration = 1.2f;          // 돌진에 걸리는 총 시간
    public float diveDipDepth = 4.0f;       // U자로 파고들 깊이 (숫자를 키울수록 푹 꺼짐)
    private bool isGliding = false;
    private bool isFlying = false;  // 공중 상태 여부 (TakeOff ~ Land 전체 구간)

    [Header("--- Hitbox References ---")]
    public Weapon biteWeapon;   // 물기용
    public Weapon clawWeapon;     // 할퀴기용
    public Weapon bodyDive;     // 활강 돌진용

    [Header("--- Hurtbox References ---")]
    public List<BossHurtbox> hurtboxes = new List<BossHurtbox>();

    [Header("--- Combat Settings ---")]
    public float groundAttackRange = 5.0f;
    public float breathCooldown = 15.0f;
    private float lastBreathTime;
    [Header("--- Combat Ranges ---")]
    public float tooCloseRange = 3.5f;   // 이 거리 이하면 뒤로 물러남 (물기 모션 허공 방지)
    public float biteRange = 4.0f;       // 깨물기 사거리 (초근접)
    public float clawRange = 8.0f;       // 돌진 할퀴기 사거리 (조금 떨어졌을 때)
    public float breathRange = 12.0f;    // 지상 브레스 사거리 (중거리)
    public float idleRange = 5.0f;       // 쿨타임 중 유지할 거리 (낮을수록 플레이어에게 붙음)

    [Header("--- Fire Breath Visuals ---")]
    public GameObject fireBreathVFX; // 불 이펙트 (입 앞)
    public Transform mouthPosition;  // (선택) 발사 위치 보정용
    [Header("--- Nightmare Spike Settings ---")]
    public GameObject spikePrefab;
    public float spikeSpawnInterval = 2.0f; // 2초마다 하나씩 생성
    public float spikeYOffset = 1.0f; // ★ 추가: 바닥으로부터 생성될 높이 오프셋
    private float _lastSpikeSpawnTime;
    
    // attackIndex 매핑
    // 0: Basic (물기), 1: Claw (돌진 할퀴기), 2: Flame (지상 브레스)
    // 3: Fly Flame (공중 브레스), 4: Fly Glide (공중 활강 덮치기)
    private int currentAttackIndex = 0;
    private bool isBossFightStarted = false;

    // --- 콤보 시스템 ---
    private const int COMBO_BACKAWAY = -1;    // 콤보 큐에서 BackAway를 나타내는 마커
    private Queue<int> _comboQueue = new Queue<int>();
    private bool _isInCombo = false;
    private bool _backAwayIsRecovery = false; // true면 공격 없이 GroundChase로 복귀 (거리 확보용)

    // --- 공격별 후딜 (Recovery) ---
    [Header("--- Per-Attack Recovery ---")]
    public float biteRecovery = 1.5f;         // 물기: 빠른 회복
    public float clawRecovery = 2.5f;         // 할퀴기: 중간
    public float breathRecovery = 4.0f;       // 브레스: 긴 회복 (플레이어 징벌 기회)
    public float comboEndRecovery = 1.0f;     // 콤보 마지막 타에 추가되는 쿨타임
    private float _currentCooldown;

    // 1. 애니메이션 파라미터 해시값 (성능 최적화)
    private static readonly int AnimID_Speed = Animator.StringToHash("speed");
    private static readonly int AnimID_FlySpeed = Animator.StringToHash("flySpeed");
    private static readonly int AnimID_DoBackAway = Animator.StringToHash("doBackAway");
    private void Awake()
    {
        if (_agent == null) _agent = GetComponent<NavMeshAgent>();
        if (_animator == null) _animator = GetComponent<Animator>();
        if (_stats == null) _stats = GetComponent<CharacterStats>();
    }

    private void Start()
    {
        if (_target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) _target = player.transform;
        }

        // 보스 스탯 초기화
        if (_stats != null)
        {
            _stats.maxEgo = 1000f;
            _stats.currentEgo = 1000f;
            _stats.OnDeath += OnDie;
            // ★ 체력이 변할 때마다 페이즈 체크 함수 실행
            _stats.OnEgoChanged += CheckPhaseTwo;
            _stats.OnComposureBroken += GoToGroggyState; // 강인도 파괴 시 실행될 함수 연결
        }

        lastBreathTime = -breathCooldown;
        lastFlightTime = Time.time; // 초기화
        _currentCooldown = groundAttackCooldown; // 첫 공격은 기본 쿨타임 사용
        _walkSpeed = walkSpeed;
        _runSpeed  = runSpeed;
        _agent.speed = _runSpeed;
    }

    private void Update()
    {
        if (currentState == DragonState.Die) return;
        if (_target == null) return;

        // 상태별 지속 업데이트 로직
        switch (currentState)
        {
            case DragonState.GroundIdle:
                CheckStartCombat();
                break;
            case DragonState.BackAway:
                UpdateBackAway();
                break;
            case DragonState.GroundChase:
                UpdateGroundChase();
                break;
            case DragonState.GroundAttack:
                LookAtTarget(2.0f);
                break;
            case DragonState.FlyHover:
                UpdateFlyHover();
                break;
            case DragonState.FlyAttack:
                UpdateFlyAttack(); 
                break;
        }
    }

    // ==========================================================
    // 상태 전환 (State Machine)
    // ==========================================================
    public void ChangeState(DragonState newState)
    {
        if (currentState == DragonState.Die) return;
        if (currentState == newState) return;

        // 1. 상태 [종료] 시 처리
        switch (currentState)
        {
            case DragonState.GroundChase:
                _agent.isStopped = true;
                break;
            case DragonState.FlyHover:
                // 공중 체공 종료
            case DragonState.Groggy:
                _animator.SetTrigger("doRecover");
                break;
        }

        currentState = newState;

        // 2. 상태 [진입] 시 처리
        switch (currentState)
        {
            case DragonState.GroundChase:
                _agent.enabled = true; // 지상 이동 활성화
                _agent.isStopped = false;
                break;

            case DragonState.BackAway:
                _agent.enabled = false;
                _animator.SetFloat(AnimID_Speed, 0f);
                _animator.SetTrigger(AnimID_DoBackAway);
                break;

            case DragonState.GroundAttack:
                if (_agent.enabled) _agent.isStopped = true;
                _animator.SetFloat(AnimID_Speed, 0f);
                // 애니메이션 파라미터 전달
                _animator.SetInteger("attackIndex", currentAttackIndex);
                _animator.SetTrigger("doAttack");
                break;

            case DragonState.TakeOff:
                // ★ 비행 시작: NavMeshAgent를 꺼야 Z축(공중) 이동이 가능해짐
                isFlying = true;
                _agent.enabled = false;
                _animator.SetFloat(AnimID_Speed, 0f);
                _animator.SetTrigger("doTakeOff");
                break;

            case DragonState.FlyHover:
                _animator.SetBool("isFlying", true);
                break;

            case DragonState.FlyAttack:
                _animator.SetInteger("attackIndex", currentAttackIndex);
                _animator.SetTrigger("doFlyAttack");
                _animator.SetFloat(AnimID_FlySpeed, 0f);
                _animator.applyRootMotion = false;
                // ★ 3번(활강) 패턴일 경우 돌진 목표 좌표 계산
                if (currentAttackIndex == 3)
                {
                    isGliding = true;
                    glideTimer = 0f;
                    
                    // 시작점은 현재 공중 위치
                    glideStartPosition = transform.position;
                    
                    // 도착점은 플레이어를 관통하여 15m 뒤에 있는 '공중' 위치
                    Vector3 dirToPlayer = (_target.position - transform.position);
                    dirToPlayer.y = 0; 
                    dirToPlayer.Normalize();
                    
                    glideEndPosition = _target.position + (dirToPlayer * 10f);
                    glideEndPosition.y = currentFlyAltitude; // 시작점과 동일한 높이로 설정
                    
                    // 도달 시간 계산 (거리 / 속도)
                    float dist = Vector2.Distance(
                        new Vector2(glideStartPosition.x, glideStartPosition.z), 
                        new Vector2(glideEndPosition.x, glideEndPosition.z)
                    );
                    glideSpeed = dist / glideDuration;
                }
                else
                {
                    isGliding = false; // 공중 브레스(4번)는 제자리에서 쏨
                }
                break;

            case DragonState.Land:
                isFlying = false;
                _animator.SetBool("isFlying", false);
                _animator.SetTrigger("doLand");
                break;

            case DragonState.Groggy:
                _comboQueue.Clear();
                _isInCombo = false;
                _backAwayIsRecovery = false;
                _agent.enabled = false;
                fireBreathVFX.SetActive(false);
                // 만약 비행 중에 그로기가 걸렸다면 바닥으로 추락
                if (isFlying)
                {
                    isFlying = false;
                    isGliding = false;
                    StopAllCoroutines(); // 기존 비행/공격 루틴 중단
                    // 바닥 충돌 시 연출
                    _animator.SetTrigger("doLandGroggy"); // 바닥에 처박히는 전용 애니메이션
                    _animator.SetBool("isFlying", false);
                    StartCoroutine(FallToGround());
                }
                else
                {
                    _animator.SetTrigger("doGroggy");
                    // 지상 그로기일 경우 단순히 일정 시간 후 회복
                    StartCoroutine(RecoverFromGroggy());
                }
                break;
        }
    }

    // ==========================================================
    // 지상 로직 (Ground Logic)
    // ==========================================================
    private void CheckStartCombat()
    {
        float dist = Vector3.Distance(transform.position, _target.position);
        if (dist < 20.0f && !isBossFightStarted)
        {
            isBossFightStarted = true;
            if (bossEgoBar != null) bossEgoBar.Initialize(_stats, bossName);
            
            // 전투 시작 시 포효 연출 후 추격
            _animator.SetTrigger("doScream");
            Invoke(nameof(StartChase), 3.0f);
        }
    }

    private void StartChase() => ChangeState(DragonState.GroundChase);

    private void UpdateGroundChase()
    {
        if (!_agent.enabled) return;

        float dist = Vector3.Distance(transform.position, _target.position);

        // ★ 1. 2페이즈 & 비행 쿨타임 도달 시 즉시 이륙 (거리 상관없음)
        if (isPhaseTwo && Time.time >= lastFlightTime + flightCooldown)
        {
            ChangeState(DragonState.TakeOff);
            return;
        }

        // 2. 너무 가까우면 BackAway 상태로 전환 — 애니메이션으로 물러난 뒤 물기 공격
        if (dist < tooCloseRange)
        {
            ChangeState(DragonState.BackAway);
            return;
        }

        // 3. 공격 판단 조건 (쿨타임 및 사거리 확인)
        bool isAttackAvailable = Time.time >= lastGroundAttackTime + _currentCooldown;

        if (isAttackAvailable && dist <= breathRange)
        {
            if (TrySelectGroundPattern(dist))
            {
                ChangeState(DragonState.GroundAttack);
                return;
            }
        }

        // 4. 이동 로직 분기: 쿨타임 중 천천히 스토킹 / 쿨타임 끝나면 빠르게 돌진
        if (!isAttackAvailable)
        {
            // 쿨타임 중 이동 — 멀리 있으면 run으로 추격, idleRange 근처면 walk로 스토킹
            _agent.stoppingDistance = idleRange;

            if (dist > idleRange + 0.5f)
            {
                // breathRange 이상 멀어지면 run으로 추격, 그 이하면 walk 스토킹
                _agent.speed = dist > breathRange ? _runSpeed : _walkSpeed;
                _agent.isStopped = false;
                _agent.SetDestination(_target.position);
            }
            else
            {
                _agent.speed = _walkSpeed;
                _agent.isStopped = true;
            }
            _animator.SetFloat(AnimID_Speed, _agent.desiredVelocity.magnitude, 0.15f, Time.deltaTime);
            LookAtTarget(3.0f);
        }
        else
        {
            // 공격 가능 — 달리기 속도로 빠르게 접근
            _agent.speed = _runSpeed;
            _agent.stoppingDistance = biteRange - 1.0f;

            if (dist <= _agent.stoppingDistance + 0.5f)
            {
                _agent.isStopped = true;
                _animator.SetFloat(AnimID_Speed, 0f, 0.2f, Time.deltaTime);
                LookAtTarget(6.0f);
            }
            else
            {
                _agent.isStopped = false;
                _agent.SetDestination(_target.position);
                _animator.SetFloat(AnimID_Speed, _agent.desiredVelocity.magnitude, 0.1f, Time.deltaTime);
            }
        }
    }

    // 뒤로 물러나기 — 코드로 이동
    private void UpdateBackAway()
    {
        LookAtTarget(3.0f);
        // 코드로 직접 드래곤의 중심축을 뒤로 밀어냄
        transform.Translate(Vector3.back * 10.0f * Time.deltaTime, Space.Self);
    }

    // --- 지상 공격 패턴 선택 (가중치 랜덤 + 콤보) ---
    private bool TrySelectGroundPattern(float dist)
    {
        if (Time.time < lastGroundAttackTime + _currentCooldown) return false;

        _comboQueue.Clear();
        _isInCombo = false;

        bool breathReady = Time.time >= lastBreathTime + breathCooldown;
        float roll = Random.value;
        bool doCombo = Random.value < (isPhaseTwo ? 0.55f : 0.3f);

        // --- 초근접 (≤ biteRange) ---
        if (dist <= biteRange)
        {
            if (doCombo)
            {
                if (isPhaseTwo && roll < 0.25f)
                {
                    // 2페이즈 풀콤보: 물기 → 물기 → BackAway → 브레스
                    if (breathReady)
                    {
                        enqueueCombo(0, 0, COMBO_BACKAWAY, 2);
                        lastBreathTime = Time.time;
                    }
                    else
                        enqueueCombo(0, 0, 1);           // 브레스 쿨이면 3연타
                }
                else if (breathReady && roll < 0.45f)
                {
                    enqueueCombo(0, COMBO_BACKAWAY, 2);   // 물기 → BackAway → 브레스
                    lastBreathTime = Time.time;
                }
                else if (roll < 0.7f)
                    enqueueCombo(0, 0);                   // 물기 → 물기
                else
                    enqueueCombo(0, 1);                   // 물기 → 할퀴기 (돌진 복귀)
            }
            else
            {
                currentAttackIndex = roll < 0.75f ? 0 : 1;
            }
        }
        // --- 중거리 (biteRange ~ clawRange): 할퀴기 주력 ---
        else if (dist <= clawRange)
        {
            if (doCombo)
            {
                if (breathReady && roll < 0.35f)
                {
                    enqueueCombo(1, 2);                   // 할퀴기 → 브레스 (돌진 복귀 후 화염)
                    lastBreathTime = Time.time;
                }
                else
                    enqueueCombo(1, 1);                   // 할퀴기 → 할퀴기 (연속 돌진)
            }
            else
            {
                if (breathReady && roll < 0.25f)
                {
                    currentAttackIndex = 2;
                    lastBreathTime = Time.time;
                }
                else
                    currentAttackIndex = 1;
            }
        }
        // --- 원거리 (clawRange ~ breathRange) ---
        else if (dist <= breathRange)
        {
            if (breathReady)
            {
                currentAttackIndex = 2;
                lastBreathTime = Time.time;
            }
            else
                currentAttackIndex = 1;               // 브레스 쿨이면 할퀴기로 접근
        }
        else
        {
            return false;
        }

        // 콤보 큐가 채워졌으면 첫 번째 공격 꺼내기
        if (_comboQueue.Count > 0)
        {
            currentAttackIndex = _comboQueue.Dequeue();
            _isInCombo = true;
        }

        return true;
    }

    private void enqueueCombo(params int[] attacks)
    {
        foreach (int a in attacks)
            _comboQueue.Enqueue(a);
    }

    private float getAttackCooldown(int attackIdx)
    {
        switch (attackIdx)
        {
            case 0:  return biteRecovery;
            case 1:  return clawRecovery;
            case 2:  return breathRecovery;
            default: return groundAttackCooldown;
        }
    }

    // ==========================================================
    // 공중 로직 (Air Logic)
    // ==========================================================
    private void UpdateFlyHover()
    {
        // 공중 체공 시간이 다 끝났는지 확인 -> 끝났으면 무조건 착륙
        if (Time.time >= currentFlightStartTime + flightDuration)
        {
            ChangeState(DragonState.Land);
            return;
        }

        // 1. 드래곤 -> 플레이어 향하는 수평 방향 구하기
        Vector3 dirToPlayer = (_target.position - transform.position);
        dirToPlayer.y = 0; // 높이는 무시 (X, Z 평면만 계산)
        dirToPlayer.Normalize(); // 방향만 남기고 길이를 1로 만듦
        // 2. 플레이어 위치에서 '드래곤이 있는 쪽'으로 5m 물러난 오프셋(거리) 설정
        float hoverDistance = 15.0f;
        Vector3 hoverOffset = -dirToPlayer * hoverDistance;

        // 3. 목표 위치 = 플레이어 위치 + 오프셋 (높이는 이륙 높이 유지)
        targetFlyPosition = new Vector3(
            _target.position.x + hoverOffset.x, 
            currentFlyAltitude, 
            _target.position.z + hoverOffset.z
        );

        transform.position = Vector3.SmoothDamp(
            transform.position, targetFlyPosition, ref currentFlyVelocity, flySmoothTime, flySpeed);
            
        LookAtTarget(2.0f);

        // 3. 거리 계산 (애니메이션 블렌딩)
        Vector2 myPos2D = new Vector2(transform.position.x, transform.position.z);
        Vector2 targetPos2D = new Vector2(_target.position.x, _target.position.z);
        float dist2D = Vector2.Distance(myPos2D, targetPos2D);
        if (dist2D > hoverDistance + 1.0f) _animator.SetFloat(AnimID_FlySpeed, 1.0f);
        else _animator.SetFloat(AnimID_FlySpeed, 0.0f);

        // ★ 4. 공중 공격 쿨타임이 아직 안 지났으면 여기서 멈춤 (공격 안 하고 맴돌기만 함)
        if (Time.time < lastAirAttackTime + airAttackCooldown) return;

        // ★ 5. 쿨타임도 찼고, 거리도 좁혀졌으면 공중 패턴 실행!
        if (dist2D <= hoverDistance + 1.0f) 
        {
            currentAttackIndex = Random.value < 0.5f ? 3 : 4;
            ChangeState(DragonState.FlyAttack);
        }
    }
    private void UpdateFlyAttack()
    {
        // [3번 패턴: 활강]
        if (currentAttackIndex == 3 && isGliding)
        {
            glideTimer += Time.deltaTime;
            float t = Mathf.Clamp01(glideTimer / glideDuration); 
            
            // 1. 현재 위치 계산 (U자형)
            Vector3 currentPos = Vector3.Lerp(glideStartPosition, glideEndPosition, t);
            float dipAmount = Mathf.Sin(t * Mathf.PI); 
            currentPos.y = Mathf.Lerp(glideStartPosition.y, glideStartPosition.y - diveDipDepth, dipAmount);
            
            // 2. 다음 프레임 위치를 미리 계산하여 "바라볼 방향"을 정밀하게 구함 (고장 해결 핵심)
            float nextT = Mathf.Clamp01((glideTimer + 0.05f) / glideDuration);
            Vector3 nextPos = Vector3.Lerp(glideStartPosition, glideEndPosition, nextT);
            nextPos.y = Mathf.Lerp(glideStartPosition.y, glideStartPosition.y - diveDipDepth, Mathf.Sin(nextT * Mathf.PI));
            
            Vector3 moveDir = (nextPos - currentPos).normalized;
            
            // 3. 실제 위치 및 회전 적용
            transform.position = currentPos;
            if (moveDir != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(moveDir), Time.deltaTime * 10f);
            }

            if (t >= 1.0f) isGliding = false;
        }
        else if (currentAttackIndex == 4)
        {
            // 플레이어를 추적하며 고개 숙이기
            Vector3 dirToPlayer = (_target.position - transform.position).normalized;
            dirToPlayer.y = Mathf.Clamp(dirToPlayer.y, -0.8f, -0.2f);
            
            if (dirToPlayer != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(dirToPlayer);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 2.0f);
            }

            // 악몽의 쐐기 생성 기믹 추가
            if (Time.time >= _lastSpikeSpawnTime + spikeSpawnInterval)
            {
                SpawnSpikeOnGround();
                _lastSpikeSpawnTime = Time.time;
            }
        }
    }
    // ==========================================================
    // 애니메이션 이벤트 수신 (Animation Events)
    // ==========================================================
    
    // BackAway 애니메이션 끝 — 회복용 / 콤보용 / 단독 분기
    public void OnBackAwayEnd()
    {
        // 공격 후 거리 확보용 BackAway → 공격 없이 GroundChase (브레스 → BackAway → 브레스 루프 차단)
        if (_backAwayIsRecovery)
        {
            _backAwayIsRecovery = false;
            ChangeState(DragonState.GroundChase);
            return;
        }

        // 콤보 큐에 다음 공격이 예약되어 있으면 그대로 실행
        if (_comboQueue.Count > 0)
        {
            currentAttackIndex = _comboQueue.Dequeue();
            ChangeState(DragonState.GroundAttack);
            return;
        }

        // 단독 BackAway — 랜덤 후속 공격 선택
        float roll = Random.value;
        bool breathReady = Time.time >= lastBreathTime + breathCooldown;

        if (breathReady && roll < 0.15f)
        {
            currentAttackIndex = 2; // 브레스 (15%, 거리 벌린 뒤 화염)
            lastBreathTime = Time.time;
        }
        else if (roll < 0.75f)
            currentAttackIndex = 0; // 물기 (기본)
        else
            currentAttackIndex = 1; // 할퀴기 (돌진 복귀)

        ChangeState(DragonState.GroundAttack);
    }

    // 지상 공격 끝 — 콤보 연속 실행 or 쿨타임 차등 적용
    public void OnGroundAttackEnd()
    {
        // BackAway 후 공격이었다면 에이전트 복구
        if (!_agent.enabled)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            {
                _agent.enabled = true;
                _agent.Warp(hit.position);
            }
        }

        // 콤보 다음 타가 남아있으면 즉시 실행
        if (_comboQueue.Count > 0)
        {
            int next = _comboQueue.Dequeue();

            // BackAway 마커면 상태 전환
            if (next == COMBO_BACKAWAY)
            {
                ChangeState(DragonState.BackAway);
                return;
            }

            // 일반 공격이면 애니메이션만 재트리거 (이미 GroundAttack 상태)
            currentAttackIndex = next;
            _animator.SetInteger("attackIndex", currentAttackIndex);
            _animator.SetTrigger("doAttack");
            return;
        }

        // 콤보 or 단일 공격 종료 — 쿨타임 차등 적용
        _currentCooldown = getAttackCooldown(currentAttackIndex);
        if (_isInCombo) _currentCooldown += comboEndRecovery;
        _isInCombo = false;

        lastGroundAttackTime = Time.time;

        // 확률적 회복 BackAway — 가까울 때만 발동 (뒤로 빠지며 거리 확보 후 walk→run 루프 유도)
        float distToPlayer = _target != null ? Vector3.Distance(transform.position, _target.position) : float.MaxValue;
        if (distToPlayer < clawRange && Random.value < 0.45f)
        {
            _backAwayIsRecovery = true;
            ChangeState(DragonState.BackAway);
            return;
        }

        ChangeState(DragonState.GroundChase);
    }

    // 이륙 애니메이션이 공중 궤도에 진입했을 때 호출
    public void OnTakeOffEnd()
    {
        // 1. 여기서 고도를 단 한 번만 고정!
        currentFlyAltitude = transform.position.y;
        
        // 2. 공중 체공 시작 시간 기록
        currentFlightStartTime = Time.time;
        
        // 3. 첫 공중 공격은 타겟에 도달하면 바로 쏠 수 있도록 쿨타임 조정
        lastAirAttackTime = Time.time - airAttackCooldown;

        ChangeState(DragonState.FlyHover);
    }

    // 공중 브레스 끝났을 때 호출
    public void OnFlyAttackEnd()
    {
        isGliding = false;
        lastAirAttackTime = Time.time; // 공중 공격 쿨타임 리셋
        _animator.applyRootMotion = false;

        ChangeState(DragonState.FlyHover);
    }

    // 착륙 애니메이션이 끝나고 발이 땅에 닿았을 때 호출
    public void OnLandEnd()
    {
        // 1. 루트 모션 다시 켜기 (지상 애니메이션 이동 복구)
        _animator.applyRootMotion = true;
        // 착륙 완료 시점부터 다시 지상 쿨타임 계산 시작
        lastFlightTime = Time.time;
        // 착륙 완료 시 NavMeshAgent 다시 켜기 (바닥 좌표를 샘플링해서 안전하게 켬)
        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 5.0f, NavMesh.AllAreas))
        {
            transform.position = hit.position;
            _agent.enabled = true;
        }
        ChangeState(DragonState.GroundChase);
    }
    // 애니메이션 이벤트에서 호출할 함수 (불 켜기)
    public void OnFireBreathVFX()
    {
        // Groggy/Die 상태에서 중단된 브레스 애니메이션의 이벤트가 뒤늦게 발화하는 경우 차단
        if (currentState == DragonState.Groggy || currentState == DragonState.Die) return;

        if (fireBreathVFX != null)
        {
            fireBreathVFX.SetActive(true);
            // 오디오도 여기서 재생하면 입 벌릴 때 딱 소리 남
            // audioSource.PlayOneShot(fireSound);
        }
    }
    public void EndFireBreathVFX()
    {
        if (fireBreathVFX != null) 
        {
            fireBreathVFX.SetActive(false);
        }
    }
    // ==========================================================
    // Phase 관리
    // ==========================================================
    // 체력이 깎일 때마다 호출됨
    private void CheckPhaseTwo(float current, float max)
    {
        if (isPhaseTwo) return;

        if (current <= max * phaseTwoEgoRatio)
        {
            isPhaseTwo = true;
            Debug.Log("🐉 드래곤 2페이즈 돌입! 비행 패턴 개방!");

            // 2페이즈 돌입 즉시 포효 후, GroundChase로 돌아오면 바로 이륙하도록 쿨타임 만료 처리
            _animator.SetTrigger("doScream");
            lastFlightTime = Time.time - flightCooldown;
            
            // 공격/이동 속도 강화
            _walkSpeed *= 1.2f;
            _runSpeed  *= 1.2f;
            _agent.speed = _runSpeed;
        }
    }
    // ==========================================================
    // 그로기 로직
    // ==========================================================
    private void GoToGroggyState()
    {
        if (currentState == DragonState.Die) return;
        ChangeState(DragonState.Groggy);
    }
    private IEnumerator RecoverFromGroggy()
    {
        yield return new WaitForSeconds(7.0f);
        lastFlightTime = Time.time;
        // 애니메이터에서 일어나는 모션 처리 후
        ChangeState(DragonState.GroundChase);
    }
    private IEnumerator FallToGround()
    {
        float fallSpeed = 0f;
        float gravity = 25f;

        // 바닥에 닿을 때까지 하강
        while (true)
        {
            fallSpeed += gravity * Time.deltaTime;
            transform.Translate(Vector3.down * fallSpeed * Time.deltaTime, Space.World);

            // Raycast나 단순 높이 체크로 바닥 확인
            if (transform.position.y <= 0.1f) // 0.1f는 바닥 높이 (환경에 맞게 수정)
            {
                Vector3 landPos = transform.position;
                landPos.y = 0;
                transform.position = landPos;
                break;
            }
            yield return null;
        }
        // CameraShake.Instance.Shake(0.5f, 1.0f); // 카메라 흔들림 추가 추천
        
        yield return RecoverFromGroggy();
    }
    // ==========================================================
    // 기타 로직
    // ==========================================================
    private void SpawnSpikeOnGround()
    {
        if (spikePrefab == null || hurtboxes.Count == 0) return;

        // 1단계: 입 방향으로 레이캐스트 → 브레스가 향하는 XZ 좌표 추출
        if (!Physics.Raycast(mouthPosition.position, mouthPosition.forward, out RaycastHit forwardHit, 40f))
            return; // 아무것도 맞지 않으면 생성 안 함

        // 2단계: 그 XZ 위치에서 수직으로 아래 레이캐스트 → 정확한 바닥 Y 좌표 확보
        Vector3 groundCheckOrigin = new Vector3(forwardHit.point.x, forwardHit.point.y + 10f, forwardHit.point.z);
        if (!Physics.Raycast(groundCheckOrigin, Vector3.down, out RaycastHit groundHit, 20f))
            return; // 바닥이 없는 허공이면 생성 안 함

        Vector3 spawnPos = groundHit.point + Vector3.up * spikeYOffset;
        Instantiate(spikePrefab, spawnPos, Quaternion.identity)
            .GetComponent<NightmareSpike>()?.Setup(hurtboxes[1]);
    }
    
    private void OnAnimatorMove()
    {
        // 특수 처리: BackAway 상태일 때는 루트 모션 연산을 아예 무시함
        if (currentState == DragonState.BackAway) return;

        // BackAway 후 공격(에이전트 비활성) 중에는 위치 잠금
        bool lockPosition = currentState == DragonState.GroundAttack && !_agent.enabled;

        if (lockPosition)
        {
            transform.rotation *= _animator.deltaRotation;
        }
        else
        {
            transform.position += _animator.deltaPosition;
            transform.rotation *= _animator.deltaRotation;
        }
    }

    private void LookAtTarget(float speed)
    {
        Vector3 dir = (_target.position - transform.position).normalized;
        dir.y = 0; // 공중에서도 플레이어 방향 수평 회전만 처리
        if (dir != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * speed);
        }
    }

    private void OnDie()
    {
        _comboQueue.Clear();
        _isInCombo = false;
        _backAwayIsRecovery = false;
        ChangeState(DragonState.Die);
        _agent.enabled = false;
        _animator.SetTrigger("doDie");
        if (bossEgoBar != null) bossEgoBar.Hide();
        // 사망 연출, GameManager 이벤트 등 추가
    }

    // ====================================
    // 애니메이션 이벤트 - 히트박스 
    // ====================================
    public void EnableBite() => biteWeapon?.EnableHitbox();
    public void DisableBite() => biteWeapon?.DisableHitbox();

    public void EnableClaw() => clawWeapon?.EnableHitbox();
    public void DisableClaw() => clawWeapon?.DisableHitbox();

    // 2. 활강(Glide) 공격 제어
    public void EnableGlideHitbox() => bodyDive?.EnableHitbox();
    public void DisableGlideHitbox() => bodyDive?.DisableHitbox();
}