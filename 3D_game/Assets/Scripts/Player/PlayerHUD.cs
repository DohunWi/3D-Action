using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PlayerHUD : MonoBehaviour
{
    [Header("Target Player")]
    public GameObject player;
    private PlayerStats _playerStats; 
    private PlayerWallet _playerWallet; 

    [Header("Ego UI")]
    public Slider egoSlider;     
    public Slider egoEaseSlider;   
    [Header("Lucidity UI")]
    public Slider luciditySlider;     
    public Slider lucidityEaseSlider;   
    [Header("Volition UI")]
    public Slider volitionSlider;     
    public Slider volitionEaseSlider;   

    [Header("Memory UI")]
    public TMPro.TextMeshProUGUI memoryText;

    [Header("Settings")]
    public float easeSpeed = 5.0f; 
    
    // ★ 1 스탯당 몇 픽셀(Pixel)로 할 것인가?
    public float widthMultiplier = 3.0f; 
    public float maxWidth = 1000f; // 길이 제한

    // 코루틴 관리용 변수
    private Coroutine _egoCoroutine;
    private Coroutine _lucidityCoroutine;
    private Coroutine _volitionCoroutine;

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
        InitBar(egoSlider, egoEaseSlider, _playerStats.maxEgo, _playerStats.currentEgo);
        InitBar(luciditySlider, lucidityEaseSlider, _playerStats.maxLucidity, _playerStats.currentLucidity);
        InitBar(volitionSlider, volitionEaseSlider, _playerStats.maxVolition, _playerStats.currentVolition);

        UpdateMemoriesUI(_playerWallet.GetCurrentMemory());

        // 2. 이벤트 구독
        // 중복 구독 방지를 위해 먼저 뺐다가 더함
        _playerStats.OnEgoChanged -= UpdateEgo;
        _playerStats.OnLucidityChanged -= UpdateLucidity;
        _playerStats.OnVolitionChanged -= UpdateVolition;
        _playerWallet.OnMemoryChanged -= UpdateMemoriesUI;
        _playerStats.OnStatsRefreshed -= RefreshAllBars; // ★ 추가된 이벤트

        _playerStats.OnEgoChanged += UpdateEgo;
        _playerStats.OnLucidityChanged += UpdateLucidity;
        _playerStats.OnVolitionChanged += UpdateVolition;
        _playerWallet.OnMemoryChanged += UpdateMemoriesUI;
        _playerStats.OnStatsRefreshed += RefreshAllBars; // ★ 추가된 이벤트
    }

    private void OnDisable()
    {
        if (_playerStats != null)
        {
            _playerStats.OnEgoChanged -= UpdateEgo;
            _playerStats.OnLucidityChanged -= UpdateLucidity;
            _playerStats.OnVolitionChanged -= UpdateVolition;
            _playerStats.OnStatsRefreshed -= RefreshAllBars;
        }
        if (_playerWallet != null)
        {
            _playerWallet.OnMemoryChanged -= UpdateMemoriesUI;
        }
    }

    // ---------------------------------------------------------
    // 1. HP 업데이트 로직
    // ---------------------------------------------------------
    private void UpdateEgo(float current, float max)
    {
        // ★ 최대 체력이 변했다면 길이와 MaxValue 갱신
        if (egoSlider.maxValue != max)
        {
            egoSlider.maxValue = max;
            if (egoEaseSlider != null) egoEaseSlider.maxValue = max;
            ResizeBar(egoSlider, max);
            ResizeBar(egoEaseSlider, max);
        }

        egoSlider.value = current;

        // 잔상 효과
        if (egoSlider.value > egoEaseSlider.value)
        {
            egoEaseSlider.value = egoSlider.value;
            if (_egoCoroutine != null) StopCoroutine(_egoCoroutine);
            _egoCoroutine = null;
        }
        else if (egoSlider.value < egoEaseSlider.value)
        {
            if (_egoCoroutine == null) _egoCoroutine = StartCoroutine(EaseEgoProcess());
        }
    }

    private IEnumerator EaseEgoProcess()
    {
        while (egoEaseSlider.value > egoSlider.value)
        {
            egoEaseSlider.value = Mathf.Lerp(egoEaseSlider.value, egoSlider.value, Time.deltaTime * easeSpeed);
            if (Mathf.Abs(egoEaseSlider.value - egoSlider.value) < 0.01f)
            {
                egoEaseSlider.value = egoSlider.value;
                break;
            }
            yield return null;
        }
        _egoCoroutine = null;
    }

    // ---------------------------------------------------------
    // 2. MP 업데이트 로직
    // ---------------------------------------------------------
    private void UpdateLucidity(float current, float max)
    {
        // ★ 최대 마나가 변했다면 길이와 MaxValue 갱신
        if (luciditySlider.maxValue != max)
        {
            luciditySlider.maxValue = max;
            if (lucidityEaseSlider != null) lucidityEaseSlider.maxValue = max;
            ResizeBar(luciditySlider, max);
            ResizeBar(lucidityEaseSlider, max);
        }

        luciditySlider.value = current;

        if (luciditySlider.value > lucidityEaseSlider.value)
        {
            lucidityEaseSlider.value = luciditySlider.value;
            if (_lucidityCoroutine != null) StopCoroutine(_lucidityCoroutine);
            _lucidityCoroutine = null;
        }
        else if (luciditySlider.value < lucidityEaseSlider.value)
        {
            if (_lucidityCoroutine == null) _lucidityCoroutine = StartCoroutine(EaseLucidityProcess());
        }
    }

    private IEnumerator EaseLucidityProcess()
    {
        while (lucidityEaseSlider.value > luciditySlider.value)
        {
            lucidityEaseSlider.value = Mathf.Lerp(lucidityEaseSlider.value, luciditySlider.value, Time.deltaTime * easeSpeed);
            if (Mathf.Abs(lucidityEaseSlider.value - luciditySlider.value) < 0.01f)
            {
                lucidityEaseSlider.value = luciditySlider.value;
                break;
            }
            yield return null;
        }
        _lucidityCoroutine = null;
    }

    // ---------------------------------------------------------
    // 3. 스태미나 업데이트 로직
    // ---------------------------------------------------------
    private void UpdateVolition(float current, float max)
    {
        // ★ 최대 스태미나가 변했다면 길이와 MaxValue 갱신
        if (volitionSlider.maxValue != max)
        {
            volitionSlider.maxValue = max;
            if (volitionEaseSlider != null) volitionEaseSlider.maxValue = max;
            ResizeBar(volitionSlider, max);
            ResizeBar(volitionEaseSlider, max);
        }

        volitionSlider.value = current;

        if (volitionSlider.value > volitionEaseSlider.value)
        {
            volitionEaseSlider.value = volitionSlider.value;
            if (_volitionCoroutine != null) StopCoroutine(_volitionCoroutine);
            _volitionCoroutine = null;
        }
        else if (volitionSlider.value < volitionEaseSlider.value)
        {
            if (_volitionCoroutine == null) _volitionCoroutine = StartCoroutine(EaseStaminaProcess());
        }
    }

    private IEnumerator EaseStaminaProcess()
    {
        while (volitionEaseSlider.value > volitionSlider.value)
        {
            volitionEaseSlider.value = Mathf.Lerp(volitionEaseSlider.value, volitionSlider.value, Time.deltaTime * easeSpeed);
            if (Mathf.Abs(volitionEaseSlider.value - volitionSlider.value) < 0.01f)
            {
                volitionEaseSlider.value = volitionSlider.value;
                break;
            }
            yield return null;
        }
        _volitionCoroutine = null;
    }

    // ---------------------------------------------------------
    // Memory UI 업데이트
    // ---------------------------------------------------------
    private void RefreshAllBars()
    {
        UpdateEgo(_playerStats.currentEgo, _playerStats.maxEgo);
        UpdateLucidity(_playerStats.currentLucidity, _playerStats.maxLucidity);
        UpdateVolition(_playerStats.currentVolition, _playerStats.maxVolition);
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