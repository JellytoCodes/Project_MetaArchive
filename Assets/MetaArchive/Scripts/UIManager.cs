using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

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
    
    string playerName = "신입생";

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        HideAll();
        HideMissionStemp();
        startButton.onClick.AddListener(StartGame);

        nameConfirmButton.onClick.AddListener(OnNameConfirm);
        nextDialogueButton.onClick.AddListener(() => StoryManager.Instance.OnNextDialoguePressed());
        onARCameraActivateButton.onClick.AddListener(() => StoryManager.Instance.OnARCameraActivatePressed());
        onARCameraCloseButton.onClick.AddListener(() => StoryManager.Instance.OnARCameraClosePressed());
        stempSubmitButton.onClick.AddListener(() => StoryManager.Instance.StempSubmitPressed());
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
    public void ShowDialogue(string npc, string line)
    {
        SwitchTo(dialoguePanel);
        npcNameText.text = npc;
        dialogueText.text = line;
    }
    
    public string GetPlayerName() => playerName;

    public void ShowMissionStemp(MissionID CurrentID)
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

    // ========== Helpers ==========
    void OnNameConfirm()
    {
        playerName = string.IsNullOrWhiteSpace(nameInputField.text) ? "신입생" : nameInputField.text.Trim();
        StoryManager.Instance.SetPlayerName(playerName);
        StoryManager.Instance.SetStoryState(StoryState.Intro_Meet_Dungddangi);
    }

    void StartGame()
    {
        StoryManager.Instance.SetStoryState(StoryState.Player_Name_Input);
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
