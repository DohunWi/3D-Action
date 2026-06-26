using UnityEngine;
using System.Collections.Generic;

public class DamagePopupPool : MonoBehaviour
{
    public static DamagePopupPool Instance { get; private set; }

    private readonly Queue<DamagePopup> _pool = new();
    private Transform _poolRoot;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // DontDestroyOnLoad는 루트 오브젝트에만 동작
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        _poolRoot = new GameObject("[DamagePopup Pool]").transform;
        _poolRoot.SetParent(transform);
    }

    /// <summary>
    /// 풀에서 팝업을 꺼내 position에 활성화합니다.
    /// prefab은 풀이 비었을 때 새 인스턴스 생성에 사용됩니다.
    /// </summary>
    public DamagePopup Get(GameObject prefab, Vector3 position)
    {
        DamagePopup popup;
        if (_pool.Count > 0)
            popup = _pool.Dequeue();
        else
        {
            GameObject obj = Instantiate(prefab, _poolRoot);
            popup = obj.GetComponent<DamagePopup>();
        }

        popup.transform.SetPositionAndRotation(position, Quaternion.identity);
        popup.gameObject.SetActive(true);
        return popup;
    }

    /// <summary>
    /// 사용이 끝난 팝업을 풀에 반환합니다.
    /// </summary>
    public void Return(DamagePopup popup)
    {
        popup.gameObject.SetActive(false);
        popup.transform.SetParent(_poolRoot);
        _pool.Enqueue(popup);
    }

    public void WarmUp(GameObject prefab, int count)
    {
        if (_pool.Count >= count) return; // 이미 충분하면 skip
        int toCreate = count - _pool.Count;
        for (int i = 0; i < toCreate; i++)
        {
            GameObject obj = Instantiate(prefab, _poolRoot);
            obj.SetActive(false);
            _pool.Enqueue(obj.GetComponent<DamagePopup>());
        }
    }
}
