using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

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
    Die,         // 사망
    Interact,  // 상호작용
    UseItem     // 아이템 사용
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

    [Header("Volition Costs")] // 행동별 소모량 설정
    public float rollVolitionCost = 20f;
    public float attackVolitionCost = 15f;
    public float sprintVolitionCost = 10f; // 달리기 (초당 소모량)

    [Header("References")]
    public Transform cameraTransform;
    public Animator animator;
    private PlayerStats _stats;

    [Header("Combat")]
    public PlayerWeapon myWeapon;
    private float initialDamage;

    [Header("Combo Settings")]
    private bool _comboInputReceived = false; // 공격 중 입력이 들어왔는가?
    private int _comboStep = 0;               // 현재 몇 번째 타격인가? (0, 1, 2...)
    public int maxComboCount = 3; // 콤보가 3개라면 인덱스는 0, 1, 2

    [Header("Skill Settings")]
    public SkillData activeSkill; // SO-SkillData
    private float _lastSkillTime = -10f;      // 마지막 사용 시간 (초기값은 즉시 사용 가능하게)

    [Header("Parry & Counter")]
    public float counterWindowDuration = 1.5f; // 패링 후 반격 가능한 시간
    private bool _canCounterAttack = false;    // 현재 반격이 가능한가?
    public float counterDamageMultiplier = 3.0f; // 반격 데미지 배율

    // 외부에서 락온 상태인지 물어볼 때 토스해줌
    [Header("Lock On Settings")]
    private PlayerLockOn _lockOnSystem; 
    public bool IsLockOn => _lockOnSystem != null && _lockOnSystem.isLockOn; 
    public Transform LockOnTarget => _lockOnSystem != null ? _lockOnSystem.currentTarget : null;
    public Transform cameraRoot; 

    [Header("Audio Clip")]
    public AudioClip parrySound; // 휘두르는 소리
    public AudioClip[] footsteps;
    public AudioClip jumpSound;

    
    // 내부 변수
    private CharacterController _controller;
    private PlayerControls _inputActions;
    private Vector2 _inputMove;
    private Vector3 _verticalVelocity;
    private bool _isGrounded;
    private PlayerInteraction _playerInteraction;

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
    private static readonly int AnimID_IsLockOn = Animator.StringToHash("isLockOn");
    private static readonly int AnimID_LockOn = Animator.StringToHash("LockOn");
    private static readonly int AnimID_InputX = Animator.StringToHash("InputX");
    private static readonly int AnimID_InputY = Animator.StringToHash("InputY");
    private static readonly int AnimID_IsCountering = Animator.StringToHash("isCountering");

    // 스킬 범위 판정 버퍼 (매 시전마다 할당 방지)
    private readonly Collider[] _skillHitBuffer = new Collider[20];
    private readonly HashSet<CharacterStats> _skillHitSet = new HashSet<CharacterStats>();
    private int AnimID_SkillAnimHash;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _inputActions = new PlayerControls();
        _stats = GetComponent<PlayerStats>();
        _lockOnSystem = GetComponent<PlayerLockOn>(); 
        _playerInteraction = GetComponent<PlayerInteraction>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }
    private void Start()
    {
        initialDamage = myWeapon.damage; // 초기 데미지 저장

        if (activeSkill?.impactVFX != null && VFXPoolManager.Instance != null)
            VFXPoolManager.Instance.WarmUp(activeSkill.impactVFX, 3);
    }
    private void OnEnable()
    {
        if (_inputActions == null) return;
        _inputActions.Player.Enable();
        _inputActions.Player.Jump.performed += OnJump;
        _inputActions.Player.Attack.performed += OnAttack;
        _inputActions.Player.Roll.performed += OnRoll;
        _inputActions.Player.Parry.performed += OnParry;
        _inputActions.Player.Skill.performed += OnSkill;
        _inputActions.Player.LockOn.performed += OnLockOnInput;
        _inputActions.Player.Interact.performed += OnInteract; 

        if (_stats != null)
        {
            _stats.OnTakeDamage += OnHit;
            _stats.OnDeath += OnDie;
        }
    }

    private void OnDisable()
    {
        if (_inputActions == null) return;
        _inputActions.Player.Disable();
        _inputActions.Player.Jump.performed -= OnJump;
        _inputActions.Player.Attack.performed -= OnAttack;
        _inputActions.Player.Roll.performed -= OnRoll;
        _inputActions.Player.Parry.performed -= OnParry;
        _inputActions.Player.Skill.performed -= OnSkill;
        _inputActions.Player.LockOn.performed -= OnLockOnInput;
        _inputActions.Player.Interact.performed -= OnInteract; 

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
    private void LateUpdate() // 모든 물리/이동 계산 후 마지막에 호출
    {
        if (IsLockOn && LockOnTarget != null)
        {
            // 1. 적이 있는 방향을 계산
            Vector3 dirToEnemy = LockOnTarget.position - transform.position;
            dirToEnemy.y = 0; // 높이는 무시 (땅과 수평)

            if (dirToEnemy != Vector3.zero)
            {
                // 2. ★핵심★: 몸통(Player)이 어디로 구르든 상관없이, 
                // 카메라는 무조건 적을 바라보도록 강제로 고정합니다.
                cameraRoot.rotation = Quaternion.LookRotation(dirToEnemy);
            }
        }
        else
        {
            // 락온이 아니면 그냥 플레이어의 원래 회전을 따라감
            cameraRoot.localRotation = Quaternion.identity;
        }
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
                if (myWeapon != null) myWeapon.damageMultiplier = 1.0f;
                break;

            case PlayerState.CounterAttack:
                WeaponDisable();
                // 무기 데미지 원상복구
                if (myWeapon != null) myWeapon.damageMultiplier = 1.0f;
                // 카운터 상태를 어떤 경로로 빠져나가든(피격·사망 등으로 중단 포함) isCountering을 반드시 해제.
                // 애니메이션 이벤트 OnCounterEnd(1.16초)에만 의존하면, 그 전에 카운터가 끊길 경우
                // isCountering이 true로 남아 이후 일반 공격 전이(조건: isCountering==false)가 영구 차단됨.
                animator.SetBool(AnimID_IsCountering, false);
                animator.ResetTrigger(AnimID_DoAttack);
                _canCounterAttack = false;
                CancelInvoke(nameof(ResetCounterWindow));
                break;

            case PlayerState.Attack:
                WeaponDisable(); // 공격 끊기면 무기 끄기
                if (myWeapon != null) myWeapon.damageMultiplier = 1.0f;
                break;

            case PlayerState.Interact:
                // 상호작용 끝날 때: 시간 재개, 커서 숨기기
                Time.timeScale = 1f;
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
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
                // 소비되지 않고 남은 공격 트리거 정리 (다음 행동에서 오발동 방지)
                animator.ResetTrigger(AnimID_DoCounterAttack);
                animator.ResetTrigger(AnimID_DoAttack);
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

                animator.SetTrigger(AnimID_SkillAnimHash); // 스킬 애니메이션

                // 데미지 뻥튀기
                // if (myWeapon != null) myWeapon.damageMultiplier = 2.5f;
                break;

            case PlayerState.CounterAttack:
                animator.applyRootMotion = true;
                if (myWeapon != null) myWeapon.damageMultiplier = 2.0f;
                // 반격 애니메이션 재생
                animator.SetTrigger(AnimID_DoCounterAttack);
                animator.ResetTrigger(AnimID_DoAttack);

                animator.SetBool(AnimID_IsCountering, true);
                break;

            case PlayerState.Attack:
                animator.applyRootMotion = true;

                // 첫 1타 배율 초기화
                if (myWeapon != null) myWeapon.damageMultiplier = GetCurrentComboMultiplier(); // 1.0f

                // ★ 콤보 단계에 따라 다른 애니메이션 재생
                // (Animator에 파라미터로 "ComboStep" int형이나, 각각의 Trigger가 필요함)
                animator.SetInteger(AnimID_ComboStep, _comboStep);
                // 직전에 소비되지 않고 큐에 남은 카운터 트리거가 일반 공격 중 발동하는 것 방지
                // (남아있으면 카운터 애니메이션만 재생되고 데미지/사운드는 1.0배 일반 공격으로 나감)
                animator.ResetTrigger(AnimID_DoCounterAttack);
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
            
            case PlayerState.Interact:
                Time.timeScale = 0f;
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                break;

            case PlayerState.UseItem:
                break;
        }
    }

    // --- 상태별 Update 로직 ---

    private void UpdateLocomotion()
    {
        if (_controller == null || !_controller.enabled) return;
        // 락온 상태인지 확인 (LockOnSystem이 있고, 타겟도 있어야 함)
        bool isStrafing = IsLockOn && LockOnTarget != null;

        // 애니메이터에 락온 상태 전달 (Transition 조건용)
        animator.SetBool(AnimID_IsLockOn, isStrafing);

        if(isStrafing) animator.SetFloat(AnimID_LockOn, 1.0f);        
        else animator.SetFloat(AnimID_LockOn, 0.0f);

        // 이동 입력값 (Vector2)
        float inputX = _inputMove.x; // A, D (좌우)
        float inputY = _inputMove.y; // W, S (전후)

        if (isStrafing)
        {
            // =================================================
            // [모드 A] 락온 이동 (Strafe Mode)
            // =================================================

            // 1. 회전: 몸을 강제로 적 쪽으로 돌림
            Vector3 targetDir = LockOnTarget.position - transform.position;
            targetDir.y = 0; 
            
            if (targetDir != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(targetDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 15f);
            }

            // 2. 실제 이동 로직 
            // 캐릭터가 이미 적을 보고 있으므로, transform.right/forward를 기준으로 이동합니다.
            Vector3 strafeDirection = (transform.right * inputX + transform.forward * inputY).normalized;
            
            // 이동 속도 적용 (락온 중엔 보통 걷기 속도 사용)
            Vector3 velocity = strafeDirection * moveSpeed;
            
            // 중력 적용
            velocity += _verticalVelocity;

            // 실제 이동 실행
            _controller.Move(velocity * Time.deltaTime);


            // 3. 애니메이션 파라미터 전달
            float currentX = animator.GetFloat(AnimID_InputX);
            float currentY = animator.GetFloat(AnimID_InputY);

            animator.SetFloat(AnimID_InputX, Mathf.MoveTowards(currentX, inputX, Time.deltaTime * 5f));
            animator.SetFloat(AnimID_InputY, Mathf.MoveTowards(currentY, inputY, Time.deltaTime * 5f));
        }
        else
        {
            // =================================================
            // [모드 B] 일반 이동 (Free Look Mode)
            // =================================================

            // Strafe 애니메이션 값 초기화 (락온 풀었을 때 잔상 제거)
            animator.SetFloat(AnimID_InputX, 0);
            animator.SetFloat(AnimID_InputY, 0);
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
                    if (_stats != null && _stats.UseVolition(sprintVolitionCost * Time.deltaTime))
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
            // (기존) 달리기 애니메이션 처리
            // animator.SetFloat("Speed", ...); 
        }
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
        // 평상시(Locomotion)뿐 아니라 공격(Attack) 중에도 구르기로 캔슬 허용 → 콤보를 끊고 적 공격 회피.
        // 애니메이터는 AnyState→Roll(doRoll, Has Exit Time 0) 전이로 즉시 전환되고,
        // Attack 종료 블록이 무기 히트박스/배율을, Roll 진입 블록이 콤보 상태를 정리한다.
        bool canRoll = currentState == PlayerState.Locomotion || currentState == PlayerState.Attack;
        if (canRoll && _isGrounded)
        {
            // 스태미나 없으면 구르기 불가
            if (_stats != null && _stats.UseVolition(rollVolitionCost))
            {
                ChangeState(PlayerState.Roll);
            }
        }
    }

    private void OnAttack(InputAction.CallbackContext context)
    {
        if (!_isGrounded) return; // 공중 공격 제외
        if (currentState == PlayerState.CounterAttack || currentState == PlayerState.Skill) 
            return;

        // 1. 패링 성공 직후라면 -> 강력한 반격!
        if (currentState == PlayerState.Locomotion || currentState == PlayerState.Parry)
        {
            // 카운터 공격
            if (_canCounterAttack)
            {
                ChangeState(PlayerState.CounterAttack);
                return;
            }
            else // 일반 공격
            {
                if (_stats.UseVolition(attackVolitionCost)) // 즉시 소모
                {
                    _comboStep = 0;
                    _comboInputReceived = false;
                    ChangeState(PlayerState.Attack);
                }
            }
        }

        // 2. 이미 공격 중일 때 -> 다음 콤보 예약
        else if (currentState == PlayerState.Attack)
        {
            if (!_comboInputReceived)
            {
                // [수정] UseStamina 대신 HasStamina로 확인만!
                // "지금 당장 안 깎고, 나중에 때릴 때 깎을게. 근데 잔고는 있지?"
                if (_stats.HasVolition(attackVolitionCost)) 
                {
                    _comboInputReceived = true;
                }
            }
        }
    }
    // 콤보 배율을 안전하게 가져오는 헬퍼 함수
    private float GetCurrentComboMultiplier()
    {
        // 1. 데이터가 없거나 리스트가 비었으면 기본 1배
        if (myWeapon == null || myWeapon.weaponData == null) return 1.0f;
        var multipliers = myWeapon.weaponData.comboMultipliers;
        if (multipliers == null || multipliers.Count == 0) return 1.0f;

        // 2. 현재 콤보 스텝이 리스트 범위 안인지 체크
        if (_comboStep < multipliers.Count)
        {
            return multipliers[_comboStep];
        }
        else
        {
            // 3. 만약 리스트보다 콤보가 길면? -> 마지막 설정값 유지 (혹은 1.0f)
            return multipliers[multipliers.Count - 1];
        }
    }
    private void OnSkill(InputAction.CallbackContext context)
    {
        if (activeSkill == null) return;
        // 땅에 있고, 이동 중일 때만 가능 (공격 캔슬 스킬을 원하면 조건 완화 가능)
        if (currentState != PlayerState.Locomotion || !_isGrounded) return;
        
        // 1. 쿨타임 체크
        if (Time.time < _lastSkillTime + activeSkill.cooldown)
        {
            return;
        }
        // 스킬 트리거 가져오기
        if (!string.IsNullOrEmpty(activeSkill.animTriggerName))
        {
            AnimID_SkillAnimHash = Animator.StringToHash(activeSkill.animTriggerName);
        }
        else
        {
            Debug.LogWarning($"[{activeSkill.skillName}] 스킬에 애니메이션 이름이 없습니다!");
        }
        // 2. 마나 체크 (데이터에서 가져옴)
        if (_stats != null && _stats.UseLucidity(activeSkill.lucidityCost))
        {
            // 시전 사운드 재생
            if (activeSkill.castSound != null)
                SoundManager.Instance.PlayPlayerSFX(activeSkill.castSound, 1.0f);
            
            // 시전 이펙트 
            if (activeSkill.castVFX != null)
                Instantiate(activeSkill.castVFX, transform.position, transform.rotation);

            ChangeState(PlayerState.Skill);
        }
    }
    public void OnSkillImpact()
    {
        if (activeSkill == null) return;
        float finalDamage = activeSkill.damage;
        if (myWeapon != null) finalDamage += myWeapon.damage;

       // 1. 타격/폭발 이펙트 (내 위치 혹은 타겟 위치)
        if (activeSkill.impactVFX != null && VFXPoolManager.Instance != null)
            VFXPoolManager.Instance.PlayVFX(activeSkill.impactVFX, transform.position + transform.forward, Quaternion.identity);

        // 2. ★ 타격 사운드 재생 
        if (activeSkill.impactSound != null)
            SoundManager.Instance.PlayPlayerSFX(activeSkill.impactSound, 2.0f);

        // 3. camera impulse    
        if (activeSkill.impulseDefinition != null)
        {
            // Impulse 생성 (내 위치에서, 기본 속도 Vector3.down 등으로 설정 가능)
            activeSkill.impulseDefinition.CreateEvent(transform.position, Vector3.down);
        }
        // 4. 범위 공격 판정 (데이터의 반경 사용)
        // GetComponentInParent로 부모까지 탐색 → 보스 hurtbox처럼 자식 콜라이더도 인식
        // HashSet으로 같은 대상 중복 피격 방지
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, activeSkill.impactRadius, _skillHitBuffer);
        _skillHitSet.Clear();
        for (int i = 0; i < hitCount; i++)
        {
            var enemyStats = _skillHitBuffer[i].GetComponentInParent<CharacterStats>();
            if (enemyStats != null && enemyStats.CompareTag("Enemy") && _skillHitSet.Add(enemyStats))
            {
                enemyStats.TakeDamage(finalDamage, activeSkill.composureDamage, transform);
            }
        }
        
        // CameraShake.Instance.Shake(0.5f);
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
        _canCounterAttack = true;
        
        if (parrySound != null)
            {
                SoundManager.Instance.PlayPlayerSFX(parrySound, 2.0f);
            }  
        // 일정 시간 뒤에 기회 박탈
        CancelInvoke(nameof(ResetCounterWindow));
        Invoke(nameof(ResetCounterWindow), counterWindowDuration);
    }
    private void ResetCounterWindow()
    {
        _canCounterAttack = false;
    }

    // --- 피격 및 무적 로직 ---

    private void OnHit()
    {
        if (currentState == PlayerState.Die) return;

        // ★ [핵심] 구르기 상태라면 무적! (데미지/피격모션 무시)
        if (currentState == PlayerState.Roll)
        {
            return; // 구르기 무적(i-frame)
        }

        // 그 외 상태면 피격 처리
        ChangeState(PlayerState.Hit);
    }
    //-----------------------------
    // Death
    //-----------------------------
    private void OnDie()
    {
        ChangeState(PlayerState.Die);
        // GameManager 에게 알리기
        if (GameManager.Instance != null)
        {
            PlayerWallet myWallet = GetComponent<PlayerWallet>();
            
            // 1. ★ 현재 가진 돈을 '유실물'로 등록 (위치 저장)
            int currentMemory = myWallet.GetCurrentMemory();
            GameManager.Instance.SaveLostMemory(currentMemory, transform.position);

            // 2. ★ 내 지갑 0원으로 초기화 (중요!)
            // 그래야 부활했을 때 빈털터리로 시작함
            myWallet.SpendMemory(currentMemory); // 전부 소모(0으로 만듦)

            StartCoroutine(DeathSequence());

            // 4. 게임 오버 처리
            // GameManager.Instance.GameOver(); 
        }
    }
    public void EndOfDeath()
    {
        GameManager.Instance.RespawnAtAltar();
    }
    IEnumerator DeathSequence()
    {
        Debug.Log("💀 YOU DIED...");

        // ★ [추가] 데스 패널 서서히 등장
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowDeathPanel();
        }
        
        // 사망 애니메이션이 충분히 연출될 시간 대기
        // (화면이 점점 어두워지는 Fade Out 효과가 있다면 여기서 실행)
        yield return new WaitForSeconds(5.0f);

        // D. 매니저에게 부활 요청
        // -> 마지막 세이브 로드 + 돈 0원 처리 + 씬 재시작
        GameManager.Instance.RespawnAtAltar();
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
                // 여기서 실제로 스태미나 소모
                if (_stats != null && _stats.UseVolition(attackVolitionCost))
                {
                    // 결제 성공 -> 다음 공격 진행
                    _comboStep++;
                    if (myWeapon != null)
                    {
                        myWeapon.damageMultiplier = GetCurrentComboMultiplier();
                    }
                    _comboInputReceived = false;

                    animator.SetInteger(AnimID_ComboStep, _comboStep);
                    animator.SetTrigger(AnimID_DoAttack);
                }
                else
                {
                    // 결제 실패 (예약은 했는데 막상 때리려니 스태미나 부족) -> 공격 중단
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
        else if (currentState == PlayerState.Skill)
        {
            // 스킬 끝 -> 대기 상태로
            ChangeState(PlayerState.Locomotion);
        }
        else
        {
            // 카운터는 전용 애니메이션 이벤트(OnCounterEnd, 카운터 클립 1.16초)로만 종료한다.
            // 여기서 처리하면 Parry 클립의 OnAnimationEnd 이벤트가 desync 구간(코드=CounterAttack,
            // 애니=Parry 클립 재생 중)에서 발화해 카운터를 조기 종료시킬 수 있으므로 건드리지 않는다.
            if(currentState == PlayerState.CounterAttack) return;
            ChangeState(PlayerState.Locomotion);
        }
    }
    public void OnFootstep()
    {
        // 애니메이션 이벤트로 호출하는 함수
        SoundManager.Instance.PlayRandomSFX(footsteps, 0.3f);
    }
    public void OnCounterEnd()
    {
        animator.SetBool(AnimID_IsCountering, false);
        animator.ResetTrigger(AnimID_DoAttack);
        _comboInputReceived = false; // 예약된 콤보 입력 취소
        _comboStep = 0;  // 콤보 순서 초기화
        ChangeState(PlayerState.Locomotion);
    }
    
    // (기존 이벤트들 유지)
    public void EndHit() => ChangeState(PlayerState.Locomotion);
    public void WeaponEnable() => myWeapon?.EnableHitbox();
    public void WeaponDisable() => myWeapon?.DisableHitbox();

    //Lock on function
    private void OnLockOnInput(InputAction.CallbackContext context)
    {
        // ★ 내가 직접 안 찾고, 담당자에게 "락온 버튼 눌렸어"라고 전달만 함
        if (_lockOnSystem != null)
        {
            _lockOnSystem.ToggleLockOn();
        }
    }

    // ----------------------------------
    // Interact function
    // ----------------------------------
    public void OnInteract(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        // 1. 상태 체크 (구르거나 공격 중엔 상호작용 불가)
        if (currentState != PlayerState.Locomotion) return;

        // 2. Interaction 스크립트에게 시도 요청
        if (_playerInteraction != null && _playerInteraction.TryInteract())
        {
            // 3. 성공했다면 상태 변경 (UI 열림 등)
            // (StatUpgradeUI 같은 애가 내부에서 ChangeState(Interaction)을 호출해줄 수도 있고,
            //  여기서 즉시 바꿀 수도 있음. 보통은 UI가 열리면서 바꾸는 게 자연스러움)
            Debug.Log("상호작용 성공 -> 상태 변경 로직은 UI나 대상에서 처리");
        }
    }
}