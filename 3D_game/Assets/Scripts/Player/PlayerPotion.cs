using UnityEngine;
using UnityEngine.InputSystem; 
using TMPro; // TextMeshPro 필수
using System.Collections;

public class PlayerPotion : MonoBehaviour
{
    [Header("Lucid Drop Settings")]
    public int basicPotion = 3;
    public int maxPotions = 3;        
    public int currentPotions;        
    public float restoreAmount = 30f; // Ego 회복량
    
    [Header("Cooltime")]
    public float potionCooldown = 3.0f;
    private float _lastPotionTime;

    [Header("References")]
    public PlayerStats _stats;
    public PlayerController _controller; // 상태 변경용
    public Animator _animator;
    private PlayerControls _inputActions;

    [Header("UI")]
    public TextMeshProUGUI currentPotionText;

    private void Awake()
    {
        if(_stats == null || _controller == null || _animator == null)
        {
            _stats = GetComponent<PlayerStats>();
            _controller = GetComponent<PlayerController>();
            _animator = GetComponentInChildren<Animator>();
        }
        _inputActions = new PlayerControls();
    }
    private void Start()
    {
        RefillPotions();
    }
    private void OnEnable()
    {
        if (_inputActions == null) return;
        _inputActions.Player.Enable();
        _inputActions.Player.UseItem.performed += OnUseItem;
    }
    private void OnDisable()
    {
        if (_inputActions == null) return;
        _inputActions.Player.Disable();
        _inputActions.Player.UseItem.performed -= OnUseItem;
    }
    public void AddMaxPotion(int amount)
    {
        maxPotions += amount;
        currentPotions += amount;
        UpdateUI();
    }
    // ★ Input System이 자동으로 호출하는 함수 (Send Messages)
    // 액션 이름이 "UseItem"이어야 함
    void OnUseItem(InputAction.CallbackContext context)
    {
        // 1. 상태 체크: 가만히 있거나(Idle) 걷는 중(Move)일 때만 마실 수 있음
        // 구르거나 공격 중에는 못 마심!
        if (_controller.currentState != PlayerState.Locomotion)
            return;

        // 2. 조건 체크 (쿨타임, 개수, 풀피)
        if (Time.time < _lastPotionTime + potionCooldown) return;
        if (currentPotions <= 0) 
        {
            Debug.Log("물약이 없습니다!");
            return;
        }
        if (_stats.currentEgo >= _stats.maxEgo)
        {
            Debug.Log("자아(Ego)가 이미 온전합니다.");
            return;
        }

        // 4. 실행
        // StartCoroutine(DrinkRoutine());
        StartToDrink();
    }

    IEnumerator DrinkRoutine()
    {
        _lastPotionTime = Time.time;
        currentPotions--;

        // A. 상태 변경 (이제 플레이어는 못 움직임)
        _controller.ChangeState(PlayerState.UseItem);

        // B. 애니메이션 재생
        if (_animator != null) _animator.SetTrigger("doDrink");

        // C. 마시는 시간 대기 (애니메이션 길이만큼, 예: 1.5초)
        // ★ 더 정확하게 하려면 Animation Event를 써야 하지만, 코루틴이 편함
        yield return new WaitForSeconds(1.5f); 

        // D. 회복 적용 (마시는 모션 끝날 때쯤 회복)
        _stats.RestoreEgo(restoreAmount);
        Debug.Log($"<color=cyan>[Lucid Drop] 사용! 남은 개수: {currentPotions}</color>");

        // E. 상태 복귀 (다시 움직일 수 있음)
        _controller.ChangeState(PlayerState.Locomotion);
    }
    private void StartToDrink()
    {
        // A. 상태 변경 (이제 플레이어는 못 움직임)
        _controller.ChangeState(PlayerState.UseItem);

        // B. 애니메이션 재생
        if (_animator != null) _animator.SetTrigger("doDrink");
    }
    public void UpdateMaxPotions(int level)
    {
        int bonusPotions = level / 5; // 5로 나눈 몫만큼 추가
        maxPotions = basicPotion + bonusPotions;
    }
    public void RefillPotions()
    {
        UpdateMaxPotions(_stats.level);
        currentPotions = maxPotions;
        UpdateUI();
    }
    private void UpdateUI()
    {
        currentPotionText.text = $"{currentPotions}";
    }
    // ---------------------------
    // Animation Event
    // ---------------------------
    public void OnConsume()
    {
        _lastPotionTime = Time.time;
        currentPotions--;
        UpdateUI();
        _stats.RestoreEgo(restoreAmount);
        Debug.Log($"<color=cyan>[Lucid Drop] 사용! 남은 개수: {currentPotions}</color>");
    }
    public void OnEndDrink()
    {
        if (_controller.currentState == PlayerState.UseItem)
        {
            _controller.ChangeState(PlayerState.Locomotion);
        }
    }
}