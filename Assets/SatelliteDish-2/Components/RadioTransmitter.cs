using System;

public class RadioTransmitter : NodeComponent
{
    public double FrequencyHz; // Transmission frequency
    public float RatedPowerWatts; // Maximum output power
    public float CurrentPowerWatts; // Actual output
    public float TemperatureC; // Operating temperature
    public bool IsTransmitting; // Current transmission state
    public bool IsPowered; // Power availability
}
