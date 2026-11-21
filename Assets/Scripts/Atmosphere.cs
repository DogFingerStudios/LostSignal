using System;
using System.Collections.Generic;
using UnityEngine;

public class Atmosphere : MonoBehaviour
{
    public static Atmosphere Instance { get; private set; } = null!;

    private readonly List<Receiver> _receivers = new List<Receiver>();

    public void RegisterReceiver(Receiver receiver)
    {
        if (!_receivers.Contains(receiver))
        {
            _receivers.Add(receiver);
        }
    }

    public void UnregisterReceiver(Receiver receiver)
    {
        if (_receivers.Contains(receiver))
        {
            _receivers.Remove(receiver);
        }
    }

    public void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    public void Broadcast(NodeSignal signal)
    {
        foreach (var receiver in _receivers)
        {
            if (Vector3.Distance(signal.Node.transform.position, receiver.Position) <= signal.Node.Range)
            {
                receiver.OnSignalReceived(signal);
            }
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
