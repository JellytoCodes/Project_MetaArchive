using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

[DisallowMultipleComponent]
public sealed class SceneStateClipPlayer : MonoBehaviour
{
    [Header("Clip")]
    public AnimationClip clip;       // 단 하나의 클립
    public bool loop;                // true=루프, false=원샷

    [Header("When to play")]
    public StoryState[] playOnStates; // 이 상태로 전환될 때만 재생

    [Header("Exit behavior")]
    public bool stopOnExit = true;    // 상태가 벗어나면 정지
    public bool rewindOnExit = true;  // 정지 시 0프레임로 리셋

    Animator _anim;
    PlayableGraph _graph;
    AnimationPlayableOutput _output;
    AnimationClipPlayable _current;
    bool _ready;
    StoryState _lastState;
    Coroutine _oneShotCo;

    void Awake()
    {
        _anim = GetComponentInChildren<Animator>() ?? gameObject.AddComponent<Animator>();
        _anim.applyRootMotion = false; // 권장
    }

    void Start()
    {
        _lastState = StoryManager.Instance ? StoryManager.Instance.CurrentStoryState : default;
        // 시작 시 자동 재생 없음
    }

    void Update()
    {
        var sm = StoryManager.Instance;
        if (!sm) return;

        var cur = sm.CurrentStoryState;
        if (cur == _lastState) return;

        bool wasPlayable = ShouldPlayOn(_lastState);
        bool nowPlayable = ShouldPlayOn(cur);

        if (!wasPlayable && nowPlayable)
            Play();                 // 진입 시 재생

        if (wasPlayable && !nowPlayable)
            OnExitState();          // 이탈 시 정지/리셋

        _lastState = cur;
    }

    // ===== Core =====
    void Play()
    {
        if (!clip) return;
        EnsureGraph();

        if (_current.IsValid()) _current.Destroy();
        _current = AnimationClipPlayable.Create(_graph, clip);
        _current.SetApplyFootIK(false);
        _output.SetSourcePlayable(_current);
        if (!_graph.IsPlaying()) _graph.Play();

        if (_oneShotCo != null) { StopCoroutine(_oneShotCo); _oneShotCo = null; }
        if (!loop) _oneShotCo = StartCoroutine(CoStopAfter(clip.length));
    }

    IEnumerator CoStopAfter(float seconds)
    {
        // 실시간 대기: timeScale 영향 없음
        float t0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - t0 < Mathf.Max(0.001f, seconds))
            yield return null;
        _oneShotCo = null;
        OnExitState(); // 원샷 끝나면 자동 정지/리셋
    }

    void OnExitState()
    {
        if (!stopOnExit) return;

        if (_oneShotCo != null) { StopCoroutine(_oneShotCo); _oneShotCo = null; }

        if (_current.IsValid())
        {
            if (rewindOnExit) _current.SetTime(0);
            _current.Pause();
        }
        if (_graph.IsValid() && _graph.IsPlaying()) _graph.Stop();
    }

    // ===== Helpers =====
    bool ShouldPlayOn(StoryState s)
    {
        if (playOnStates == null || playOnStates.Length == 0) return false;
        for (int i = 0; i < playOnStates.Length; i++)
            if (playOnStates[i] == s) return true;
        return false;
    }

    void EnsureGraph()
    {
        if (_ready) return;
        _graph  = PlayableGraph.Create($"SceneStateClipPlayer:{name}");
        _output = AnimationPlayableOutput.Create(_graph, "Anim", _anim);
        _ready  = true;
    }

    void OnDisable()
    {
        if (_oneShotCo != null) { StopCoroutine(_oneShotCo); _oneShotCo = null; }
        if (_ready) _graph.Stop();
    }

    void OnDestroy()
    {
        if (_ready) { _graph.Destroy(); _ready = false; }
    }
}
