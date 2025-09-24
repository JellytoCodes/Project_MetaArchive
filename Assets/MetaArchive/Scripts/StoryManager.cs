using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[System.Serializable]
public sealed class ScanEntry
{
    public string imageName;    // Image Library 이름
    public GameObject prefab;   // 스폰할 캐릭터
    public string npcName = "NPC";  //대화창에 표시할 이름
    [TextArea] public string[] lines;   //대사
}

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

    [Header("Scanning Table")]
    [SerializeField] private List<ScanEntry> scanEntries = new(); // 인스펙터에서 세팅
    [SerializeField] private string waveAnimStateName = "Wave";
    [SerializeField] private float waveCrossfade = 0.1f;
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 0f, 0.1f); // 이미지 앞 10cm

    [Header("In-Game Camera")]
    [SerializeField] Camera inGameCamera;

    readonly Dictionary<string, GameObject> _spawned = new();
    Dictionary<string, ScanEntry> _scanMap;
    Queue<string> _dialogueQueue;
    string _playerName = "플레이어";
    string _dialogueName = "뚱땅이";
    AudioListener _arAL, _gameAL;

    bool bIsMeetMinjae = false;
    bool bIsMeetSukyng = false;
    bool bIsMeetSehee = false;

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
                    "메타버스콘텐츠과에 오신걸 환영합니다!",
                    "저는 오늘 학과 소개를 도와드릴 뚱땅이입니다.",
                    "우리 과는 탄탄한 커리큘럼으로 단기간에 체계적인 실무 중심의 수업을 통해",
                    "메타버스콘텐츠 포트폴리오를 제작하고 개인의 역량을 더 빠르게 향상시킬 수 있어요!",
                    "그러면 무엇을 배우는지 보기위해 전공과목 수업이 진행되는 콘텐츠제작실로 안내할게요!"
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

            case StoryState.Dialogue_Running:
                    // TODO : 스폰 위치 조정하기
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

        StartCoroutine(PlayCharacterIntroRoutine(img, entry));
    }

    // 코루틴 및 유틸 추가
    IEnumerator PlayCharacterIntroRoutine(ARTrackedImage img, ScanEntry entry)
    {
        SetStoryState(StoryState.Character_Intro);

        // 1) 스폰
        var go = GetOrSpawn(entry.prefab, entry.imageName);
        var t = go.transform;

        // 이미지 평면 앞쪽으로 오프셋, 카메라를 바라보게 정렬
        var forward = arCamera ? (t.position - arCamera.transform.position) : img.transform.forward;
        t.SetPositionAndRotation(
            img.transform.TransformPoint(spawnOffset),
            Quaternion.LookRotation(forward, Vector3.up)
        );
        go.SetActive(true);

        // 2) 애니 재생(선택)
        var animator = go.GetComponentInChildren<Animator>();
        if (animator && !string.IsNullOrEmpty(waveAnimStateName))
        {
            animator.Update(0f);
            animator.CrossFade(waveAnimStateName, waveCrossfade);
            yield return WaitForAnimDone(animator, waveAnimStateName);
        }
        else
        {
            yield return new WaitForSeconds(0.8f);
        }

        // 3) 대화 시작
        if (entry.lines != null && entry.lines.Length > 0)
            BeginDialogue(entry.npcName, entry.lines);
        else
            BeginDialogue(entry.npcName, new[] { "설정된 대사가 없습니다." });

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

    IEnumerator WaitForAnimDone(Animator animator, string stateName)
    {
        int layer = 0;
        // 진입 대기
        while (true)
        {
            var st = animator.GetCurrentAnimatorStateInfo(layer);
            if (st.IsName(stateName)) break;
            yield return null;
        }
        // 종료 대기
        while (true)
        {
            var st = animator.GetCurrentAnimatorStateInfo(layer);
            if (!st.IsName(stateName) || st.normalizedTime >= 1f) break;
            yield return null;
        }
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

    void SwitchCameraAR(bool on)
    {
        if (arCamera) arCamera.gameObject.SetActive(on);
        if (inGameCamera) inGameCamera.gameObject.SetActive(!on);

        if (_arAL) _arAL.enabled = on;
        if (_gameAL) _gameAL.enabled = !on;

        if (arSession) arSession.enabled = on;
        if (arOrigin) arOrigin.enabled = on;
        if (arBackground) arBackground.enabled = on;
        if (trackedImageManager) trackedImageManager.enabled = on && CurrentStoryState == StoryState.Camera_Scanning;
    }
}
