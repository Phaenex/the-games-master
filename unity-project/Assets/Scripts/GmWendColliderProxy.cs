using UnityEngine;

[DisallowMultipleComponent]
public sealed class GmWendColliderProxy : MonoBehaviour
{
    [SerializeField] string sourcePath;
    [SerializeField] Vector3 sourceWorldCenter;
    [SerializeField] Vector3 sourceWorldSize;

    public string SourcePath => sourcePath;
    public Vector3 SourceWorldCenter => sourceWorldCenter;
    public Vector3 SourceWorldSize => sourceWorldSize;

    public void Configure(string path, Vector3 center, Vector3 size)
    {
        sourcePath = path;
        sourceWorldCenter = center;
        sourceWorldSize = size;
    }
}
