using UnityEngine;
using System.Collections.Generic;

public class Weapon : MonoBehaviour
{
    public float damage = 10f; // 공격력
    // [NEW] "나의 주인님" 태그 (이 태그를 가진 놈은 안 때림)
    [Header("Settings")]
    [Tooltip("무기 주인의 태그를 적으세요 (예: Player 또는 Enemy)")]
    public string ownerTag;
    private BoxCollider _collider;

    // 한 번 휘두를 때 같은 적을 두 번 때리면 안 되니까 때린 놈을 기억하는 리스트
    private List<Collider> _alreadyHitList = new List<Collider>();

    private void Awake()
    {
        _collider = GetComponent<BoxCollider>();
    }

    private void OnEnable()
    {
        // 시작할 땐 판정을 꺼둡니다.
        DisableHitbox();
    }

    // 애니메이션 이벤트가 부를 함수: 판정 켜기
    public void EnableHitbox()
    {
        _collider.enabled = true;
        _alreadyHitList.Clear(); // 때린 놈 목록 초기화 (새 공격 시작)
    }

    // 애니메이션 이벤트가 부를 함수: 판정 끄기
    public void DisableHitbox()
    {
        _collider.enabled = false;
    }

    // ★ 충돌 감지 (Is Trigger가 켜져 있어야 작동)
    private void OnTriggerEnter(Collider other)
    {
        // 주인님(아군)이면 무시!
        // (주인이 Player면 Player 무시, 주인이 Enemy면 Enemy 무시)
        if (other.CompareTag(ownerTag)) return;

        // 혹시라도 "내 자신"의 콜라이더와 부딪히면 무시 (안전장치)
        if (other.transform.root == transform.root) return;

        // 2. 이미 이번 공격에서 때린 놈이면 패스
        if (_alreadyHitList.Contains(other)) return;

        // 3. 부딪힌 오브젝트한테서 IDamageable 계약서를 찾는다.
        IDamageable target = other.GetComponent<IDamageable>();

        // 4. 계약서가 있는 놈(때릴 수 있는 놈)이면 때린다.
        if (target != null)
        {
            target.TakeDamage(damage, transform.root); // 데미지 전달!
            Debug.Log($"<color=red>Hit! : {other.name}</color>");
            
            // 때린 목록에 추가 (한 번 공격에 두 번 안 맞게)
            _alreadyHitList.Add(other);
        }
    
    }
}