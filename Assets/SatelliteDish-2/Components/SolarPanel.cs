using UnityEngine;

public class SolarPanel : NodeComponent
{
    public float SurfaceVisible = 1.0f;
    public float RatedOutputWatts = 300.0f;
    public float CurrentOutputWatts = 0.0f;
    public bool IsConnected = true;
}
