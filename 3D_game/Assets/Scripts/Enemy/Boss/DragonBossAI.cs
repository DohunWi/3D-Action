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
    public float biteRange = 4.0f;       // 깨물기 사거리 (초근접)
    public float clawRange = 8.0f;       // 돌진 할퀴기 사거리 (조금 떨어졌을 때)
    public float breathRange = 12.0f;    // 지상 브레스 사거리 (중거리)

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

    // 1. 애니메이션 파라미터 해시값 (성능 최적화)
    private static readonly int AnimID_Speed = Animator.StringToHash("speed");
    private static readonly int AnimID_FlySpeed = Animator.StringToHash("flySpeed");
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

            case DragonState.GroundAttack:
                _agent.isStopped = true;
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

        // 2. 공격 판단 조건 (쿨타임 및 사거리 확인)
        bool isAttackAvailable = Time.time >= lastGroundAttackTime + groundAttackCooldown;

        if (isAttackAvailable && dist <= breathRange)
        {
            if (TrySelectGroundPattern(dist))
            {
                ChangeState(DragonState.GroundAttack);
                return;
            }
        }

        // 3. 거리 유지 및 추적 로직 (겹침 방지 핵심)
        // 공격이 불가능한 쿨타임 중에는 더 멀리서 멈추도록 설정 (예: 7m)
        float dynamicStoppingDist = isAttackAvailable ? biteRange - 1.0f : 7.0f;
        _agent.stoppingDistance = dynamicStoppingDist;

        if (dist <= _agent.stoppingDistance + 0.5f)
        {
            // 목적지에 거의 도달했다면 에이전트를 멈추고 제자리 회전
            _agent.isStopped = true;
            _animator.SetFloat(AnimID_Speed, 0f, 0.2f, Time.deltaTime);
            LookAtTarget(6.0f); // 제자리에서 플레이어를 부드럽게 주시
        }
        else
        {
            // 아직 멀다면 추격 진행
            _agent.isStopped = false;
            _agent.SetDestination(_target.position);
            _animator.SetFloat(AnimID_Speed, _agent.desiredVelocity.magnitude, 0.1f, Time.deltaTime);
        }
    }

    // 지상 공격 패턴
    private bool TrySelectGroundPattern(float dist)
    {
        // 0. 지상 공격 통합 쿨타임 체크
        if (Time.time < lastGroundAttackTime + groundAttackCooldown) return false;

        // 1. 중거리 (Claw Range ~ Breath Range)
        if (dist > clawRange && dist <= breathRange)
        {
            // 브레스 쿨타임이 돌았다면 지상 브레스 뿜기
            if (Time.time >= lastBreathTime + breathCooldown)
            {
                currentAttackIndex = 2; // Flame Attack
                lastBreathTime = Time.time; // 쿨타임 리셋
                return true;
            }
            return false; // 브레스 쿨이면 더 다가가기 위해 false 반환
        }

        // 2. 약간 떨어짐 (Bite Range ~ Claw Range)
        if (dist > biteRange && dist <= clawRange)
        {
            // 돌진 할퀴기로 거리 좁히면서 공격
            currentAttackIndex = 1; // Claw Attack
            return true;
        }

        // 3. 초근접 (0 ~ Bite Range)
        if (dist <= biteRange)
        {
            currentAttackIndex = 0;
            return true;
        }

        return false;
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
    
    // 지상 공격 끝
    public void OnGroundAttackEnd()
    {
        ChangeState(DragonState.GroundChase);
        lastGroundAttackTime = Time.time;
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
            
            // 공격/이동 속도 강화 등 추가 가능
            _agent.speed *= 1.2f; 
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