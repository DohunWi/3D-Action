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
    [Header("--- Player Growth Stats ---")]
    public int level = 1;
    public int sanity = 10;      
    public int awareness = 10;       
    public int tenacity = 10;  
    public int conviction = 10;   
    public int insight = 10;

    [Header("Experience System")]
    public int currentExp = 0;     // 현재 경험치
    public int maxExp = 100;       // 레벨업에 필요한 경험치

    [Header("--- Calculated Combat Stats ---")]
    public float attackPower; // 최종 공격력 (무기 데미지 + 스탯 보정)

    [Header("Player Specific")]
    public PlayerController _playerController; // 상태 확인용 연결
    public PlayerPotion _playerPotion;
    public float parryAngle = 90f; // 전방 90도 안에서 온 공격만 막기

    [Header("Volition Settings")] // Volition(Stamina) 설정
    public float maxVolition = 100f;
    public float currentVolition { get; private set; } // 읽기 전용 프로퍼티
    public float volitionRegenRate = 15f;   // 초당 회복량
    public float volitionRegenDelay = 2.0f; // 스태미나 쓴 후 회복 시작까지 딜레이
    private float _lastVolitionUseTime; // 마지막 사용 시간 기록용

    [Header("Lucidity Settings")] // Lucidity (Mana) 설정
    public float maxLucidity = 100f;
    public float currentLucidity { get; private set; }
    public float lucidityRegenRate = 5f; // 초당 5 회복 (스태미나보다 느리게)
    [Header("Movement Settings")]
    public float baseMoveSpeed = 4.5f; // 기본 이동 속도 
    public float baseSprintSpeed = 9.5f; // 기본 이동 속도 

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
        // 1. 매니저에게 맡겨둔 내 돈과 스탯 Get
        if (GameManager.Instance != null)
        {
            PlayerWallet myWallet = GetComponent<PlayerWallet>();
            GameManager.Instance.ApplyStatsToPlayer(this, myWallet);
        }

        // 1. 스탯 먼저 계산 (이게 maxHealth, maxMana 등을 설정함)
        RecalculateStats();

        // 2. 부모의 Start 실행
        base.Start();
        currentVolition = maxVolition; // 시작 시 풀 충전
        currentLucidity = maxLucidity;

        // 시작하자마자 UI 한번 갱신해줌 (꽉 찬 상태 보여주기)
        OnLucidityChanged?.Invoke(currentLucidity, maxLucidity);
        OnVolitionChanged?.Invoke(currentVolition, maxVolition);
    }
    // ★ 스탯 기반 능력치 재계산 로직
    public void RecalculateStats()
    {
        // 1. 생명력 -> 최대 체력 (부모 변수 maxHealth 수정)
        float oldMaxEgo = maxEgo;
        // 공식: 기본 50 + (생명력 * 10)
        maxEgo = sanity * 10f; 

        // 레벨업으로 체력통이 커졌으면, 커진 만큼 현재 체력도 채워줌 (공짜 회복 느낌)
        if (currentEgo > 0 && maxEgo > oldMaxEgo)
        {
            float EgoDiff = maxEgo - oldMaxEgo;
            currentEgo += EgoDiff;
            // 부모 이벤트 호출 (CharacterStats의 UI 갱신)
            // base.OnHealthChanged는 public event가 아니면 직접 호출 불가할 수 있음.
            // 하지만 CharacterStats.cs를 보니 event가 public임.
            // 만약 event 호출이 안되면 TakeDamage(0) 같은 꼼수나 별도 함수 필요.
            // 여기서는 CharacterStats의 OnHealthChanged가 public Action이므로 직접 호출 가능하다고 가정하거나
            // CharacterStats에 RefreshUI() 같은 함수를 두는게 좋지만, 일단 직접 수정은 안한다고 했으므로 패스.
        }

        // 2. 정신력 -> 최대 마나
        maxLucidity = awareness * 5f;

        // 3. 지구력 -> 최대 스태미나
        maxVolition = 50f + (tenacity * 3f);

        // 4. 근력(Strength) -> 기본 공격력 (맨손 or 무기 보정용 기초값)
        // 무기 데미지 계산식(CalculateTotalDamage)에서 strength를 직접 쓰므로
        // 여기서는 attackPower를 '표기용'이나 '맨손 데미지'로만 씁니다.
        attackPower = conviction * 1.5f;

        // ★ 5. 기량(Dexterity) -> 이동 속도 변경!
        if (_playerController != null)
        {
            // 공식: 기본속도 + (기량 * 0.05) 
            // 예: 기량 10 -> +0.5 증가 / 기량 50 -> +2.5 증가
            // 너무 빨라지지 않게 소수점을 작게 잡는 것이 포인트입니다.
            float bonusSpeed = insight * 0.05f;
            
            // PlayerController에 있는 변수(예: moveSpeed)를 직접 수정
            _playerController.moveSpeed = baseMoveSpeed + bonusSpeed;
            _playerController.sprintSpeed = baseSprintSpeed + bonusSpeed;
            // (선택) 구르기 속도나 애니메이션 속도도 같이 올릴 수 있습니다.
            // _playerController.rollSpeed = ...
        }

        // UI 갱신 (마나, 스태미나) - 체력은 위에서 처리됨
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
        if (Time.time > _lastVolitionUseTime + volitionRegenDelay && currentVolition < maxVolition)
        {
            float prevStamina = currentVolition;
            currentVolition += volitionRegenRate * Time.deltaTime;
            currentVolition = Mathf.Min(currentVolition, maxVolition); // 최대치 초과 방지
            // 값이 조금이라도 변했다면 UI 갱신
            if (currentVolition != prevStamina)
                OnVolitionChanged?.Invoke(currentVolition, maxVolition);
        }
        // 마나 자동 회복 (항상 천천히 회복)
        if (currentLucidity < maxLucidity)
        {
            currentLucidity += lucidityRegenRate * Time.deltaTime;
            currentLucidity = Mathf.Min(currentLucidity, maxLucidity);
            OnLucidityChanged?.Invoke(currentLucidity, maxLucidity); // UI 갱신
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
        Debug.Log("마나가 부족합니다!");
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
        
        Debug.Log("스태미나 부족! (헥헥)");
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
            Debug.Log("구르기 무적(i-frame)으로 공격을 피했습니다!");
            return; // 데미지 적용 안 하고 종료
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
                    Debug.Log($"<color=cyan>패링 성공! (Angle: {angle:F1})</color>");
                    
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
        Debug.Log($"<color=green>자아 회복! (+{amount}) 현재: {currentEgo}</color>");
    }
}