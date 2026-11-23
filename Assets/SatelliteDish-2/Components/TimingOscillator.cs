using System;
using UnityEngine;
using UnityEngine.UIElements;

public class TimingOscillator : MonoBehaviour
{
    public double NominalFrequencyHz; // Intended oscillator frequency
    public double ActualFrequencyHz; // Current oscillator frequency
    public double TimingOffsetMs; // Current timing error
    public double DriftRatePpm; // Rate of frequency drift
    public float Health; // Component condition
    public float TemperatureC; // Operating temperature
    public DateTime LastCalibration; // Most recent calibration
}
