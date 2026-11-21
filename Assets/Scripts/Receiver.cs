using UnityEngine;

public class Receiver : MonoBehaviour
{
    public Vector3 Position => transform.position;

    public void OnSignalReceived(NodeSignal signal)
    {
        Debug.Log($"Receiver {gameObject.name} received signal: {signal}");
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
