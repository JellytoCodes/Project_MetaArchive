// FaceCamera.cs
using UnityEngine;

public sealed class FaceCamera : MonoBehaviour
{
    [SerializeField] Camera target;
    [SerializeField] bool yawOnly = true;
    [SerializeField] bool continuous = true;

    public void Init(Camera cam, bool yawOnly = true, bool continuous = true)
    {
        target = cam;
        this.yawOnly = yawOnly;
        this.continuous = continuous;
        FaceOnce();
    }

    void LateUpdate()
    {
        if (!continuous) return;
        FaceOnce();
    }

    void FaceOnce()
    {
        if (!target) return;
        Vector3 dir = target.transform.position - transform.position;
        if (yawOnly) dir.y = 0f;
        if (dir.sqrMagnitude < 1e-6f) return;
        transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }
}