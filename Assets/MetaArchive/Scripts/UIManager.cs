using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public sealed class UIManager : MonoBehaviour
{
    public static UIManager instance { get; private set; }

    [Header("Panels")]
    [SerializeField] private GameObject startPanel;
    [SerializeField] private GameObject nameInputPanel;
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private GameObject cameraActivePanel;
    [SerializeField] private GameObject cameraScanningPanel;
    [SerializeField] private GameObject finalPanel;

    [Header("Start")]
    [SerializeField] private Button startButton;

    [Header("Name Input")]
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private Button nameConfirmButton;

    [Header("Dialogue")]
    [SerializeField] private TextMeshProUGUI npcNameText;
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private Button nextDialogueButton;

    [Header("CameraButtons")]
    [SerializeField] private Button onARCameraActivateButton;
    [SerializeField] private Button onARCameraCloseButton;
    [SerializeField] private Button stempSubmitButton;
    
    [Header("Mission Stamp")]
    [SerializeField] private Image firstContentRoomImage;
    [SerializeField] private Image secondContentRoomImage;
    [SerializeField] private Image thirdContentRoomImage;
    [SerializeField] private Image ARVRContentRoomImage;
    [SerializeField] private Image metaverseShowRoomImage;
    [SerializeField] private Image RestRoomImage;
    
    [Header("Fade (Image)")]
    [SerializeField] private Image fadeImage;      // 풀스크린 검정 이미지

    Coroutine _fadeCo;
    
    string playerName = "신입생";
    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        
        // 항상 검정으로 고정
        if (fadeImage)
        {
            var c = fadeImage.color;
            fadeImage.color = new Color(0f, 0f, 0f, c.a);
            fadeImage.raycastTarget = c.a > 0.001f;
            
            // 최상단 보장
            var canvas = fadeImage.GetComponentInParent<Canvas>(true);
            if (canvas)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = 32767;
            }
            
            fadeImage.gameObject.SetActive(true); // 비활성로 안 보이는 문제 방지
            fadeImage.enabled = true;
        }
    }

    void Start()
    {
        HideAll();
        HideMissionStemp();
        startButton.onClick.AddListener(StartGame);

        nameConfirmButton.onClick.AddListener(OnNameConfirm);
        nextDialogueButton.onClick.AddListener(() => StoryManager.instance.OnNextDialoguePressed());
        onARCameraActivateButton.onClick.AddListener(() => StoryManager.instance.OnARCameraActivatePressed());
        onARCameraCloseButton.onClick.AddListener(() => StoryManager.instance.OnARCameraClosePressed());
        stempSubmitButton.onClick.AddListener(() => StoryManager.instance.StempSubmitPressed());
    }

    void OnDestroy()
    {
        // 메모리 누수 방지
        startButton.onClick.RemoveAllListeners();
        nameConfirmButton.onClick.RemoveAllListeners();
        nextDialogueButton.onClick.RemoveAllListeners();
        onARCameraActivateButton.onClick.RemoveAllListeners();
    }

    // ========== Public API ==========
    public void ShowStart()             { SwitchTo(startPanel); }
    public void ShowNameInput()         { SwitchTo(nameInputPanel); }
    public void ShowActivateCamera()    { SwitchTo(cameraActivePanel); }
    public void ShowCameraScanning()    { SwitchTo(cameraScanningPanel); }
    public void ShowGiftBox() {SwitchTo(finalPanel);}
    public void ShowDialogue(string npc, string line)
    {
        SwitchTo(dialoguePanel);
        npcNameText.text = npc;
        dialogueText.text = line;
    }
    
    public string GetPlayerName() => playerName;

    public void ShowMissionStamp(MissionID CurrentID)
    {
        switch (CurrentID)
        {
            case MissionID.M1:
            firstContentRoomImage.color = new Color(1.0f, 1.0f, 1.0f, 1.0f);
                break;
            case MissionID.M2:
            secondContentRoomImage.color = new Color(1.0f, 1.0f, 1.0f, 1.0f);
                break;
            case MissionID.M3:
            thirdContentRoomImage.color = new Color(1.0f, 1.0f, 1.0f, 1.0f);
                break;
            case MissionID.M4:
            ARVRContentRoomImage.color = new Color(1.0f, 1.0f, 1.0f, 1.0f);
                break;
            case MissionID.M5:
            metaverseShowRoomImage.color = new Color(1.0f, 1.0f, 1.0f, 1.0f);
                break;
            case MissionID.M6:
            RestRoomImage.color = new Color(1.0f, 1.0f, 1.0f, 1.0f);
                break;
        }
    }
    
    public void SetFadeInstant(float a)
    {
        if (!fadeImage) return;
        a = Mathf.Clamp01(a);
        fadeImage.color = new Color(0f, 0f, 0f, a);
        fadeImage.raycastTarget = a > 0.001f;
    }

    public Coroutine FadeTo(float targetA, float duration)
    {
        if (!fadeImage) return null;
        if (_fadeCo != null) StopCoroutine(_fadeCo);
        targetA = Mathf.Clamp01(targetA);
        duration = Mathf.Max(0f, duration);
        _fadeCo = StartCoroutine(FadeCo(targetA, duration));
        return _fadeCo;
    }
    public Coroutine FadeOut(float dur) => FadeTo(1f, dur);
    public Coroutine FadeIn (float dur) => FadeTo(0f, dur);

    System.Collections.IEnumerator FadeCo(float targetA, float dur)
    {
        fadeImage.gameObject.SetActive(true);
        fadeImage.enabled = true;

        Color start = fadeImage.color;                 // RGB는 이미 (0,0,0)
        Color end   = new Color(0f, 0f, 0f, targetA);
        fadeImage.raycastTarget = true;

        if (dur <= 0f)
        {
            fadeImage.color = end;
            fadeImage.raycastTarget = targetA > 0.001f;
            _fadeCo = null;
            yield break;
        }

        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = t / dur;
            fadeImage.color = Color.Lerp(start, end, k);
            yield return null;
        }
        fadeImage.color = end;
        fadeImage.raycastTarget = targetA > 0.001f;
        _fadeCo = null;
    }

    // ========== Helpers ==========
    void OnNameConfirm()
    {
        playerName = string.IsNullOrWhiteSpace(nameInputField.text) ? "신입생" : nameInputField.text.Trim();
        StoryManager.instance.SetPlayerName(playerName);
        StoryManager.instance.SetStoryState(StoryState.Intro_Meet_Dungddangi);
    }

    void StartGame()
    {
        StoryManager.instance.SetStoryState(StoryState.Player_Name_Input);
        if(stempSubmitButton) stempSubmitButton.gameObject.SetActive(false);
    }

    void SwitchTo(GameObject target)
    {
        HideAll();
        Show(target);
    }

    void HideAll()
    {
        startPanel.SetActive(false);
        nameInputPanel.SetActive(false);
        dialoguePanel.SetActive(false);
        cameraActivePanel.SetActive(false);
        cameraScanningPanel.SetActive(false);
        finalPanel.SetActive(false);
    }

    void HideMissionStemp()
    {
        firstContentRoomImage.color = new Color(1.0f, 1.0f, 1.0f, 0.0f);
        secondContentRoomImage.color = new Color(1.0f, 1.0f, 1.0f, 0.0f);
        thirdContentRoomImage.color = new Color(1.0f, 1.0f, 1.0f, 0.0f);
        ARVRContentRoomImage.color = new Color(1.0f, 1.0f, 1.0f, 0.0f);
        metaverseShowRoomImage.color = new Color(1.0f, 1.0f, 1.0f, 0.0f);
        RestRoomImage.color = new Color(1.0f, 1.0f, 1.0f, 0.0f);
    }
    static void Show(GameObject go) => go.SetActive(true);
    
    public void HideDialogue()
    {
        if (dialoguePanel) dialoguePanel.SetActive(false);
    }

    public void SwapCameraButton()
    {
        if(onARCameraActivateButton) onARCameraActivateButton.gameObject.SetActive(false);
        if(stempSubmitButton) stempSubmitButton.gameObject.SetActive(true);
    }
}
