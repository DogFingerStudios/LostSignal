using System;

[Serializable]
public class RFCable : NodeComponent
{
    public float LengthMeters = 1.0f;
    public float SignalLossDb = 0.0f; // signal attenuation in decibels
    public float WaterIngress = 0.0f; // moisture contamination, 0-1
    public float ConnectorHealth = 1.0f; // health of the connectors, 0-1
    public bool IsConnected = true;
}
