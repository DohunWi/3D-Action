using UnityEngine;
using UnityEngine.AI;

public enum EnemyState
{
    Idle,   // 대기
    Patrol, // 배회 (순찰)
    Chase,  // 추격
    Attack, // 공격
    Parried, // 패링당함
    Hit,    // 피격
    Die     // 사망
}

public class Enemy : MonoBehaviour 
{
    [Header("State")]
    public EnemyState currentState = EnemyState.Patrol; // 시작하자마자 배회하도록 설정

    [Header("Components")]
    private NavMeshAgent _agent;
    private Animator _animator;
    private CharacterStats _stats;

    [Header("Sensors (Patrol & Chase)")]
    public float detectionRange = 8.0f;     // 플레이어 감지 거리 (이 안으로 오면 추격 시작)
    public float chaseGiveUpRange = 15.0f;  // 추격 포기 거리 (이 밖으로 나가면 다시 순찰)
    public float patrolRadius = 5.0f;       // 순찰 반경
    public float patrolWaitTime = 2.0f;     // 목적지 도착 후 대기 시간

    private Vector3 _startPosition;         // 순찰 중심점 (처음 태어난 위치)
    private float _patrolTimer;             // 순찰 대기 타이머

    [Header("Combat Settings")]
    public float attackRange = 1.5f;
    public float jumpAttackRange = 4.0f;
    public float attackCooldown = 2.0f;
    
    [Header("Jump Attack Boost")]
    public float jumpBoostMultiplier = 2.0f;
    private bool _isJumpBoosting = false;

    [Header("Rotation Settings")]
    [Range(0f, 1f)] public float attackRotateDuration = 0.3f;

    [Header("Weapons")]
    public Weapon leftWeapon;
    public Weapon rightWeapon; 
    public Weapon jumpAtaackWave;

    private float _lastAttackTime;
    private Transform _target;
    
    private static readonly int AnimID_Speed = Animator.StringToHash("speed");
    private static readonly int AnimID_Attack = Animator.StringToHash("doAttack");
    private static readonly int AnimID_Hit = Animator.StringToHash("doHit");
    private static readonly int AnimID_Die = Animator.StringToHash("doDie");
    private static readonly int AnimID_AttackIndex = Animator.StringToHash("attackIndex");
    private static readonly int AnimID_Parried = Animator.StringToHash("doParried");

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _animator = GetComponent<Animator>();
        _stats = GetComponent<CharacterStats>();
    }

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) _target = player.transform;

        if (leftWeapon != null) leftWeapon.damage = 10f;
        if (rightWeapon != null) rightWeapon.damage = 10f;

        // 태어난 위치 기억 (여기를 중심으로 배회함)
        _startPosition = transform.position;
        
        // 시작 시 순찰로 변경 (Idle 대신)
        ChangeState(EnemyState.Patrol);
    }

    private void OnEnable()
    {
        if (_stats != null)
        {
            _stats.OnTakeDamage.AddListener(OnTakeDamage);
            _stats.OnDeath.AddListener(OnDie);
        }
    }

    private void OnDisable()
    {
        if (_stats != null)
        {
            _stats.OnTakeDamage.RemoveListener(OnTakeDamage);
            _stats.OnDeath.RemoveListener(OnDie);
        }
    }

    private void Update()
    {
        if (currentState == EnemyState.Die) return;
        if (_target == null) return;

        switch (currentState)
        {
            case EnemyState.Idle:   /* Idle 로직 필요 시 추가 */ break;
            case EnemyState.Patrol: UpdatePatrol(); break; // [NEW] 순찰 로직
            case EnemyState.Chase:  UpdateChase();  break;
            case EnemyState.Attack: break; 
            case EnemyState.Hit:    break;
        }
    }

    public void ChangeState(EnemyState newState)
    {
        if (currentState == EnemyState.Die) return;
        if (currentState == newState) return;

        // [로그 추가] 예: "Patrol -> Chase" 처럼 출력됨
        // Debug.Log($"[Enemy] State Change: {currentState} -> {newState}");
        // [Exit]
        switch (currentState)
        {
            case EnemyState.Attack:
                _agent.isStopped = false; 
                DisableAllWeapons();
                _isJumpBoosting = false;
                break;
            case EnemyState.Parried: // 패링 상태 끝날 때
                _agent.isStopped = false; // 다시 움직임 허용
                break;
            case EnemyState.Hit:
                _agent.isStopped = false;
                break;
        }

        currentState = newState;

        // [Enter]
        switch (currentState)
        {
            case EnemyState.Patrol:
                _agent.isStopped = false;
                _agent.speed = 2.0f; // [Option] 순찰 땐 천천히 걷기
                MoveToRandomPatrolPoint(); // 첫 목적지 설정
                break;

            case EnemyState.Chase:
                _agent.isStopped = false;
                _agent.speed = 3.5f; // [Option] 추격 땐 빠르게 뛰기
                break;

            case EnemyState.Attack:
                _agent.isStopped = true;
                _agent.velocity = Vector3.zero;
                // Attack Trigger는 StartAttack에서 함
                break;

            case EnemyState.Hit:
                _agent.isStopped = true;
                _agent.velocity = Vector3.zero;
                _animator.SetTrigger(AnimID_Hit);
                break;

            case EnemyState.Parried: // 패링당함 상태 진입
                _agent.isStopped = true;        // 이동 정지
                _agent.velocity = Vector3.zero; // 미끄러짐 방지
                _animator.SetTrigger(AnimID_Parried); // 리액션 애니메이션 재생
                DisableAllWeapons();            // 공격 판정 끄기 (공격하다 튕겨나갔으니까)
                break;

            case EnemyState.Die:
                _agent.isStopped = true;
                _agent.enabled = false;
                GetComponent<Collider>().enabled = false;
                _animator.SetTrigger(AnimID_Die);
                Destroy(gameObject, 5f);
                break;
        }
    }

    // --- 순찰(Patrol) 로직 ---
    private void UpdatePatrol()
    {
        // 1. 플레이어 감지 체크
        float distanceToPlayer = Vector3.Distance(transform.position, _target.position);
        if (distanceToPlayer <= detectionRange)
        {
            ChangeState(EnemyState.Chase); // 감지되면 추격 시작!
            return;
        }

        // 2. 목적지 도착 확인 (NavMeshAgent가 경로 계산 중이 아니고, 남은 거리가 짧을 때)
        if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.1f)
        {
            // 도착했으면 잠시 대기
            _patrolTimer += Time.deltaTime;
            _animator.SetFloat(AnimID_Speed, 0f); // 멈춤 애니메이션

            if (_patrolTimer >= patrolWaitTime)
            {
                MoveToRandomPatrolPoint(); // 다음 장소로 이동
                _patrolTimer = 0f;
            }
        }
        else
        {
            // 이동 중
            _animator.SetFloat(AnimID_Speed, _agent.desiredVelocity.magnitude, 0.1f, Time.deltaTime);
        }
    }

    // NavMesh 위의 랜덤한 점을 찾는 함수
    private void MoveToRandomPatrolPoint()
    {
        Vector3 randomDirection = Random.insideUnitSphere * patrolRadius; // 반지름 내 랜덤 좌표
        randomDirection += _startPosition; // 시작 위치 기준

        NavMeshHit hit;
        // NavMesh 위의 유효한 좌표인지 확인 (SamplePosition)
        if (NavMesh.SamplePosition(randomDirection, out hit, patrolRadius, NavMesh.AllAreas))
        {
            _agent.SetDestination(hit.position);
        }
    }

    // --- 추격(Chase) 로직 수정 ---
    private void UpdateChase()
    {
        float distance = Vector3.Distance(transform.position, _target.position);
        
        // 플레이어가 너무 멀어지면 다시 순찰 모드로 복귀 (포기)
        if (distance > chaseGiveUpRange)
        {
            ChangeState(EnemyState.Patrol);
            return;
        }

        // 공격 사거리 체크
        if (Time.time >= _lastAttackTime + attackCooldown)
        {
            if (distance > attackRange && distance <= jumpAttackRange)
            {
                StartAttack(2); 
                return;
            }
            else if (distance <= attackRange - 0.2f)
            {
                int randomAttack = Random.Range(0, 2); 
                StartAttack(randomAttack);
                return;
            }
        }

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

    private void StartAttack(int index)
    {
        ChangeState(EnemyState.Attack);
        _animator.SetInteger(AnimID_AttackIndex, index);
        _animator.SetTrigger(AnimID_Attack);
        _lastAttackTime = Time.time;
    }

    private void LookAtTarget()
    {
        Vector3 direction = (_target.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            Quaternion lookRot = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * 5f);
        }
    }
    public void GetParried()
    {
        if (currentState == EnemyState.Die) return;

        Debug.Log($"{gameObject.name}: 으악! 패링당했다!");
        
        // 패링 상태로 강제 전환 (FSM에 Parried 상태가 있어야 함)
        ChangeState(EnemyState.Parried);
    }
    public void OnParriedEnd()
    {
        // 정신 차리고 다시 추격 
        ChangeState(EnemyState.Chase);
    }

    // --- 물리 및 애니메이션 처리 ---
    private void OnAnimatorMove()
    {
        if (currentState == EnemyState.Die || !_agent.enabled) return;

        bool canRotate = false;
        Vector3 targetDir = Vector3.zero;

        // [수정] 순찰(Patrol) 중에도 NavMesh 방향으로 회전해야 함!
        if (currentState == EnemyState.Chase || currentState == EnemyState.Idle || currentState == EnemyState.Patrol)
        {
            canRotate = true;
            targetDir = _agent.desiredVelocity; // 이동하려는 방향
        }
        else if (currentState == EnemyState.Attack)
        {
            AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsTag("Attack") && stateInfo.normalizedTime < attackRotateDuration)
            {
                canRotate = true;
                if (_target != null) targetDir = _target.position - transform.position;
            }
        }

        if (canRotate && targetDir.sqrMagnitude > 0.01f)
        {
            targetDir.y = 0;
            if (targetDir != Vector3.zero)
            {
                Quaternion lookRot = Quaternion.LookRotation(targetDir);
                float turnSpeed = (currentState == EnemyState.Attack) ? 20f : 10f;
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * turnSpeed);
            }
        }

        Vector3 rootMotionVelocity = _animator.deltaPosition / Time.deltaTime;
        if (_isJumpBoosting) rootMotionVelocity *= jumpBoostMultiplier;
        _agent.velocity = rootMotionVelocity;
    }

    public void OnTakeDamage()
    {
        ChangeState(EnemyState.Hit);
    }

    private void OnDie()
    {
        ChangeState(EnemyState.Die);
    }

    // Animation Events
    public void OnHitEnd() => ChangeState(EnemyState.Chase); // 맞고 나면 다시 바로 추격
    public void OnAttackEnd() => ChangeState(EnemyState.Chase);
    public void StartJumpBoost() => _isJumpBoosting = true;
    public void EndJumpBoost() => _isJumpBoosting = false;
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