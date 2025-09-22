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
    [SerializeField] private GameObject missionPanel;
    [SerializeField] private GameObject actionPanel;
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

    [Header("Mission")]
    [SerializeField] private TextMeshProUGUI missionText;

    [Header("Actions")]
    [SerializeField] private Button deliverObjectButton;
    [SerializeField] private Button moveToNextLocationButton;
    [SerializeField] private Button receiveGiftButton;

    [Header("Final")]
    [SerializeField] private TextMeshProUGUI finalQuestionText;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;

    string playerName = "신입생";

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        HideAll();

        // 시작 화면
        //Show(startPanel);

        // 버튼 바인딩
        startButton.onClick.AddListener(StartGame);

        nameConfirmButton.onClick.AddListener(OnNameConfirm);
        nextDialogueButton.onClick.AddListener(() => StoryManager.Instance.OnNextDialoguePressed());
        deliverObjectButton.onClick.AddListener(() => StoryManager.Instance.OnDeliverPressed());
        moveToNextLocationButton.onClick.AddListener(() => StoryManager.Instance.OnMoveNextPressed());
        receiveGiftButton.onClick.AddListener(() => StoryManager.Instance.OnReceiveGiftPressed());
        yesButton.onClick.AddListener(() => StoryManager.Instance.OnFinalAnswer(true));
        noButton.onClick.AddListener(() => StoryManager.Instance.OnFinalAnswer(false));
    }

    void OnDestroy()
    {
        // 메모리 누수 방지
        startButton.onClick.RemoveAllListeners();
        nameConfirmButton.onClick.RemoveAllListeners();
        nextDialogueButton.onClick.RemoveAllListeners();
        deliverObjectButton.onClick.RemoveAllListeners();
        moveToNextLocationButton.onClick.RemoveAllListeners();
        receiveGiftButton.onClick.RemoveAllListeners();
        yesButton.onClick.RemoveAllListeners();
        noButton.onClick.RemoveAllListeners();
    }

    // ========== Public API ==========
    public void ShowStart()                    { SwitchTo(startPanel); }
    public void ShowNameInput()                { SwitchTo(nameInputPanel); }
    public void ShowDialogue(string npc, string line)
    {
        SwitchTo(dialoguePanel);
        npcNameText.text = npc;
        dialogueText.text = line;
    }
    public void ShowMission(string text)
    {
        SwitchTo(missionPanel);
        missionText.text = text;
    }
    public void ShowActions(bool showDeliver, bool showMoveNext, bool showGift)
    {
        SwitchTo(actionPanel);
        deliverObjectButton.gameObject.SetActive(showDeliver);
        moveToNextLocationButton.gameObject.SetActive(showMoveNext);
        receiveGiftButton.gameObject.SetActive(showGift);
    }
    public void ShowFinal(string question)
    {
        SwitchTo(finalPanel);
        finalQuestionText.text = question;
    }
    public string GetPlayerName() => playerName;

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
        missionPanel.SetActive(false);
        actionPanel.SetActive(false);
        finalPanel.SetActive(false);
    }
    static void Show(GameObject go) => go.SetActive(true);
}
