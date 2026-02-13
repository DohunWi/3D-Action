using UnityEngine;
using TMPro; // TextMeshPro 필수
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections.Generic; // 리스트 사용
using System.Linq;

public class StatUpgradeUI : MonoBehaviour
{
    [System.Serializable]
    public class StatRow
    {
        public string label;          // 인스펙터 구별용 이름 (예: Vigor)
        public StatType statType;     // 스탯 종류
        public TextMeshProUGUI valueText; // 현재 수치 텍스트
        public TextMeshProUGUI costText;  // ★ 각자 다른 비용 표시용
        public Button upgradeButton;      // 버튼 (돈 없으면 끄기 위해)
    }
    [Header("Target")]
    public PlayerStats playerStats;
    public PlayerWallet playerWallet;
    public PlayerController playerController;

    [Header("UI References")]
    public CanvasGroup canvasGroup;
    public TextMeshProUGUI levelText;        // 전체 레벨
    public TextMeshProUGUI currentMemoryText;// 보유 재화

    [Header("Input Settings")]
    // ★ 인스펙터에서 아까 만든 'Cancel' 액션을 드래그해서 넣을 변수
    public InputActionReference cancelAction;
    
    [Header("Stat Rows (Auto Setup Available)")]
    public List<StatRow> statRows = new List<StatRow>();

    private void OnEnable()
    {
        // UI가 켜질 때 입력 감지 시작
        if (cancelAction != null) cancelAction.action.Enable();
    }

    private void OnDisable()
    {
        // UI 꺼질 때 입력 감지 중단 (최적화)
        if (cancelAction != null) cancelAction.action.Disable();
    }

    private void Start()
    {
        // ★ 게임 시작하자마자 "투명하게" 만들어서 숨김
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        Close();
    }

   private void Update()
    {
        // 투명하면(닫혀있으면) 입력 무시
        if (canvasGroup.alpha == 0) return;

        if (cancelAction != null && cancelAction.action.triggered)
        {
            Close();
        }
    }

    public void ToggleMenu()
    {
        bool isOpen = canvasGroup.alpha > 0f;

        if (isOpen) 
        {
            Close();
        }
        else 
        {
            Open();
    }
    }

    public void Open()
    {
        if (playerStats == null || playerWallet == null) return;
        playerController.ChangeState(PlayerState.Interact);
        // ★ 핵심: 켜는 게 아니라 투명도만 1로 올림
        UpdateUI();
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        
        // 게임 정지
        Time.timeScale = 0f;
        
        // 마우스 커서 보이게 하기
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }


    public void Close()
    {
        playerController.ChangeState(PlayerState.Locomotion);
        // ★ 핵심: 끄는 게 아니라 투명도만 0으로 내림
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        
        // 게임 재개
        Time.timeScale = 1f;
        
        // 마우스 다시 잠그기 (FPS/TPS 게임이라면)
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    // UI 갱신 (버튼 누를 때마다 호출)
    private void UpdateUI()
    {
        // 1. 공통 정보 갱신
        levelText.text = $"Lv. {playerStats.level}"; // 전체 레벨 (경험치 기반)
        int currentMoney = playerWallet.GetCurrentMemory();
        currentMemoryText.text = $"{currentMoney:N0}";

        // 2. 각 스탯 줄(Row)을 돌면서 정보 갱신
        foreach (var row in statRows)
        {
            // (A) 현재 스탯 값 가져오기
            int currentValue = GetStatValue(row.statType);
            
            // (B) 텍스트 갱신
            if (row.valueText != null) row.valueText.text = $"{currentValue}";

            // (C) 비용 계산 (스탯 레벨에 따라 증가)
            // 공식: 기본 100 + (현재수치 * 100) -> 1렙:200원, 10렙:1100원
            int cost = CalculateStatCost(currentValue);
            
            if (row.costText != null) 
            {
                row.costText.text = $"{cost:N0}";
                
                // (D) 돈 부족하면 빨간색 표시
                row.costText.color = (currentMoney >= cost) ? Color.white : Color.red;
            }

            // (E) 돈 부족하면 버튼 비활성화 (선택 사항)
            if (row.upgradeButton != null)
            {
                row.upgradeButton.interactable = (currentMoney >= cost);
            }
        }
    }
    private int CalculateStatCost(int currentStatValue)
    {
        int calculatedCost = (currentStatValue - 10) * 100 + 100;
        return Mathf.Max(100, calculatedCost);
    }

    // 스탯 타입으로 현재 수치를 가져오는 헬퍼 함수
    private int GetStatValue(StatType type)
    {
        switch (type)
        {
            case StatType.Vigor: return playerStats.vigor;
            case StatType.Mind: return playerStats.mind;
            case StatType.Endurance: return playerStats.endurance;
            case StatType.Strength: return playerStats.strength;
            case StatType.Dexterity: return playerStats.dexterity;
            default: return 0;
        }
    }

    // 버튼 클릭 연결 함수 (인스펙터 버튼 OnClick에 연결)
    // index 대신 StatType을 직접 쓰거나, 기존처럼 index(0~4)를 써도 됨.
    public void OnClickUpgrade(int statIndex)
    {
        // 리스트에서 해당 타입 찾기
        StatType type = (StatType)statIndex;
        StatRow targetRow = statRows.Find(r => r.statType == type);

        if (targetRow == null) return;

        int currentValue = GetStatValue(type);
        int cost = CalculateStatCost(currentValue);

        if (playerWallet.GetCurrentMemory() >= cost)
        {
            playerWallet.SpendMemory(cost);
            playerStats.UpgradeStat(type);
            UpdateUI();
            Debug.Log($"{type} 강화 완료 (-{cost})");
        }
        else
        {
            Debug.Log("돈이 부족합니다.");
        }
    }
}