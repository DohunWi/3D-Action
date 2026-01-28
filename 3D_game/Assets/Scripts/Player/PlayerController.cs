using UnityEngine;
using UnityEngine.InputSystem;

// 플레이어 상태 정의
public enum PlayerState
{
    Locomotion, // 대기 및 이동
    Roll,       // 구르기 (무적)
    Attack,     // 공격
    CounterAttack, // 패링 성공 후
    Skill,      // 스킬
    Parry,      // 패링
    Hit,        // 피격
    Die         // 사망
}

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("State")]
    public PlayerState currentState = PlayerState.Locomotion;

    [Header("Movement Settings")]
    public float moveSpeed = 5.0f;
    public float sprintSpeed = 8.0f;
    public float rotationSpeed = 15.0f;
    public float gravity = -20.0f;
    public float jumpHeight = 1.2f;

    [Header("Stamina Costs")] // 행동별 소모량 설정
    public float rollStaminaCost = 20f;
    public float attackStaminaCost = 15f;
    public float sprintStaminaCost = 10f; // 달리기 (초당 소모량)

    [Header("References")]
    public Transform cameraTransform;
    public Animator animator;
    private PlayerStats _stats;

    [Header("Combat")]
    public Weapon myWeapon;
    private float initialDamage;

    [Header("Combo Settings")]
    private bool _comboInputReceived = false; // 공격 중 입력이 들어왔는가?
    private int _comboStep = 0;               // 현재 몇 번째 타격인가? (0, 1, 2...)
    public int maxComboCount = 3; // 콤보가 3개라면 인덱스는 0, 1, 2

    [Header("Skill Settings")]
    public float skillCooldown = 5.0f;        // 5초 쿨타임
    public float skillManaCost = 50.0f;       // 마나 30 소모
    public float skillDamage = 30.0f;         // 스킬 데미지
    private float _lastSkillTime = -10f;      // 마지막 사용 시간 (초기값은 즉시 사용 가능하게)

    [Header("Parry & Counter")]
    public float counterWindowDuration = 1.5f; // 패링 후 반격 가능한 시간
    private bool _canCounterAttack = false;    // 현재 반격이 가능한가?
    public float counterDamageMultiplier = 3.0f; // 반격 데미지 배율

    // 내부 변수
    private CharacterController _controller;
    private PlayerControls _inputActions;
    private Vector2 _inputMove;
    private Vector3 _verticalVelocity;
    private bool _isGrounded;

    // 애니메이션 해시
    private static readonly int AnimID_Speed = Animator.StringToHash("speed");
    private static readonly int AnimID_IsGrounded = Animator.StringToHash("isGrounded");
    private static readonly int AnimID_Jump = Animator.StringToHash("jump");
    private static readonly int AnimID_DoAttack = Animator.StringToHash("doAttack");
    private static readonly int AnimID_Roll = Animator.StringToHash("doRoll");
    private static readonly int AnimID_IsDead = Animator.StringToHash("isDead");
    private static readonly int AnimID_DoHit = Animator.StringToHash("doHit");
    private static readonly int AnimID_Parry = Animator.StringToHash("doParry");
    private static readonly int AnimID_ComboStep = Animator.StringToHash("ComboStep");
    private static readonly int AnimID_DoCounterAttack = Animator.StringToHash("doCounterAttack");
    private static readonly int AnimID_DoSkill = Animator.StringToHash("doSkill");

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _inputActions = new PlayerControls();
        _stats = GetComponent<PlayerStats>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }
    private void Start()
    {
        initialDamage = myWeapon.damage; // 초기 데미지 저장
    }
    private void OnEnable()
    {
        _inputActions.Player.Enable();
        _inputActions.Player.Jump.performed += OnJump;
        _inputActions.Player.Attack.performed += OnAttack;
        _inputActions.Player.Roll.performed += OnRoll;
        _inputActions.Player.Parry.performed += OnParry;
        _inputActions.Player.Skill.performed += OnSkill;

        if (_stats != null)
        {
            _stats.OnTakeDamage += OnHit;
            _stats.OnDeath += OnDie;
        }
    }

    private void OnDisable()
    {
        _inputActions.Player.Disable();
        _inputActions.Player.Jump.performed -= OnJump;
        _inputActions.Player.Attack.performed -= OnAttack;
        _inputActions.Player.Roll.performed -= OnRoll;
        _inputActions.Player.Parry.performed -= OnParry;
        _inputActions.Player.Skill.performed -= OnSkill;

        if (_stats != null)
        {
            _stats.OnTakeDamage -= OnHit;
            _stats.OnDeath -= OnDie;
        }
    }

    private void Update()
    {
        if (currentState == PlayerState.Die) return;

        _isGrounded = _controller.isGrounded;

        // 중력 계산 (공통)
        if (_isGrounded && _verticalVelocity.y < 0)
        {
            _verticalVelocity.y = -2f;
        }
        _verticalVelocity.y += gravity * Time.deltaTime;

        // 입력값 읽기
        _inputMove = _inputActions.Player.Move.ReadValue<Vector2>();

        // 상태별 업데이트
        switch (currentState)
        {
            case PlayerState.Locomotion:
                UpdateLocomotion();
                break;
            
            // 공격, 구르기, 패링 중에는 '회전 보정'이 필요하면 여기서 처리
            case PlayerState.Attack:
                UpdateAttackRotation();
                break;
                
            case PlayerState.Roll:
            case PlayerState.Parry:
            case PlayerState.Hit:
                // 이 상태들은 루트모션이 이동을 전담하므로 Update 로직은 비워둠
                break;
        }
        
        // 애니메이션 파라미터 갱신 (땅 체크 등)
        animator.SetBool(AnimID_IsGrounded, _isGrounded);
    }

    // ★★★ 핵심: 상태 변경 함수 ★★★
    public void ChangeState(PlayerState newState)
    {
        if (currentState == PlayerState.Die) return;
        if (currentState == newState) return;

        // [Exit] 상태 나갈 때
        switch (currentState)
        {
            case PlayerState.Skill:
                WeaponDisable();
                // 데미지 원상복구 (안전을 위해 배율 나누기 대신 원래값 복구 방식 추천)
                if (myWeapon != null) myWeapon.damage = initialDamage;
                break;

            case PlayerState.CounterAttack:
                WeaponDisable();
                // 무기 데미지 원상복구 
                if (myWeapon != null) myWeapon.damage = initialDamage;
                break;
            case PlayerState.Attack:
                WeaponDisable(); // 공격 끊기면 무기 끄기
                break;
        }

        currentState = newState;

        // [Enter] 상태 들어올 때
        switch (currentState)
        {
            case PlayerState.Locomotion:
                animator.applyRootMotion = false; // 직접 코드로 이동
                // 대기 상태로 복귀 시 콤보 관련 모든 변수/파라미터 초기화
                _comboStep = 0;
                _comboInputReceived = false;
                
                // 애니메이터도 0으로 돌려놔야 다음 공격이나 트랜지션이 꼬이지 않음
                animator.SetInteger(AnimID_ComboStep, 0);
                break;

            case PlayerState.Roll:
                // 구르기 시작하면 콤보 끊기
                _comboStep = 0;
                _comboInputReceived = false;
                animator.SetInteger(AnimID_ComboStep, 0);

                animator.applyRootMotion = true; // 애니메이션 이동 사용
                animator.SetTrigger(AnimID_Roll);
                // ★ 구르기 방향 보정 (입력한 쪽을 보고 구르도록)
                RotateToInputDirection();
                break;

            case PlayerState.Skill:
                _lastSkillTime = Time.time; // 쿨타임 갱신
                
                animator.applyRootMotion = true;
                animator.SetTrigger(AnimID_DoSkill); // 점프 공격 애니메이션

                // 데미지 뻥튀기
                if (myWeapon != null) myWeapon.damage = skillDamage;
                break;

            case PlayerState.CounterAttack:
                animator.applyRootMotion = true;
                
                // 반격 애니메이션 재생
                animator.SetTrigger(AnimID_DoCounterAttack);

                // ★ 데미지 뻥튀기
                if (myWeapon != null) myWeapon.damage *= counterDamageMultiplier;

                // 반격 기회 소모 (한 번만 때리게)
                _canCounterAttack = false;
                CancelInvoke(nameof(ResetCounterWindow));
                break;

            case PlayerState.Attack:
                animator.applyRootMotion = true;
                
                // ★ 콤보 단계에 따라 다른 애니메이션 재생
                // (Animator에 파라미터로 "ComboStep" int형이나, 각각의 Trigger가 필요함)
                animator.SetInteger(AnimID_ComboStep, _comboStep); 
                animator.SetTrigger(AnimID_DoAttack);
                
                // 공격 시작할 때 콤보 입력 초기화 (이번 공격에 대한 입력을 새로 받아야 하니까)
                _comboInputReceived = false; 
                break;

            case PlayerState.Parry:
                animator.applyRootMotion = true;
                animator.SetTrigger(AnimID_Parry);
                break;

            case PlayerState.Hit:
                // 시작하면 콤보 끊기
                _comboStep = 0;
                _comboInputReceived = false;
                animator.SetInteger(AnimID_ComboStep, 0);
                animator.applyRootMotion = true;
                animator.SetTrigger(AnimID_DoHit);
                WeaponDisable(); // 공격하다 맞으면 무기 끄기
                break;

            case PlayerState.Die:
                animator.applyRootMotion = true;
                animator.SetBool(AnimID_IsDead, true);
                _inputActions.Player.Disable(); // 조작 차단
                break;
        }
    }

    // --- 상태별 Update 로직 ---

    private void UpdateLocomotion()
    {
        // 1. 방향 벡터 계산
        Vector3 moveDirection = Vector3.zero;
        if (_inputMove != Vector2.zero)
        {
            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;
            forward.y = 0f; right.y = 0f;
            forward.Normalize(); right.Normalize();
            moveDirection = (forward * _inputMove.y + right * _inputMove.x).normalized;
        }

        // 2. 회전
        if (moveDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // 이동 속도 결정 로직
        float targetSpeed = 0.0f;
        if (_inputMove != Vector2.zero)
        {
            bool isSprinting = _inputActions.Player.Sprint.IsPressed();
            
            // [수정] 달리기 스태미나 처리
            if (isSprinting)
            {
                // 지속 소모 (deltaTime 곱해서 프레임당 소모량 계산)
                if (_stats != null && _stats.UseStamina(sprintStaminaCost * Time.deltaTime))
                {
                    targetSpeed = sprintSpeed; // 스태미나 있으면 달리기
                }
                else
                {
                    targetSpeed = moveSpeed; // 없으면 강제로 걷기
                }
            }
            else
            {
                targetSpeed = moveSpeed; // 쉬프트 안 누름
            }
        }

        Vector3 horizontalMove = moveDirection * targetSpeed;
        Vector3 finalMove = horizontalMove + _verticalVelocity;

        _controller.Move(finalMove * Time.deltaTime);

        // 4. 애니메이션 속도
        Vector3 horizontalVelocity = new Vector3(_controller.velocity.x, 0, _controller.velocity.z);
        animator.SetFloat(AnimID_Speed, horizontalVelocity.magnitude, 0.1f, Time.deltaTime);
    }

    // 공격 중에는 느리게 방향 전환 (Tracking)
    private void UpdateAttackRotation()
    {
        if (_inputMove != Vector2.zero)
        {
            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;
            forward.y = 0f; right.y = 0f;
            Vector3 dir = (forward * _inputMove.y + right * _inputMove.x).normalized;

            if (dir != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, (rotationSpeed * 0.2f) * Time.deltaTime);
            }
        }
    }

    // --- 루트 모션 처리 (Locomotion 아닐 때만 작동) ---
    private void OnAnimatorMove()
    {
        // Locomotion 상태가 아닐 때(공격, 구르기 등)는 애니메이션이 이동을 주도
        if (currentState != PlayerState.Locomotion && _controller != null && animator != null)
        {
            // 1. 애니메이션이 이동하려는 양(Delta Position)을 가져옴
            Vector3 rootMotion = animator.deltaPosition;

            // 2. ★ [핵심] 카운터 어택 상태라면 이동량을 줄임
            if (currentState == PlayerState.CounterAttack)
            {
                rootMotion *= 0.5f; // 0.5배
            }

            // 3. 중력 적용 (Y축은 애니메이션 무시하고 중력 법칙 따름)
            rootMotion.y = _verticalVelocity.y * Time.deltaTime; 

            // 4. 최종 이동 적용
            _controller.Move(rootMotion);
        }
    }

    // --- 입력 이벤트 처리 ---

    private void OnJump(InputAction.CallbackContext context)
    {
        // Locomotion 상태일 때만 점프 가능
        if (currentState == PlayerState.Locomotion && _isGrounded)
        {
            _verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            animator.SetTrigger(AnimID_Jump);
        }
    }

    private void OnRoll(InputAction.CallbackContext context)
    {
        if (currentState == PlayerState.Locomotion && _isGrounded)
        {
            // 스태미나 없으면 구르기 불가
            if (_stats != null && _stats.UseStamina(rollStaminaCost))
            {
                ChangeState(PlayerState.Roll);
            }
        }
    }

    private void OnAttack(InputAction.CallbackContext context)
    {
        if (!_isGrounded) return; // 공중 공격 제외

        // 1. 패링 성공 직후라면 -> 강력한 반격!
        if (currentState == PlayerState.Locomotion || currentState == PlayerState.Parry)
        {
            if (_canCounterAttack)
            {
                ChangeState(PlayerState.CounterAttack);
                return;
            }
        }

        // 2. 대기/이동 중일 때 -> 첫 공격 시작
        if (currentState == PlayerState.Locomotion)
        {
            if (_stats.UseStamina(attackStaminaCost)) // 즉시 소모
            {
                _comboStep = 0;
                _comboInputReceived = false;
                ChangeState(PlayerState.Attack);
            }
        }
        // 3. 이미 공격 중일 때 -> 다음 콤보 예약
        else if (currentState == PlayerState.Attack)
        {
            if (!_comboInputReceived)
            {
                // [수정] UseStamina 대신 HasStamina로 확인만!
                // "지금 당장 안 깎고, 나중에 때릴 때 깎을게. 근데 잔고는 있지?"
                if (_stats.HasStamina(attackStaminaCost)) 
                {
                    Debug.Log("콤보 예약됨 (스태미나 아직 안 깎음)");
                    _comboInputReceived = true;
                }
            }
        }
    }
    private void OnSkill(InputAction.CallbackContext context)
    {
        // 땅에 있고, 이동 중일 때만 가능 (공격 캔슬 스킬을 원하면 조건 완화 가능)
        if (currentState != PlayerState.Locomotion || !_isGrounded) return;

        // 1. 쿨타임 체크
        if (Time.time < _lastSkillTime + skillCooldown)
        {
            Debug.Log($"스킬 쿨타임! ({_lastSkillTime + skillCooldown - Time.time:F1}초 남음)");
            return;
        }

        // 2. 마나 체크
        if (_stats != null && _stats.UseMana(skillManaCost))
        {
            ChangeState(PlayerState.Skill);
        }
    }
    public void OnSkillImpact()
{
    float impactRadius = 3.0f; // 반경 3미터
    float damage = 0;

    if (myWeapon != null) damage = myWeapon.damage; // 이미 뻥튀기된 데미지 가져옴

    // 1. 이펙트 생성 (있다면)
    // Instantiate(skillVFX, transform.position + transform.forward, Quaternion.identity);

    // 2. 범위 내 적 찾기
    Collider[] hitColliders = Physics.OverlapSphere(transform.position, impactRadius);
    foreach (var hit in hitColliders)
    {
        if (hit.CompareTag("Enemy"))
        {
            var enemyStats = hit.GetComponent<CharacterStats>();
            if (enemyStats != null)
            {
                // 범위 데미지 적용
                enemyStats.TakeDamage(damage, transform);
                
                // (선택) 적에게 강한 넉백이나 띄우기 효과를 주면 더 좋음!
            }
        }
    }
    
    // 화면 흔들림(Camera Shake) 효과를 여기서 호출하면 완벽함
}
    private void OnParry(InputAction.CallbackContext context)
    {
        if (currentState == PlayerState.Locomotion && _isGrounded)
        {
            ChangeState(PlayerState.Parry);
        }
    }
    // ★ Stats에서 패링 성공 시 호출할 함수
    public void OnParrySuccess()
    {
        Debug.Log("<color=yellow>반격 기회 포착! (Counter Ready)</color>");
        _canCounterAttack = true;
        
        // 일정 시간 뒤에 기회 박탈
        CancelInvoke(nameof(ResetCounterWindow));
        Invoke(nameof(ResetCounterWindow), counterWindowDuration);
    }
    private void ResetCounterWindow()
    {
        _canCounterAttack = false;
        Debug.Log("반격 기회 종료...");
    }

    // --- 피격 및 무적 로직 ---

    private void OnHit()
    {
        if (currentState == PlayerState.Die) return;

        // ★ [핵심] 구르기 상태라면 무적! (데미지/피격모션 무시)
        if (currentState == PlayerState.Roll)
        {
            Debug.Log("구르기 무적(i-frame)으로 회피했습니다!");
            return;
        }

        // 그 외 상태면 피격 처리
        ChangeState(PlayerState.Hit);
    }

    private void OnDie()
    {
        ChangeState(PlayerState.Die);
        // GameManager 에게 알리기
        if (GameManager.Instance != null)
        {
            GameManager.Instance.GameOver(); 
        }
    }

    // --- Helper Functions ---

    // 구르기 시작할 때 입력 방향으로 몸을 확 돌려주는 함수
    private void RotateToInputDirection()
    {
        if (_inputMove != Vector2.zero)
        {
            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;
            forward.y = 0; right.y = 0;
            Vector3 targetDir = (forward * _inputMove.y + right * _inputMove.x).normalized;
            transform.rotation = Quaternion.LookRotation(targetDir);
        }
    }

    // --- Animation Events (애니메이션 클립에 설정 필요) ---

    // 1. 행동 종료 -> Locomotion 복귀
    public void OnAnimationEnd()
    {
        if (currentState == PlayerState.Attack)
        {
            // 입력이 있었고, 막타가 아니라면 -> 다음 콤보 시도
            if (_comboInputReceived && _comboStep < maxComboCount - 1)
            {
                // ★ [핵심] 여기서 실제로 스태미나 소모!
                if (_stats != null && _stats.UseStamina(attackStaminaCost))
                {
                    // 결제 성공 -> 다음 공격 진행
                    _comboStep++;
                    _comboInputReceived = false;

                    animator.SetInteger(AnimID_ComboStep, _comboStep);
                    animator.SetTrigger(AnimID_DoAttack);
                }
                else
                {
                    // 결제 실패 (예약은 했는데 막상 때리려니 스태미나 부족) -> 공격 중단
                    Debug.Log("스태미나 부족으로 콤보 중단!");
                    _comboStep = 0;
                    ChangeState(PlayerState.Locomotion);
                }
            }
            else
            {
                // 입력 없거나 막타침 -> 종료
                _comboStep = 0;
                ChangeState(PlayerState.Locomotion);
            }
        }
        else if (currentState == PlayerState.CounterAttack || currentState == PlayerState.Skill)
            {
                // 반격 끝 -> 대기 상태로
                ChangeState(PlayerState.Locomotion);
            }
        else
        {
            ChangeState(PlayerState.Locomotion);
        }
    }
    
    // (기존 이벤트들 유지)
    public void EndHit() => ChangeState(PlayerState.Locomotion);
    public void WeaponEnable() => myWeapon?.EnableHitbox();
    public void WeaponDisable() => myWeapon?.DisableHitbox();
}