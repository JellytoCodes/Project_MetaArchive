// PinToParent.cs
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PinToParent : MonoBehaviour
{
    public Vector3 localPos = Vector3.zero;
    public Quaternion localRot = Quaternion.identity;
    public Vector3 localScale = Vector3.one;

    void LateUpdate()
    {
        var t = transform;
        t.localPosition = localPos;
        t.localRotation = localRot;
        t.localScale    = localScale;
    }
}