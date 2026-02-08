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
    public AudioClip parrySound; // 휘두르는 소리

    // 외부에서 락온 상태인지 물어볼 때 토스해줌
    [Header("Lock On Settings")]
    private PlayerLockOn _lockOnSystem; 
    public bool IsLockOn => _lockOnSystem != null && _lockOnSystem.isLockOn; 
    public Transform LockOnTarget => _lockOnSystem != null ? _lockOnSystem.currentTarget : null;
    public Transform cameraRoot; 

    
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
    private static readonly int AnimID_IsLockOn = Animator.StringToHash("isLockOn");
    private static readonly int AnimID_LockOn = Animator.StringToHash("LockOn");
    private static readonly int AnimID_InputX = Animator.StringToHash("InputX");
    private static readonly int AnimID_InputY = Animator.StringToHash("InputY");
    private static readonly int AnimID_IsCountering = Animator.StringToHash("isCountering");
    private int AnimID_SkillAnimHash;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _inputActions = new PlayerControls();
        _stats = GetComponent<PlayerStats>();
        _lockOnSystem = GetComponent<PlayerLockOn>(); 

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
        _inputActions.Player.LockOn.performed += OnLockOnInput;

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
        _inputActions.Player.LockOn.performed -= OnLockOnInput;

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
                // animator.SetBool(AnimID_CanCounterAttack, false);
                animator.ResetTrigger(AnimID_DoAttack);
                _canCounterAttack = false;
                CancelInvoke(nameof(ResetCounterWindow));
                break;
            case PlayerState.Attack:
                WeaponDisable(); // 공격 끊기면 무기 끄기
                if (myWeapon != null) myWeapon.damageMultiplier = 1.0f;
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
                if (_stats.UseStamina(attackStaminaCost)) // 즉시 소모
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
                if (_stats.HasStamina(attackStaminaCost)) 
                {
                    Debug.Log("콤보 예약됨 (스태미나 아직 안 깎음)");
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
            Debug.Log($"스킬 쿨타임! ({_lastSkillTime + activeSkill.cooldown - Time.time:F1}초 남음)");
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
        if (_stats != null && _stats.UseMana(activeSkill.manaCost))
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
        if (activeSkill.impactVFX != null)
             Instantiate(activeSkill.impactVFX, transform.position + transform.forward, Quaternion.identity);

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
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, activeSkill.impactRadius);
        foreach (var hit in hitColliders)
        {
            if (hit.CompareTag("Enemy"))
            {
                var enemyStats = hit.GetComponent<CharacterStats>();
                if (enemyStats != null)
                {
                    enemyStats.TakeDamage(finalDamage, 50.0f, transform);
                }
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
        Debug.Log("<color=yellow>반격 기회 포착! (Counter Ready)</color>");
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
                // 여기서 실제로 스태미나 소모
                if (_stats != null && _stats.UseStamina(attackStaminaCost))
                {
                    // 결제 성공 -> 다음 공격 진행
                    _comboStep++;
                    if (myWeapon != null)
                    {
                        myWeapon.damageMultiplier = GetCurrentComboMultiplier();
                        // 디버그용: 배율 확인
                        // Debug.Log($"콤보 {_comboStep + 1}타! 데미지 배율: {myWeapon.damageMultiplier}x");
                    }
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
        else if (currentState == PlayerState.Skill)
        {
            // 스킬 끝 -> 대기 상태로
            ChangeState(PlayerState.Locomotion);
        }
        else
        {
            if(currentState == PlayerState.CounterAttack) return;
            ChangeState(PlayerState.Locomotion);
        }
    }
    public void OnCounterEnd()
    {
        animator.SetBool(AnimID_IsCountering, false);
        animator.ResetTrigger(AnimID_DoAttack);
        _comboInputReceived = false; // 예약된 콤보 삭제 (이게 범인!)
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
}