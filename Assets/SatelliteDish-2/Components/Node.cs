using System;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;


public class Node : MonoBehaviour
{
    [SerializeField] 
    public string NodeName;

    [SerializeField]
    private int _batteryCount = 1;
    public int BatteryCount => _batteryCount;

    [SerializeField]
    private float _range = 100f;
    public float Range => _range;

    private SolarPanel[] _solarPanels;
    public SolarPanel[] SolarPanels => _solarPanels;
    
    private Battery[] _batteries;
    public Battery[] Batteries => _batteries;
    
    private RFCable[] _cables;
    public RFCable[] Cables => _cables;

    private Antenna _antenna;
    public Antenna Antenna => _antenna;

    private TimingOscillator _timingOscillator;
    public TimingOscillator TimingOscillator => _timingOscillator;

    private NodeController _nodeController;
    public NodeController NodeController => _nodeController;

    // private RadioTransmitter _transmitter;
    // public RadioTransmitter Transmitter => _transmitter;

    private float _timer;
    private const float Interval = 0.5f; // Interval for emitting signals in seconds

    // the condition of the node, representing its health or operational status
    // Value ranges from 0 (completely non-functional) to 1 (fully operational)
    private float _condition;
    public float Condition => _condition;

    public void Init()
    {
        _batteries = new Battery[_batteryCount];

        for (int i = 0; i < _batteries.Length; i++)
        {
            _batteries[i] = new Battery();
        }

        // NodeManager.Instance.RegisterNode(this);
    }

    void Start()
    {
        Init();
    }

    void OnDestroy()
    {
        NodeManager.Instance.UnregisterNode(this);
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

    float TotalBatteryCapacityPercent()
    {
        if (_batteries == null || _batteries.Length == 0)
        {
            return 0.0f;
        }

        float totalCapacity = 0.0f;
        float totalCharge = 0.0f;
        foreach (var battery in _batteries)
        {
            totalCapacity += battery.CapacityWattHours;
            totalCharge += battery.CurrentChargeWattHours;
        }

        return totalCapacity > 0 ? (totalCharge / totalCapacity) * 100.0f : 0.0f;
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
