using UnityEngine;

public class MemoryPickup : MonoBehaviour
{
    [Header("Settings")]
    public int memoryAmount = 100; // 기억의 크기
    public GameObject pickupVFX;   // 획득 시 반짝이는 이펙트
    public AudioClip pickupSound;  // 몽환적인 띠링~ 소리

    private bool _isCollected = false;

    private void Start()
    {
        // 바닥에 떨어지면 통~ 튀어오르는 물리 효과 (악몽의 불안정함)
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 force = Vector3.up * 4f + Random.insideUnitSphere * 1.5f;
            rb.AddForce(force, ForceMode.Impulse);
        }
        Destroy(gameObject, 60f); // 1분 뒤 기억 소멸
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_isCollected) return;

        if (other.CompareTag("Player"))
        {
            // PlayerWallet을 찾아서 기억을 전달
            PlayerWallet wallet = other.GetComponent<PlayerWallet>();
            if (wallet != null)
            {
                Collect(wallet);
            }
        }
    }

    private void Collect(PlayerWallet wallet)
    {
        _isCollected = true;
        
        // 지갑(기억 저장소)에 추가
        wallet.CollectMemory(memoryAmount);

        // 사운드 & 이펙트
        if (pickupSound != null) AudioSource.PlayClipAtPoint(pickupSound, transform.position);
        if (pickupVFX != null) Instantiate(pickupVFX, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }
}