using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class NodeManager : MonoBehaviour
{
    public static NodeManager Instance { get; private set; } = null!;

    private readonly List<Node> _nodes = new List<Node>();

    public void RegisterNode(Node node)
    {
        if (!_nodes.Contains(node))
        {
            Debug.Log($"Registering node: {node.name}");
            _nodes.Add(node);
        }
    }

    public void UnregisterNode(Node node)
    {
        if (_nodes.Contains(node))
        {
            Debug.Log($"Unregistering node: {node.name}");
            _nodes.Remove(node);
        }
    }

    public void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        Instance = this;
    }
}
