using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5.0f;
    public float sprintSpeed = 8.0f;
    public float rotationSpeed = 15.0f;
    public float gravity = -20.0f;
    public float jumpHeight = 1.2f;

    [Header("References")]
    public Transform cameraTransform;
    public Animator animator;
    // 내 몸상태 스크립트 연결
    private CharacterStats _stats;

    [Header("Combat")]
    public Weapon myWeapon; // 무기 스크립트 연결용

    // 내부 변수
    private CharacterController _controller;
    private PlayerControls _inputActions;
    private Vector2 _inputMove;
    private Vector3 _verticalVelocity;
    private bool _isGrounded;
    
    // 상태 체크용 변수
    private bool _isBusy;
    private bool _isAttacking;
    private bool _isRolling;
    private bool _isParrying;
    private bool _isDead = false;
    private bool _isHit = false;

    // 애니메이션 해시
    private static readonly int AnimID_Speed = Animator.StringToHash("speed");
    private static readonly int AnimID_IsGrounded = Animator.StringToHash("isGrounded");
    private static readonly int AnimID_Jump = Animator.StringToHash("jump");
    private static readonly int AnimID_DoAttack = Animator.StringToHash("doAttack");
    private static readonly int AnimID_Roll = Animator.StringToHash("doRoll");
    private static readonly int AnimID_IsDead = Animator.StringToHash("isDead");
    private static readonly int AnimID_DoHit = Animator.StringToHash("doHit");
    private static readonly int AnimID_Parry = Animator.StringToHash("doParry");

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
            _stats.OnTakeDamage.AddListener(OnHit); // 맞으면 OnHit 실행
            _stats.OnDeath.AddListener(OnDie);      // 죽으면 OnDie 실행
        }
    }

    private void OnDisable()
    {
        _inputActions.Player.Disable();
        _inputActions.Player.Jump.performed -= OnJump;
        _inputActions.Player.Attack.performed -= OnAttack;
        _inputActions.Player.Roll.performed -= OnRoll;
        _inputActions.Player.Parry.performed -= OnParry;
        // 연결 해제 
        if (_stats != null)
        {
            _stats.OnTakeDamage.RemoveListener(OnHit);
            _stats.OnDeath.RemoveListener(OnDie);
        }
    }

    private void Update()
    {
        if (_isDead) return; // 죽었으면 아무것도 안 함

        _isGrounded = _controller.isGrounded;

        // 중력 계산
        if (_isGrounded && _verticalVelocity.y < 0)
        {
            _verticalVelocity.y = -2f;
        }
        _verticalVelocity.y += gravity * Time.deltaTime;

        // 공격 상태 매 프레임 갱신
        CheckActionState();

        // 공격 중이면 Root Motion을 켜서 애니메이션이 이동을 주도하게 하고,
        // 공격이 아니면 꺼서 코드가 이동을 주도하게 함.
        if (animator != null)
        {
            animator.applyRootMotion = _isBusy;
        }

        // 이동 처리
        Move();
    }

    // 현재 애니메이션 태그로 확인
    private void CheckActionState() 
    {
        if (animator == null) return;

        // 1. 현재 애니메이터 상태 정보 가져오기
        var stateInfo = animator.GetCurrentAnimatorStateInfo(0);

        // 2. 각 태그별 상태 저장 (멤버 변수에 기록)
        _isAttacking = stateInfo.IsTag("Attack");
        _isRolling   = stateInfo.IsTag("Roll");
        _isParrying  = stateInfo.IsTag("Parry");

        // 3. 통합 상태 갱신 (하나라도 하고 있으면 바쁜 거임)
        _isBusy = _isAttacking || _isRolling || _isParrying;
    }

    private void Move()
    {
        if(_isHit) return;
        
        // 1. 입력값 읽기 (공격 중이어도 방향키 입력은 받음 -> 회전을 위해)
        _inputMove = _inputActions.Player.Move.ReadValue<Vector2>();
        bool isSprinting = _inputActions.Player.Sprint.IsPressed();


        // 2. 방향 벡터 계산
        Vector3 moveDirection = Vector3.zero;
        if (_inputMove != Vector2.zero)
        {
            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;
            forward.y = 0f; right.y = 0f;
            forward.Normalize(); right.Normalize();

            moveDirection = (forward * _inputMove.y + right * _inputMove.x).normalized;
        }

        // ====================================================
        // ★ [핵심] 회전(Rotation) 로직 분리
        // ====================================================
        
        if (_isRolling || _isParrying)
        {
            // Case A: 구르기 & 패링 -> "절대 회전 금지"
            // (입력 방향 무시하고 가던 방향 그대로 감)
        }
        else if (_isAttacking)
        {
            // Case B: 공격 -> "느린 회전 허용 (Tracking)"
            // (공격 중에 방향키를 누르면 그쪽으로 천천히 틂)
            if (_inputMove != Vector2.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                // 평소보다 5배 느리게(0.2f) 회전시켜서 묵직함 부여
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, (rotationSpeed * 0.2f) * Time.deltaTime);
            }
        }
        else
        {
            // Case C: 평상시 -> "빠릿빠릿한 회전"
            if (_inputMove != Vector2.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
        // ====================================================
        // ★ 이동(Position) 로직 분리
        // ====================================================

        if (_isBusy)
        {
            // 공격, 구르기, 패링 중에는 "코드로 이동 금지"
            // (대신 Root Motion이 캐릭터를 밀어줌)
        }
        else
        {
            // 평상시엔 코드로 이동
            float targetSpeed = (_inputMove == Vector2.zero) ? 0.0f : (_inputActions.Player.Sprint.IsPressed() ? sprintSpeed : moveSpeed);
            Vector3 horizontalMove = moveDirection * targetSpeed;
            Vector3 finalMove = horizontalMove + _verticalVelocity;

            _controller.Move(finalMove * Time.deltaTime);
        }

        // 4. 애니메이션 파라미터 업데이트
        if (animator != null)
        {
            Vector3 horizontalVelocity = new Vector3(_controller.velocity.x, 0, _controller.velocity.z);
            float currentSpeed = horizontalVelocity.magnitude;
            animator.SetFloat(AnimID_Speed, currentSpeed, 0.1f, Time.deltaTime);
            animator.SetBool(AnimID_IsGrounded, _isGrounded);
        }
    }

    // Animator의 "Apply Root Motion"이 체크되어 있으면 이 함수가 자동으로 실행됨
    private void OnAnimatorMove()
    {
        // 공격 중에만 애니메이션의 움직임(Root Motion)을 적용
        if (_isBusy && _controller != null && animator != null)
        {
            // 1. 애니메이션이 이번 프레임에 움직인 거리(deltaPosition)를 가져옴
            Vector3 rootMotion = animator.deltaPosition;

            // 2. Y축(중력)은 별도로 계산한 값을 덮어씌움 (안 그러면 공중부양 함)
            rootMotion.y = _verticalVelocity.y * Time.deltaTime;

            // 3. 캐릭터 컨트롤러를 통해 이동시킴
            _controller.Move(rootMotion);
        }
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        // 공격 중엔 점프 불가
        if (_isGrounded && !_isBusy)
        {
            _verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            if (animator != null) animator.SetTrigger(AnimID_Jump);
        }
    }

    private void OnAttack(InputAction.CallbackContext context)
    {
        if (_isGrounded && animator != null)
        {
            animator.SetTrigger(AnimID_DoAttack);
        }
    }
    // 구르기 입력 처리
    private void OnRoll(InputAction.CallbackContext context)
    {
        // 땅에 있고, 다른 행동 중이 아닐 때만 구름 (캔슬 구르기는 나중에 구현)
        if (_isGrounded && !_isBusy)
        {
            // 구르기 직전에, 입력한 방향을 바라보게 강제로 돌려줌 (중요!)
            // 안 그러면 뒤로 구르려는데 앞으로 구르는 참사가 일어남
            if (_inputMove != Vector2.zero)
            {
                Vector3 forward = cameraTransform.forward; Vector3 right = cameraTransform.right;
                forward.y = 0; right.y = 0;
                Vector3 targetDir = (forward * _inputMove.y + right * _inputMove.x).normalized;
                transform.rotation = Quaternion.LookRotation(targetDir);
            }

            animator.SetTrigger(AnimID_Roll);
        }
    }
    // 패링 입력 처리
    private void OnParry(InputAction.CallbackContext context)
    {
        if (_isGrounded && !_isBusy)
        {
            animator.SetTrigger(AnimID_Parry);
        }
    }
    // 죽었을 때 실행될 함수
    private void OnDie()
    {
        _isDead = true;
        animator.SetBool(AnimID_IsDead, true);// 사망 애니메이션 재생 (나중에 추가)
        _inputActions.Player.Disable(); // 조작 불능 만들기
    }
    private void OnHit()
    {
        // 죽은 상태면 피격 모션 재생 안 함 (사망 모션이 우선)
        if (_isDead) return;

        // 애니메이터에게 "피격 모션 재생해!" 명령
        animator.SetTrigger(AnimID_DoHit);
        _isHit = true;
    }
    public void EndHit()
    {
        _isHit = false;
    }

    // 무기 애니메이션
    // 애니메이션 이벤트용 함수 (무기 켜기)
    public void WeaponEnable()
    {
        if (myWeapon != null) myWeapon.EnableHitbox();
    }
    // 애니메이션 이벤트용 함수 (무기 끄기)
    public void WeaponDisable()
    {
        if (myWeapon != null) myWeapon.DisableHitbox();
    }
}