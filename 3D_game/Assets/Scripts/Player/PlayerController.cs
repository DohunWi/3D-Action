using UnityEngine;
using UnityEngine.InputSystem;

// 플레이어 상태 정의
public enum PlayerState
{
    Locomotion, // 대기 및 이동
    Roll,       // 구르기 (무적)
    Attack,     // 공격
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

    [Header("References")]
    public Transform cameraTransform;
    public Animator animator;
    private CharacterStats _stats;

    [Header("Combat")]
    public Weapon myWeapon;

    [Header("Combo Settings")]
    private bool _comboInputReceived = false; // 공격 중 입력이 들어왔는가?
    private int _comboStep = 0;               // 현재 몇 번째 타격인가? (0, 1, 2...)
    public int maxComboCount = 3; // 콤보가 3개라면 인덱스는 0, 1, 2

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

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _inputActions = new PlayerControls();
        _stats = GetComponent<CharacterStats>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    private void OnEnable()
    {
        _inputActions.Player.Enable();
        _inputActions.Player.Jump.performed += OnJump;
        _inputActions.Player.Attack.performed += OnAttack;
        _inputActions.Player.Roll.performed += OnRoll;
        _inputActions.Player.Parry.performed += OnParry;

        if (_stats != null)
        {
            _stats.OnTakeDamage.AddListener(OnHit);
            _stats.OnDeath.AddListener(OnDie);
        }
    }

    private void OnDisable()
    {
        _inputActions.Player.Disable();
        _inputActions.Player.Jump.performed -= OnJump;
        _inputActions.Player.Attack.performed -= OnAttack;
        _inputActions.Player.Roll.performed -= OnRoll;
        _inputActions.Player.Parry.performed -= OnParry;

        if (_stats != null)
        {
            _stats.OnTakeDamage.RemoveListener(OnHit);
            _stats.OnDeath.RemoveListener(OnDie);
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

        // 3. 이동
        float targetSpeed = (_inputMove == Vector2.zero) ? 0.0f : (_inputActions.Player.Sprint.IsPressed() ? sprintSpeed : moveSpeed);
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
            Vector3 rootMotion = animator.deltaPosition;
            rootMotion.y = _verticalVelocity.y * Time.deltaTime; // 중력 적용
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
            ChangeState(PlayerState.Roll);
        }
    }

    private void OnAttack(InputAction.CallbackContext context)
    {
        if (!_isGrounded) return; // 공중 공격 제외

        // 1. 대기/이동 중일 때 -> 첫 공격 시작
        if (currentState == PlayerState.Locomotion)
        {
            _comboStep = 0; // 콤보 초기화
            _comboInputReceived = false;
            ChangeState(PlayerState.Attack);
        }
        // 2. 이미 공격 중일 때 -> 다음 콤보 예약
        else if (currentState == PlayerState.Attack)
        {
            // 아직 콤보 입력이 안 들어왔다면 접수
            if (!_comboInputReceived)
            {
                Debug.Log($"콤보 예약됨! (Step: {_comboStep + 1})");
                _comboInputReceived = true;
            }
        }
    }

    private void OnParry(InputAction.CallbackContext context)
    {
        if (currentState == PlayerState.Locomotion && _isGrounded)
        {
            ChangeState(PlayerState.Parry);
        }
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
            // [수정된 로직]
            // 입력이 들어왔고(AND) + 현재 스텝이 마지막이 아닐 때만 다음 콤보로!
            // 예: 3타 공격이면, step 0 -> 1(가능), 1 -> 2(가능), 2 -> 3(불가능, 종료)
            if (_comboInputReceived && _comboStep < maxComboCount - 1)
            {
                _comboStep++;
                _comboInputReceived = false;

                // 다음 공격 실행
                animator.SetInteger(AnimID_ComboStep, _comboStep);
                animator.SetTrigger(AnimID_DoAttack);
            }
            else
            {
                // 입력이 없거나, 이미 막타(2타)까지 다 쳤으면 -> 종료
                _comboStep = 0; // (선택) 여기서 초기화해주면 안전함
                ChangeState(PlayerState.Locomotion);
            }
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