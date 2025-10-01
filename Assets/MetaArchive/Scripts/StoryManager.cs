using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public enum MissionID { M1, M2, M3, M4, M5, M6}

[System.Serializable]
public sealed class ScanEntry
{
    public string imageName;
    public GameObject prefab;
    public string npcName = "NPC";
    [TextArea] public string[] lines;

    public MissionID mission;
    public AnimationClip encounterClip;
    public AnimationClip talkLoopClip;
    public AnimationClip farewellClip;
}

public sealed class StoryManager : MonoBehaviour
{
    public static StoryManager Instance { get; private set; }
    public StoryState CurrentStoryState { get; private set; } = StoryState.Game_Start_Screen;

    [Header("AR")]
    [SerializeField] ARSession arSession;
    [SerializeField] ARCameraBackground arBackground;
    [SerializeField] ARTrackedImageManager trackedImageManager;
    [SerializeField] Camera arCamera;

    [Header("Scanning Table")]
    [SerializeField] private List<ScanEntry> scanEntries = new();

    [Header("In-Game Camera")]
    [SerializeField] Camera inGameCamera;
    [SerializeField] Camera inGameStandby;
    
    readonly Dictionary<string, GameObject> _spawned = new();
    Dictionary<string, ScanEntry> _scanMap;
    Queue<string> _dialogueQueue;

    string _playerName = "플레이어";
    string _dialogueName = "뚱땅이";
    AudioListener _arAL, _gameAL;

    // === Mission / Runtime ===
    Dictionary<MissionID, bool> _missionDone = new();
    MissionID _currentMission;
    ScanEntry _currentEntry;          // 현재 엔트리 캐시
    GameObject _currentActor;
    Animator _currentAnimator;
    bool _introPlaying;
    bool _isCameraStandby = false;
    void OnEnable()
    {
        if (trackedImageManager) trackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
    }
    void OnDisable()
    {
        if (trackedImageManager) trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (!trackedImageManager)
            trackedImageManager = FindAnyObjectByType<ARTrackedImageManager>(FindObjectsInactive.Include);

        if (arCamera)    _arAL   = arCamera.GetComponent<AudioListener>() ?? arCamera.gameObject.AddComponent<AudioListener>();
        if (inGameCamera)_gameAL = inGameCamera.GetComponent<AudioListener>() ?? inGameCamera.gameObject.AddComponent<AudioListener>();

        _scanMap = new Dictionary<string, ScanEntry>();
        foreach (var e in scanEntries)
        {
            if (e != null && !string.IsNullOrWhiteSpace(e.imageName) && !_scanMap.ContainsKey(e.imageName))
                _scanMap.Add(e.imageName, e);
        }
        foreach (MissionID id in System.Enum.GetValues(typeof(MissionID)))
            _missionDone[id] = false;
    }

    void Start() => SetStoryState(StoryState.Game_Start_Screen);

    // ===== Public API =====
    public void SetPlayerName(string name) => _playerName = string.IsNullOrWhiteSpace(name) ? "플레이어" : name.Trim();

    public void SetStoryState(StoryState next)
    {
        var sceneMascot = FindObjectOfType<SceneAnimPlayer>();
        CurrentStoryState = next;
        switch (next)
        {
            case StoryState.Game_Start_Screen:
                SwitchCameraAR(false);
                UIManager.Instance.ShowStart();
                break;

            case StoryState.Player_Name_Input:
                SwitchCameraAR(false);
                UIManager.Instance.ShowNameInput();
                break;

            case StoryState.Intro_Meet_Dungddangi:
                SwitchCameraAR(false);
                _isCameraStandby = true;
                
                if (sceneMascot) StartCoroutine(sceneMascot.PlayIntroThenLoop());

                BeginDialogue("뚱땅이", new[]
                {
                    $"{_playerName}님 안녕하세요!",
                    "메타버스콘텐츠과에 \n오신걸 환영합니다!",
                    "저는 오늘 학과 소개를 도와드릴 \n뚱땅이입니다.",
                    "우리 과는 탄탄한 커리큘럼으로 \n단기간에 체계적인 실무 중심의 수업을 통해",
                    "메타버스콘텐츠 포트폴리오를 \n제작하고 개인의 역량을 더 빠르게 향상시킬 수 있어요!",
                    "그러면 학과를 돌아다니면서 \n재학생들과 대화를 통해 \n저희 과에 대한 소개를 들어보고 스탬프를 모아와주세요!"
                });
                break;

            case StoryState.Camera_Standby:
                SwitchCameraAR(false);
                UIManager.Instance.ShowActivateCamera();
                break;

            case StoryState.Camera_Scanning:
                UIManager.Instance.ShowCameraScanning();
                SwitchCameraAR(true);
                break;

            case StoryState.Character_Intro:
                
                break;

            case StoryState.Dialogue_Running:
                
                break;
        }
    }

    public void OnNextDialoguePressed() => AdvanceDialogue();
    public void OnReceiveGiftPressed() => SetStoryState(StoryState.Gift_Received);
    public void OnARCameraClosePressed() => SetStoryState(StoryState.Camera_Standby);
    public void OnFinalAnswer(bool yes) => SetStoryState(StoryState.Game_Ended);
    public void OnARCameraActivatePressed() => SetStoryState(StoryState.Camera_Scanning);

    // ===== AR =====
    void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs args)
    {
        foreach (var a in args.added)   TryHandleTracked(a);
        foreach (var u in args.updated) TryHandleTracked(u);
        foreach (var r in args.removed)
            if (_spawned.TryGetValue(r.referenceImage.name, out var go) && go) go.SetActive(false);
    }

    void TryHandleTracked(ARTrackedImage img)
    {
        if (CurrentStoryState != StoryState.Camera_Scanning) return;
        if (_introPlaying) return;
        if (img.trackingState != TrackingState.Tracking) return;

        var key = img.referenceImage.name;
        if (!_scanMap.TryGetValue(key, out var entry) || entry.prefab == null) return;

        // 완료한 미션은 무시
        if (_missionDone.TryGetValue(entry.mission, out var done) && done) return;

        _currentMission = entry.mission;
        _currentEntry   = entry;
        _introPlaying   = true;
        StartCoroutine(PlayCharacterIntroRoutine(img, entry));
    }

    IEnumerator PlayCharacterIntroRoutine(ARTrackedImage img, ScanEntry entry)
    {
        _currentActor = GetOrSpawn(entry.prefab, entry.imageName);
        _currentActor.transform.SetPositionAndRotation(img.transform.position, img.transform.rotation);
        _currentActor.SetActive(true);

        _currentAnimator = _currentActor.GetComponentInChildren<Animator>();
        var ap = _currentActor.GetComponentInChildren<AnimPlayer>() ?? _currentActor.AddComponent<AnimPlayer>();
        
        if (entry.encounterClip) yield return ap.PlayOnce(entry.encounterClip);
        if (entry.talkLoopClip) ap.PlayLoop(entry.talkLoopClip);
        
        BeginDialogue(entry.npcName, entry.lines);
        SetStoryState(StoryState.Dialogue_Running);

        _introPlaying = false;
    }

    GameObject GetOrSpawn(GameObject prefab, string key)
    {
        if (!_spawned.TryGetValue(key, out var go) || !go)
        {
            go = Instantiate(prefab);
            _spawned[key] = go;
        }
        return go;
    }

    // ===== Dialogue =====
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
                case StoryState.Intro_Meet_Dungddangi:
                    if(inGameCamera) inGameCamera.gameObject.SetActive(false);
                    SetStoryState(StoryState.Camera_Standby);
                    break;

                case StoryState.Dialogue_Running:
                    StartCoroutine(CoFarewellThenReturn()); // 작별 대기 → 정리 → 스탠바이
                    break;

                case StoryState.Ending_With_Dungddangi:
                    SetStoryState(StoryState.Give_Gift_Prompt);
                    break;
            }
            return;
        }

        _ = _dialogueQueue.Dequeue();
        if (_dialogueQueue.Count > 0)
            UIManager.Instance.ShowDialogue(_dialogueName, _dialogueQueue.Peek());
        else
        {
            _dialogueQueue = null;
            AdvanceDialogue();
        }
    }

    IEnumerator CoFarewellThenReturn()
    {
        // 다이얼로그 가림. AR 카메라는 유지.
        UIManager.Instance.HideDialogue();

        var ap = _currentActor ? _currentActor.GetComponentInChildren<AnimPlayer>() : null;
        if (ap != null && _currentEntry != null && _currentEntry.farewellClip)
            yield return ap.PlayOnce(_currentEntry.farewellClip);

        // 미션 완료 마킹 및 UI
        _missionDone[_currentMission] = true;
        UIManager.Instance.ShowMissionStemp(_currentMission);

        // 스폰 정리 후 스탠바이로 전환(이때 카메라 종료)
        CleanupCurrentSpawn(true);
        SetStoryState(StoryState.Camera_Standby);
    }

    void CleanupCurrentSpawn(bool destroy)
    {
        if (_currentActor)
        {
            if (destroy) Destroy(_currentActor); else _currentActor.SetActive(false);
        }
        _currentActor = null;
        _currentAnimator = null;
        _currentEntry = null;
    }

    // ===== Camera =====
    void SwitchCameraAR(bool on)
    {
        if (arCamera) arCamera.gameObject.SetActive(on);
        
        if (inGameCamera && !_isCameraStandby)
        {
            inGameCamera.gameObject.SetActive(!on);
        }
        if(inGameStandby && _isCameraStandby) inGameStandby.gameObject.SetActive(!on);

        if (_arAL) _arAL.enabled = on;
        if (_gameAL) _gameAL.enabled = !on;

        if (arSession) arSession.enabled = on;
        if (arBackground) arBackground.enabled = on;
        if (trackedImageManager) trackedImageManager.enabled = on && CurrentStoryState == StoryState.Camera_Scanning;
    }
}
