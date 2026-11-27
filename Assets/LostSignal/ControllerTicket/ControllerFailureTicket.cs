using System;

namespace LostSignal.FieldService
{
    internal sealed class ControllerFailureTicket
    {
        internal enum Status
        {
            Assigned,
            Rebooting,
            ReadyToClose,
            Closed
        }

        private static readonly (float Time, string Message)[] _bootSequence =
        {
            (0.6f, "[BOOT] Controller reset"),
            (1.4f, "[BOOT] Power and memory checks passed"),
            (2.2f, "[BOOT] Station configuration loaded"),
            (3.0f, "[BOOT] Radio service online"),
            (3.8f, "[LINK] Heartbeat acknowledged (1/3)"),
            (4.5f, "[LINK] Heartbeat acknowledged (2/3)"),
            (5.2f, "[LINK] Heartbeat acknowledged (3/3). Link stable."),
        };

        private float _bootElapsed;
        private int _nextBootMessage;

        internal Status State { get; private set; } = Status.Assigned;

        internal string RunCommand(string command)
        {
            switch (command.Trim().ToLowerInvariant())
            {
                case "system":
                case "/system":
                    return GetSystemReport();
                case "reboot":
                case "/reboot":
                    return StartReboot();
                case "help":
                case "/help":
                    return "system   Show controller diagnostics\n" +
                           "/reboot  Restart the station controller";
                default:
                    return "Unknown command. Type /help for available commands.";
            }
        }

        internal void Advance(float elapsedSeconds, Action<string> writeLine)
        {
            if (State != Status.Rebooting)
            {
                return;
            }

            _bootElapsed += elapsedSeconds;

            while (_nextBootMessage < _bootSequence.Length && _bootSequence[_nextBootMessage].Time <= _bootElapsed)
            {
                writeLine(_bootSequence[_nextBootMessage].Message);
                _nextBootMessage++;
            }

            if (_nextBootMessage == _bootSequence.Length)
            {
                State = Status.ReadyToClose;
            }
        }

        internal string Close()
        {
            switch (State)
            {
                case Status.ReadyToClose:
                    State = Status.Closed;
                    return "Ticket closed. Dispatch confirmed the link is stable.";
                case Status.Closed:
                    return "This ticket is already closed.";
                case Status.Rebooting:
                    return "Waiting for dispatch to verify the link. Ticket stays open.";
                default:
                    return "Dispatch is still missing check-ins. Ticket stays open.";
            }
        }

        private string GetSystemReport()
        {
            switch (State)
            {
                case Status.Assigned:
                    return "Power supply       STABLE\n" +
                           "Service interface  RESPONDING\n" +
                           "Controller task    STALLED\n" +
                           "Last fault         Telemetry watchdog timeout\n" +
                           "Suggested action   /reboot";
                case Status.Rebooting:
                    return "Controller restarting. Waiting for dispatch acknowledgements.";
                default:
                    return "Power supply       STABLE\n" +
                           "Controller task    RUNNING\n" +
                           "Dispatch link      VERIFIED (3/3 heartbeats)";
            }
        }

        private string StartReboot()
        {
            switch (State)
            {
                case Status.Assigned:
                    _bootElapsed = 0f;
                    _nextBootMessage = 0;
                    State = Status.Rebooting;
                    return "[SERVICE] Reset requested. Restarting station controller...";
                case Status.Rebooting:
                    return "Controller restart already in progress.";
                default:
                    return "Controller is running and the link is verified.";
            }
        }
    }
}
