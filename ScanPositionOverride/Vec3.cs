namespace ScanPositionOverride;

internal sealed class Vec3
{
    public float x { get; set; }
    public float y { get; set; }
    public float z { get; set; }
    public Vector3 ToVector3()
    {
        return new Vector3(x, y, z);
    }
    public Quaternion ToQuaternion()
    {
        return Quaternion.Euler(x, y, z);
    }
}
