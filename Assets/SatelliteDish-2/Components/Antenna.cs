using System;
using UnityEngine;

[Serializable]
public class Antenna : NodeComponent
{
    public Vector3 Orientation; // current pointing direction
    public Vector3 TargetOrientation; // intended pointing direction
    public float GainDb; // antenna gain in decibels
    public float MountingHealth = 1.0f; // condition of mounting hardware (0–1)
    public float HeightMeters; // height above ground in meters
    public bool IsConnected = true; // whether the antenna is connected
}
