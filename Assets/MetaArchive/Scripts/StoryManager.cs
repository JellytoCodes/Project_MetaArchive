using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Data;

public enum MissionID { M1, M2, M3, M4, M5, M6}

[System.Serializable]
public sealed class ScanEntry
{
    public string imageName;
    public GameObject prefab;
    public string npcName = "NPC";
    [TextArea] public string[] lines;

    public MissionID mission;
    public AnimationClip encounterClip; // 원샷
    public AnimationClip talkLoopClip;  // 루프(Import > Loop Time=On)
    public AnimationClip farewellClip;  // 원샷
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
    [SerializeField] private string waveAnimStateName = "Wave";
    [SerializeField] private float waveCrossfade = 0.1f;

    [Header("In-Game Camera")]
    [SerializeField] Camera inGameCamera;

    readonly Dictionary<string, GameObject> _spawned = new();
    Dictionary<string, ScanEntry> _scanMap;
    Queue<string> _dialogueQueue;
    string _playerName = "플레이어";
    string _dialogueName = "뚱땅이";
    AudioListener _arAL, _gameAL;

    Dictionary<MissionID, bool> _missionDone = new();
    MissionID _currentMission;
    GameObject _currentActor;
    Animator _currentAnimator;

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

        if (arCamera) _arAL = arCamera.GetComponent<AudioListener>() ?? arCamera.gameObject.AddComponent<AudioListener>();
        if (inGameCamera) _gameAL = inGameCamera.GetComponent<AudioListener>() ?? inGameCamera.gameObject.AddComponent<AudioListener>();

        _scanMap = new Dictionary<string, ScanEntry>();
        foreach (var e in scanEntries)
        {
            if (e != null && !string.IsNullOrWhiteSpace(e.imageName) && !_scanMap.ContainsKey(e.imageName))
                _scanMap.Add(e.imageName, e);
        }
        foreach (MissionID id in System.Enum.GetValues(typeof(MissionID)))
        {
            _missionDone[id] = false;
        }
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
                CleanupAllSpawns(destroy:true);
                UIManager.Instance.ShowActivateCamera();
                break;

            case StoryState.Camera_Scanning:
                UIManager.Instance.ShowCameraScanning();
                SwitchCameraAR(true);
                break;
            case StoryState.Character_Intro :
                // 캐릭터 애니메이션 재생
                break;
            case StoryState.Dialogue_Running:
                _missionDone[_currentMission] = true;
                UIManager.Instance.ShowMissionStemp(_currentMission);
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
        if (img.trackingState != TrackingState.Tracking) return;

        var key = img.referenceImage.name;
        if (!_scanMap.TryGetValue(key, out var entry) || entry.prefab == null) return;

        if (_missionDone.TryGetValue(entry.mission, out var done) && done) return;

        _currentMission = entry.mission;
        StartCoroutine(PlayCharacterIntroRoutine(img, entry));
    }

    IEnumerator PlayCharacterIntroRoutine(ARTrackedImage img, ScanEntry entry)
    {
        // 스폰/부모/로컬 0 설정 그대로
        _currentActor = GetOrSpawn(entry.prefab, entry.imageName);
        var tr = _currentActor.transform;
        tr.SetParent(img.transform, false);
        tr.localPosition = Vector3.zero;
        tr.localRotation = Quaternion.identity;
        _currentActor.SetActive(true);

        var ap = _currentActor.GetComponentInChildren<AnimPlayer>() ?? _currentActor.AddComponent<AnimPlayer>();

        // 1) 조우
        if (entry.encounterClip) yield return ap.PlayOnce(entry.encounterClip);

        // 2) 대화 시작 전 루프 재생
        if (entry.talkLoopClip) ap.PlayLoop(entry.talkLoopClip);

        // 3) 대화
        BeginDialogue(entry.npcName, entry.lines);
        SetStoryState(StoryState.Dialogue_Running);
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

    void CleanupAllSpawns(bool destroy)
    {
        foreach (var kv in _spawned)
        {
            if (!kv.Value) continue;
            if (destroy) Destroy(kv.Value);
            else kv.Value.SetActive(false);
        }
        _spawned.Clear();
        _currentActor = null;
        _currentAnimator = null;
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
            if (CurrentStoryState == StoryState.Dialogue_Running)
            {
                StartCoroutine(FarewellThenReturn()); // 여기서 작별
                return;
            }
            switch (CurrentStoryState)
            {
                case StoryState.Intro_Meet_Dungddangi: SetStoryState(StoryState.Camera_Standby); break;
                case StoryState.Dialogue_Running: SetStoryState(StoryState.Camera_Standby); break;
                case StoryState.Ending_With_Dungddangi: SetStoryState(StoryState.Give_Gift_Prompt); break;
            }
            return;
        }

        _ = _dialogueQueue.Dequeue();
        if (_dialogueQueue.Count > 0) UIManager.Instance.ShowDialogue(_dialogueName, _dialogueQueue.Peek());
        else { _dialogueQueue = null; AdvanceDialogue(); }
    }
    IEnumerator FarewellThenReturn()
    {
        var entry = _scanMap.TryGetValue(_currentMission.ToString(), out var eByKey) ? eByKey : null; // 키 매칭 방식에 맞게 가져와라
        var ap = _currentActor ? _currentActor.GetComponentInChildren<AnimPlayer>() : null;

        if (entry != null && ap && entry.farewellClip)
            yield return ap.PlayOnce(entry.farewellClip);

        // 정리 후 카메라 UI로
        CleanupAllSpawns(true);
        SetStoryState(StoryState.Camera_Standby);
    }

    void SwitchCameraAR(bool on)
    {
        if (arCamera) arCamera.gameObject.SetActive(on);
        if (inGameCamera) inGameCamera.gameObject.SetActive(!on);

        if (_arAL) _arAL.enabled = on;
        if (_gameAL) _gameAL.enabled = !on;

        if (arSession) arSession.enabled = on;
        if (arBackground) arBackground.enabled = on;
        if (trackedImageManager) trackedImageManager.enabled = on && CurrentStoryState == StoryState.Camera_Scanning;
    }
}
