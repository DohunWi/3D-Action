using UnityEngine;
using System; // Action 이벤트를 위해 필요

public class PlayerWallet : MonoBehaviour
{
    [Header("Memory Fragments")]
    [SerializeField] private int currentMemory = 0;

    // UI에 "기억이 갱신됨"을 알리는 이벤트
    public event Action<int> OnMemoryChanged;

    private void Start()
    {
        OnMemoryChanged?.Invoke(currentMemory);
    }

    // 기억 회수 (획득)
    public void CollectMemory(int amount)
    {
        currentMemory += amount;
        // Debug.Log($"✨ {amount}개의 잃어버린 기억을 되찾았습니다.");
        OnMemoryChanged?.Invoke(currentMemory);
    }

    // 기억 소모 (상점/강화)
    public bool SpendMemory(int amount)
    {
        if (currentMemory >= amount)
        {
            currentMemory -= amount;
            OnMemoryChanged?.Invoke(currentMemory);
            return true;
        }
        
        Debug.Log("기억이 희미해 강화를 할 수 없습니다... (잔액 부족)");
        return false;
    }
    // 돈을 획득하는 함수
    public void AddMemory(int amount)
    {
        currentMemory += amount;
        Debug.Log($"기억의 파편 획득: +{amount} (현재: {currentMemory})");

        // UI 갱신 이벤트 호출
        OnMemoryChanged?.Invoke(currentMemory);
        
    }
    public int GetCurrentMemory() => currentMemory;
    public void SetCurrentMemory(int memory) {currentMemory = memory;}
}