using UnityEngine;
using UnityEngine.AI;
using System.Collections; // Coroutine 사용을 위해 필수

public enum EnemyState
{
    Idle,   // 대기
    Patrol, // 배회 (순찰)
    Chase,  // 추격
    Attack, // 공격
    Parried, // 패링당함 (무방비)
    Hit,    // 피격 (경직)
    Die,     // 사망
    Down     // 그로기
}

public class Enemy : MonoBehaviour 
{
    [Header("State")]
    public EnemyState currentState = EnemyState.Patrol; 

    [Header("Components")]
    protected NavMeshAgent _agent;
    protected Animator _animator;
    protected CharacterStats _stats;

    [Header("Sensors (Patrol & Chase)")]
    public float detectionRange = 8.0f;     
    public float chaseGiveUpRange = 15.0f;  
    public float patrolRadius = 5.0f;       
    public float patrolWaitTime = 2.0f;     
    private Vector3 _startPosition;         
    private float _patrolTimer;             

    [Header("Combat Settings")]
    public float attackRange = 2.5f; // 1타 사거리에 맞춰 살짝 늘림
    public float attackCooldown = 2.0f;
    public float jumpAttackReach = 5.0f;
    private float _lastAttackTime;
    protected Transform _target;

    [Header("Combo Attack")]
    // 공격 순서: 2(점프)
    protected int[] _comboSequence = { 3, 4, 2, 0, 1 }; 
    private int _currentComboStep = 0;
    private bool _isComboActive = false;
    private float _stopRotationTime;

    [Header("Down State")]
    public float downDuration = 2.0f; // 누워있는 시간
    private static readonly int AnimID_Down = Animator.StringToHash("doDown"); // 다운 애니메이션
    private static readonly int AnimID_GetUp = Animator.StringToHash("doGetUp"); // 일어나는 애니메이션 (없으면 생략 가능)
    private Coroutine _downCoroutine;

    [Header("Jump Attack Settings")]
    public float jumpAirTime = 0.8f; // 점프가 시작되고 착지할 때까지 걸리는 시간 (애니메이션에 맞춰 조절)
    public float maxJumpDistance = 10.0f; // 너무 멀면 이상하게 날아가니까 제한
    private Vector3 _calculatedJumpVelocity; // 계산된 점프 속도
    private bool _isHomingJumpActive = false; // 호밍 점프 중인지 OnAttackEnd여부

    [Header("Rotation Settings")]
    [Range(0f, 1f)] public float attackRotateDuration = 0.5f; // 공격 중 회전 가능 시간

    [Header("Weapons")]
    public Weapon leftWeapon;
    public Weapon rightWeapon; 
    public Weapon jumpAtaackWave;

    [Header("Audio Settings")]
    public AudioSource audioSource;
    public AudioClip idleSound;
    public float minIdleTime = 5f;
    public float maxIdleTime = 10f;
    private float _idleTimer;
    
    // Animation IDs (성능 최적화)
    private static readonly int AnimID_Speed = Animator.StringToHash("speed");
    private static readonly int AnimID_Attack = Animator.StringToHash("doAttack");
    private static readonly int AnimID_Hit = Animator.StringToHash("doHit");
    private static readonly int AnimID_Die = Animator.StringToHash("doDie");
    private static readonly int AnimID_AttackIndex = Animator.StringToHash("attackIndex");
    private static readonly int AnimID_Parried = Animator.StringToHash("doParried");

    protected virtual void  Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _animator = GetComponent<Animator>();
        _stats = GetComponent<CharacterStats>();
    }

    protected virtual void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) _target = player.transform;

        _startPosition = transform.position;
        _lastAttackTime = -attackCooldown;

        ChangeState(EnemyState.Patrol);

        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        _idleTimer = Random.Range(minIdleTime, maxIdleTime);
    }

    private void OnEnable()
    {
        if (_stats != null)
        {
            _stats.OnPoiseBroken += HandlePoiseBroken; // 강인도 깨짐 -> 경직
            _stats.OnTakeDamage += HandleSuperArmorHit; // 그냥 피격 -> 빨간맛 연출
            _stats.OnDeath += OnDie;
        }
    }

    private void OnDisable()
    {
        if (_stats != null)
        {
            _stats.OnPoiseBroken -= HandlePoiseBroken;
            _stats.OnTakeDamage -= HandleSuperArmorHit;
            _stats.OnDeath -= OnDie;
        }
    }

    private void Update()
    {
        if (currentState == EnemyState.Die) return;

        if (_target == null) return;

        switch (currentState)
        {
            case EnemyState.Idle:   break;
            case EnemyState.Patrol: UpdatePatrol(); break;
            case EnemyState.Chase:  UpdateChase();  break;
            case EnemyState.Attack: UpdateAttackRotation(); break; // 공격 중 회전 처리
            case EnemyState.Parried:    break;
            case EnemyState.Down:    break;
            case EnemyState.Hit:    break;
        }

        HandleIdleSound();
    }

    // --- FSM 상태 변경 ---
    public void ChangeState(EnemyState newState)
    {
        void CleanUpFlag() // 상태 변경시 초기화 할 것들
        {
            // navMesh 초기화
            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;

            // 점프 초기화
            _isHomingJumpActive = false; 
            _calculatedJumpVelocity = Vector3.zero;

            // 콤보 초기화
            _isComboActive = false;
            _currentComboStep = 0;

            //무기 콜라이더 끄기
            DisableAllWeapons();
        }
        if (currentState == EnemyState.Die) return;
        
        // 같은 상태 반복 진입 방지 (단, Attack->Attack 콤보 연계는 예외적으로 허용하거나 아래 로직에서 처리)
        if (currentState == newState && newState != EnemyState.Attack) return;
        if (currentState == EnemyState.Down)
        {
            // 1. 죽는 거면(Die) OK (죽어야 하니까)
            // 2. 추격(Chase)으로 가는 거면 OK (일어나는 거니까)
            // 3. 그 외(Hit, Attack 등)는 전부 무시! (계속 누워있어!)
            if (newState != EnemyState.Die && newState != EnemyState.Chase)
            {
                return;
            }
        }
        

        // [Exit] 이전 상태 나갈 때 처리
        switch (currentState)
        {
            case EnemyState.Attack:
                _agent.isStopped = false; 
                DisableAllWeapons();
                
                // 공격 상태를 벗어나는데(피격 등) 다음이 Attack이 아니면 콤보 끊기
                if (newState != EnemyState.Attack)
                {
                    _isComboActive = false;
                    _currentComboStep = 0;
                }
                break;
            
            case EnemyState.Parried: 
            case EnemyState.Hit:
                _agent.isStopped = false;
                break;
            case EnemyState.Down:
                _lastAttackTime = Time.time; 
                if (_downCoroutine != null) StopCoroutine(_downCoroutine);
                break;
        }

        currentState = newState;

        // [Enter] 새 상태 진입 처리
        switch (currentState)
        {
            case EnemyState.Patrol:
                _agent.isStopped = false;
                _agent.speed = 2.0f;
                MoveToRandomPatrolPoint();
                break;

            case EnemyState.Chase:
                _agent.isStopped = false;
                _agent.speed = 3.5f;
                _lastAttackTime = Time.time - attackCooldown/2;
                break;

            case EnemyState.Attack:
                _agent.isStopped = true;
                _agent.velocity = Vector3.zero;
                // Attack 애니메이션 트리거는 ProcessComboStep에서 실행함
                break;

            case EnemyState.Hit:
                CleanUpFlag();
                _animator.SetTrigger(AnimID_Hit);
                // 강인도 파괴되어 경직됐으므로 콤보 초기화
                break;
            
            case EnemyState.Down:
                CleanUpFlag();
                _animator.SetTrigger(AnimID_Down); // 넘어지는 애니메이션 재생      
                // 일정 시간 뒤에 일어나는 코루틴 시작
                _downCoroutine = StartCoroutine(RecoverFromDown());
                break;

            case EnemyState.Parried:
                CleanUpFlag();
                _animator.SetTrigger(AnimID_Parried); 
                break;

            case EnemyState.Die:
                CleanUpFlag();
                _agent.enabled = false;
                GetComponent<Collider>().enabled = false;
                _animator.SetTrigger(AnimID_Die);
                Destroy(gameObject, 5f);
                break;
        }
    }

    // --- AI Logic (Patrol) ---
    private void UpdatePatrol()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, _target.position);
        if (distanceToPlayer <= detectionRange)
        {
            ChangeState(EnemyState.Chase); 
            return;
        }

        if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.1f)
        {
            _patrolTimer += Time.deltaTime;
            _animator.SetFloat(AnimID_Speed, 0f);

            if (_patrolTimer >= patrolWaitTime)
            {
                MoveToRandomPatrolPoint();
                _patrolTimer = 0f;
            }
        }
        else
        {
            _animator.SetFloat(AnimID_Speed, _agent.desiredVelocity.magnitude, 0.1f, Time.deltaTime);
        }
    }

    private void MoveToRandomPatrolPoint()
    {
        Vector3 randomDirection = Random.insideUnitSphere * patrolRadius;
        randomDirection += _startPosition;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, patrolRadius, NavMesh.AllAreas))
        {
            _agent.SetDestination(hit.position);
        }
    }

    // --- AI Logic (Chase & Combo Attack) ---
    // // [수정] 자식이 오버라이드할 수 있게 가상 함수로 추가
    protected virtual bool TrySpecialAttack() 
    { 
        // 기본 Enemy는 특수 공격이 없으므로 false 반환
        return false; 
    }
    private void UpdateChase()
    {
        if (_isComboActive) return;
        // 자식에서 추가할 공격
        if (TrySpecialAttack()) return;

        float distance = Vector3.Distance(transform.position, _target.position);
        
        if (distance > chaseGiveUpRange)
        {
            ChangeState(EnemyState.Patrol);
            return;
        }

        // 공격 쿨타임 체크 (콤보 종료 후 대기 시간)
        if (Time.time >= _lastAttackTime + attackCooldown)
        {
            // 1. 콤보 공격 시작 (사거리 내 진입)
            if (distance <= attackRange)
            {
                StartComboAttack();
                return;
            }
        }

        // 이동 처리
        if (distance <= attackRange)
        {
            _agent.isStopped = true;
            LookAtTarget();
            _animator.SetFloat(AnimID_Speed, 0f);
        }
        else
        {
            _agent.isStopped = false;
            _agent.SetDestination(_target.position);
            _animator.SetFloat(AnimID_Speed, _agent.desiredVelocity.magnitude, 0.1f, Time.deltaTime);
        }
    }

    // --- Combo System ---
    private void StartComboAttack()
    {
        _isComboActive = true;
        _currentComboStep = 0;
        
        ProcessComboStep(); // 1타 시작
    }

    private void ProcessComboStep()
    {
        // 콤보가 끝났거나 범위를 벗어났으면 종료
        if (_currentComboStep >= _comboSequence.Length)
        {
            EndCombo();
            return;
        }
        // 플레이어 방향으로 즉시 회전
        InstantFaceTarget();

        // 현재 단계에 맞는 공격 인덱스 가져오기 (1 -> 0 -> 2)
        int attackIndex = _comboSequence[_currentComboStep];

        // 상태 전환 (이미 Attack이면 애니메이션만 재생)
        if (currentState != EnemyState.Attack)
        {
            ChangeState(EnemyState.Attack);
        }
        
        _animator.SetInteger(AnimID_AttackIndex, attackIndex);
        _animator.SetTrigger(AnimID_Attack);

        _stopRotationTime = Time.time + 0.5f;

        LookAtTarget();
    }

    private void EndCombo()
    {
        _isComboActive = false;
        _currentComboStep = 0;
        _lastAttackTime = Time.time; // 콤보가 다 끝나야 쿨타임 시작
        ChangeState(EnemyState.Chase);
    }

    // --- Animation Event Functions ---
    // 공격 애니메이션이 끝날 때 호출됨
    public void OnAttackEnd()
    {
        if (_isComboActive)
        {
            // 다음 단계가 있는지 확인
            int nextStep = _currentComboStep + 1;
            if (nextStep < _comboSequence.Length)
            {
                float dist = Vector3.Distance(transform.position, _target.position);
                
                // 기본 허용 거리 (평타는 짧게)
                float checkRange = attackRange + 1.0f; 

                // ★ [핵심] 다음 공격이 '점프 공격(2)'이라면? 허용 거리를 대폭 늘림!
                // (이전에 작성한 _comboSequence = { 3, 4, 2, 0, 1 } 기준, 2번이 점프)
                if (_comboSequence[nextStep] == 2) 
                {
                    checkRange = jumpAttackReach; // 예: 6.0m
                }
                else if(_comboSequence[nextStep] == 0)
                {
                    checkRange = attackRange - 1.0f;
                }

                // 거리가 허용 범위를 벗어났으면 콤보 중단
                if (dist > checkRange)
                {
                    EndCombo(); 
                    return;
                }

                _currentComboStep++; // 다음 단계 진행
                ProcessComboStep();  // 공격 실행
            }
            else
            {
                EndCombo(); // 콤보 끝
            }
        }
        else
        {
            // 단발성 공격이나 다른 상황이었다면 복귀
            if (currentState != EnemyState.Down) ChangeState(EnemyState.Chase);
        }
    }

    public void OnHitEnd()
    {
        if (currentState == EnemyState.Down) return; // 누워있으면 무시!
        ChangeState(EnemyState.Chase);
    }
    public void OnParriedEnd()
    {
        if (currentState == EnemyState.Down) return; // 누워있으면 무시!
        ChangeState(EnemyState.Chase);
    }    
    protected virtual void OnDie()
    {
        ChangeState(EnemyState.Die);
    }

    // --- 이벤트 핸들러 ---

    // 1. 강인도가 깨졌을 때 (Stat에서 호출해줌)
    private void HandlePoiseBroken()
    {
        if (currentState == EnemyState.Die || currentState == EnemyState.Down) return;

        // 고민할 것 없이 바로 경직 상태로!
        ChangeState(EnemyState.Hit);
    }

    // 2. 강인도로 버텼을 때 (Stat에서 호출해줌)
    private void HandleSuperArmorHit()
    {
        if (currentState == EnemyState.Die || currentState == EnemyState.Down) return;

        // 상태 변경 없이 연출만 재생
        StartCoroutine(FlashRed());
    }
    private IEnumerator FlashRed()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers) r.material.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        foreach (var r in renderers) r.material.color = Color.white;
    }
    // ----------------------------------
    public void GetParried()
    {
        if (currentState == EnemyState.Die) return;
        // 패링은 강인도 무시하고 무조건 걸림
        Debug.Log($"{gameObject.name}: 으악! 패링당했다!");
        ChangeState(EnemyState.Parried);
    }
    public void KnockDown()
    {
        if (currentState == EnemyState.Die) return;

        // 강인도고 뭐고 무조건 넘어짐
        ChangeState(EnemyState.Down);
    }
    // 다운 회복 코루틴
    private IEnumerator RecoverFromDown()
    {
        // 1. 누워있는 시간 대기
        yield return new WaitForSeconds(downDuration);

        // 2. 일어나는 애니메이션이 있다면 재생 (선택 사항)
        _animator.SetTrigger(AnimID_GetUp);
        yield return new WaitForSeconds(1.0f); // 일어나는 모션 시간 대기

        // 3. 다시 추격 상태로 복귀
        if (_agent.enabled) _agent.Warp(transform.position);
        ChangeState(EnemyState.Chase);
    }

    // --- Helper Functions ---
    private void LookAtTarget()
    {
        if (_target == null) return;
        Vector3 direction = (_target.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            Quaternion lookRot = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * 10f);
        }
    }
    
    // 공격 중에도 일정 시간동안 회전 가능하게 (유도 성능)
    private void UpdateAttackRotation()
    {
       // 그냥 시간이 남았으면 계속 쳐다봄 (전환 구간이고 뭐고 무조건 돔)
        if (Time.time < _stopRotationTime)
        {
            LookAtTarget();
        }
    }
    // 플레이어를 즉시(혹은 아주 빠르게) 바라보는 함수
    private void InstantFaceTarget()
    {
        if (_target == null) return;

        Vector3 direction = (_target.position - transform.position).normalized;
        direction.y = 0; // 높낮이 무시

        if (direction != Vector3.zero)
        {
            // 아예 1프레임만에 확 돌려버림 (정확도 100%)
            transform.rotation = Quaternion.LookRotation(direction);
            
            // 만약 너무 딱딱해 보이면 아래 코드로 대체 (아주 빠르게 회전)
            // Quaternion targetRot = Quaternion.LookRotation(direction);
            // transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, 360f); // 1프레임에 360도 회전 가능
        }
    }

    // Animator Move (물리 이동)
    private void OnAnimatorMove()
    {
        if (currentState == EnemyState.Die || !_agent.enabled) return;

        // 순찰, 추격 중엔 NavMesh 속도 사용
        if (currentState == EnemyState.Patrol || currentState == EnemyState.Chase)
        {
             if (Time.deltaTime > 0.001f)
                _agent.velocity = _agent.desiredVelocity;
        }
        // 공격 중엔 Root Motion 사용
        else if (currentState == EnemyState.Attack) 
        {
            if (Time.deltaTime > 0.001f)
            {
                // ★ 호밍 점프 중이면 계산된 속도로 날아감!
                if (_isHomingJumpActive)
                {
                    _agent.velocity = _calculatedJumpVelocity;
                }
                else
                {
                    // 일반 공격은 루트 모션 사용
                    Vector3 rootMotionVelocity = _animator.deltaPosition / Time.deltaTime;
                    _agent.velocity = rootMotionVelocity;
                }
            }
        }
    }

    private void HandleIdleSound()
    {
        if (currentState == EnemyState.Idle || currentState == EnemyState.Patrol)
        {
            _idleTimer -= Time.deltaTime;
            if (_idleTimer <= 0)
            {
                if (audioSource != null && idleSound != null)
                {
                    audioSource.pitch = Random.Range(0.9f, 1.1f);
                    audioSource.PlayOneShot(idleSound);
                }
                _idleTimer = Random.Range(minIdleTime, maxIdleTime);
            }
        }
    }
    // ★ 애니메이션 이벤트: 발이 땅에서 떨어질 때 호출
    public void CalculateJumpVector()
    {
        if (_target == null) return;

        // 1. 목표 지점 계산 (플레이어 위치)
        Vector3 targetPos = _target.position;
        Vector3 startPos = transform.position;

        // 2. 거리 계산
        Vector3 direction = targetPos - startPos;
        direction.y = 0; // 높이는 무시 (수평 이동만 계산)
        
        float distance = direction.magnitude;

        // 3. 거리 제한 (너무 멀면 최대치까지만)
        if (distance > maxJumpDistance)
        {
            distance = maxJumpDistance;
        }

        // 4. 속도 = 거리 / 시간
        // (점프 체공 시간 동안 저 거리만큼 가려면 얼마나 빨라야 하는가?)
        float requiredSpeed = distance / jumpAirTime;

        // 5. 최종 속도 벡터 저장
        _calculatedJumpVelocity = direction.normalized * requiredSpeed;

        // 6. 호밍 활성화 + 회전도 타겟 보게 맞춤
        _isHomingJumpActive = true;
        InstantFaceTarget(); 
    }

    // 애니메이션 이벤트: 착지했을 때 호출 (호밍 끄기)
    public void EndHomingJump()
    {
        _isHomingJumpActive = false;
        _agent.velocity = Vector3.zero; // 착지하면 미끄러짐 방지
    }
    // --- Animation Events ---
    public void EnableRightWeapon() => rightWeapon?.EnableHitbox();
    public void DisableRightWeapon() => rightWeapon?.DisableHitbox();
    public void EnableLeftWeapon() => leftWeapon?.EnableHitbox();
    public void DisableLeftWeapon() => leftWeapon?.DisableHitbox();
    public void EnableJumpAttack() => jumpAtaackWave?.EnableHitbox();
    public void DisableJumpAttack() => jumpAtaackWave?.DisableHitbox();

    private void DisableAllWeapons()
    {
        DisableRightWeapon();
        DisableLeftWeapon();
        DisableJumpAttack();
    }
}