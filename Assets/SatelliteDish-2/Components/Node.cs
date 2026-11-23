using System;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;


public class Node : MonoBehaviour
{
    [SerializeField] 
    private string _nodeName;
    public string NodeName
    {
        get => _nodeName;
        set => _nodeName = value;
    }

    [SerializeField]
    private float _range;
    public float Range => _range;

    SolarPanel[] _solarPanels;
    Battery[] _batteries;
    RFCable[] _cables;
    Antenna _antenna;
    TimingOscillator _timingOscillator;
    NodeController _nodeController;
    //RadioTransmitter _transmitter

    private float _timer;
    private const float Interval = 0.5f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        _timer += Time.deltaTime;

        if (_timer >= Interval)
        {
            EmitSignal();
            _timer -= Interval; // Subtracts the interval instead of resetting to 0 to prevent timing drift
        }
    }

    private void EmitSignal()
    {
        //float powerDbm = 10f * Mathf.Log10(
        //    _transmitter.CurrentPowerWatts * 1000f);

        //foreach (RFCable cable in _cables)
        //{
        //    powerDbm -= cable.SignalLossDb;
        //}


        //NodeSignal signal = new NodeSignal { 
        //    Node = this,
        //    FrequencyHz = _transmitter.FrequencyHz,
        //    PowerDbm = powerDbm,
        //    TransmitTimeMs = 0
        //};
        //Atmosphere.Instance.Broadcast(signal);
    }
}
