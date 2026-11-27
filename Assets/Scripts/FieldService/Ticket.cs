using System;
using UnityEngine;
using LostSignal.FieldService;

namespace KeepDriving
{

class Ticket
{
    public readonly string Node, Subject;
    public bool Unread = true, Pinned, Arrived, Checked, Verified, Closed;
    public int Repairs;
    public string Log = "Local service interface connected.\n";
    public Ticket(string node, string subject) { Node = node; Subject = subject; }
    public void Write(string line) => Log += line + "\n";
}

}