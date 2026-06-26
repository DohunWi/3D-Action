using UnityEngine;
using TMPro; // TextMeshPro 사용 필수

public class DamagePopup : MonoBehaviour
{
    private TextMeshPro _textMesh;
    private Color _textColor;
    private Camera _camera;
    private float _initialLifeTime;

    [Header("Settings")]
    public float moveSpeed = 2f;
    public float disappearSpeed = 3f;
    public float lifeTime = 1f; // 1초 뒤 사라짐

    private void Awake()
    {
        _textMesh = GetComponent<TextMeshPro>();
        _camera = Camera.main;
        _initialLifeTime = lifeTime;
    }

    // 풀에서 꺼낼 때 카메라가 null이면 재캐싱
    private void OnEnable()
    {
        if (_camera == null) _camera = Camera.main;
    }

    // 생성자가 호출할 초기화 함수
    public void Setup(float damageAmount)
    {
        // 1. 데미지 숫자 설정
        _textMesh.text = damageAmount.ToString("F0"); // 소수점 없이 정수만

        // 2. 풀 재사용 시 알파 리셋 (페이드아웃 후 반환된 팝업은 alpha=0 상태)
        Color c = _textMesh.color;
        c.a = 1f;
        _textMesh.color = c;
        _textColor = c;

        // 3. 풀 재사용 시 lifeTime 리셋
        lifeTime = _initialLifeTime;

        // (옵션) 크리티컬이면 글자 키우기?
        // if (damageAmount > 50) _textMesh.fontSize += 2;
    }

    private void Update()
    {
        if (_camera == null)
        {
            _camera = Camera.main;
            if (_camera == null) return;
        }

        // 1. 위로 이동 (둥둥)
        transform.position += Vector3.up * moveSpeed * Time.deltaTime;
        transform.rotation = Quaternion.LookRotation(transform.position - _camera.transform.position);

        // 2. 서서히 투명해지기 (Fade Out)
        lifeTime -= Time.deltaTime;
        if (lifeTime < 0)
        {
            float alpha = _textColor.a - (disappearSpeed * Time.deltaTime);
            _textColor.a = alpha;
            _textMesh.color = _textColor;

            // 투명도가 0보다 작아지면 풀에 반환
            if (alpha < 0)
            {
                DamagePopupPool.Instance.Return(this);
            }
        }
    }
}
