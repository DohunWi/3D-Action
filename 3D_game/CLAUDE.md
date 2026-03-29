# CLAUDE.md — Lucid Knight (Project: Somnia)

## 프로젝트 개요

- **장르**: Souls-like 3D 액션 RPG
- **엔진**: Unity 6 (v6000.3.2f1)
- **언어**: C#
- **테마**: 꿈/악몽 기반 심리적 세계관 (Lucid Dreaming / Nightmare)
- **메인 씬**: `Assets/Scenes/Somnia.unity`

---

## 디렉토리 구조

```
Assets/Scripts/
├── Manager/         # Singleton 관리자 (GameManager, SoundManager, UIManager, GameFeelManager)
├── Player/          # PlayerController, PlayerStats, PlayerWeapon, PlayerHUD, PlayerLockOn 등
├── Enemy/           # Enemy(FSM), EliteEnemy, DragonBossAI, EnemyStats, EnemyWeapon, BossHurtbox
├── SO/              # ScriptableObject (WeaponData, SkillData)
├── Interface/       # IDamageable, IInteractable
├── Interaction/     # NPC/오브젝트 상호작용
├── Currency/        # MemoryPickup, LostMemoryPickup
├── VFX/             # DamagePopup, BreathDamage, NightmareSpike
└── Map/             # 레벨 메카닉
```

---

## 핵심 아키텍처 패턴

| 패턴 | 사용처 |
|------|--------|
| **Singleton** | GameManager, SoundManager, UIManager, GameFeelManager, TimeManager |
| **State Machine (FSM)** | PlayerController (PlayerState), Enemy (EnemyState) |
| **Observer/Event** | `event Action` 기반 — OnEgoChanged, OnTakeDamage, OnDeath 등 |
| **Inheritance** | CharacterStats → PlayerStats / EnemyStats, Weapon → PlayerWeapon / EnemyWeapon, Enemy → EliteEnemy / DragonBossAI |
| **Interface** | IDamageable, IInteractable |
| **ScriptableObject** | WeaponData (공격력/콤보 배율/VFX), SkillData (비용/쿨타임/범위) |

---

## 코딩 컨벤션

### 변수 명명
```csharp
private float _camelCase;          // private 필드: 언더스코어 prefix
public float camelCase;            // public 필드: camelCase
public float Property { get; set; } // Property: PascalCase
```

### 메서드 명명
```csharp
public void PublicMethod() {}       // public: PascalCase
private void privateMethod() {}     // private: camelCase
private void OnEventName() {}       // 이벤트 핸들러: On + PascalCase
```

### Animation Hash 캐싱
```csharp
private static readonly int AnimID_DoAttack = Animator.StringToHash("doAttack");
```

### 주석 스타일
- 한국어 + 영어 혼용
- 섹션 구분: `// --- Section Name ---`
- 이모지 사용: `🆕 🩸 💾 💀 ✅` (섹션 강조)
- 복잡한 로직은 번호 + 이모지로 단계별 설명

---

## 게임 스탯 시스템

| 인게임 명칭 | 코드 변수 | 역할 | 산출식 |
|---|---|---|---|
| **에고 (Ego)** | `currentEgo` | HP | `sanity × 10` |
| **명료함 (Lucidity)** | `currentLucidity` | MP/마나 | `awareness × 5` |
| **의지 (Volition)** | `currentVolition` | 스태미나 | `50 + tenacity × 3` |
| **기억 조각 (Memory)** | wallet.memory | 재화/소울 | 죽으면 드롭 |

### 성장 스탯
- **Sanity** → 최대 에고
- **Awareness** → 최대 명료함
- **Tenacity** → 최대 의지
- **Conviction** → 공격 스케일링
- **Insight** → 이동 속도

---

## 전투 메카닉 요약

- **콤보**: 3연타 기본, 각 타격마다 배율 적용, 타격 시 Volition 소모
- **패리/반격**: 90도 정면 판정, 성공 시 1.5초 카운터 윈도우, 카운터 2배 데미지 + 넘어짐
- **컴포저 (Composure)**: 방어력 같은 개념, 0이 되면 스태거 + 50 컴포저 피해, 3초 후 회복
- **데스 루프**: 소울즈 방식 — 사망 시 기억 조각 드롭, 제단에서 부활, 재방문하면 회수 가능

---

## 상태 열거형

```csharp
// Player
enum PlayerState { Locomotion, Roll, Attack, CounterAttack, Skill, Parry, Hit, Die, Interact, UseItem }

// Enemy
enum EnemyState { Idle, Patrol, Chase, Attack, Parried, Hit, Down, Die }
```

---

## 데이터 저장 구조 (JSON)

- 경로: `Application.persistentDataPath/save.json`
- **GameData.cs**: level, currentExp, memory, sanity, awareness, tenacity, conviction, insight, sceneName, posX/Y/Z, currentPotions

---

## 주요 파일 경로

- [PlayerController.cs](Assets/Scripts/Player/PlayerController.cs)
- [PlayerStats.cs](Assets/Scripts/Player/PlayerStats.cs)
- [Enemy.cs](Assets/Scripts/Enemy/Enemy.cs)
- [DragonBossAI.cs](Assets/Scripts/Enemy/DragonBossAI.cs)
- [CharacterStats.cs](Assets/Scripts/CharacterStats.cs)
- [GameManager.cs](Assets/Scripts/Manager/GameManager.cs)
- [GameData.cs](Assets/Scripts/Manager/GameData.cs)
- [SoundManager.cs](Assets/Scripts/Manager/SoundManager.cs)
- [WeaponData.cs](Assets/Scripts/SO/WeaponData/WeaponData.cs)
- [SkillData.cs](Assets/Scripts/SO/Skill/SkillData.cs)

---

## 작업 시 주의사항

1. **UI는 이벤트 구동** — 폴링 없이 `event Action` 구독 패턴만 사용
2. **스탯 변경은 항상 이벤트 발행** — HUD 등 구독자가 있으므로 직접 값만 바꾸지 말 것
3. **Singleton 접근**: `GameManager.Instance`, `SoundManager.Instance` 등
4. **Animation 트리거는 Hash로** — 문자열 직접 사용 금지, `AnimID_*` 상수 사용
5. **ScriptableObject로 밸런싱** — 무기/스킬 수치는 코드 하드코딩 말고 SO 활용
6. **한국어 주석 허용** — 프로젝트 전반에 한국어 주석 사용 중이므로 유지

---

## 사용 에셋/플러그인

- **TextMesh Pro**: UI 텍스트
- **Cinemachine**: 카메라 (스킬 임펄스 등)
- **NavMesh**: 적 길찾기
- **New Input System**: 입력 처리
- Mixamo (캐릭터/애니메이션), Stylized Crystal (환경), Particle Pack (VFX)
