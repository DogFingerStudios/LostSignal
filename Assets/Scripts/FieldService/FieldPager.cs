using System;
using UnityEngine;
using LostSignal.FieldService;
using TicketStatus = LostSignal.FieldService.ControllerFailureTicket.Status;

namespace KeepDriving
{
    internal sealed class FieldPager
    {
        private readonly Ticket[] tickets =
        {
            new("HC-017", "Missed check-ins"),
            new("HC-042", "Low charging output"),
            new("HC-063", "Weak transmission"),
            new("HC-062", "Bad cat"),
            new("HC-163", "Dog will not pee"),
            new("HC-666", "Girlfriend is mad"),
            new("HC-65060", "Probably breaking up")
        };

        private readonly ControllerFailureTicket controller = new();
        private static readonly Color Ink = new(.10f, .15f, .08f);
        private static readonly Color Paper = new(.90f, .87f, .78f);
        private static readonly Color Amber = new(1f, .75f, .37f);
        private GUIStyle heading, text, lcdText, lcdHeading, lcdButton, action;
        private int current = -1, selection;
        private static readonly Rect InboxViewport = new(35f, 137f, 283f, 220f);
        private const float InboxRowHeight = 66f;
        private Vector2 inboxScroll, scroll;
        public bool IsOpen { get; private set; }

        public void Open() => IsOpen = true;
        public void Close() => IsOpen = false;

        public void Advance(float elapsed)
        {
            var previous = controller.State;
            controller.Advance(elapsed, tickets[0].Write);
            if (previous != TicketStatus.ReadyToClose && controller.State == TicketStatus.ReadyToClose)
                Verify(tickets[0]);
        }

        private static void Verify(Ticket ticket)
        {
            ticket.Verified = true;
            ticket.Unread = true;
        }

        public void Draw()
        {
            if (heading == null) InitStyles();
            const float width = 810f, height = 468f;
            float scale = Mathf.Max(.01f, Mathf.Min((Screen.width - 24f) / width, (Screen.height - 24f) / height));
            Matrix4x4 old = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(.07f, .07f, .05f, .82f));
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - width * scale) * .5f,
                (Screen.height - height * scale) * .5f, 0f), Quaternion.identity, Vector3.one * scale);
            GUI.Label(new Rect(0, 0, 500, 35), "Field pager", heading);
            GUI.Label(new Rect(650, 8, 160, 24), "P / Esc   Close", text);
            DrawPager();
            DrawFieldPanel();
            GUI.matrix = old;
        }

        private void DrawPager()
        {
            Fill(new Rect(0, 40, 352, 416), new Color(.10f, .10f, .08f), 34);
            Fill(new Rect(1, 41, 350, 408), new Color(.26f, .25f, .21f), 33);
            GUI.Label(new Rect(22, 55, 270, 22), "H O R I Z O N   C H A I N", text);
            int unread = 0;
            foreach (var message in tickets)
                if (message.Unread) unread++;
            Fill(new Rect(312, 59, 11, 11), unread > 0 ? Amber : new Color(.35f, .29f, .16f), 6);
            Fill(new Rect(17, 82, 318, 290), new Color(.08f, .10f, .07f), 17);
            Fill(new Rect(24, 89, 304, 276), new Color(.65f, .74f, .46f), 12);
            for (int y = 101; y < 354; y += 4)
                Fill(new Rect(30, y, 292, 1), new Color(.12f, .22f, .06f, .07f));

            bool inbox = current < 0;
            Ticket ticket = inbox ? null : tickets[current];
            string status = inbox ? (unread > 0 ? $"{unread} NEW" : $"{tickets.Length} MESSAGES") : Status(ticket);
            GUI.Label(new Rect(38, 99, 278, 25), $"{(inbox ? "DISPATCH" : $"FS-{current + 1:000}")}    {status}", lcdText);
            Fill(new Rect(38, 125, 276, 1), Ink);
            (string Label, Action Activate)[] rows;
            if (inbox)
            {
                rows = new (string, Action)[tickets.Length];
                for (int i = 0; i < tickets.Length; i++)
                {
                    int index = i;
                    var item = tickets[i];
                    rows[i] = ($"{(item.Unread ? "• " : "")}{item.Node}\n{(item.Closed ? "Ticket closed" : item.Verified ? "Service restored" : item.Subject)}",
                        () => { current = index; item.Unread = false; selection = 0; scroll = Vector2.zero; });
                }
                float contentHeight = rows.Length * InboxRowHeight;
                float rowWidth = InboxViewport.width;
                if (contentHeight > InboxViewport.height)
                {
                    var scrollbar = GUI.skin.verticalScrollbar;
                    rowWidth -= scrollbar.fixedWidth + scrollbar.margin.left + scrollbar.margin.right;
                }
                inboxScroll = GUI.BeginScrollView(InboxViewport, inboxScroll,
                    new Rect(0f, 0f, rowWidth, contentHeight), false, false);
                DrawRows(rows, 0f, 0f, rowWidth, InboxRowHeight);
                GUI.EndScrollView();
            }
            else
            {
                GUI.Label(new Rect(38, 136, 270, 40), ticket.Node, lcdHeading);
                GUI.Label(new Rect(38, 183, 267, 46), ticket.Subject, lcdText);
                Fill(new Rect(38, 232, 276, 1), Ink);
                var back = ("Back to inbox", (Action)(() => { selection = current; current = -1; }));
                var pin = (ticket.Pinned ? "Remove map marker" : "Mark on map", (Action)(() => ticket.Pinned = !ticket.Pinned));
                rows = ticket.Closed ? new[] { back } : ticket.Verified
                    ? new[] { ("Close ticket", (Action)(() => CloseTicket(ticket))), pin, back }
                    : new[] { pin, back };
                DrawRows(rows, 35f, 240f, 283f, 37f);
            }

            if (GUI.Button(new Rect(23, 388, 96, 43), "▲", action)) MoveSelection(-1, rows.Length, inbox);
            if (GUI.Button(new Rect(128, 388, 96, 43), "●", action) && rows.Length > 0) rows[Mathf.Clamp(selection, 0, rows.Length - 1)].Activate();
            if (GUI.Button(new Rect(233, 388, 96, 43), "▼", action)) MoveSelection(1, rows.Length, inbox);
        }

        private void MoveSelection(int delta, int rowCount, bool inbox)
        {
            if (rowCount == 0) return;
            selection = (selection + delta + rowCount) % rowCount;
            if (!inbox) return;

            float top = selection * InboxRowHeight;
            float bottom = top + InboxRowHeight;
            if (top < inboxScroll.y) inboxScroll.y = top;
            else if (bottom > inboxScroll.y + InboxViewport.height) inboxScroll.y = bottom - InboxViewport.height;
        }

        private void DrawRows((string Label, Action Activate)[] rows, float left, float top, float width, float height)
        {
            for (int i = 0; i < rows.Length; i++)
            {
                var rect = new Rect(left, top + i * height, width, height - 4);
                if (selection == i) Fill(rect, new Color(.43f, .55f, .29f), 4);
                if (GUI.Button(rect, (selection == i ? "› " : "  ") + rows[i].Label, lcdButton))
                    rows[i].Activate();
            }
        }

        private string Status(Ticket ticket) => ticket.Closed ? "CLOSED" : ticket.Verified ? "READY TO CLOSE"
            : current == 0 && controller.State == TicketStatus.Rebooting ? "VERIFYING" : "ASSIGNED";

        private void CloseTicket(Ticket ticket)
        {
            if (current == 0) ticket.Write(controller.Close());
            ticket.Closed = true;
            ticket.Unread = false;
            ticket.Pinned = false;
            selection = 0;
        }

        private void DrawFieldPanel()
        {
            Fill(new Rect(371, 40, 439, 416), new Color(.17f, .15f, .12f), 12);
            GUI.Label(new Rect(387, 53, 402, 27), "Field map", heading);
            DrawMap();
            if (current < 0)
            {
                GUI.Label(new Rect(387, 191, 400, 45), "Open a Dispatch message to see the assignment.", text);
                return;
            }

            Ticket ticket = tickets[current];
            GUI.Label(new Rect(387, 185, 406, 40), ticket.Closed ? "Dispatch acknowledged the closure." :
                ticket.Verified ? "Service restored. Close the ticket on the pager." :
                ticket.Arrived ? $"You’re at {ticket.Node}." : "Travel to the site to begin the repair.", text);
            if (!ticket.Arrived)
            {
                if (GUI.Button(new Rect(388, 228, 265, 35), $"Simulate driving to {ticket.Node}", action))
                    ticket.Arrived = true;
                return;
            }

            GUI.Label(new Rect(387, 222, 410, 29), $"{ticket.Node} service terminal", heading);
            DrawServiceActions(ticket);
            Fill(new Rect(386, 295, 407, 143), new Color(.08f, .11f, .08f), 6);
            GUIStyle logStyle = new(text) { normal = { textColor = new Color(.72f, .81f, .54f) }, fontSize = 13 };
            var content = new GUIContent(ticket.Log);
            float logHeight = Mathf.Max(130f, logStyle.CalcHeight(content, 372f));
            scroll = GUI.BeginScrollView(new Rect(393, 301, 393, 130), scroll, new Rect(0, 0, 372, logHeight + 2));
            GUI.Label(new Rect(0, 0, 372, logHeight), content, logStyle);
            GUI.EndScrollView();
        }

        private void DrawMap()
        {
            Fill(new Rect(387, 86, 406, 92), new Color(.57f, .55f, .39f), 7);
            Matrix4x4 old = GUI.matrix;
            GUIUtility.RotateAroundPivot(-9f, new Vector2(590, 132));
            Fill(new Rect(412, 130, 355, 4), new Color(.86f, .76f, .53f));
            GUI.matrix = old;
            Ticket ticket = current < 0 ? null : tickets[current];
            if (ticket?.Pinned == true)
            {
                Fill(new Rect(710, 97, 65, 20), Amber, 4);
                GUI.Label(new Rect(714, 97, 62, 20), ticket.Node, lcdText);
            }
            float x = ticket?.Arrived == true ? 716f : 411f;
            Fill(new Rect(x, 140, 46, 21), Ink, 4);
            GUI.Label(new Rect(x + 7, 140, 42, 21), "YOU", text);
        }

        private void DrawServiceActions(Ticket ticket)
        {
            bool pending = current == 0 && controller.State == TicketStatus.Rebooting;
            string diagnostic = current == 0 ? "system" : current == 1 ? "Inspect panels" : "Test antenna line";
            if (ServiceButton(388, 127, diagnostic, !pending))
            {
                ticket.Checked = true;
                ticket.Write(current == 0 ? "> system\n" + controller.RunCommand("system")
                    : current == 1 ? "> Inspect panels\n" + (ticket.Repairs == 2 ? "Both panel rows are clear." : "Dust is limiting the solar panels’ output.")
                    : "> Test antenna line\n" + (ticket.Repairs == 2 ? "Cable replaced and connection sealed." : "Water damage detected at the antenna cable."));
                scroll.y = float.MaxValue;
            }

            bool canRepair = ticket.Checked && !pending && !ticket.Verified && !ticket.Closed;
            if (current == 0)
            {
                if (ServiceButton(521, 120, "/reboot", canRepair))
                {
                    ticket.Write("> /reboot\n" + controller.RunCommand("/reboot"));
                    scroll.y = float.MaxValue;
                }
                return;
            }

            string repair = current == 1 ? (ticket.Repairs == 0 ? "Brush row 1" : "Brush row 2")
                : (ticket.Repairs == 0 ? "Replace cable" : "Reseal joint");
            if (ServiceButton(521, 115, repair, canRepair && ticket.Repairs < 2))
            {
                ticket.Repairs++;
                ticket.Write($"> {repair}\nDone.");
                scroll.y = float.MaxValue;
            }
            if (ServiceButton(642, 148, current == 1 ? "Verify charging" : "Retest reception",
                canRepair && ticket.Repairs == 2))
            {
                ticket.Write(current == 1 ? "[POWER] Battery charging normally." : "[LINK] Reception restored.");
                Verify(ticket);
                scroll.y = float.MaxValue;
            }
        }

        private bool ServiceButton(float x, float width, string label, bool enabled)
        {
            bool old = GUI.enabled;
            GUI.enabled = old && enabled;
            bool pressed = GUI.Button(new Rect(x, 255, width, 30), label, action);
            GUI.enabled = old;
            return pressed;
        }

        private void InitStyles()
        {
            text = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true, normal = { textColor = Paper } };
            heading = new GUIStyle(text) { fontSize = 22, fontStyle = FontStyle.Bold };
            lcdText = new GUIStyle(text) { fontStyle = FontStyle.Bold, normal = { textColor = Ink } };
            lcdHeading = new GUIStyle(lcdText) { fontSize = 31 };
            lcdButton = new GUIStyle(lcdText) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(8, 8, 0, 0) };
            lcdButton.hover.textColor = Ink;
            lcdButton.active.textColor = Ink;
            action = new GUIStyle(GUI.skin.button) { fontSize = 14 };
        }

        private static void Fill(Rect rect, Color color, float radius = 0f) =>
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, radius);
    }
}
