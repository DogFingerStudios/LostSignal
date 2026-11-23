using System;

[Serializable]
public class NodeSignal
{
    public Node Node;                   // reference to the transmitting node
    public double FrequencyHz;         // transmission frequency in hertz
    public float PowerDbm;             // transmitted signal power in dbm
    public double TransmitTimeMs;      // actual transmission time
}