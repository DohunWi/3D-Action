using UnityEngine;
using System;

// CharacterStats를 상속받음
// 스탯 종류 정의 (외부에서 쓰기 편하게 클래스 밖이나 위에 선언)
public enum StatType
{
    Sanity,      // Sanity -> Ego
    Awareness,       // Awareness -> Lucidity
    Tenacity,  // Tenacity -> Volition
    Conviction,   // Conviction -> 공격력
    Insight   // Insight -> 치명타/공속/보조공격력
}
public class PlayerStats : CharacterStats
{
    [Header("--- Base Stats (ScriptableObject) ---")]
    public PlayerBaseStatsSO baseStats;

    [Header("--- Player Growth Stats ---")]
    public int level = 1;
    public int sanity;
    public int awareness;
    public int tenacity;
    public int conviction;
    public int insight;

    [Header("Experience System")]
    public int currentExp = 0;
    public int maxExp = 100;

    [Header("--- Calculated Combat Stats ---")]
    public float attackPower;

    [Header("Player Specific")]
    public PlayerController _playerController;
    public PlayerPotion _playerPotion;
    public float parryAngle = 90f;

    [Header("Volition Settings")]
    public float maxVolition;
    public float currentVolition { get; private set; }
    private float _lastVolitionUseTime;

    [Header("Lucidity Settings")]
    public float maxLucidity;
    public float currentLucidity { get; private set; }

    // ★ UI에게 보낼 신호들 (현재값, 최대값)
    public event Action<float, float> OnLucidityChanged;
    public event Action<float, float> OnVolitionChanged;
    public event Action OnStatsRefreshed;

    // 부모의 TakeDamage를 덮어씀 (Override)
    private void Awake()
    {
        if (_playerController == null || _playerPotion == null)
        {
            _playerController = GetComponent<PlayerController>();
            _playerPotion = GetComponent<PlayerPotion>();
        }
    }
    public override void Start()
    {
        // SO가 없으면 경고
        if (baseStats == null)
            Debug.LogWarning("[PlayerStats] PlayerBaseStatsSO가 연결되지 않았습니다!");

        bool isLoaded = GameManager.Instance != null && GameManager.Instance.isLoadedGame;
        Debug.Log($"🔍 PlayerStats.Start - isLoadedGame: {isLoaded}, GameManager: {(GameManager.Instance != null ? "O" : "X")}");

        if (isLoaded)
        {
            // 세이브 파일 있을 때만 GameManager 값 적용
            PlayerWallet myWallet = GetComponent<PlayerWallet>();
            Debug.Log($"📂 로드 경로: 저장된 데이터 적용 (Lv.{GameManager.Instance.level})");
            GameManager.Instance.ApplyStatsToPlayer(this, myWallet);
        }
        else
        {
            // 뉴게임 or 테스트 씬 → SO 기본값 사용
            Debug.Log($"🆕 초기화 경로: SO 기본값 사용");
            InitFromSO();
        }

        RecalculateStats();
        base.Start();
        currentVolition = maxVolition;
        currentLucidity = maxLucidity;

        OnLucidityChanged?.Invoke(currentLucidity, maxLucidity);
        OnVolitionChanged?.Invoke(currentVolition, maxVolition);

        Debug.Log($"✅ PlayerStats 초기화 완료: Lv.{level}, HP={maxEgo}, 스탯={sanity}/{awareness}/{tenacity}/{conviction}/{insight}");
    }

    // SO 기본값으로 초기화 (뉴게임 or 테스트)
    public void InitFromSO()
    {
        if (baseStats == null) return;
        sanity     = baseStats.sanity;
        awareness  = baseStats.awareness;
        tenacity   = baseStats.tenacity;
        conviction = baseStats.conviction;
        insight    = baseStats.insight;
        maxExp     = baseStats.startingMaxExp;
    }
    public void RecalculateStats()
    {
        // SO가 없으면 계산 불가
        if (baseStats == null) return;

        // 1. Ego (체력)
        float oldMaxEgo = maxEgo;
        maxEgo = sanity * baseStats.egoPerSanity;
        if (currentEgo > 0 && maxEgo > oldMaxEgo)
            currentEgo += maxEgo - oldMaxEgo;

        // 2. Lucidity (마나)
        maxLucidity = awareness * baseStats.lucidityPerAwareness;

        // 3. Volition (스태미나)
        maxVolition = baseStats.baseVolition + tenacity * baseStats.volitionPerTenacity;

        // 4. 공격력
        attackPower = conviction * baseStats.attackPerConviction;

        // 5. 이동 속도
        if (_playerController != null)
        {
            float bonus = insight * baseStats.speedPerInsight;
            _playerController.moveSpeed   = baseStats.baseMoveSpeed   + bonus;
            _playerController.sprintSpeed = baseStats.baseSprintSpeed + bonus;
        }

        // 6. 강인도 (CharacterStats 값 갱신)
        maxComposure          = baseStats.maxComposure;
        composureRecoveryTime = baseStats.composureRecoveryTime;
        composureRecoveryRate = baseStats.composureRecoveryRate;

        OnLucidityChanged?.Invoke(currentLucidity, maxLucidity);
        OnVolitionChanged?.Invoke(currentVolition, maxVolition);
        OnStatsRefreshed?.Invoke();
    }
    // 레벨업 기능 (UI 버튼에서 호출)
    public void UpgradeStat(StatType type)
    {
        switch (type)
        {
            case StatType.Sanity: sanity++; break;
            case StatType.Awareness: awareness++; break;
            case StatType.Tenacity: tenacity++; break;
            case StatType.Conviction: conviction++; break;
            case StatType.Insight: insight++; break;
        }
        RecalculateStats(); // 수치 반영
    }
    public int CalculateTotalDamage(WeaponData weaponData)
    {
        if (weaponData == null) return 0;

        // 1. 무기 기본 데미지
        float baseDmg = weaponData.damage;

        // 2. 보정 데미지 (내 근력 * 무기 보정치)
        // 예: 근력 30 * 보정치 0.5 = +15 데미지
        float scalingDmg = this.conviction * weaponData.convictionScaling;

        // 3. 합산 (반올림 처리)
        return Mathf.RoundToInt(baseDmg + scalingDmg);
    }
    public override void Update()
    {
        // 스태미나 자동 회복 로직
        // "마지막 사용 후 딜레이(2초)가 지났고" AND "스태미나가 꽉 차지 않았다면" -> 회복
        float regenDelay = baseStats != null ? baseStats.volitionRegenDelay : 2f;
        float regenRate  = baseStats != null ? baseStats.volitionRegenRate  : 15f;

        if (Time.time > _lastVolitionUseTime + regenDelay && currentVolition < maxVolition)
        {
            float prev = currentVolition;
            currentVolition = Mathf.Min(currentVolition + regenRate * Time.deltaTime, maxVolition);
            if (currentVolition != prev)
                OnVolitionChanged?.Invoke(currentVolition, maxVolition);
        }

        float lucidRegen = baseStats != null ? baseStats.lucidityRegenRate : 5f;
        if (currentLucidity < maxLucidity)
        {
            currentLucidity = Mathf.Min(currentLucidity + lucidRegen * Time.deltaTime, maxLucidity);
            OnLucidityChanged?.Invoke(currentLucidity, maxLucidity);
        }

        // (테스트용) 스태미나 수치 확인
       // Debug.Log($"Stamina: {currentStamina:F1}");
    }

    // 마나 사용 함수
    public bool UseLucidity(float amount)
    {
        if (currentLucidity >= amount)
        {
            currentLucidity -= amount;
            // Debug.Log($"현재 마나: {currentMana:F1}");
            // ★ UI 알림
            OnLucidityChanged?.Invoke(currentLucidity, maxLucidity);
            return true;
        }
        return false;
    }
    // 스태미나 사용 함수 (성공하면 true, 실패하면 false 리턴)
    public bool UseVolition(float amount)
    {
        if (currentVolition >= amount)
        {
            currentVolition -= amount;
            _lastVolitionUseTime = Time.time; // 사용 시간 갱신 (회복 딜레이 리셋)
            // Debug.Log($"현재 스태미나: {currentStamina:F1}");
            // ★ UI 알림
            OnVolitionChanged?.Invoke(currentVolition, maxVolition);
            return true;
        }
        
        return false;
    }
    // 확인용 함수: 깎지는 않고 검사만 함
    public bool HasVolition(float amount)
    {
        return currentVolition >= amount;
    }    
    public override void TakeDamage(float damage, float composureDamage = 10f, Transform attacker = null)
    {
        // 1. 무적 판정 로직 추가
        if (_playerController != null && _playerController.currentState == PlayerState.Roll)
        {
            return; // 구르기 무적(i-frame)
        }
        // 2. 패링 시도
        if (_playerController.currentState == PlayerState.Parry)
        {
            if (attacker != null)
            {
                // 각도 계산: (적 위치 - 내 위치) 와 (내 앞 방향) 사이의 각도
                Vector3 dirToAttacker = (attacker.position - transform.position).normalized;
                float angle = Vector3.Angle(transform.forward, dirToAttacker);

                // 내 전방(parryAngle / 2) 안에 적이 있는가?
                if (angle < parryAngle * 0.5f)
                {
                    // A. 적에게 경직 주기
                    Enemy enemy = attacker.GetComponentInParent<Enemy>();
                    if (enemy != null) enemy.GetParried();

                    // B. 플레이어에게 반격 기회 부여
                    _playerController.OnParrySuccess();

                    // C. 패링 연출 (Juice)
                    GameFeelManager.Instance.DoParryEffect();

                    return; // 데미지 0
                }
            }
        }

        // 3. 무적이 아니면 부모의 원래 기능(체력 깎기) 실행
        base.TakeDamage(damage, 50.0f, attacker);
    }
    // 디버그용: 패링 각도를 눈으로 확인
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        // 내 위치에서 왼쪽 경계선
        Vector3 leftDir = Quaternion.Euler(0, -parryAngle * 0.5f, 0) * transform.forward;
        // 내 위치에서 오른쪽 경계선
        Vector3 rightDir = Quaternion.Euler(0, parryAngle * 0.5f, 0) * transform.forward;

        Gizmos.DrawRay(transform.position, leftDir * 2f);
        Gizmos.DrawRay(transform.position, rightDir * 2f);
    }
    // -------------------------------------
    // Experince System
    // -------------------------------------
    // ★ 경험치 획득 함수
    public void AddExperience(int amount)
    {
        currentExp += amount;
        // Debug.Log($"경험치 획득: +{amount}");

        // 경험치가 꽉 찼으면 레벨업! (한 번에 여러 렙업 할 수도 있으니 while)
        while (currentExp >= maxExp)
        {
            LevelUp();
        }

        // UI 갱신 이벤트 호출 (이전 시간에 만든 Action 사용)
        OnStatsRefreshed?.Invoke(); 
    }
    private void LevelUp()
    {
        currentExp -= maxExp; // 남은 경험치 이월
        level++; // 전체 레벨 증가

        // ★ 다음 레벨 필요 경험치 증가 (공식은 원하시는 대로 조절)
        // 예: 레벨 * 100 (100 -> 200 -> 300...)
        // 예: 1.2배씩 증가 (100 -> 120 -> 144...)
        maxExp = Mathf.RoundToInt(maxExp * 1.2f); 

        // 레벨업 효과 (체력 회복, 이펙트 등)
        currentEgo = maxEgo; 
        currentLucidity = maxLucidity;
        currentVolition = maxVolition;

        Debug.Log($"<color=green><b>LEVEL UP! (Lv.{level})</b></color>");
        
        // 레벨업 사운드/이펙트 재생
        // if(GameFeelManager.Instance != null) GameFeelManager.Instance.PlayLevelUpEffect();
        if(level % 5 == 0)
        {
            _playerPotion.AddMaxPotion(1);
        }
        // UI 갱신
        OnStatsRefreshed?.Invoke();
    }
    // ------------------------------
    // 체력 회복 시스템 - 자아(Ego) 회복 함수 추가
    // ------------------------------
    public void RestoreEgo(float amount)
    {
        if (currentEgo >= maxEgo) return; // 이미 풀피면 무시

        currentEgo += amount;
        
        // 최대치 넘지 않게 보정
        if (currentEgo > maxEgo) currentEgo = maxEgo;

        // UI 갱신 이벤트 호출 (이름 확인 필요: OnEgoChanged 등)
        InvokeEgoChanged(currentEgo, maxEgo);

        // (선택) 회복 이펙트/사운드 재생 등
    }
}