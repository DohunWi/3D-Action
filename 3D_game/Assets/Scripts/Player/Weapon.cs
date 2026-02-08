using UnityEngine;
using System.Collections.Generic;

public class Weapon : MonoBehaviour
{
    [Header("Data Source")]
    public WeaponData weaponData; // ★ 여기에 SO를 드래그해서 넣음

    [Header("Runtime Settings")]
    public string ownerTag; 

    [Header("Hitbox")]
    public BoxCollider _collider;
    protected List<Collider> _alreadyHitList = new List<Collider>();
    [HideInInspector] public float damageMultiplier = 1.0f;
    protected virtual void Awake()
    {
        // 내 몸에 없으면 '자식 오브젝트(뼈)' 뒤져서라도 찾아라!
        if(_collider == null) 
            _collider = GetComponentInChildren<BoxCollider>();

        if (_collider == null)
            Debug.LogError($"[Weapon] {gameObject.name}에 콜라이더가 없습니다! 인스펙터를 확인하세요.");
    }
    private void OnEnable()
    {
        // 시작할 땐 판정을 꺼둡니다.
        DisableHitbox();
    }
    public float damage 
    {
        get { return (weaponData != null ? weaponData.damage : 0f) * damageMultiplier; }
    }
    // ★ 이제 데미지나 소리는 weaponData에서 꺼내 씁니다.
    protected virtual void OnTriggerEnter(Collider other)
    {
        if (weaponData == null) 
        {
            // Debug.Log($"{gameObject} : No Weapon Data");
            return; // 데이터 없으면 작동 안 함
        }
        if (other.CompareTag(ownerTag)) 
        {
            // Debug.Log($"{gameObject} : Same Owner Tag");
            return;
        }
        if (other.transform.root == transform.root) 
        {
            // Debug.Log($"{gameObject} : Same transform root");
            return;
        }
        if (_alreadyHitList.Contains(other)) 
        {
            // Debug.Log($"{gameObject} : Already Hit");
            return;
        }
        IDamageable target = other.GetComponent<IDamageable>();
        if (target != null)
        {
            // 데이터에서 데미지 가져오기
            target.TakeDamage(this.damage, weaponData.poiseDamage, transform.root);
            Debug.Log($"{gameObject}가 {this.damage}데미지를 입혔습니다.");
            _alreadyHitList.Add(other);
            OnAttackSuccess(other);
        }
    }
    public void EnableHitbox()
    {
        if (_collider != null) 
        {
            _collider.enabled = true;
            _alreadyHitList.Clear();
             // 소리도 데이터에서 가져와 재생
            if (weaponData.swingSound != null)
            {
                SoundManager.Instance.PlaySFX(weaponData.swingSound, transform.position);
            }  
            // 
            // Debug.Log($"[Weapon] 히트박스 켜짐! (GameObj: {gameObject.name})"); 
        }
        else
        {
            // Debug.LogError("[Weapon] 콜라이더가 연결되지 않았습니다!");
        }
    }

    public void DisableHitbox()
    {
        if (_collider != null) 
            _collider.enabled = false;
    }
    protected virtual void OnAttackSuccess(Collider victim)
    {
        // 소리도 데이터에서 가져와 재생
        if (weaponData.hitSound != null)
        {
            SoundManager.Instance.PlaySFX(weaponData.hitSound, transform.position);
        }

        // 이펙트도 데이터에서
        if (weaponData.hitVFX != null)
        {
            // (VFX 생성 로직...)
        }
    }
}