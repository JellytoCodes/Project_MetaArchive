using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public sealed class StoryManager : MonoBehaviour
{
    public static StoryManager Instance { get; private set; }
    public StoryState CurrentStoryState { get; private set; } = StoryState.Game_Start_Screen;

    [Header("AR")]
    [SerializeField] ARSession arSession;
    [SerializeField] ARSessionOrigin arOrigin;
    [SerializeField] ARCameraBackground arBackground;
    [SerializeField] ARTrackedImageManager trackedImageManager;
    [SerializeField] Camera arCamera;

    [Header("In-Game Camera")]
    [SerializeField] Camera inGameCamera;

    [Header("NPC Prefabs")]
    [SerializeField] GameObject dungddangiPrefab;
    [SerializeField] GameObject minjaePrefab;
    [SerializeField] GameObject sukyungPrefab;
    [SerializeField] GameObject seheePrefab;

    [Header("Quest Prefabs")]
    [SerializeField] GameObject keyboardPrefab;
    [SerializeField] GameObject vrGogiPrefab;
    [SerializeField] GameObject tabletPrefab;
    [SerializeField] GameObject giftBoxPrefab;

    [Header("Image Names (Reference Image Library)")]
    [SerializeField] string dungddangiImageName   = "Dungddangi_Image";
    [SerializeField] string contentRoomImageName  = "콘텐츠제작실_Image";
    [SerializeField] string keyboardImageName     = "Keyboard_Image";
    [SerializeField] string showroomImageName     = "쇼룸_Image";
    [SerializeField] string vrGogiImageName       = "VRGogi_Image";
    [SerializeField] string arvrRoomImageName     = "ARVR_Image";
    [SerializeField] string tabletImageName       = "Tablet_Image";
    [SerializeField] string giftBoxImageName      = "GiftBox_Image";

    readonly Dictionary<string, GameObject> _spawned = new();
    Queue<string> _dialogueQueue;
    string _playerName = "플레이어";
    string _dialogueName = "뚱땅이";
    AudioListener _arAL, _gameAL;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (!trackedImageManager)
            trackedImageManager = FindAnyObjectByType<ARTrackedImageManager>(FindObjectsInactive.Include);

        if (arCamera)    _arAL   = arCamera.GetComponent<AudioListener>() ?? arCamera.gameObject.AddComponent<AudioListener>();
        if (inGameCamera)_gameAL = inGameCamera.GetComponent<AudioListener>() ?? inGameCamera.gameObject.AddComponent<AudioListener>();
    }

    void OnEnable()
    {
        if (trackedImageManager) trackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
    }

    void OnDisable()
    {
        if (trackedImageManager) trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
    }

    void Start()
    {
        SetStoryState(StoryState.Game_Start_Screen);
    }

    // ===== Public API =====
    public void SetPlayerName(string name) => _playerName = string.IsNullOrWhiteSpace(name) ? "플레이어" : name.Trim();

    public void SetStoryState(StoryState next)
    {
        CurrentStoryState = next;
        SwitchCameraFor(next, focus:null);

        switch (next)
        {
            case StoryState.Game_Start_Screen:
                UIManager.Instance.ShowStart();
                break;

            case StoryState.Player_Name_Input:
                UIManager.Instance.ShowNameInput();
                break;

            case StoryState.Intro_Meet_Dungddangi:
                BeginDialogue("뚱땅이", new[]
                {
                    $"안녕, {_playerName}!",
                    "메타버스콘텐츠과에 온걸 환영해!",
                    "우리 학과에 대해 소개할게"
                });
                break;

            // === 콘텐츠 제작실 ===
            case StoryState.Move_To_Content_Room:
                UIManager.Instance.ShowActions(false, true, false);
                break;

            case StoryState.Meet_Minjae_In_Content_Room:
                BeginDialogue("민재", new[]
                {
                    "콘텐츠 제작실에 온 걸 환영해!",
                    "키보드를 찾아서 가져다줘."
                });
                break;

            case StoryState.Quest_Find_Keyboard:
                UIManager.Instance.ShowMission("키보드 이미지를 비춰서 찾아라.");
                break;

            case StoryState.Found_Keyboard:
                UIManager.Instance.ShowActions(true, false, false);
                break;

            case StoryState.Delivered_Keyboard:
                BeginDialogue("민재", new[] { "완벽해. 다음 장소로 가자." });
                break;

            case StoryState.Content_Room_Explained:
                UIManager.Instance.ShowActions(false, true, false);
                break;

            // === 쇼룸 ===
            case StoryState.Move_To_ShowRoom:
                UIManager.Instance.ShowActions(false, true, false);
                break;

            case StoryState.Meet_Sukyung_In_Showroom:
                BeginDialogue("수경", new[] { "쇼룸이야. VR 기기를 찾아와." });
                break;

            case StoryState.Quest_Find_VRGogi:
                UIManager.Instance.ShowMission("VR 기기 이미지를 인식시켜라.");
                break;

            case StoryState.Found_VRGogi:
                UIManager.Instance.ShowActions(true, false, false);
                break;

            case StoryState.Delivered_VRGogi:
                BeginDialogue("수경", new[] { "좋아. 이제 AR/VR 실습실로 이동하자." });
                break;

            case StoryState.Showroom_Explained:
                UIManager.Instance.ShowActions(false, true, false);
                break;

            // === AR/VR 실습실 ===
            case StoryState.Move_To_ARVR_Room:
                UIManager.Instance.ShowActions(false, true, false);
                break;

            case StoryState.Meet_Sehee_In_ARVR_Room:
                BeginDialogue("세희", new[] { "실습실이야. 타블렛을 찾아서 넘겨줘." });
                break;

            case StoryState.Quest_Find_Tablet:
                UIManager.Instance.ShowMission("타블렛 이미지를 인식시켜라.");
                break;

            case StoryState.Found_Tablet:
                UIManager.Instance.ShowActions(true, false, false);
                break;

            case StoryState.Delivered_Tablet:
                BeginDialogue("세희", new[] { "수고했어. 마지막으로 돌아가자." });
                break;

            case StoryState.ARVR_Room_Explained:
                UIManager.Instance.ShowActions(false, true, false);
                break;

            // === 엔딩 ===
            case StoryState.Ending_With_Dungddangi:
                BeginDialogue("뚱땅이", new[] { "마지막이야. 선물을 줄게." });
                break;

            case StoryState.Give_Gift_Prompt:
                UIManager.Instance.ShowActions(false, false, true);
                break;

            case StoryState.Gift_Received:
                UIManager.Instance.ShowActions(false, true, false);
                break;

            case StoryState.Final_Ending_Prompt:
                UIManager.Instance.ShowFinal("입학을 결정했습니까?");
                break;

            case StoryState.Game_Ended:
                BeginDialogue("시스템", new[] { "플레이 감사합니다." });
                break;
        }
    }

    public void OnNextDialoguePressed() => AdvanceDialogue();
    public void OnDeliverPressed() => AdvanceDeliver();
    public void OnMoveNextPressed() => GoNextPlace();
    public void OnReceiveGiftPressed() => SetStoryState(StoryState.Gift_Received);
    public void OnFinalAnswer(bool yes) => SetStoryState(StoryState.Game_Ended);

    // ===== AR =====
    void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs args)
    {
        foreach (var a in args.added)   TryHandleTracked(a);
        foreach (var u in args.updated) TryHandleTracked(u);
        foreach (var r in args.removed)
        {
            if (_spawned.TryGetValue(r.referenceImage.name, out var go) && go) go.SetActive(false);
        }
    }

    void TryHandleTracked(ARTrackedImage img)
    {
        if (img.trackingState != TrackingState.Tracking) return;

        string expected = GetExpectedImageNameForCurrentState();
        if (string.IsNullOrEmpty(expected)) return;
        if (!string.Equals(img.referenceImage.name, expected, System.StringComparison.Ordinal)) return;

        switch (CurrentStoryState)
        {
            case StoryState.Intro_Meet_Dungddangi:
                SpawnAt(img, dungddangiPrefab);
                SwitchCameraFor(CurrentStoryState, focus:_spawned[dungddangiImageName].transform);
                break;

            case StoryState.Quest_Find_Keyboard:
                SpawnAt(img, keyboardPrefab);
                SetStoryState(StoryState.Found_Keyboard);
                break;

            case StoryState.Quest_Find_VRGogi:
                SpawnAt(img, vrGogiPrefab);
                SetStoryState(StoryState.Found_VRGogi);
                break;

            case StoryState.Quest_Find_Tablet:
                SpawnAt(img, tabletPrefab);
                SetStoryState(StoryState.Found_Tablet);
                break;

            case StoryState.Ending_With_Dungddangi:
                SpawnAt(img, giftBoxPrefab);
                SwitchCameraFor(CurrentStoryState, focus:_spawned[giftBoxImageName].transform);
                break;
        }

        if (_spawned.TryGetValue(img.referenceImage.name, out var spawned)) spawned.SetActive(true);
    }

    // ===== Flow helpers =====
    void BeginDialogue(string npc, IEnumerable<string> lines)
    {
        _dialogueQueue = new Queue<string>(lines);
        _dialogueName = npc;
        UIManager.Instance.ShowDialogue(npc, _dialogueQueue.Peek());
    }

    void AdvanceDialogue()
    {
        if (_dialogueQueue == null || _dialogueQueue.Count == 0)
        {
            switch (CurrentStoryState)
            {
                case StoryState.Intro_Meet_Dungddangi:  SetStoryState(StoryState.Move_To_Content_Room); break;
                case StoryState.Delivered_Keyboard:     SetStoryState(StoryState.Content_Room_Explained); break;
                case StoryState.Delivered_VRGogi:       SetStoryState(StoryState.Showroom_Explained); break;
                case StoryState.Delivered_Tablet:       SetStoryState(StoryState.ARVR_Room_Explained); break;
                case StoryState.Ending_With_Dungddangi: SetStoryState(StoryState.Give_Gift_Prompt); break;
                default:                                UIManager.Instance.ShowActions(false, true, false); break;
            }
            return;
        }

        _ = _dialogueQueue.Dequeue();
        if (_dialogueQueue.Count > 0) UIManager.Instance.ShowDialogue(_dialogueName, _dialogueQueue.Peek());
        else { _dialogueQueue = null; AdvanceDialogue(); }
    }

    void AdvanceDeliver()
    {
        switch (CurrentStoryState)
        {
            case StoryState.Found_Keyboard: SetStoryState(StoryState.Delivered_Keyboard); break;
            case StoryState.Found_VRGogi:   SetStoryState(StoryState.Delivered_VRGogi);   break;
            case StoryState.Found_Tablet:   SetStoryState(StoryState.Delivered_Tablet);   break;
        }
    }

    void GoNextPlace()
    {
        switch (CurrentStoryState)
        {
            case StoryState.Move_To_Content_Room:
            case StoryState.Content_Room_Explained:
                SetStoryState(StoryState.Meet_Minjae_In_Content_Room); break;

            case StoryState.Move_To_ShowRoom:
            case StoryState.Showroom_Explained:
                SetStoryState(StoryState.Meet_Sukyung_In_Showroom); break;

            case StoryState.Move_To_ARVR_Room:
            case StoryState.ARVR_Room_Explained:
                SetStoryState(StoryState.Meet_Sehee_In_ARVR_Room); break;

            case StoryState.Gift_Received:
                SetStoryState(StoryState.Final_Ending_Prompt); break;
        }
    }

    // ===== Spawn =====
    void SpawnAt(ARTrackedImage img, GameObject prefab)
    {
        if (!prefab || !img) return;

        if (!_spawned.TryGetValue(img.referenceImage.name, out var go) || !go)
        {
            go = Instantiate(prefab);
            _spawned[img.referenceImage.name] = go;
        }

        go.transform.SetPositionAndRotation(img.transform.position, img.transform.rotation);
        go.SetActive(true);
    }

    // ===== Camera control (StoryManager 내부 처리) =====
    enum CamMode { AR, InGame }

    void SwitchCameraFor(StoryState state, Transform focus)
    {
        bool isSearch =
            state is StoryState.Quest_Find_Keyboard
                       or StoryState.Quest_Find_VRGogi
                       or StoryState.Quest_Find_Tablet
                       or StoryState.Meet_Minjae_In_Content_Room
                       or StoryState.Meet_Sukyung_In_Showroom
                       or StoryState.Meet_Sehee_In_ARVR_Room;

        bool isDialogue =
            state is StoryState.Intro_Meet_Dungddangi
                       or StoryState.Ending_With_Dungddangi
                       or StoryState.Game_Ended;

        if (isSearch) SetCamMode(CamMode.AR, null);
        else if (isDialogue) SetCamMode(CamMode.InGame, focus ?? GetFocusTransformForState());
        else SetCamMode(CamMode.AR, null);
    }

    void SetCamMode(CamMode mode, Transform focus)
    {
        bool useAR = mode == CamMode.AR;

        if (arCamera)     arCamera.gameObject.SetActive(useAR);
        if (inGameCamera) inGameCamera.gameObject.SetActive(!useAR);

        if (_arAL)   _arAL.enabled   = useAR;
        if (_gameAL) _gameAL.enabled = !useAR;

        if (arSession)       arSession.enabled = useAR;
        if (arOrigin)        arOrigin.enabled  = useAR;
        if (arBackground)    arBackground.enabled = useAR;
        if (trackedImageManager) trackedImageManager.enabled = useAR;

        if (!useAR && inGameCamera && focus)
        {
            // 간단한 대화 샷
            Vector3 offset = new Vector3(0f, 1.7f, 2.2f);
            inGameCamera.transform.position = focus.position + focus.TransformDirection(offset);
            inGameCamera.transform.LookAt(focus);
        }
    }

    Transform GetFocusTransformForState()
    {
        string key = CurrentStoryState switch
        {
            StoryState.Intro_Meet_Dungddangi      => dungddangiImageName,
            StoryState.Meet_Minjae_In_Content_Room=> contentRoomImageName,
            StoryState.Meet_Sukyung_In_Showroom   => showroomImageName,
            StoryState.Meet_Sehee_In_ARVR_Room    => arvrRoomImageName,
            StoryState.Ending_With_Dungddangi     => giftBoxImageName,
            _ => null
        };

        if (key != null && _spawned.TryGetValue(key, out var go) && go) return go.transform;
        return null;
    }

    string GetExpectedImageNameForCurrentState()
    {
        return CurrentStoryState switch
        {
            StoryState.Intro_Meet_Dungddangi => dungddangiImageName,

            StoryState.Meet_Minjae_In_Content_Room or StoryState.Content_Room_Explained
                => contentRoomImageName,
            StoryState.Quest_Find_Keyboard or StoryState.Found_Keyboard or StoryState.Quest_Deliver_Keyboard
                => keyboardImageName,

            StoryState.Meet_Sukyung_In_Showroom or StoryState.Showroom_Explained
                => showroomImageName,
            StoryState.Quest_Find_VRGogi or StoryState.Found_VRGogi or StoryState.Quest_Deliver_VRGogi
                => vrGogiImageName,

            StoryState.Meet_Sehee_In_ARVR_Room or StoryState.ARVR_Room_Explained
                => arvrRoomImageName,
            StoryState.Quest_Find_Tablet or StoryState.Found_Tablet or StoryState.Quest_Deliver_Tablet
                => tabletImageName,

            StoryState.Ending_With_Dungddangi
                => giftBoxImageName,

            _ => string.Empty
        };
    }
}
