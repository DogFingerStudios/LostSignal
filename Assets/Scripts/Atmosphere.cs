using System;
using UnityEngine;

public class Atmosphere : MonoBehaviour
{
    public static Atmosphere Instance { get; private set; } = null!;

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

    public void Broadcast(NodeSignal signal, Vector3 position, float range)
    {
        Collider[] colliders = Physics.OverlapSphere(position, range);

        //foreach (Collider collider in colliders)
        //{
        //    if (collider.TryGetComponent(out Receiver receiver))
        //    {
        //        // calculate signal attenuation and propagation delay

        //        receiver.OnSignalReceived(signal);
        //    }
        //}
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
