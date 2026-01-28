using UnityEngine;
using TMPro; // TextMeshPro 사용 필수

public class DamagePopup : MonoBehaviour
{
    private TextMeshPro _textMesh;
    private Color _textColor;
    
    [Header("Settings")]
    public float moveSpeed = 2f;
    public float disappearSpeed = 3f;
    public float lifeTime = 1f; // 1초 뒤 사라짐

    private void Awake()
    {
        _textMesh = GetComponent<TextMeshPro>();
    }

    // 생성자가 호출할 초기화 함수
    public void Setup(float damageAmount)
    {
        // 1. 데미지 숫자 설정
        _textMesh.text = damageAmount.ToString("F0"); // 소수점 없이 정수만
        
        // 2. 색상 가져오기 (투명도 조절용)
        _textColor = _textMesh.color;

        // 3. (옵션) 크리티컬이면 글자 키우기? 
        // if (damageAmount > 50) _textMesh.fontSize += 2;
    }

    private void Update()
    {
        // 1. 위로 이동 (둥둥)
        transform.position += Vector3.up * moveSpeed * Time.deltaTime;
        transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
        
        // 2. 서서히 투명해지기 (Fade Out)
        lifeTime -= Time.deltaTime;
        if (lifeTime < 0)
        {
            float alpha = _textColor.a - (disappearSpeed * Time.deltaTime);
            _textColor.a = alpha;
            _textMesh.color = _textColor;

            // 투명도가 0보다 작아지면 삭제
            if (alpha < 0)
            {
                Destroy(gameObject);
            }
        }
    }
}