using UnityEngine;
using UnityEngine.UI;
using TMPro; // TextMeshPro 쓴다면 필수

public class BossEgoBar : MonoBehaviour
{
    [Header("UI Components")]
    public Slider egoSlider;    // 앞쪽 빨간 바
    public Slider easeSlider;  // 뒤쪽 흰색 잔상 바
    public TextMeshProUGUI bossNameText; // 보스 이름
    public CanvasGroup canvasGroup; // 페이드 효과용

    [Header("Settings")]
    public float easeSpeed = 0.05f; // 잔상이 따라오는 속도

    private CharacterStats _targetStats;

    private void Start()
    {
        // 시작할 땐 숨겨두기
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0; 
        }
    }

    private void Update()
    {
        // 잔상 효과 (Lerp): 붉은색 바보다 잔상 바가 더 크면 천천히 줄어듦
        if (egoSlider.value < easeSlider.value)
        {
            easeSlider.value = Mathf.Lerp(easeSlider.value, egoSlider.value, easeSpeed);
            
            // 거의 다 왔으면 딱 맞춤 (연산 낭비 방지)
            if (Mathf.Abs(easeSlider.value - egoSlider.value) < 0.01f)
            {
                easeSlider.value = egoSlider.value;
            }
        }
    }

    // ★ 외부(EliteEnemy)에서 이 함수를 호출해서 초기화
    public void Initialize(CharacterStats stats, string bossName)
    {
        _targetStats = stats;
        
        // 1. 이름 설정
        if (bossNameText != null) bossNameText.text = bossName;

        // 2. 슬라이더 최대값 설정
        egoSlider.maxValue = stats.maxEgo;
        egoSlider.value = stats.currentEgo;
        
        easeSlider.maxValue = stats.maxEgo;
        easeSlider.value = stats.currentEgo;

        // 3. 이벤트 구독 (체력 변할 때마다 UpdateHealthUI 실행)
        _targetStats.OnEgoChanged += UpdateEgoUI;
        _targetStats.OnDeath += Hide; // 죽으면 숨기기

        // 4. UI 등장 (페이드 인)
        StartCoroutine(FadeIn());
    }

    private void OnDisable()
    {
        // 이벤트 구독 해제 (메모리 누수 방지)
        if (_targetStats != null)
        {
            _targetStats.OnEgoChanged -= UpdateEgoUI;
            _targetStats.OnDeath -= Hide;
        }
    }

    // 이벤트가 발생할 때 실행될 함수
    private void UpdateEgoUI(float current, float max)
    {
        // 빨간 바는 즉시 줄어듦 -> 뒤의 Ease 바는 Update에서 천천히 따라옴
        egoSlider.value = current;
    }

    private System.Collections.IEnumerator FadeIn()
    {
        float timer = 0f;
        while (timer < 1.0f)
        {
            timer += Time.deltaTime;
            if (canvasGroup != null) canvasGroup.alpha = timer;
            yield return null;
        }
    }
    public void Hide()
    {
        StartCoroutine(FadeOut());
    }
    private System.Collections.IEnumerator FadeOut()
    {
        float timer = 1.0f; // 현재 알파값 (또는 canvasGroup.alpha에서 시작)
        while (timer > 0f)
        {
            timer -= Time.deltaTime;
            if (canvasGroup != null) canvasGroup.alpha = timer;
            yield return null;
        }
        if (canvasGroup != null) canvasGroup.alpha = 0;
    }
}