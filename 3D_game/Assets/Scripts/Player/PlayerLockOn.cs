using UnityEngine;
using System.Collections.Generic; // 리스트 사용
using Unity.Cinemachine;
public class PlayerLockOn : MonoBehaviour
{
    [Header("Settings")]
    public float detectionRadius = 15.0f;
    public LayerMask enemyLayer;
    public float maxLockOnDistance = 20.0f; // 락온 유지 최대 거리

    [Header("Status")]
    public Transform currentTarget;
    public bool isLockOn => currentTarget != null; // 외부에서 확인용

    [Header("UI Settings")]
    public GameObject lockOnIconPrefab; // UI indicator
    public float iconHeightOffset = 1.2f; // 적의 발밑이 아니라 가슴/머리 쪽에 뜨도록
    public float iconForwardOffset = 0.4f;

    [Header("Camera Settings")]
    public CinemachineCamera lockOnCamera;

    // 내부 변수
    private GameObject _currentIcon; // 실제 생성된 아이콘 인스턴스

    // 락온 켜기/끄기 토글 (PlayerController가 호출)
    public void ToggleLockOn()
    {
        if (isLockOn)
        {
            ClearLockOn();
        }
        else
        {
            FindTarget();
        }
    }

    private void FindTarget()
    {
        // 1. 주변 적 탐색
        Collider[] colliders = Physics.OverlapSphere(transform.position, detectionRadius, enemyLayer);
        
        // 거리 대신 '화면 중앙과의 거리'를 비교할 변수
        float minScreenDistance = Mathf.Infinity; 
        Transform bestTarget = null;

        // 화면 정중앙 좌표 (0.5, 0.5)
        Vector2 screenCenter = new Vector2(0.5f, 0.5f);

        foreach (var collider in colliders)
        {
            CharacterStats targetStats = collider.GetComponent<CharacterStats>();
            
            // 살아있는지 체크
            if (targetStats != null && targetStats.currentHealth > 0)
            {
                // ★ [핵심 로직 변경] ★
                // 3D 월드 좌표를 -> 2D 뷰포트 좌표(0~1)로 변환
                Vector3 viewPos = Camera.main.WorldToViewportPoint(collider.transform.position);

                // 조건 1: 화면 앞쪽에 있어야 함 (z < 0 이면 카메라 뒤에 있는 것)
                // 조건 2: 화면 안에 들어와 있어야 함 (x, y가 0~1 사이)
                if (viewPos.z > 0 && viewPos.x > 0 && viewPos.x < 1 && viewPos.y > 0 && viewPos.y < 1)
                {
                    // 화면 중앙(0.5, 0.5)과 적 위치 사이의 2D 거리를 계산
                    float distFromCenter = Vector2.Distance(screenCenter, new Vector2(viewPos.x, viewPos.y));

                    // 중앙에 더 가까운 녀석이 나타나면 갱신
                    if (distFromCenter < minScreenDistance)
                    {
                        minScreenDistance = distFromCenter;
                        bestTarget = collider.transform;
                    }
                }
            }
        }

        // 4. 최종 타겟 선정
        if (bestTarget != null)
        {
            currentTarget = bestTarget;
            Debug.Log($"<color=cyan>시야 중앙 타겟 락온: {currentTarget.name}</color>");

            EnableLockOnIcon();
            if (lockOnCamera != null)
            {
                lockOnCamera.LookAt = currentTarget; 
                lockOnCamera.Priority = 20;
            }
        }
        else
        {
            Debug.Log("화면 내에 락온할 적이 없습니다.");
        }
    }

    private void Update()
    {
        // 락온 중일 때, 적이 너무 멀어지거나 죽으면 자동으로 풀기
        if (isLockOn)
        {
            // 1. 타겟 유효성 검사 (적이 사라지거나 죽었는지)
            if (CheckTargetIsDeadOrNull()) 
            {
                ClearLockOn();
                return;
            }

            // 거리가 너무 멀어짐
            float distance = Vector3.Distance(transform.position, currentTarget.position);
            if (distance > maxLockOnDistance)
            {
                ClearLockOn();
                return;
            }

            // 아이콘을 적 위치로 이동 + 빌보드(카메라 보기)
            if (_currentIcon != null)
            {
                // 적의 위치 + 높이 오프셋
                Vector3 targetPos = currentTarget.position + Vector3.up * iconHeightOffset - Vector3.forward * iconForwardOffset;
                _currentIcon.transform.position = targetPos;

                // 항상 카메라를 정면으로 바라보게 함
                _currentIcon.transform.LookAt(Camera.main.transform);
            }
        }
    }

    public void ClearLockOn()
    {
        currentTarget = null;
        Debug.Log("락온 해제");
        // ★ 아이콘 끄기
        if (_currentIcon != null)
        {
            Destroy(_currentIcon); // 혹은 SetActive(false)로 재활용 가능
            _currentIcon = null;
        }
        // ★ 락온 카메라 끄기 (원래 카메라로 복귀)
        if (lockOnCamera != null)
        {
            lockOnCamera.LookAt = null;
            lockOnCamera.Priority = 0; // 우선순위 낮춤
        }
    }
    // 아이콘 생성 함수
    private void EnableLockOnIcon()
    {
        if (lockOnIconPrefab != null && _currentIcon == null)
        {
            _currentIcon = Instantiate(lockOnIconPrefab);
        }
    }

    // 타겟이 죽었거나 없는지 체크하는 헬퍼 함수
    private bool CheckTargetIsDeadOrNull()
    {
        if (currentTarget == null) return true;
        
        var stats = currentTarget.GetComponent<CharacterStats>();
        if (stats != null && stats.currentHealth <= 0) return true;
        
        return false;
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}