using UnityEngine;
using TMPro;
using UnityEngine.InputSystem; // ★ 필수 네임스페이스

public class PlayerInteraction : MonoBehaviour
{
    [Header("Settings")]
    public float interactRange = 2.0f;
    public LayerMask interactLayer;

    
    [Header("UI")]
    public GameObject promptPanel;
    public TextMeshProUGUI promptText;
    public CanvasGroup canvasGroup; // ★ 인스펙터에서 연결 (자기 자신)

    private IInteractable _currentInteractable;
    private PlayerController _playerController;

    private void Awake()
    {
        _playerController = GetComponent<PlayerController>();
    }
    private void Start()
    {
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }
    
    private void Update()
    {
        // 매 프레임 앞에 뭐가 있는지 확인은 계속 해야 함 (UI 띄우기 위해)
        // 상호작용 중이면 확인 x 
        if (_playerController != null && _playerController.currentState == PlayerState.Interact)
        {
            // 혹시 켜져있던 안내 문구가 있다면 확실하게 꺼줌
            ShowPrompt(false, ""); 
            _currentInteractable = null; 
            return; 
        }
        CheckForInteractable();
    }

    // 키 입력은 여기서 안 받음, Controller가 호출할 함수.
    public bool TryInteract()
    {
        if (_currentInteractable != null)
        {
            ShowPrompt(false,"");
            _currentInteractable.Interact(gameObject); // 실행
            return true; // 상호작용 성공!
        }
        return false; // 앞에 아무것도 없음
    }

    private void CheckForInteractable()
    {
        RaycastHit hit;
        Vector3 origin = transform.position + Vector3.up * 1.0f;

        bool isHit = Physics.SphereCast(origin, 0.5f, transform.forward, out hit, interactRange, interactLayer);

        if (isHit)
        {
            IInteractable interactable = hit.collider.GetComponent<IInteractable>();

            if (interactable != null)
            {
                _currentInteractable = interactable;
                ShowPrompt(true, interactable.GetInteractPrompt());
                return;
            }
        }

        _currentInteractable = null;
        ShowPrompt(false, "");
    }

   private void ShowPrompt(bool isActive, string message)
    {
        if (isActive && promptText!= null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            promptText.text = message; 
        }
        else
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        // 1. 패널이 있는지 확인
        // if (promptPanel != null)
        // {
        //     promptPanel.SetActive(isActive); 

        //     // 2. 켜진 상태라면 텍스트 내용 업데이트
        //     if (isActive && promptText != null)
        //     {
        //         promptText.text = message; 
        //     }
        // }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Vector3 origin = transform.position + Vector3.up * 1.0f;
        Gizmos.DrawWireSphere(origin + transform.forward * interactRange, 0.5f);
    }
}