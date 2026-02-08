using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthUI : MonoBehaviour
{
    [Header("References")]
    public Image healthFillImage;
    public Canvas healthCanvas; // 캔버스 자체 (죽었을 때 끄기 위해)

    private CharacterStats _myStats;
    private Enemy _enemyController;
    private Camera _mainCamera;

    private void Start()
    {
        // 1. 카메라 찾기
        _mainCamera = Camera.main;

        // 2. 내 몸통(부모)에 있는 Stats 찾기
        _myStats = GetComponentInParent<CharacterStats>();
        _enemyController = GetComponentInParent<Enemy>();

        if (_myStats != null)
        {
            // 이벤트 구독
            _myStats.OnHealthChanged += UpdateHealthUI;
            
        }
        
        // 캔버스 초기 설정
        if (healthCanvas != null)
        {
            if (healthCanvas.worldCamera == null)
                healthCanvas.worldCamera = _mainCamera;
                
            // ★ [핵심] 게임오브젝트를 끄지 말고, 캔버스 컴포넌트만 끕니다!
            // 그래야 이 스크립트(Update)가 계속 돌아갑니다.
            healthCanvas.enabled = false; 
        }
    }

    private void Update()
    {
        // 정보가 없으면 실행 안 함
        if (healthCanvas == null || _enemyController == null || _myStats == null) return;

        // 1. 현재 몬스터의 상태 확인
        EnemyState state = _enemyController.currentState;

        // 2. 보여줄지 말지 결정
        bool shouldShow = false;

        switch (state)
        {
            case EnemyState.Idle:
            case EnemyState.Patrol: // 순찰 중일 땐 숨김
            case EnemyState.Die:
                shouldShow = false;
                break;
                
            case EnemyState.Chase:  // 추격 시작하면 보임
            case EnemyState.Attack: // 공격 중 보임
            case EnemyState.Hit:    // 맞으면 보임 (OnTakeDamage -> Hit 전환됨)
            case EnemyState.Parried: // 패링 당해도 보임
            case EnemyState.Down: // 다운 당해도 보임
                shouldShow = true;
                break;
        }

        // 죽었으면 무조건 숨김 (안전장치)
        if (_myStats.currentHealth <= 0) shouldShow = false;

        // 3. 상태 적용 (Canvas 컴포넌트의 체크박스만 껐다 켰다 함)
        if (healthCanvas.enabled != shouldShow)
        {
            healthCanvas.enabled = shouldShow;
        }

        // 4. 빌보드 (캔버스가 보일 때만 카메라 바라보기)
        if (shouldShow && _mainCamera != null)
        {
            healthCanvas.transform.rotation = _mainCamera.transform.rotation;
        }
    }

    private void UpdateHealthUI(float current, float max)
    {
        if (healthFillImage != null)
        {
            healthFillImage.fillAmount = current / max;
        }
    }

    private void OnDisable()
    {
        // 이벤트 해제
        if (_myStats != null)
        {
            _myStats.OnHealthChanged -= UpdateHealthUI;
        }
    }
}