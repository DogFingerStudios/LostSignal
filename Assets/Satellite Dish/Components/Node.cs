using UnityEngine;

public class Node : MonoBehaviour
{
    [SerializeField] 
    private string _name;
    public string Name => _name;

    SolarPanel[] _solarPanels;
    Battery[] _batteries;
    RFCable[] _rfCables;
    Antenna _antenna;
    TimingOscillator _timingOscillator;
    NodeController _nodeController;
    RadioTransmitter _radioTransmitter;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
