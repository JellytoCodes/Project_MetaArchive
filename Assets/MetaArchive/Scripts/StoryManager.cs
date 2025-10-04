// StoryManager.cs — 월드락 스폰 + 발바닥 Y보정 + 카메라(Yaw) 바라보기 포함

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public enum MissionID { M1, M2, M3, M4, M5, M6 }
public enum CameraState { Intro, OnGoing, Outro, Gift }

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
    [SerializeField] Camera inGameCamera;   // 오프닝
    [SerializeField] Camera inGameStandby;  // 스탠바이
    [SerializeField] Camera inGameEnding;   // 엔딩
    [SerializeField] Camera inGameGift;     // 기프트

    [Header("World Lock Root (optional)")]
    [SerializeField] Transform worldRoot; // 비우면 씬 루트

    [Header("Spawn / Align")]
    [Tooltip("모델 피벗이 허리·가슴일 때 발이 이미지 평면에 닿도록 자동 보정")]
    [SerializeField] bool snapFeetToImage = true;
    [Tooltip("추가 Y 보정(+ 위로). 소수로 미세 조정")]
    [SerializeField] float extraYOffset = 0f;

    // Runtime
    readonly Dictionary<string, GameObject> _spawned = new();
    readonly HashSet<string> _spawnLocked = new();
    readonly Dictionary<MissionID, bool> _missionDone = new();
    Dictionary<string, ScanEntry> _scanMap;
    Queue<string> _dialogueQueue;

    string _playerName = "플레이어";
    string _dialogueName = "뚱땅이";
    AudioListener _arAL, _gameAL;

    MissionID _currentMission;
    ScanEntry _currentEntry;
    GameObject _currentActor;
    Animator _currentAnimator;
    GameObject _worldLockNode;
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
                cameraState = CameraState.Intro;
                SwitchCameraAR(false);
                BeginDialogue("뚱땅이", new[]
                {
                    $"{_playerName}님 안녕하세요!\n저는 오늘 학과 소개를 도와드릴\n뚱땅이라고 합니다.",
                    "메타버스콘텐츠과에 오신 걸 환영해요!",
                    "저희 과는 탄탄한 커리큘럼으로\n단기간 체계적인 실무 중심의 수업으로\n개인의 역량을 빠르게 향상시켜\n다양한 메타버스콘텐츠를 제작하는 것이 목표입니다",
                    "저와 함께 학과를 탐방하면서\n총 6개의 마크를 찾아 사진을 찍어\n선배들에게 학과에 대한 얘기를 듣고\n스탬프를 모아보세요!"
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
                BeginDialogue("뚱땅이", new[]
                {
                    $"{_playerName}님 수고하셨습니다!\n오늘 학과를 돌아다녀보니 어떠셨나요?",
                    $"지금 플레이하고 계신 이 게임은\n{_playerName}님의 선배분들께서\n신입생들에게 도움이 되었으면 하여\n만든 작품입니다!",
                    $"{_playerName}님의 앞날을 기원하며\\n저희 과에서 신입생들을 위해 준비한\n선물을 꼭 받아가시길 바래요!"
                });
                break;

            case StoryState.Gift_Box:
                cameraState = CameraState.Gift;
                SwitchCameraAR(false);
                UIManager.Instance.ShowGiftBox();
                break;
        }
    }

    public void OnNextDialoguePressed() => AdvanceDialogue();
    public void OnARCameraClosePressed() => SetStoryState(StoryState.Camera_Standby);
    public void StempSubmitPressed() => SetStoryState(StoryState.Game_Ended);
    public void OnARCameraActivatePressed() => SetStoryState(StoryState.Camera_Scanning);

    // ===== AR Events =====
    void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs args)
    {
        foreach (var a in args.added)   TryHandleTracked(a);
        foreach (var u in args.updated) TryHandleTracked(u);
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
        StartCoroutine(PlayCharacterIntroRoutine_WorldLock(img, entry));
    }

    // ===== World-lock spawn =====
    IEnumerator PlayCharacterIntroRoutine_WorldLock(ARTrackedImage img, ScanEntry entry)
    {
        // 1) 월드락 노드 생성(이미지 포즈 복사)
        var pose = new Pose(img.transform.position, img.transform.rotation);
        _worldLockNode = new GameObject($"WorldLock_{entry.imageName}");
        if (worldRoot) _worldLockNode.transform.SetParent(worldRoot, false);
        else           _worldLockNode.transform.SetParent(null, false);
        _worldLockNode.transform.SetPositionAndRotation(pose.position, pose.rotation);

        // 2) 프리팹 스폰(부모=월드락, 로컬 0)
        _currentActor = GetOrSpawn(entry.prefab, entry.imageName);
        var t = _currentActor.transform;
        t.SetParent(_worldLockNode.transform, false);
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale    = Vector3.one;
        _currentActor.SetActive(true);

        // 3) 발바닥 Y 보정(부모 로컬 기준 y=0에 맞춤)
        if (snapFeetToImage)
        {
            yield return null; // bounds 안정화
            float lift = CalcLocalLiftToGround(_currentActor, _worldLockNode.transform, extraYOffset);
            if (!Mathf.Approximately(lift, 0f))
                t.localPosition += new Vector3(0f, lift, 7.0f);
        }

        // 4) 카메라 바라보기(Yaw만)
        var face = _currentActor.GetComponentInChildren<FaceCamera>();
        if (!face) face = _currentActor.AddComponent<FaceCamera>();
        face.Init(arCamera, yawOnly: true, continuous: false);

        // 안전장치
        var anim = _currentActor.GetComponentInChildren<Animator>(true);
        if (anim) anim.applyRootMotion = false;
        var rb = _currentActor.GetComponentInChildren<Rigidbody>(true);
        if (rb) { rb.isKinematic = true; rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        var cc = _currentActor.GetComponentInChildren<CharacterController>(true);
        if (cc) cc.enabled = false;
        DisableBillboards(_currentActor);

        _currentAnimator = anim;
        var ap = _currentActor.GetComponentInChildren<AnimPlayer>() ?? _currentActor.AddComponent<AnimPlayer>();

        // 5) 애니 + 대사
        if (entry.encounterClip) yield return ap.PlayOnce(entry.encounterClip);
        if (entry.talkLoopClip)  ap.PlayLoop(entry.talkLoopClip);

        BeginDialogue(entry.npcName, entry.lines);
        SetStoryState(StoryState.Dialogue_Running);

        _introPlaying = false;
    }

    // ===== 로컬 기준 바닥 스냅 =====
    float CalcLocalLiftToGround(GameObject root, Transform parentOfRoot, float extraY)
    {
        bool hasAny = false;
        float minLocalY = float.PositiveInfinity;

        void AccMin(Renderer r)
        {
            if (!r.enabled) return;
            var b = r.bounds;
            Vector3 c = b.center;
            Vector3 e = b.extents;

            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                Vector3 cornerW = c + new Vector3(e.x * sx, e.y * sy, e.z * sz);
                Vector3 cornerL = parentOfRoot.InverseTransformPoint(cornerW);
                if (cornerL.y < minLocalY) { minLocalY = cornerL.y; hasAny = true; }
            }
        }

        foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) AccMin(r);
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))       AccMin(r);

        if (!hasAny) return 0f;
        return -minLocalY + extraY; // y=0 평면까지 올림
    }

    void DisableBillboards(GameObject root)
    {
        var comps = root.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var c in comps)
        {
            if (!c) continue;
            var n = c.GetType().Name;
            if (n.Contains("Billboard") || n.Contains("LookAt") || n.Contains("Follow"))
                c.enabled = false;
        }
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
                    SetStoryState(StoryState.Gift_Box);
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

        if (_worldLockNode) { Destroy(_worldLockNode); _worldLockNode = null; }
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
            case CameraState.Gift:
                if (inGameEnding) inGameEnding.gameObject.SetActive(false);
                if (inGameGift)  inGameGift.gameObject.SetActive(!on);
                break;
        }

        if (_arAL) _arAL.enabled = on;
        if (_gameAL) _gameAL.enabled = !on;

        if (arBackground)  arBackground.enabled = on;
        if (trackedImageManager) trackedImageManager.enabled = on && CurrentStoryState == StoryState.Camera_Scanning;
    }
}
