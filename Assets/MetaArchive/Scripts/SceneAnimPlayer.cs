using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

[DisallowMultipleComponent]
public sealed class SceneAnimPlayer : MonoBehaviour
{
    [Header("Clips")]
    public AnimationClip introClip;   // 원샷(조우)
    public AnimationClip loopClip;    // 루프(Loop Time=On)
    public AnimationClip outroClip;   // 원샷(작별)

    [Header("Options")]
    public bool autoPlayOnStart = false; // Start에서 Intro→Loop 자동 재생

    Animator _anim;
    PlayableGraph _graph;
    AnimationPlayableOutput _output;
    AnimationClipPlayable _current;
    bool _ready;

    void Awake()
    {
        _anim = GetComponentInChildren<Animator>() ?? gameObject.AddComponent<Animator>();
        _anim.applyRootMotion = false;
    }

    void Start()
    {
        if (autoPlayOnStart) StartCoroutine(PlayIntroThenLoop());
    }

    void OnDisable() { if (_ready) _graph.Stop(); }
    void OnDestroy() { if (_ready) { _graph.Destroy(); _ready = false; } }

    // ===== Public API =====
    public void PlayLoop()                  { if (loopClip)  PlayLoop(loopClip); }
    public void PlayIntro()                 { if (introClip) StartCoroutine(PlayOnce(introClip)); }
    public void PlayOutro(System.Action cb = null) { if (outroClip) StartCoroutine(PlayOnce(outroClip, cb)); }

    public IEnumerator PlayIntroThenLoop()
    {
        if (introClip) yield return PlayOnce(introClip);
        if (loopClip)  PlayLoop(loopClip);
    }

    // ===== Internal =====
    IEnumerator PlayOnce(AnimationClip clip, System.Action onEnd = null)
    {
        if (!clip) yield break;
        Ensure();

        if (_current.IsValid()) _current.Destroy();
        _current = AnimationClipPlayable.Create(_graph, clip);
        _current.SetApplyFootIK(false);
        _output.SetSourcePlayable(_current);
        _graph.Play();

        double dur = clip.length;
        double t0  = Time.realtimeSinceStartupAsDouble;
        const double TIMEOUT = 10.0;
        while (_current.IsValid() && _current.GetTime() < dur)
        {
            if (!_graph.IsPlaying()) _graph.Play();
            if (Time.realtimeSinceStartupAsDouble - t0 > TIMEOUT) break;
            yield return null;
        }
        onEnd?.Invoke();
    }

    void PlayLoop(AnimationClip clip)
    {
        if (!clip) return;
        Ensure();

        if (_current.IsValid()) _current.Destroy();
        _current = AnimationClipPlayable.Create(_graph, clip);
        _current.SetApplyFootIK(false);
        _output.SetSourcePlayable(_current);
        _graph.Play(); // 루프 여부는 Import의 Loop Time 설정에 따름
    }

    void Ensure()
    {
        if (_ready) return;
        _graph  = PlayableGraph.Create($"SceneAnimGraph:{name}");
        _output = AnimationPlayableOutput.Create(_graph, "Anim", _anim);
        _ready  = true;
    }
}
