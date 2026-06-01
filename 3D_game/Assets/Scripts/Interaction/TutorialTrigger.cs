using UnityEngine;

public class TutorialTrigger : MonoBehaviour
{
    [Header("Content")]
    public string tutorialTitle = "Controls";

    [TextArea(4, 12)]
    public string tutorialContent = "";

    private Collider _col;
    private PlayerController _player;
    private bool _playerInside = false;
    private bool _wasInteracting = false;

    private void Awake()
    {
        _col = GetComponent<Collider>();
    }

    private void Update()
    {
        if (!_playerInside || _player == null) return;

        // 텔레포트 등으로 멀어졌는데 OnTriggerExit가 안 불린 경우 대비 — 실제 거리로 재확인
        if (_col != null)
        {
            float maxDist = _col.bounds.extents.magnitude + 3f;
            if (Vector3.Distance(_player.transform.position, _col.bounds.center) > maxDist)
            {
                ClearInside();
                return;
            }
        }

        bool isInteracting = _player.currentState == PlayerState.Interact;

        if (isInteracting && !_wasInteracting)
        {
            TutorialUI.Instance?.Close();
        }
        else if (!isInteracting && _wasInteracting)
        {
            TutorialUI.Instance?.Show(tutorialTitle, tutorialContent);
        }

        _wasInteracting = isInteracting;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _player = other.GetComponent<PlayerController>();
        _playerInside = true;
        _wasInteracting = false;
        TutorialUI.Instance?.Show(tutorialTitle, tutorialContent);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        ClearInside();
    }

    private void ClearInside()
    {
        _playerInside = false;
        _player = null;
        _wasInteracting = false;
        TutorialUI.Instance?.Close();
    }
}
