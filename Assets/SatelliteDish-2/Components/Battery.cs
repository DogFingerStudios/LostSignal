using System;
using UnityEngine;

[Serializable]  
public class Battery : NodeComponent
{
    public float CapacityWatts = 2000.0f;
    public float CurrentChargeWatts = 2000.0f;
    public float Voltage = 24.0f;

    public int CycleCount = 0;

    public bool IsConnected = true;
}
