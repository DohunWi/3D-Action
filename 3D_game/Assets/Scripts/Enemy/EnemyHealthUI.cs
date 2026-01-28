using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthUI : MonoBehaviour
{
    [Header("References")]
    public Image healthFillImage;
    public Canvas healthCanvas; // 캔버스 자체 (죽었을 때 끄기 위해)

    private CharacterStats _myStats;
    private Camera _mainCamera;

    private void Start()
    {
        // 1. 카메라 찾기
        _mainCamera = Camera.main;

        // 2. 내 몸통(부모)에 있는 Stats 찾기
        _myStats = GetComponentInParent<CharacterStats>();

        if (_myStats != null)
        {
            // 이벤트 구독
            _myStats.OnHealthChanged += UpdateHealthUI;
            
        }
        
        // 캔버스는 월드 카메라를 사용
        if (healthCanvas != null && healthCanvas.worldCamera == null)
        {
            healthCanvas.worldCamera = _mainCamera;
        }
    }

    private void Update()
    {
        // ★ [핵심] 빌보드 로직: 항상 카메라를 정면으로 바라보게 함
        if (healthCanvas != null && _mainCamera != null)
        {
            // 캔버스의 회전을 카메라의 회전과 일치시킴
            healthCanvas.transform.rotation = _mainCamera.transform.rotation;
        }
    }

    private void UpdateHealthUI(float current, float max)
    {
        if (healthFillImage != null)
        {
            healthFillImage.fillAmount = current / max;
        }

        // (선택) 체력이 0이 되면 체력바 숨기기
        if (current <= 0 && healthCanvas != null)
        {
            healthCanvas.gameObject.SetActive(false);
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