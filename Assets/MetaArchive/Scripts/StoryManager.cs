using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public enum MissionID { M1, M2, M3, M4, M5, M6 }
public enum CameraState { Intro, OnGoing, Outro }

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
    [SerializeField] List<ScanEntry> scanEntries = new();

    [Header("In-Game Camera")]
    [SerializeField] Camera inGameCamera;
    [SerializeField] Camera inGameStandby;
    [SerializeField] Camera inGameEnding;

    // runtime
    readonly Dictionary<string, GameObject> _spawned = new();
    readonly HashSet<string> _spawnLocked = new();                 // 이미지별 1회 스폰 가드
    Dictionary<string, ScanEntry> _scanMap;
    readonly Dictionary<MissionID, bool> _missionDone = new();
    Queue<string> _dialogueQueue;

    string _playerName = "플레이어";
    string _dialogueName = "뚱땅이";
    AudioListener _arAL, _gameAL;

    MissionID _currentMission;
    ScanEntry _currentEntry;
    GameObject _currentActor;
    Animator _currentAnimator;
    bool _introPlaying;
    CameraState cameraState = CameraState.Intro;

    // ===== Unity =====
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
            if (e != null && !string.IsNullOrWhiteSpace(e.imageName) && !_scanMap.ContainsKey(e.imageName))
                _scanMap.Add(e.imageName, e);

        foreach (MissionID id in System.Enum.GetValues(typeof(MissionID))) _missionDone[id] = false;
    }

    void Start() => SetStoryState(StoryState.Game_Start_Screen);

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
                    $"{_playerName}님 안녕하세요! \n메타버스콘텐츠과에 오신걸 환영합니다!",
                    "저는 오늘 학과를 안내할 뚱땅이입니다.",
                    "우리 과는 탄탄한 커리큘럼으로 단기간 \n체계적인 실무 중심의 수업을 통해 \n다양한 메타버스콘텐츠를 제작하고 \n개인의 역량을 더 빠르게 향상시킬 수 있답니다!",
                    "그러면 학과를 돌아다니면서 \n재학생들과의 대화를 통해 \n저희 과에 대한 소개를 들어보고 스탬프를 모아와주세요!"
                });
                break;

            case StoryState.Camera_Standby:
                cameraState = CameraState.OnGoing;
                SwitchCameraAR(false);
                if (_missionDone[MissionID.M1] && _missionDone[MissionID.M2] &&
                    _missionDone[MissionID.M3] && _missionDone[MissionID.M4] &&
                    _missionDone[MissionID.M5] && _missionDone[MissionID.M6])
                    UIManager.Instance.SwapCameraButton();
                UIManager.Instance.ShowActivateCamera();
                break;

            case StoryState.Camera_Scanning:
                UIManager.Instance.ShowCameraScanning();
                SwitchCameraAR(true);
                break;

            case StoryState.Dialogue_Running:
                break;

            case StoryState.Game_Ended:
                cameraState = CameraState.Outro;
                SwitchCameraAR(false);
                BeginDialogue("뚱땅이", new[] { $"{_playerName}님 수고하셨습니다!" });
                break;
        }
    }

    public void OnNextDialoguePressed() => AdvanceDialogue();
    public void OnARCameraClosePressed() => SetStoryState(StoryState.Camera_Standby);
    public void StempSubmitPressed() => SetStoryState(StoryState.Game_Ended);
    public void OnARCameraActivatePressed() => SetStoryState(StoryState.Camera_Scanning);

    // ===== AR =====
    void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs args)
    {
        foreach (var a in args.added)   TryHandleTracked(a);
        foreach (var u in args.updated) TryHandleTracked(u); // 업데이트도 처리. 락으로 중복 방지.
        foreach (var r in args.removed)
        {
            _spawnLocked.Remove(r.referenceImage.name);
            if (_spawned.TryGetValue(r.referenceImage.name, out var go) && go) go.SetActive(false);
        }
    }

    void TryHandleTracked(ARTrackedImage img)
    {
        if (CurrentStoryState != StoryState.Camera_Scanning) return;
        if (_introPlaying) return;
        if (img.trackingState != TrackingState.Tracking) return;

        var key = img.referenceImage.name;
        if (_spawnLocked.Contains(key)) return;
        if (!_scanMap.TryGetValue(key, out var entry) || entry.prefab == null) return;
        if (_missionDone.TryGetValue(entry.mission, out var done) && done) return;

        _spawnLocked.Add(key);
        _currentMission = entry.mission;
        _currentEntry   = entry;
        _introPlaying   = true;
        StartCoroutine(PlayCharacterIntroRoutine(img, entry, key));
    }

    // ===== Spawn & Flow =====
    IEnumerator PlayCharacterIntroRoutine(ARTrackedImage img, ScanEntry entry, string key)
    {
        // 1) 이미지 오브젝트에 ARAnchor 보장(6.2 안전)
        var anchor = img.GetComponent<ARAnchor>();
        if (!anchor) anchor = img.gameObject.AddComponent<ARAnchor>();
        var parent = anchor.transform;

        // 2) 스폰: 부모=앵커, 로컬 0 고정
        _currentActor = GetOrSpawn(entry.prefab, entry.imageName);
        var t = _currentActor.transform;
        t.SetParent(parent, false);
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale    = Vector3.one;
        _currentActor.SetActive(true);

        // 3) 이동 유발자 차단
        var anim = _currentActor.GetComponentInChildren<Animator>(true);
        if (anim != null) anim.applyRootMotion = false;
        var rb = _currentActor.GetComponentInChildren<Rigidbody>(true);
        if (rb != null) { rb.isKinematic = true; rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        var cc = _currentActor.GetComponentInChildren<CharacterController>(true);
        if (cc != null) cc.enabled = false;

        // 4) 초기 3프레임 재설정
        for (int i = 0; i < 3; i++) { t.localPosition = Vector3.zero; t.localRotation = Quaternion.identity; yield return null; }

        _currentAnimator = anim;
        var ap = _currentActor.GetComponentInChildren<AnimPlayer>() ?? _currentActor.AddComponent<AnimPlayer>();

        // 5) 애니 + 대화
        if (entry.encounterClip) yield return ap.PlayOnce(entry.encounterClip);
        if (entry.talkLoopClip)  ap.PlayLoop(entry.talkLoopClip);

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
                    if (inGameCamera) inGameCamera.gameObject.SetActive(false);
                    if (inGameEnding) inGameEnding.gameObject.SetActive(false);
                    SetStoryState(StoryState.Camera_Standby);
                    break;

                case StoryState.Dialogue_Running:
                    StartCoroutine(CoFarewellThenReturn());
                    break;

                case StoryState.Game_Ended:
                    SetStoryState(StoryState.Game_Ended);
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
        UIManager.Instance.HideDialogue();

        var ap = _currentActor ? _currentActor.GetComponentInChildren<AnimPlayer>() : null;
        if (ap != null && _currentEntry != null && _currentEntry.farewellClip)
            yield return ap.PlayOnce(_currentEntry.farewellClip);

        _missionDone[_currentMission] = true;
        UIManager.Instance.ShowMissionStemp(_currentMission);

        // 같은 이미지를 다시 쓰게 하려면 락 해제
        if (_currentEntry != null) _spawnLocked.Remove(_currentEntry.imageName);

        CleanupCurrentSpawn(true);
        SetStoryState(StoryState.Camera_Standby);
    }

    void CleanupCurrentSpawn(bool destroy)
    {
        if (_currentActor)
        {
            if (destroy) Destroy(_currentActor);
            else _currentActor.SetActive(false);
        }
        _currentActor = null;
        _currentAnimator = null;
        _currentEntry = null;
    }

    // ===== Camera =====
    void SwitchCameraAR(bool on)
    {
        if (arCamera) arCamera.gameObject.SetActive(on);

        switch (cameraState)
        {
            case CameraState.Intro:
                if (inGameCamera) inGameCamera.gameObject.SetActive(!on);
                break;
            case CameraState.OnGoing:
                if (inGameStandby) inGameStandby.gameObject.SetActive(!on);
                break;
            case CameraState.Outro:
                if (inGameStandby) inGameStandby.gameObject.SetActive(false);
                if (inGameEnding)  inGameEnding.gameObject.SetActive(!on);
                break;
        }

        if (_arAL) _arAL.enabled = on;
        if (_gameAL) _gameAL.enabled = !on;

        if (arSession)     arSession.enabled = on;
        if (arBackground)  arBackground.enabled = on;
        if (trackedImageManager) trackedImageManager.enabled = on && CurrentStoryState == StoryState.Camera_Scanning;
    }
}
