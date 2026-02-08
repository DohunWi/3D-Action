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
    private NavMeshAgent _agent;
    private Animator _animator;
    private CharacterStats _stats;

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
    private float _lastAttackTime;
    private Transform _target;

    [Header("Combo Attack")]
    // 공격 순서: 1(왼손) -> 0(오른손) -> 2(점프)
    private int[] _comboSequence = { 3, 4, 0, 1, 2 }; 
    private int _currentComboStep = 0;
    private bool _isComboActive = false;
    private float _stopRotationTime;

    [Header("Special Pattern (Fire Breath)")]
    public bool useFireBreath = false; 
    public float fireBreathCooldown = 10.0f;
    private float _lastFireBreathTime;

    [Header("Poise (Super Armor)")]
    public float maxPoise = 50f;      // 최대 강인도 (높을수록 잘 안 넘어짐)
    public float poiseRecoveryTime = 5.0f; // 비전투 시 회복 시작 시간
    private float _currentPoise;
    private float _lastDamageTime;

    [Header("Down State")]
    public float downDuration = 2.0f; // 누워있는 시간
    private static readonly int AnimID_Down = Animator.StringToHash("doDown"); // 다운 애니메이션
    private static readonly int AnimID_GetUp = Animator.StringToHash("doGetUp"); // 일어나는 애니메이션 (없으면 생략 가능)
    private Coroutine _downCoroutine;

    [Header("Jump Attack Boost")]
    public float jumpBoostMultiplier = 2.0f;
    private bool _isJumpBoosting = false;

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

        _startPosition = transform.position;
        _currentPoise = maxPoise; // 강인도 초기화
        _lastAttackTime = -attackCooldown;

        ChangeState(EnemyState.Patrol);

        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        _idleTimer = Random.Range(minIdleTime, maxIdleTime);
    }

    private void OnEnable()
    {
        if (_stats != null)
        {
            _stats.OnTakeDamage += OnTakeDamage; // 여기서 연결됨
            _stats.OnDeath += OnDie;
        }
    }

    private void OnDisable()
    {
        if (_stats != null)
        {
            _stats.OnTakeDamage -= OnTakeDamage;
            _stats.OnDeath -= OnDie;
        }
    }

    private void Update()
    {
        if (currentState == EnemyState.Die) return;
        
        // 강인도 자동 회복 (맞지 않고 일정 시간 지나면)
        if (Time.time > _lastDamageTime + poiseRecoveryTime)
        {
            if (currentState != EnemyState.Hit && currentState != EnemyState.Die && currentState != EnemyState.Parried)
            {
                _currentPoise = Mathf.MoveTowards(_currentPoise, maxPoise, Time.deltaTime * 10f);
            }
        }

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
                _isJumpBoosting = false;
                
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
                _agent.isStopped = true;
                _agent.velocity = Vector3.zero;
                _animator.SetTrigger(AnimID_Hit);
                // 강인도 파괴되어 경직됐으므로 콤보 초기화
                _isComboActive = false;
                _currentComboStep = 0;
                break;
            
            case EnemyState.Down:
                _agent.isStopped = true;        // 이동 정지
                _agent.velocity = Vector3.zero;
                _animator.SetTrigger(AnimID_Down); // 넘어지는 애니메이션 재생
                // 강인도 초기화 (일어날 때 다시 쌩쌩하게)
                _currentPoise = maxPoise;
                
                DisableAllWeapons(); // 공격 판정 끄기
                
                // 일정 시간 뒤에 일어나는 코루틴 시작
                _downCoroutine = StartCoroutine(RecoverFromDown());
                break;

            case EnemyState.Parried:
                _agent.isStopped = true;        
                _agent.velocity = Vector3.zero; 
                _animator.SetTrigger(AnimID_Parried); 
                DisableAllWeapons();
                // 패링당하면 당연히 콤보 끊김
                _isComboActive = false;
                _currentComboStep = 0;
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
    private void UpdateChase()
    {
        if (_isComboActive) return;
        float distance = Vector3.Distance(transform.position, _target.position);
        
        if (distance > chaseGiveUpRange)
        {
            ChangeState(EnemyState.Patrol);
            return;
        }

        // 공격 쿨타임 체크 (콤보 종료 후 대기 시간)
        if (Time.time >= _lastAttackTime + attackCooldown)
        {
            // 1. 불 뿜기 (나중에 구현할 특수 패턴)
            if (useFireBreath && Time.time >= _lastFireBreathTime + fireBreathCooldown)
            {
                if (distance <= 5.0f) 
                {
                    // StartFireBreath(); // TODO: 구현 필요
                    // _lastFireBreathTime = Time.time;
                    // return;
                }
            }

            // 2. 콤보 공격 시작 (사거리 내 진입)
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
            // 거리 체크 로직
            float dist = Vector3.Distance(transform.position, _target.position);
            // "공격 사거리 + 약간의 여유(1.0m)"보다 멀어졌다면?
            if (dist > attackRange + 1.0f)
            {
                // 콤보 중단
                EndCombo(); 
                return;
            }

            _currentComboStep++; // 다음 단계로

            // 콤보가 남았으면 계속 공격
            if (_currentComboStep < _comboSequence.Length)
            {
                // (선택) 플레이어가 너무 멀어졌으면 콤보 중단? -> 일단 소울류처럼 헛치더라도 끝까지 하게 둠
                ProcessComboStep();
            }
            else
            {
                EndCombo();
            }
        }
        else
        {
            // 단발성 공격이나 다른 상황이었다면 복귀
            ChangeState(EnemyState.Chase);
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
    private void OnDie()
    {
        ChangeState(EnemyState.Die);
    }

    // --- Damage & Poise System (슈퍼아머) ---
    public void OnTakeDamage()
    {
        if (currentState == EnemyState.Die) return;

        if (currentState == EnemyState.Down) 
        {
            // 데미지는 들어가지만 상태 변경은 안 함
            // (연출을 위해 몸이 움찔거리는 정도는 괜찮음)
            return; 
        }
        // 1. 피격 시간 기록
        _lastDamageTime = Time.time;

        // 2. 강인도 감소 (기본 10 감소로 가정)
        _currentPoise -= 20f; 

        // 3. 강인도 체크
        if (_currentPoise <= 0)
        {
            // 강인도 파괴! -> 경직 발생
            _currentPoise = maxPoise; // 초기화
            ChangeState(EnemyState.Hit);
            // Debug.Log("강인도 파괴! 경직!");
        }
        else
        {
            // 슈퍼아머 발동! (상태 안 바꿈 = 공격 안 끊김)
            // 대신 시각적 피드백 제공 (빨간색 깜빡임)
            StartCoroutine(FlashRed());
            // Debug.Log($"슈퍼아머! 남은 강인도: {_currentPoise}");
        }
    }

    private IEnumerator FlashRed()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers) r.material.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        foreach (var r in renderers) r.material.color = Color.white;
    }

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
                Vector3 rootMotionVelocity = _animator.deltaPosition / Time.deltaTime;
                if (_isJumpBoosting) rootMotionVelocity *= jumpBoostMultiplier;
                _agent.velocity = rootMotionVelocity;
            }
            else
            {
                _agent.velocity = Vector3.zero; // 시간 정지 시 멈춤
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

    // --- Animation Events (무기 콜라이더) ---
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