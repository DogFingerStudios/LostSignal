using System;

public class NodeController : NodeComponent
{
    public bool IsPowered; // Whether controller has power
    public bool IsResponsive; // Whether controller responds to commands
    public string FirmwareVersion; // Installed firmware version
    public bool ConfigurationValid; // Whether configuration is valid
    public float TemperatureC; // Operating temperature
    public DateTime LastRestart; // Last restart
    public int ErrorCode; // Reported diagnostic error
}
