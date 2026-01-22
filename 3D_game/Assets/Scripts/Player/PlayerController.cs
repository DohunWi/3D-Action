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

    // 내부 변수
    private CharacterController _controller;
    private PlayerControls _inputActions;
    private Vector2 _inputMove;
    private Vector3 _verticalVelocity;
    private bool _isGrounded;
    
    // 상태 체크용 변수
    private bool _isAttacking;

    // 애니메이션 해시
    private static readonly int AnimID_Speed = Animator.StringToHash("Speed");
    private static readonly int AnimID_IsGrounded = Animator.StringToHash("IsGrounded");
    private static readonly int AnimID_Jump = Animator.StringToHash("Jump");
    private static readonly int AnimID_DoAttack = Animator.StringToHash("doAttack");

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _inputActions = new PlayerControls();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    private void OnEnable()
    {
        _inputActions.Player.Enable();
        _inputActions.Player.Jump.performed += OnJump;
        _inputActions.Player.Attack.performed += OnAttack;
    }

    private void OnDisable()
    {
        _inputActions.Player.Disable();
        _inputActions.Player.Jump.performed -= OnJump;
        _inputActions.Player.Attack.performed -= OnAttack;
    }

    private void Update()
    {
        _isGrounded = _controller.isGrounded;

        // 중력 계산
        if (_isGrounded && _verticalVelocity.y < 0)
        {
            _verticalVelocity.y = -2f;
        }
        _verticalVelocity.y += gravity * Time.deltaTime;

        // 공격 상태 매 프레임 갱신
        CheckAttackState();

        // 공격 중이면 Root Motion을 켜서 애니메이션이 이동을 주도하게 하고,
        // 공격이 아니면 꺼서 코드가 이동을 주도하게 함.
        if (animator != null)
        {
            animator.applyRootMotion = _isAttacking;
        }

        // 이동 처리
        Move();
    }

    // 현재 공격 중인지 태그로 확인
    private void CheckAttackState()
    {
        if (animator != null)
        {
            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            _isAttacking = stateInfo.IsTag("Attack");
        }
        else
        {
            _isAttacking = false;
        }
    }

    private void Move()
    {
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

        // 3. 이동 실행 분기 (★ 핵심 로직)
        if (_isAttacking)
        {
            // [공격 중일 때]
            // 코드로 이동하지 않음 -> _controller.Move 호출 안 함
            // 대신 아래 OnAnimatorMove()가 호출되어 애니메이션이 캐릭터를 밈.
            
            // 단, 회전은 허용 (소울류처럼 공격 방향 조절)
            if (_inputMove != Vector2.zero)
            {
                // 공격 중엔 회전을 약간 느리게 해서 무게감 주기 (0.5배)
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, (rotationSpeed * 0.5f) * Time.deltaTime);
            }
        }
        else
        {
            // [평소 상태]
            // 코드로 직접 이동 시킴 (W,A,S,D)
            float targetSpeed = (_inputMove == Vector2.zero) ? 0.0f : (isSprinting ? sprintSpeed : moveSpeed);
            Vector3 horizontalMove = moveDirection * targetSpeed;
            Vector3 finalMove = horizontalMove + _verticalVelocity; // 중력 포함

            _controller.Move(finalMove * Time.deltaTime);

            // 평소 회전
            if (_inputMove != Vector2.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
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
        if (_isAttacking && _controller != null && animator != null)
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
        if (_isGrounded && !_isAttacking)
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
}