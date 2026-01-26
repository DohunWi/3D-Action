using UnityEngine;
using UnityEngine.AI;

public class Enemy : MonoBehaviour 
{
    [Header("Components")]
    private NavMeshAgent _agent;
    private Animator _animator;     // [NEW] 애니메이터
    private CharacterStats _stats;  // 체력 관리

    [Header("Combat Settings")]
    public float attackRange = 1.5f;      // 기본 공격 사거리 (주먹)
    public float jumpAttackRange = 4.0f;  // 점프 공격 사거리 (멀 때)
    public float attackCooldown = 2.0f;
    [Header("Jump Attack Boost")]
    public float jumpBoostMultiplier = 2.0f; // 1.0이면 원래 거리, 2.0이면 2배
    private bool _isJumpAttacking = false;   // 지금 점프 공격 중인가?

    // 손 공격도 있으니 왼손 무기도 필요함!
    public Weapon leftWeapon;  // 왼손 히트박스
    public Weapon rightWeapon; // 오른손 히트박스 
    public Weapon jumpAtaackWave; // 점프 공격 히트박스

    private float _lastAttackTime;
    private Transform _target;      // 플레이어
    private bool _isDead = false;
    private bool _isAttacking = false; // 공격 중인지 확인하는 플래그

    // 애니메이션 ID 캐싱 (성능 최적화)
    private static readonly int AnimID_Speed = Animator.StringToHash("speed");
    private static readonly int AnimID_Attack = Animator.StringToHash("doAttack");
    private static readonly int AnimID_Hit = Animator.StringToHash("doHit");
    private static readonly int AnimID_Die = Animator.StringToHash("doDie");
    // 애니메이션 ID
    private static readonly int AnimID_AttackIndex = Animator.StringToHash("attackIndex");

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _animator = GetComponent<Animator>();
        _stats = GetComponent<CharacterStats>();
    }

    private void Start()
    {
        // 플레이어 찾기
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) _target = player.transform;

        // 무기에 데미지 설정 (Stats에 있는 값이나 별도 변수 활용 가능)
        if (leftWeapon != null) leftWeapon.damage = 10f;
        if (rightWeapon != null) rightWeapon.damage = 10f; 
    }

    private void OnEnable()
    {
        // Stats 이벤트 구독
        if (_stats != null)
        {
            _stats.OnTakeDamage.AddListener(OnHit);
            _stats.OnDeath.AddListener(OnDie);
        }
    }

    private void OnDisable()
    {
        if (_stats != null)
        {
            _stats.OnTakeDamage.RemoveListener(OnHit);
            _stats.OnDeath.RemoveListener(OnDie);
        }
    }

    private void Update()
    {
        if (_isDead || _target == null) return;

        // 공격 중이면 이동 로직을 아예 실행하지 않음!
        if (_isAttacking)
        {
            _agent.isStopped = true; // 확실하게 멈춤
            
            // 공격 중에도 플레이어를 계속 쳐다보게 하려면 
            // LookAtTarget(); 
            return;
        }
        // 1. 애니메이터에 속도 전달 (뛰는 모션 제어)
        float targetSpeed = _agent.desiredVelocity.magnitude;
        // _animator.SetFloat(AnimID_Speed, _agent.velocity.magnitude);
        _animator.SetFloat(AnimID_Speed, targetSpeed, 0.1f, Time.deltaTime);

        // 2. 거리 계산 및 행동 결정
        float distance = Vector3.Distance(transform.position, _target.position);

        // [패턴 1] 거리가 적당히 멀면 -> 점프 공격 (2번)
        if (distance > attackRange && distance <= jumpAttackRange)
        {
            if (Time.time >= _lastAttackTime + attackCooldown)
            {
                _agent.isStopped = true; // 점프 공격 준비
                StartAttack(2); // 2번: 점프 공격 실행
            }
            else
            {
                // 쿨타임 중이면 추격
                _agent.isStopped = false;
                _agent.SetDestination(_target.position);
            }
        }
        // [패턴 2] 거리가 가까우면 -> 평타 (0번 or 1번)
        else if (distance <= attackRange)
        {
            _agent.isStopped = true;
            LookAtTarget();

            if (Time.time >= _lastAttackTime + attackCooldown)
            {
                // 0(오른손) 또는 1(왼손) 랜덤 선택
                int randomAttack = Random.Range(0, 2); 
                StartAttack(randomAttack);
            }
        }
        else
        {
            // 너무 멀면 그냥 추격
            _agent.isStopped = false;
            _agent.SetDestination(_target.position);
        }
    }
    private void OnAnimatorMove()
    {
        // 죽었거나 Agent가 없으면 아무것도 안 함
        if (_isDead || !_agent.enabled) return;

        // 1. [회전 로직 수정] 
        // ★★★ 핵심 변경 사항: 공격 중(_isAttacking)이 아닐 때만 방향 회전 수행 ★★★
        if (!_isAttacking) 
        {
            Vector3 direction = _agent.desiredVelocity;

            // 방향이 있을 때만 회전 (제자리일 땐 회전 안 함)
            if (direction.sqrMagnitude > 0.1f)
            {
                // Y축(높이) 방향은 무시하고 수평 회전만 계산
                direction.y = 0; 
                if (direction != Vector3.zero)
                {
                    Quaternion lookRotation = Quaternion.LookRotation(direction);
                    // 부드럽게 회전 (Time.deltaTime * 회전속도)
                    transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 10f);
                }
            }
        }

        // 2. [이동 계산] 애니메이션이 가려는 속도 계산
        Vector3 rootMotionVelocity = _animator.deltaPosition / Time.deltaTime;

        // 3. 점프 공격 중일 때만 속도 뻥튀기!
        if (_isJumpAttacking)
        {
            rootMotionVelocity *= jumpBoostMultiplier;
        }

        // 4. 최종 속도를 NavMeshAgent에 주입
        // (Agent가 길 찾기 연산은 하되, 실제 이동 속도는 애니메이션을 따라가게 만듦)
        _agent.velocity = rootMotionVelocity;
    }
    // [NEW] 애니메이션 이벤트에서 부를 함수 (시작)
    public void StartJumpBoost()
    {
        _isJumpAttacking = true;
    }

    // [NEW] 애니메이션 이벤트에서 부를 함수 (끝 - 착지할 때)
    public void EndJumpBoost()
    {
        _isJumpAttacking = false;
    }
    private void StartAttack(int index)
    {
        _isAttacking = true;
        _agent.isStopped = true;
        // 1. 무슨 공격 할지 번호부터 세팅
        _animator.SetInteger(AnimID_AttackIndex, index);
        
        // 2. 공격 트리거 당기기
        _animator.SetTrigger(AnimID_Attack);
        
        _lastAttackTime = Time.time;
    }
    public void OnAttackEnd()
    {
        _isAttacking = false; // 공격 종료 (다시 이동 가능)
    }
    private void LookAtTarget()
    {
        Vector3 direction = (_target.position - transform.position).normalized;
        direction.y = 0; // 위아래로는 고개 들지 않음
        if (direction != Vector3.zero)
        {
            Quaternion lookRot = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * 5f);
        }
    }

    private void OnHit()
    {
        if (_isDead) return;
        _animator.SetTrigger(AnimID_Hit); // 피격 모션
    }

    private void OnDie()
    {
        _isDead = true;
        _agent.isStopped = true;
        _agent.enabled = false;          // 길막 방지
        GetComponent<Collider>().enabled = false; // 시체 판정 끄기

        _animator.SetTrigger(AnimID_Die); // 사망 모션

        Destroy(gameObject, 5f); // 5초 뒤 시체 삭제
    }

    // 오른손 (Animation Event: EnableRightWeapon)
    public void EnableRightWeapon() 
    {
        if (rightWeapon != null) rightWeapon.EnableHitbox();
    }
    public void DisableRightWeapon()
    {
        if (rightWeapon != null) rightWeapon.DisableHitbox();
    }

    // 왼손 (Animation Event: EnableLeftWeapon)
    public void EnableLeftWeapon()
    {
        if (leftWeapon != null) leftWeapon.EnableHitbox();
    }
    public void DisableLeftWeapon()
    {
        if (leftWeapon != null) leftWeapon.DisableHitbox();
    }
    
    // 점프 공격용 (양손 다 켜거나, 범위 넓은거 하나 켜기)
    public void EnableJumpAttack()
    {
        if (jumpAtaackWave != null) jumpAtaackWave.EnableHitbox();
    }
    public void DisableJumpAttack()
    {
        if (jumpAtaackWave != null) jumpAtaackWave.DisableHitbox();
    }
}