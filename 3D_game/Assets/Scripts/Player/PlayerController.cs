using UnityEngine;
using UnityEngine.InputSystem; // 필수

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5.0f;
    public float sprintSpeed = 8.0f;
    public float rotationSpeed = 15.0f; // 회전 속도 (부드러움 조절)
    public float gravity = -20.0f;      // 중력 (기본 물리보다 좀 더 세게)
    public float jumpHeight = 1.2f;

    [Header("References")]
    public Transform cameraTransform;   // 메인 카메라
    public Animator animator;           // Visual의 애니메이터

    // 내부 변수
    private CharacterController _controller;
    private PlayerControls _inputActions; // Input System C# 클래스
    private Vector2 _inputMove;
    private Vector3 _velocity; // 중력/점프 계산용 수직 속도
    private bool _isGrounded;

    // 애니메이션 최적화 (해싱)
    private static readonly int AnimID_Speed = Animator.StringToHash("Speed");
    private static readonly int AnimID_IsGrounded = Animator.StringToHash("IsGrounded");
    private static readonly int AnimID_Jump = Animator.StringToHash("Jump");

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        
        // Input System 인스턴스 생성
        _inputActions = new PlayerControls();

        // 카메라가 비어있으면 자동으로 찾기
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    private void OnEnable()
    {
        // 입력 활성화 및 점프 이벤트 연결
        _inputActions.Player.Enable();
        _inputActions.Player.Jump.performed += OnJump;
    }

    private void OnDisable()
    {
        _inputActions.Player.Disable();
        _inputActions.Player.Jump.performed -= OnJump;
    }

    private void Update()
    {
        HandleGravity(); // 중력 처리
        HandleMovement(); // 이동 처리
    }

    private void HandleMovement()
    {
        // 1. 입력값 읽기
        _inputMove = _inputActions.Player.Move.ReadValue<Vector2>();
        bool isSprinting = _inputActions.Player.Sprint.IsPressed();

        // 입력이 없으면 애니메이션 0으로 만들고 리턴 (미세 떨림 방지)
        if (_inputMove == Vector2.zero)
        {
            if (animator != null) animator.SetFloat(AnimID_Speed, 0f, 0.1f, Time.deltaTime);
            return;
        }

        // 2. 목표 속도 설정
        float targetSpeed = isSprinting ? sprintSpeed : moveSpeed;

        // 3. 이동 방향 계산 (카메라 기준)
        // 카메라가 보는 방향(Forward)과 오른쪽(Right)을 가져옴
        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;

        // Y축(높이) 제거 -> 평지 이동만 하도록
        //forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        // 최종 이동 방향 벡터
        Vector3 moveDir = (forward * _inputMove.y + right * _inputMove.x).normalized;

        // 4. 이동 실행 (CharacterController 사용)
        _controller.Move(moveDir * targetSpeed * Time.deltaTime);

        // 5. 회전 처리 (캐릭터가 "이동하는 방향"을 바라보게 함)
        if (moveDir != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            // Slerp로 부드럽게 회전
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // 6. 애니메이션 동기화
        if (animator != null)
        {
            // 실제 이동 속도를 기반으로 블렌딩
            float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;
            animator.SetFloat(AnimID_Speed, currentHorizontalSpeed, 0.1f, Time.deltaTime);
            animator.SetBool(AnimID_IsGrounded, _isGrounded);
        }
    }

    private void HandleGravity()
    {
        _isGrounded = _controller.isGrounded;

        if (_isGrounded && _velocity.y < 0)
        {
            _velocity.y = -2f; // 땅에 붙어있게 약간의 힘 유지
        }

        _velocity.y += gravity * Time.deltaTime;
        _controller.Move(_velocity * Time.deltaTime);
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        if (_isGrounded)
        {
            // 점프 공식: v = sqrt(h * -2 * g)
            _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            if (animator != null) animator.SetTrigger(AnimID_Jump);
        }
    }
}