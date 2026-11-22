using System;

[Serializable]
public abstract class NodeComponent
{
    public string Id;
    public float Health = 1.0f;
    public bool IsOperational => Health > 0.0f;

    public DateTime LastServiced;
}
