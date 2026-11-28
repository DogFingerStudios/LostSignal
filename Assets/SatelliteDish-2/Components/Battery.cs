using System;
using UnityEngine;

[Serializable]  
public class Battery : NodeComponent
{
    public float CapacityWattHours = 2000.0f;
    public float CurrentChargeWattHours = 2000.0f;
    public float Voltage = 24.0f;

    public int CycleCount = 0;

    public bool IsConnected = true;

    void Tick()
    {
        if (IsConnected)
        {
            // Simulate battery discharge over time
            CurrentChargeWattHours -= Time.deltaTime * 10.0f; // Example discharge rate
            if (CurrentChargeWattHours < 0)
            {
                CurrentChargeWattHours = 0;
            }
        }
    }
}
