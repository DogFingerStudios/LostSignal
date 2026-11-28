using UnityEngine;

public class SolarPanel : NodeComponent
{
    public float SurfaceVisible = 1.0f;
    public float RatedOutputWatts = 300.0f;
    public float CurrentOutputWatts = 0.0f;
    public bool IsConnected = true;

    void Tick()
    {
        if (IsConnected)
        {
            // Simulate solar panel output based on surface visibility
            CurrentOutputWatts = SurfaceVisible * RatedOutputWatts;
        }
        else
        {
            CurrentOutputWatts = 0.0f;
        }
    }
}
