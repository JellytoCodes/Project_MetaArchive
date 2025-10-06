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

    private Animator anim;
    private PlayableGraph graph;
    private AnimationPlayableOutput output;
    private AnimationClipPlayable current;
    private bool ready;
    private StoryState lastState;
    private Coroutine oneShotCo;

    private void Awake()
    {
        anim = GetComponentInChildren<Animator>() ?? gameObject.AddComponent<Animator>();
        anim.applyRootMotion = false;
    }

    private void Start()
    {
        lastState = StoryManager.instance ? StoryManager.instance.currentStoryState : default;
    }

    private void Update()
    {
        var sm = StoryManager.instance;
        if (!sm) return;

        var cur = sm.currentStoryState;
        if (cur == lastState) return;

        bool wasPlayable = ShouldPlayOn(lastState);
        bool nowPlayable = ShouldPlayOn(cur);

        if (!wasPlayable && nowPlayable)
            Play();                 // 진입 시 재생

        if (wasPlayable && !nowPlayable)
            OnExitState();          // 이탈 시 정지/리셋

        lastState = cur;
    }

    // ===== Core =====
    void Play()
    {
        if (!clip) return;
        EnsureGraph();

        if (current.IsValid()) current.Destroy();
        current = AnimationClipPlayable.Create(graph, clip);
        current.SetApplyFootIK(false);
        output.SetSourcePlayable(current);
        if (!graph.IsPlaying()) graph.Play();

        if (oneShotCo != null) { StopCoroutine(oneShotCo); oneShotCo = null; }
        if (!loop) oneShotCo = StartCoroutine(CoStopAfter(clip.length));
    }

    IEnumerator CoStopAfter(float seconds)
    {
        // 실시간 대기: timeScale 영향 없음
        float t0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - t0 < Mathf.Max(0.001f, seconds))
            yield return null;
        oneShotCo = null;
        OnExitState(); // 원샷 끝나면 자동 정지/리셋
    }

    void OnExitState()
    {
        if (!stopOnExit) return;

        if (oneShotCo != null) { StopCoroutine(oneShotCo); oneShotCo = null; }

        if (current.IsValid())
        {
            if (rewindOnExit) current.SetTime(0);
            current.Pause();
        }
        if (graph.IsValid() && graph.IsPlaying()) graph.Stop();
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
        if (ready) return;
        graph  = PlayableGraph.Create($"SceneStateClipPlayer:{name}");
        output = AnimationPlayableOutput.Create(graph, "Anim", anim);
        ready  = true;
    }

    void OnDisable()
    {
        if (oneShotCo != null) { StopCoroutine(oneShotCo); oneShotCo = null; }
        if (ready) graph.Stop();
    }

    void OnDestroy()
    {
        if (ready) { graph.Destroy(); ready = false; }
    }
}
