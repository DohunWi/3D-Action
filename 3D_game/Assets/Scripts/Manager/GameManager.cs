using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public event Action OnGameOverEvent;

    [Header("Prefabs")]
    public GameObject lostMemoryPrefab; // ★ 인스펙터에 아까 만든 프리팹 연결 필수!

    [Header("Lost Memory Data")]
    public bool hasLostMemory = false;
    public int lostMemoryAmount = 0;
    public Vector3 lostMemoryPos;
    public string lostSceneName; // 다른 맵에서 죽었을 때를 대비

    // ... (기존 Player Data 변수들) ...
    [Header("Player Data (Saved)")]
    public int level = 1;
    public int currentExp = 0;
    public int maxExp = 100;
    public int memory = 0; 

    // ... (스탯 변수들) ..
    [Header("Attributes")]
    public int sanity = 10;
    public int awareness = 10;
    public int tenacity = 10;
    public int conviction = 10;
    public int insight = 10;

    private bool _isGameOver = false;

     private void Awake()
    {
        // ★ 싱글톤 패턴 강화: 씬이 바껴도 파괴되지 않도록 설정
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 핵심: 나를 파괴하지 마라!
        }
        else
        {
            // 이미 원조 GameManager가 존재한다면, 새로 생긴 나는 가짜다.
            Destroy(gameObject); 
        }
    }

    // ★ 씬 로드될 때마다 유실물 소환 체크
    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 1. 잃어버린 돈이 있고 && 2. 죽었던 그 맵이라면
        if (hasLostMemory && scene.name == lostSceneName)
        {
            if (lostMemoryPrefab != null)
            {
                // 바닥에 살짝 묻히지 않게 Y축 +0.5f
                Instantiate(lostMemoryPrefab, lostMemoryPos + Vector3.up * 0.5f, Quaternion.identity);
                Debug.Log($"유실물 생성됨! 위치: {lostMemoryPos}");
            }
        }
    }

    // ★ 죽었을 때 호출: 위치와 돈 저장 (덮어쓰기)
    public void SaveLostMemory(int amount, Vector3 pos)
    {
        // 엘든링 방식: 죽으면 기존에 떨군 돈은 사라짐 (덮어쓰기)
        // 만약 0원을 들고 죽었다? -> 기존 유실물은 사라지고, 새 유실물도 안 생김 (완전 삭제)
        if (amount > 0)
        {
            hasLostMemory = true;
            lostMemoryAmount = amount;
            lostMemoryPos = pos;
            lostSceneName = SceneManager.GetActiveScene().name;
            Debug.Log($"[사망] 돈 {amount}원을 바닥에 떨어뜨렸습니다.");
        }
        else
        {
            hasLostMemory = false;
            Debug.Log("[사망] 가진 돈이 없어 유실물이 생성되지 않았습니다 (기존 유실물 소멸).");
        }
    }

    // ★ 유실물 프리팹이 호출 (얼만지 확인용)
    public int GetLostMemoryAmount() => lostMemoryAmount;

    // ★ 먹었을 때 호출 (데이터 클리어)
    public void ClearLostMemory()
    {
        hasLostMemory = false;
        lostMemoryAmount = 0;
    }

    // ★ 1. 플레이어 -> 매니저 (데이터 맡기기)
    // 죽기 직전에 호출해야 함
    public void SavePlayerData(PlayerStats stats, PlayerWallet wallet)
    {
        level = stats.level;
        currentExp = stats.currentExp;
        maxExp = stats.maxExp;
        
        sanity = stats.sanity;
        awareness = stats.awareness;
        tenacity = stats.tenacity;
        conviction = stats.conviction;
        insight = stats.insight;

        if (wallet != null)
        {
            memory = wallet.GetCurrentMemory();
        }

        Debug.Log("[GameManager] 데이터 저장 완료 (백업 성공)");
    }

    // ★ 2. 매니저 -> 플레이어 (데이터 돌려주기)
    // 부활하자마자 호출해야 함
    public void LoadPlayerData(PlayerStats stats, PlayerWallet wallet)
    {
        stats.level = level;
        stats.currentExp = currentExp;
        stats.maxExp = maxExp;
        
        stats.sanity = sanity;
        stats.awareness = awareness;
        stats.tenacity = tenacity;
        stats.conviction = conviction;
        stats.insight = insight;

        if (wallet != null)
        {
            wallet.SetCurrentMemory(memory);
        }
        
        // 스탯 수치가 바뀌었으니, 체력/마나/공격력 등을 다시 계산하라고 명령
        stats.RecalculateStats();

        Debug.Log("[GameManager] 데이터 로드 완료 (복구 성공)");
    }

    public void GameOver()
    {
        if (_isGameOver) return;
        _isGameOver = true;

        Debug.Log("Game Over Logic Start");

        OnGameOverEvent?.Invoke();
        StartCoroutine(RestartGame());
    }

    IEnumerator RestartGame()
    {
        yield return new WaitForSeconds(5.0f);
        _isGameOver = false; // 재시작 전에 플래그 초기화
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}