using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PlayerHUD : MonoBehaviour
{
    [Header("Target Player")]
    public GameObject player;
    private PlayerStats _playerStats; 
    private PlayerWallet _playerWallet; 

    [Header("Health UI")]
    public Slider hpSlider;     
    public Slider hpEaseSlider;   
    [Header("Mana UI")]
    public Slider mpSlider;     
    public Slider mpEaseSlider;   
    [Header("Stamina UI")]
    public Slider staminaSlider;     
    public Slider staminaEaseSlider;   

    [Header("Memory UI")]
    public TMPro.TextMeshProUGUI memoryText;

    [Header("Settings")]
    public float easeSpeed = 5.0f; 
    
    // ★ 1 스탯당 몇 픽셀(Pixel)로 할 것인가?
    public float widthMultiplier = 3.0f; 
    public float maxWidth = 1000f; // 길이 제한

    // 코루틴 관리용 변수
    private Coroutine _hpCoroutine;
    private Coroutine _mpCoroutine;
    private Coroutine _staminaCoroutine;

    private void Start()
    {
        // Player 오브젝트가 연결되어 있다면 컴포넌트 가져오기
        if (player != null)
        {
            _playerStats = player.GetComponent<PlayerStats>();
            _playerWallet = player.GetComponent<PlayerWallet>();
        }

        if (_playerStats != null && _playerWallet != null)
        {
            Initialize(_playerStats);
            Debug.Log($"✅ PlayerHUD: {_playerStats.name}와 연결되어 UI가 초기화되었습니다.");
        }
        else
        {
            Debug.LogError("❌ PlayerHUD: Player 오브젝트가 연결되지 않았거나, Stats/Wallet 컴포넌트가 없습니다.");
        }
    }

    public void Initialize(PlayerStats playerStats)
    {
        _playerStats = playerStats;

        // 1. 초기값 설정 및 길이 조절 (InitBar 안에서 ResizeBar 호출)
        InitBar(hpSlider, hpEaseSlider, _playerStats.maxHealth, _playerStats.currentHealth);
        InitBar(mpSlider, mpEaseSlider, _playerStats.maxMana, _playerStats.currentMana);
        InitBar(staminaSlider, staminaEaseSlider, _playerStats.maxStamina, _playerStats.currentStamina);

        UpdateMemoriesUI(_playerWallet.GetCurrentMemory());

        // 2. 이벤트 구독
        // 중복 구독 방지를 위해 먼저 뺐다가 더함
        _playerStats.OnHealthChanged -= UpdateHP;
        _playerStats.OnManaChanged -= UpdateMP;
        _playerStats.OnStaminaChanged -= UpdateStamina;
        _playerWallet.OnMemoryChanged -= UpdateMemoriesUI;
        _playerStats.OnStatsRefreshed -= RefreshAllBars; // ★ 추가된 이벤트

        _playerStats.OnHealthChanged += UpdateHP;
        _playerStats.OnManaChanged += UpdateMP;
        _playerStats.OnStaminaChanged += UpdateStamina;
        _playerWallet.OnMemoryChanged += UpdateMemoriesUI;
        _playerStats.OnStatsRefreshed += RefreshAllBars; // ★ 추가된 이벤트
    }

    private void OnDisable()
    {
        if (_playerStats != null)
        {
            _playerStats.OnHealthChanged -= UpdateHP;
            _playerStats.OnManaChanged -= UpdateMP;
            _playerStats.OnStaminaChanged -= UpdateStamina;
        }
        if (_playerWallet != null)
        {
            _playerWallet.OnMemoryChanged -= UpdateMemoriesUI;
        }
        _playerStats.OnStatsRefreshed -= RefreshAllBars; // ★ 추가된 이벤트
    }

    // ---------------------------------------------------------
    // 1. HP 업데이트 로직
    // ---------------------------------------------------------
    private void UpdateHP(float current, float max)
    {
        // ★ 최대 체력이 변했다면 길이와 MaxValue 갱신
        if (hpSlider.maxValue != max)
        {
            hpSlider.maxValue = max;
            if (hpEaseSlider != null) hpEaseSlider.maxValue = max;
            ResizeBar(hpSlider, max);
            ResizeBar(hpEaseSlider, max);
        }

        hpSlider.value = current;

        // 잔상 효과
        if (hpSlider.value > hpEaseSlider.value)
        {
            hpEaseSlider.value = hpSlider.value;
            if (_hpCoroutine != null) StopCoroutine(_hpCoroutine);
            _hpCoroutine = null;
        }
        else if (hpSlider.value < hpEaseSlider.value)
        {
            if (_hpCoroutine == null) _hpCoroutine = StartCoroutine(EaseHPProcess());
        }
    }

    private IEnumerator EaseHPProcess()
    {
        while (hpEaseSlider.value > hpSlider.value)
        {
            hpEaseSlider.value = Mathf.Lerp(hpEaseSlider.value, hpSlider.value, Time.deltaTime * easeSpeed);
            if (Mathf.Abs(hpEaseSlider.value - hpSlider.value) < 0.01f)
            {
                hpEaseSlider.value = hpSlider.value;
                break;
            }
            yield return null;
        }
        _hpCoroutine = null;
    }

    // ---------------------------------------------------------
    // 2. MP 업데이트 로직
    // ---------------------------------------------------------
    private void UpdateMP(float current, float max)
    {
        // ★ 최대 마나가 변했다면 길이와 MaxValue 갱신
        if (mpSlider.maxValue != max)
        {
            mpSlider.maxValue = max;
            if (mpEaseSlider != null) mpEaseSlider.maxValue = max;
            ResizeBar(mpSlider, max);
            ResizeBar(mpEaseSlider, max);
        }

        mpSlider.value = current;

        if (mpSlider.value > mpEaseSlider.value)
        {
            mpEaseSlider.value = mpSlider.value;
            if (_mpCoroutine != null) StopCoroutine(_mpCoroutine);
            _mpCoroutine = null;
        }
        else if (mpSlider.value < mpEaseSlider.value)
        {
            if (_mpCoroutine == null) _mpCoroutine = StartCoroutine(EaseMPProcess());
        }
    }

    private IEnumerator EaseMPProcess()
    {
        while (mpEaseSlider.value > mpSlider.value)
        {
            mpEaseSlider.value = Mathf.Lerp(mpEaseSlider.value, mpSlider.value, Time.deltaTime * easeSpeed);
            if (Mathf.Abs(mpEaseSlider.value - mpSlider.value) < 0.01f)
            {
                mpEaseSlider.value = mpSlider.value;
                break;
            }
            yield return null;
        }
        _mpCoroutine = null;
    }

    // ---------------------------------------------------------
    // 3. 스태미나 업데이트 로직
    // ---------------------------------------------------------
    private void UpdateStamina(float current, float max)
    {
        // ★ 최대 스태미나가 변했다면 길이와 MaxValue 갱신
        if (staminaSlider.maxValue != max)
        {
            staminaSlider.maxValue = max;
            if (staminaEaseSlider != null) staminaEaseSlider.maxValue = max;
            ResizeBar(staminaSlider, max);
            ResizeBar(staminaEaseSlider, max);
        }

        staminaSlider.value = current;

        if (staminaSlider.value > staminaEaseSlider.value)
        {
            staminaEaseSlider.value = staminaSlider.value;
            if (_staminaCoroutine != null) StopCoroutine(_staminaCoroutine);
            _staminaCoroutine = null;
        }
        else if (staminaSlider.value < staminaEaseSlider.value)
        {
            if (_staminaCoroutine == null) _staminaCoroutine = StartCoroutine(EaseStaminaProcess());
        }
    }

    private IEnumerator EaseStaminaProcess()
    {
        while (staminaEaseSlider.value > staminaSlider.value)
        {
            staminaEaseSlider.value = Mathf.Lerp(staminaEaseSlider.value, staminaSlider.value, Time.deltaTime * easeSpeed);
            if (Mathf.Abs(staminaEaseSlider.value - staminaSlider.value) < 0.01f)
            {
                staminaEaseSlider.value = staminaSlider.value;
                break;
            }
            yield return null;
        }
        _staminaCoroutine = null;
    }

    // ---------------------------------------------------------
    // Memory UI 업데이트
    // ---------------------------------------------------------
    private void RefreshAllBars()
    {
        UpdateHP(_playerStats.currentHealth, _playerStats.maxHealth);
        UpdateMP(_playerStats.currentMana, _playerStats.maxMana);
        UpdateStamina(_playerStats.currentStamina, _playerStats.maxStamina);
    }
    private void UpdateMemoriesUI(int amount)
    {
        if (memoryText != null)
        {
            memoryText.text = amount.ToString("N0");
        }
    }

    // ---------------------------------------------------------
    // 헬퍼 함수
    // ---------------------------------------------------------
    private void InitBar(Slider main, Slider ease, float max, float current)
    {
        main.maxValue = max;
        main.value = current;
        ResizeBar(main, max); // 초기 길이 설정

        if (ease != null)
        {
            ease.maxValue = max;
            ease.value = current;
            ResizeBar(ease, max); // 초기 길이 설정
        }
    }

    // ★ [신규 기능] 슬라이더의 물리적 길이를 변경하는 함수
    private void ResizeBar(Slider slider, float maxValue)
    {
        if (slider == null) return;

        RectTransform rect = slider.GetComponent<RectTransform>();
        if (rect != null)
        {
            // 목표 너비 계산 (최대치 * 배율)
            float newWidth = maxValue * widthMultiplier;
            
            // 너무 길어지지 않게 제한
            newWidth = Mathf.Min(newWidth, maxWidth);

            // 높이(y)는 유지하고 너비(x)만 변경
            rect.sizeDelta = new Vector2(newWidth, rect.sizeDelta.y);
        }
    }
}