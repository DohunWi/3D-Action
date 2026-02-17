using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System;
using System.IO; 

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Prefabs")]
    public GameObject lostMemoryPrefab; 

    [Header("Lost Memory Data (RAM Only)")]
    // 유실물은 굳이 파일 저장 안 하고 램에만 둬도 됨 (게임 끄면 사라지는 게 보통)
    public bool hasLostMemory = false;
    public int lostMemoryAmount = 0;
    public Vector3 lostMemoryPos;
    public string lostSceneName; 

    [Header("Player Data (Runtime & Save)")]
    public int level = 1;
    public int currentExp = 0;
    public int maxExp = 100;
    public int memory = 0; 

    [Header("Attributes")]
    public int sanity = 10;
    public int awareness = 10;
    public int tenacity = 10;
    public int conviction = 10;
    public int insight = 10;

    [Header("Save Data Buffer")]
    // 저장된 위치를 잠시 기억할 변수 추가
    public bool newGame = false;  // 테스트용 플래그 - 인스펙터에서 설정
    public Vector3 lastSavedPosition; 
    public bool isLoadedGame = false; // 이어하기/부활인지, 새 게임인지 구분
    private string path;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); 
            path = Path.Combine(Application.persistentDataPath, "save.json");
            // =========================================================
            // 게임 켜지자마자 저장된 파일 읽기 (Auto Load)
            // =========================================================
            if (newGame) // 테스트용 플래그 - 인스펙터에서 설정
            {
                Debug.Log("🆕 게임 시작: 저장된 파일 없음 (새 게임 상태)");
            }
            else
            {
                if (File.Exists(path))
                {
                    LoadGameFromJson(); // 여기서 JSON 데이터를 매니저 변수로 가져옴
                    Debug.Log("📂 게임 시작: 저장된 데이터 로드 완료");
                }
                else
                {
                    Debug.Log("🆕 게임 시작: 저장된 파일 없음 (새 게임 상태)");
                }
            }
        }
        else
        {
            Destroy(gameObject); 
        }
    }
    private void Start()
    {
        // 씬 로드 이벤트(OnSceneLoaded)는 씬이 '바뀔 때'만 발동함.
        // 맨 처음 게임을 켰을 때(이미 씬에 있는 상태)는 발동 안 함.
        // 그래서 Start에서 수동으로 한 번 적용해줘야 함.

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            PlayerStats pStats = player.GetComponent<PlayerStats>();
            PlayerWallet pWallet = player.GetComponent<PlayerWallet>();
            
            // 아까 Awake에서 읽어온 데이터를 플레이어에게 주입
            ApplyStatsToPlayer(pStats, pWallet);
            
            // (선택) 에디터 테스트 편의를 위해:
            // 저장된 위치로 이동시킬지, 아니면 에디터에 배치한 위치에서 시작할지 결정
            // 보통 에디터 테스트 중에는 위치 이동은 안 하는 게 편함.
            // if (isLoadedGame) player.transform.position = lastSavedPosition;
        }
    }

    // -----------------------------------------------------------------------
    // 1️⃣ 유실물 (Lost Memory) 로직 - 죽은 위치에 생성
    // -----------------------------------------------------------------------
    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 1. 유실물이 있고, 죽었던 그 맵에 돌아왔다면 생성
        if (hasLostMemory && scene.name == lostSceneName)
        {
            if (lostMemoryPrefab != null)
            {
                Instantiate(lostMemoryPrefab, lostMemoryPos + Vector3.up * 0.5f, Quaternion.identity);
                Debug.Log($"🩸 유실물 생성됨! ({lostMemoryAmount} Memory)");
            }
        }
        // 2. 플레이어 위치 이동 (로드된 게임일 경우만)
        if (isLoadedGame)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                // CharacterController를 쓴다면 잠시 껐다 켜야 이동됨
                CharacterController cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                // 위치 이동
                player.transform.position = lastSavedPosition;
                
                // 회전값도 저장했다면 여기서 적용 (지금은 위치만)
                // player.transform.rotation = ...

                if (cc != null) cc.enabled = true;

                Debug.Log($"📍 플레이어 위치 복구 완료: {lastSavedPosition}");
            }
        }
    }

    public void SaveLostMemory(int amount, Vector3 pos)
    {
        if (amount > 0)
        {
            hasLostMemory = true;
            lostMemoryAmount = amount;
            lostMemoryPos = pos;
            lostSceneName = SceneManager.GetActiveScene().name;
        }
        else
        {
            // 돈 없으면 유실물도 없음
            hasLostMemory = false;
        }
    }
    public int GetLostMemoryAmount()
    {
        return lostMemoryAmount;
    }

    public void ClearLostMemory()
    {
        hasLostMemory = false;
        lostMemoryAmount = 0;
    }

    // -----------------------------------------------------------------------
    // 2️⃣ 데이터 동기화 (GameManager -> PlayerStats)
    // JSON에서 로드한 데이터를 실제 플레이어에게 적용하는 유틸리티 함수
    // -----------------------------------------------------------------------
    public void ApplyStatsToPlayer(PlayerStats stats, PlayerWallet wallet)
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
        
        stats.RecalculateStats(); // 적용된 스탯으로 HP/MP/SP 재계산
        Debug.Log("✅ 플레이어 스탯 동기화 완료");
    }

    // -----------------------------------------------------------------------
    // 3️⃣ 흐름 제어 (New Game, Continue, Death Loop)
    // -----------------------------------------------------------------------
    public void StartNewGame()
    {
        // 초기화
        level = 1; currentExp = 0; memory = 0;
        sanity = 10; awareness = 10; tenacity = 10; conviction = 10; insight = 10;
        hasLostMemory = false;
        isLoadedGame = false;

        SceneManager.LoadScene("Somnia"); // 실제 씬 이름으로 변경
    }

    public void ContinueGame()
    {
        if (LoadGameFromJson())
        {
            // 로드된 데이터(sceneName)가 있다면 그 씬으로, 없다면 Somnia
            SceneManager.LoadScene("Somnia"); 
        }
        else
        {
            StartNewGame();
        }
    }

    public void RespawnAtAltar()
    {
        // 1. 마지막 세이브(제단) 불러오기
        if (!LoadGameFromJson()) 
        {
            StartNewGame();
            return;
        }

        // 2. 소울라이크 규칙: 죽었으니 소지금은 0원 (유실물은 이미 저장됨)
        memory = 0; 

        // 3. 씬 재시작 (플레이어 위치는 Altar 로직이나 시작 지점으로 이동됨)
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        
        Debug.Log("💀 사망 루프: 제단 스탯으로 부활 (Memory 0)");
    }

    // -----------------------------------------------------------------------
    // 4️⃣ JSON 저장 / 로드
    // -----------------------------------------------------------------------
    public void SaveGameToJson(PlayerStats playerStats, PlayerPotion potion)
    {
        GameData data = new GameData();

       if (playerStats != null)
        {
            // 1. 성장 스탯 (레벨, 경험치)
            data.level = playerStats.level;
            data.currentExp = playerStats.currentExp;

            // 2. ★ [수정] 속성 스탯도 플레이어 값을 직접 저장!
            // (이제 매니저의 sanity 변수는 저장할 때 무시됨)
            data.sanity = playerStats.sanity;
            data.awareness = playerStats.awareness;
            data.tenacity = playerStats.tenacity;
            data.conviction = playerStats.conviction;
            data.insight = playerStats.insight;

            // 3. 위치 저장
            data.sceneName = SceneManager.GetActiveScene().name;
            data.posX = playerStats.transform.position.x;
            data.posY = playerStats.transform.position.y;
            data.posZ = playerStats.transform.position.z;
        }
        else
        {
            // 플레이어가 없으면 매니저가 기억하던 값 사용
            data.level = level;
            data.currentExp = currentExp;
            data.sanity = sanity;
            data.awareness = awareness;
            data.tenacity = tenacity;
            data.conviction = conviction;
            data.insight = insight;
        };

        // 지갑(Memory) 정보 저장
        data.memory = memory; 
        
        if (potion != null) data.currentPotions = potion.currentPotions;

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(path, json);
        Debug.Log("💾 제단 저장 완료");
    }

    public bool LoadGameFromJson()
    {
        if (!File.Exists(path)) return false;

        string json = File.ReadAllText(path);
        GameData data = JsonUtility.FromJson<GameData>(json);

        level = data.level;
        currentExp = data.currentExp;
        memory = data.memory;

        sanity = data.sanity;
        awareness = data.awareness;
        tenacity = data.tenacity;
        conviction = data.conviction;
        insight = data.insight;
        
        // 위치 정보 매니저 변수에 담아두기
        lastSavedPosition = new Vector3(data.posX, data.posY, data.posZ);
        isLoadedGame = true; // "나 지금 로드한 상태야"라고 표시

        return true;
    }
}