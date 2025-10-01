using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

public sealed class AnimPlayer : MonoBehaviour
{
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

    void OnDisable() { if (_ready) _graph.Stop(); }
    void OnDestroy() { if (_ready) { _graph.Destroy(); _ready = false; } }

    public void PlayLoop(AnimationClip clip)
    {
        if (!clip) return;
        EnsureGraph();

        if (_current.IsValid()) _current.Destroy();
        _current = AnimationClipPlayable.Create(_graph, clip);
        _current.SetApplyFootIK(false);            // 6.2 OK
        _output.SetSourcePlayable(_current);
        _graph.Play();                              // 루프 여부는 Import 설정(Loop Time=On)
    }

    public IEnumerator PlayOnce(AnimationClip clip, System.Action onEnd = null)
    {
        if (!clip) yield break;
        EnsureGraph();

        if (_current.IsValid()) _current.Destroy();
        _current = AnimationClipPlayable.Create(_graph, clip);
        _current.SetApplyFootIK(false);
        _output.SetSourcePlayable(_current);
        _graph.Play();

        double duration = clip.length;
        double t0 = Time.realtimeSinceStartupAsDouble;
        const double TIMEOUT = 10.0;

        while (_current.IsValid() && _current.GetTime() < duration)
        {
            if (!_graph.IsPlaying()) _graph.Play();
            if (Time.realtimeSinceStartupAsDouble - t0 > TIMEOUT)
            {
                break;
            }
            yield return null;
        }
        onEnd?.Invoke();
    }

    public void Stop()
    {
        if (_ready) _graph.Stop();
    }

    void EnsureGraph()
    {
        if (_ready) return;
        _graph = PlayableGraph.Create($"AnimGraph:{name}");
        _output = AnimationPlayableOutput.Create(_graph, "Anim", _anim);
        _ready = true;
    }
}
