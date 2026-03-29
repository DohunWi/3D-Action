# DragonBossAI 개선 작업 기록

> 포트폴리오용 개발 일지 | 시행착오 및 문제 해결 과정 중심

---

## 개요

Souls-like 3D 액션 RPG 프로젝트 **Somnia**의 드래곤 보스(The Nightmare Dragon) AI를 개선한 작업 기록.
기존 보스 AI는 단순 거리 기반 단타 공격과 멍청한 이동으로 인해 보스전의 긴장감이 없었고,
이를 콤보 시스템, 랜덤 패턴 선택, 역동적 이동으로 개선했다.

---

## 1. BackAway 이동 방식 — Root Motion vs 코드 이동

### 문제

BackAway 상태(너무 가까울 때 뒤로 물러나는 행동)의 이동 방식이 혼재되어 있었다.

**기존 구조:**
- `OnAnimatorMove`에서 BackAway 상태일 때 `deltaPosition`을 차단 (위치 잠금)
- `UpdateBackAway()`에서 `transform.Translate(backDir * speed)`로 코드 직접 이동

**문제점:**
- `applyRootMotion = true` 상태에서 위치를 코드로 덮어쓰는 구조라 혼란스러움
- 애니메이션 이동과 코드 이동이 충돌 가능성 있음

### 시도 1 — Root Motion으로 전환

`OnAnimatorMove`에서 BackAway 차단 조건을 제거하여 애니메이션의 `deltaPosition`이 그대로 적용되도록 변경.

```csharp
// 변경 전
bool lockPosition = currentState == DragonState.BackAway ||
                    (currentState == DragonState.GroundAttack && !_agent.enabled);

// 변경 후
bool lockPosition = currentState == DragonState.GroundAttack && !_agent.enabled;
```

**결과:** Backaway.anim 클립에 루트 모션 키프레임이 없어서 드래곤이 제자리에 서 있음.

### 시도 2 — 코드 이동으로 확정

Root Motion을 포기하고, `OnAnimatorMove`에서 BackAway 상태일 때 루트 모션 연산 자체를 무시(`return`)하고 `UpdateBackAway()`에서 `transform.Translate(Vector3.back * 10f * Time.deltaTime, Space.Self)`로 직접 이동하는 방식으로 최종 결정.

```csharp
private void OnAnimatorMove()
{
    if (currentState == DragonState.BackAway) return; // 루트 모션 무시, 코드로 이동
    ...
}
```

---

## 2. 보스가 기본 공격(물기)만 반복하는 버그

### 문제

플레이 테스트 결과, 보스가 `attackIndex = 0` (물기)만 계속 사용하고 할퀴기·브레스를 전혀 사용하지 않았다.

### 원인 분석

코드 흐름을 추적한 결과 두 가지 문제가 연쇄적으로 발생하고 있었다.

**원인 1 — 공격 후 제자리에 멈춤**

`OnGroundAttackEnd` 이후 GroundChase로 복귀하면:
- 쿨타임 중 `stoppingDistance = 7.0f`
- 하지만 `stoppingDistance`는 "이미 너무 가까우면 뒤로 물러나라"가 아니라 **목표에 다가갈 때 멈추는 거리**
- 드래곤이 이미 3~4m에 있으면 `dist(4) <= stoppingDistance(7) + 0.5` → `isStopped = true`로 그냥 그 자리에 서버림

**원인 2 — 쿨타임 끝날 때 항상 물기 사거리 안**

쿨타임이 끝났을 때 드래곤이 ~4m에 있으면:

```csharp
// TrySelectGroundPattern(4.0f)
if (dist <= biteRange)   // 4.0 <= 4.0 → 항상 bite (index 0)
{
    currentAttackIndex = 0;
    return true;
}
```

`biteRange = 4.0f`이고 드래곤이 4m 근처에 서 있으니 매번 물기만 선택.

**전체 루프:**
```
GroundChase → (3.5m) BackAway → 물기 → GroundChase
→ 쿨타임 중 3~4m에 isStopped → 쿨타임 끝 → dist ≤ 4.0 → 물기 → 반복
```

### 해결

추후 콤보·랜덤 패턴 시스템으로 근본 해결 (섹션 3 참조).

---

## 3. 콤보 시스템 및 랜덤 패턴 선택 구현

### 목표

- 거리만으로 패턴이 결정되는 단순함 제거
- 2~3연속 공격 콤보 도입
- 공격별 후딜 차등화 (플레이어의 반격 기회 조절)

### 3-1. 가중치 랜덤 패턴 선택

**기존:** 거리 → 패턴 1:1 고정

**변경:** 거리 구간마다 후보군을 두고 확률 선택

| 거리 구간 | 패턴 선택 로직 |
|---|---|
| ≤ biteRange (4m) | 물기 75% / 할퀴기 25% |
| biteRange ~ clawRange (4~8m) | 할퀴기 주력, 브레스 쿨 찼으면 25% |
| clawRange ~ breathRange (8~12m) | 브레스 (쿨 찼을 때) / 할퀴기 |

### 3-2. 콤보 체인 시스템

**핵심 설계 결정 — 할퀴기 애니메이션 특성 반영**

할퀴기 애니메이션은 **앞으로 돌진했다가 제자리로 돌아오는 구조**이므로,
할퀴기 이후에는 드래곤이 원래 위치(중거리)로 복귀한다. 이 특성을 기반으로 유효/불가 콤보를 설계했다.

| 콤보 | 가능 여부 | 이유 |
|---|---|---|
| 물기 → 물기 | ✅ | 근접 연타 |
| 물기 → 할퀴기 | ✅ | 근접 후 돌진 복귀 |
| 할퀴기 → 할퀴기 | ✅ | 동일 위치에서 연속 돌진 |
| 할퀴기 → 브레스 | ✅ | 돌진 복귀 후 중거리에서 화염 |
| 할퀴기 → 물기 | ❌ | 돌진 복귀 후 중거리라 물기 사거리 밖 |

**BackAway를 콤보에 포함** — 물기 후 가까운 상황에서 BackAway를 콤보 중간 단계로 활용

```csharp
// 콤보 큐에서 BackAway를 나타내는 마커 상수
private const int COMBO_BACKAWAY = -1;
```

| 콤보 | 구성 |
|---|---|
| 1페이즈 | 물기→물기 / 물기→할퀴기 / 물기→BackAway→브레스 / 할퀴기→할퀴기 / 할퀴기→브레스 |
| 2페이즈 추가 | 물기→물기→BackAway→브레스 (풀콤보) / 물기→물기→할퀴기 (3연타) |

**콤보 확률:** 1페이즈 30%, 2페이즈 55%

**OnGroundAttackEnd 흐름:**
```csharp
if (_comboQueue.Count > 0)
{
    int next = _comboQueue.Dequeue();
    if (next == COMBO_BACKAWAY) { ChangeState(BackAway); return; }
    // 같은 GroundAttack 상태 내에서 애니메이션만 재트리거
    _animator.SetTrigger("doAttack");
    return;
}
// 콤보 종료 → 쿨타임 설정 → GroundChase
```

### 3-3. 공격별 후딜 차등

**기존:** 항상 고정 `groundAttackCooldown = 3.0f`

**변경:** 공격 종류에 따라 다른 후딜

| 공격 | 후딜 | 의도 |
|---|---|---|
| 물기 | 1.5초 | 빠른 연속 행동 가능 |
| 할퀴기 | 2.5초 | 중간 |
| 브레스 | 4.0초 | 강하지만 큰 빈틈 (플레이어 징벌 기회) |
| 콤보 마지막 타 | +1.0초 추가 | 콤보 후 보스도 잠시 쉬어야 균형이 맞음 |

---

## 4. 이동 시스템 개선 — Walk/Run 분리

### 배경

Unity Animator 블렌드 트리 구성:
- `speed` 파라미터 값 `4` → Walk 애니메이션
- `speed` 파라미터 값 `8` → Run 애니메이션

### 문제 1 — 쿨타임 중 가만히 서 있음

공격 후 GroundChase로 복귀하면 드래곤이 4m 근처에 서서 3초 쿨타임을 아무것도 안 하고 기다렸다. 보스가 멍청하고 생기 없어 보이는 핵심 원인.

**해결:** 쿨타임 중 Walk 속도로 `idleRange(5m)`까지 천천히 접근하는 "스토킹" 행동 구현.

### 문제 2 — Run 애니메이션이 사실상 재생되지 않음

Walk/Run 분리 후 테스트했더니 항상 Walk만 나왔다.

**원인 추적:**

`UpdateGroundChase`의 실행 순서를 분석한 결과:

```
1. BackAway 체크 (dist < tooCloseRange)
2. isAttackAvailable && TrySelectGroundPattern() → 즉시 GroundAttack으로 return
3. 이동 코드 (run) → 2번에서 return했으니 실행 안 됨
```

쿨타임이 끝나면 공격 체크가 먼저 실행되어 `return`되기 때문에 run 이동 코드 자체가 실행되지 않았다. Run은 브레스 쿨타임 중 8~12m 구간이라는 극히 좁은 조건에서만 재생됐다.

### 문제 3 — 멀리 도망가도 Walk로 따라옴

`_agent.speed = _baseSpeed * 0.5f`로 고정해놓아서 플레이어가 12m 이상 도망가도 Walk 속도(4)로 따라왔다.

**원인 분석:**

`_baseSpeed`를 NavMeshAgent Inspector의 speed 값에서 초기화하는데, 그 값이 `4.0f`였다.
- `_baseSpeed = 4`
- "full speed" = `_baseSpeed = 4` → desiredVelocity ≈ 4 → 항상 Walk
- "walk speed" = `_baseSpeed * 0.5f = 2` → 더 느린 속도

**해결:** `_baseSpeed * 배율` 방식을 폐기하고 `walkSpeed / runSpeed`를 명시적으로 분리.

```csharp
public float walkSpeed = 4.0f;  // blend tree walk 기준값과 일치
public float runSpeed  = 8.0f;  // blend tree run 기준값과 일치
private float _walkSpeed;       // 런타임 값 (2페이즈 배율 반영)
private float _runSpeed;        // 런타임 값 (2페이즈 배율 반영)
```

### 최종 이동 로직

| 상황 | 속도 | 애니메이션 |
|---|---|---|
| 쿨타임 중 + `dist > breathRange` (12m↑) | `_runSpeed` | **Run** — 도망가도 따라옴 |
| 쿨타임 중 + `idleRange < dist ≤ breathRange` | `_walkSpeed` | **Walk** — 스토킹 |
| 쿨타임 중 + `dist ≤ idleRange` | 정지 | — 주시 대기 |
| 공격 가능 + `dist > biteRange - 1` | `_runSpeed` | **Run** — 돌진 |
| 2페이즈 | 둘 다 × 1.2배 | — |

---

## 5. 확률적 회복 BackAway

### 문제

Walk/Run 분리 후에도, 공격 직후 드래곤이 여전히 `idleRange(5m)` 안에 있어서 쿨타임 동안 그 자리에서 멈추게 됐다. 결과적으로 Walk 스토킹 구간이 거의 재생되지 않았다.

### 시도 1 — 모든 공격 후 BackAway

모든 공격 종료 시 BackAway를 트리거하여 거리를 확보하는 방안.

**거절 이유 (팀 피드백):** "모든 공격 끝난 뒤 무조건 backaway하면 너무 어색함"

### 시도 2 — 브레스 공격 후에만 BackAway

브레스는 원거리 공격이므로 이후 물러나는 것이 자연스럽다는 설계.

**거절 이유:** 브레스 → BackAway → GroundChase → 다시 브레스 루프 발생 가능성.

### 최종 해결 — 확률적 회복 BackAway

조건을 충족할 때 **45% 확률**로 공격 후 BackAway 실행. 이때 BackAway는 공격으로 이어지지 않고 GroundChase로만 복귀하도록 플래그로 구분.

```csharp
private bool _backAwayIsRecovery = false; // true면 공격 없이 GroundChase 복귀

// OnGroundAttackEnd 내부
if (distToPlayer < clawRange && Random.value < 0.45f)
{
    _backAwayIsRecovery = true;
    ChangeState(DragonState.BackAway);
    return;
}

// OnBackAwayEnd 내부
if (_backAwayIsRecovery)
{
    _backAwayIsRecovery = false;
    ChangeState(DragonState.GroundChase); // 공격 없이 복귀
    return;
}
```

이로써 브레스 → BackAway recovery → GroundChase의 루프도 차단됐다.

**최종 전투 루프 (45% 케이스):**
```
공격 → BackAway (거리 확보) → Walk 스토킹 → 쿨타임 끝 → Run 돌진 → 공격
```

---

## 최종 시스템 구조

```
DragonBossAI
├── 이동 시스템
│   ├── 쿨타임 중: dist에 따라 Walk(4) or Run(8) 속도로 idleRange까지 접근
│   └── 공격 가능: Run(8)으로 biteRange까지 돌진
├── 패턴 선택 (TrySelectGroundPattern)
│   ├── 거리 기반 가중치 랜덤 선택
│   └── 1페이즈 30% / 2페이즈 55% 확률로 콤보 선택
├── 콤보 시스템 (_comboQueue)
│   ├── COMBO_BACKAWAY(-1) 마커로 BackAway를 콤보 중간 단계로 활용
│   └── 콤보 종료 시 _isInCombo 플래그로 추가 쿨타임 부여
├── 공격별 후딜 (getAttackCooldown)
│   └── 물기 1.5s / 할퀴기 2.5s / 브레스 4.0s / 콤보 마지막 +1.0s
├── 회복 BackAway (_backAwayIsRecovery)
│   └── 45% 확률 발동, 공격 없이 거리만 확보
└── 안전장치
    └── Groggy / Die 진입 시 comboQueue, _isInCombo, _backAwayIsRecovery 클리어
```

---

## 핵심 배운 점

1. **애니메이션 특성을 AI 설계에 반영해야 한다** — 할퀴기가 "돌진 후 복귀"라는 특성을 모르고 설계하면 물리적으로 말이 안 되는 콤보가 생긴다.

2. **Inspector 기본값과 코드 로직의 의존성을 명확히 해야 한다** — `_baseSpeed * 0.5f` 방식은 Inspector 값이 무엇인지에 따라 완전히 다른 결과를 냈다. 명시적인 `walkSpeed / runSpeed` 분리가 훨씬 안전하다.

3. **FSM에서 return 순서가 실행 가능성을 결정한다** — 공격 체크가 이동 코드보다 먼저 `return`하기 때문에 run 이동 코드가 실질적으로 데드코드가 되는 상황이 발생했다.

4. **단순해 보이는 기믹도 루프를 만들 수 있다** — 브레스 → BackAway 설계 시 브레스 → BackAway → 다시 브레스 루프를 처음에 간과했다. `_backAwayIsRecovery` 플래그로 BackAway의 "목적"을 명확히 구분하여 해결했다.
