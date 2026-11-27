using UnityEngine;
using UnityEngine.UIElements;

namespace KeepDriving
{
    internal sealed class FieldReceiverUI
    {
        enum View { Waveform, Strength, Timing }

        // Received power and display metadata belong to the acquisition, not the transmitted packet.
        readonly struct Acquisition
        {
            public readonly NodeSignal Packet;
            public readonly string Label, Detail;
            public readonly float ReceivedDbm;

            public Acquisition(string label, string detail, float receivedDbm, double frequencyHz, double transmitTimeMs)
            {
                Packet = new NodeSignal { FrequencyHz = frequencyHz, TransmitTimeMs = transmitTimeMs, PowerDbm = 40 };
                Label = label;
                Detail = detail;
                ReceivedDbm = receivedDbm;
            }
        }

        static readonly Color Ink = new(.82f, .91f, .85f);
        static readonly Color Muted = new(.62f, .73f, .67f);
        static readonly Color Green = new(.62f, .94f, .73f);
        static readonly Color Screen = new(.06f, .12f, .10f);
        static readonly Color Edge = new(.29f, .40f, .35f);
        static readonly Color Body = new(.09f, .16f, .14f);

        readonly PanelSettings settings;
        readonly GameObject host;
        readonly VisualElement overlay, plot;
        readonly ScrollView list;
        readonly Label reading, scale, signalHeading;
        readonly Button[] tabs = new Button[3];
        Acquisition[] signals;
        Button[] signalButtons;
        int selected;
        View view;

        public bool IsOpen { get; private set; }

        public FieldReceiverUI(Transform parent)
        {
            settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(960, 600);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = .5f;
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("FieldReceiverTheme");

            host = new GameObject("HCA Field Receiver");
            host.SetActive(false);
            host.transform.SetParent(parent, false);
            var document = host.AddComponent<UIDocument>();
            document.panelSettings = settings;
            host.SetActive(true);
            overlay = document.rootVisualElement;
            overlay.style.flexGrow = 1;
            overlay.style.alignItems = Align.Center;
            overlay.style.justifyContent = Justify.Center;
            overlay.style.backgroundColor = new Color(0, 0, 0, .65f);
            overlay.style.display = DisplayStyle.None;

            var instrument = new ScrollView(ScrollViewMode.Vertical);
            instrument.style.width = Length.Percent(94);
            instrument.style.maxWidth = 760;
            instrument.style.maxHeight = Length.Percent(94);
            Frame(instrument, Body, 16);
            overlay.Add(instrument);

            var header = Row(instrument);
            var title = new VisualElement();
            header.Add(title);
            AddLabel(title, "HCA FIELD RECEIVER", 23, Ink, true);
            AddLabel(title, "MODEL R-8  |  HORIZON CHAIN AUTHORITY", 11, Muted);
            AddLabel(header, "ONLINE", 12, Green, true);

            var fix = new VisualElement();
            Frame(fix, Screen, 10);
            fix.style.marginTop = 12;
            fix.style.marginBottom = 14;
            instrument.Add(fix);
            var fixHeader = Row(fix);
            AddLabel(fixHeader, "CURRENT POSITION", 13, Ink, true);
            AddLabel(fixHeader, "VALID FIX", 12, Green, true);
            var coordinates = Row(fix);
            AddMetric(coordinates, "EASTING", "128.417 km", 25);
            AddMetric(coordinates, "NORTHING", "064.832 km", 25);
            var metrics = Row(fix);
            AddMetric(metrics, "EST. ERROR", "±0.7 mi", 14);
            AddMetric(metrics, "REFERENCES", "3 nodes", 14);
            AddMetric(metrics, "FIX QUALITY", "GOOD", 14);

            var diagnostics = Row(instrument);
            var acquisitions = new VisualElement { style = { flexGrow = 1, flexBasis = 260, marginRight = 12 } };
            diagnostics.Add(acquisitions);
            signalHeading = AddLabel(acquisitions, "", 13, Ink, true);
            AddLabel(acquisitions, "SORTED BY SIGNAL STRENGTH", 11, Muted);
            list = new ScrollView(ScrollViewMode.Vertical);
            list.style.height = 174;
            list.style.marginTop = 5;
            acquisitions.Add(list);

            var analyzer = new VisualElement { style = { flexGrow = 1, flexBasis = 310 } };
            diagnostics.Add(analyzer);
            AddLabel(analyzer, "SIGNAL ANALYZER", 13, Ink, true);
            var tabRow = Row(analyzer);
            tabRow.style.justifyContent = Justify.FlexStart;
            for (int index = 0; index < tabs.Length; index++)
            {
                var targetView = (View)index;
                tabs[index] = AddButton(tabRow, targetView.ToString(), () => { view = targetView; RefreshAnalyzer(); });
                tabs[index].style.marginRight = 4;
            }
            reading = AddLabel(analyzer, "", 12, Ink);
            plot = new VisualElement();
            plot.style.height = 126;
            Frame(plot, Screen, 0);
            plot.generateVisualContent += DrawPlot;
            analyzer.Add(plot);
            scale = AddLabel(analyzer, "", 11, Muted);

            var footer = Row(instrument);
            footer.style.marginTop = 12;
            AddLabel(footer, "HCA / FIELD SYSTEMS / R8", 11, Muted);
            AddLabel(footer, "I / ESC  CLOSE", 11, Muted);
        }

        public void Open()
        {
            signals = CreateStubAcquisitions();
            System.Array.Sort(signals, (a, b) => b.ReceivedDbm.CompareTo(a.ReceivedDbm));
            signalHeading.text = $"ACQUIRED SIGNALS ({signals.Length})";
            list.Clear();
            signalButtons = new Button[signals.Length];
            for (int index = 0; index < signals.Length; index++)
            {
                int target = index;
                var signal = signals[index];
                var button = AddButton(list, "", () => { selected = target; RefreshAnalyzer(); });
                button.style.flexDirection = FlexDirection.Row;
                button.style.justifyContent = Justify.SpaceBetween;
                button.style.alignItems = Align.Center;
                button.style.minHeight = 36;
                var name = AddLabel(button, signal.Label, 12, Ink);
                name.style.flexShrink = 1;
                AddLabel(button, $"{signal.ReceivedDbm:0} dBm", 11, Muted).style.flexShrink = 0;
                signalButtons[index] = button;
            }
            selected = 0;
            view = View.Waveform;
            list.scrollOffset = Vector2.zero;
            IsOpen = true;
            overlay.style.display = DisplayStyle.Flex;
            RefreshAnalyzer();
        }

        public void Close()
        {
            IsOpen = false;
            overlay.style.display = DisplayStyle.None;
        }

        public void Dispose()
        {
            Object.Destroy(host);
            Object.Destroy(settings);
        }

        static Acquisition[] CreateStubAcquisitions() => new[]
        {
            new Acquisition("HC-017", "Fix reference", -43, 86.2e3, 1),
            new Acquisition("HC-009", "Fix reference", -52, 89.4e3, 4),
            new Acquisition("Unregistered pulse detected", "Unknown source / phase stable", -58, 92.7e3, 7),
            new Acquisition("HC-024", "Fix reference", -61, 96.1e3, 10),
            new Acquisition("HC-031", "Acquired", -73, 99.5e3, 13),
            new Acquisition("HC-046", "Acquired", -86, 103.0e3, 16)
        };

        void RefreshAnalyzer()
        {
            for (int index = 0; index < signalButtons.Length; index++)
                MarkSelected(signalButtons[index], index == selected);
            for (int index = 0; index < tabs.Length; index++)
                MarkSelected(tabs[index], index == (int)view);
            var signal = signals[selected];
            reading.text = $"{signal.Label}  |  {signal.Detail}";
            scale.text = view switch
            {
                View.Waveform => "RELATIVE AMPLITUDE  |  0–20 ms",
                View.Strength => "−95 TO −35 dBm  |  0–30 s",
                _ => "PULSE STATE  |  0–100 ms"
            };
            plot.MarkDirtyRepaint();
        }

        void DrawPlot(MeshGenerationContext context)
        {
            var bounds = plot.contentRect;
            if (!IsOpen || bounds.width <= 0 || bounds.height <= 0) return;
            var painter = context.painter2D;
            painter.strokeColor = Edge;
            painter.lineWidth = 1;
            painter.BeginPath();
            for (int column = 1; column < 8; column++)
            {
                float x = bounds.xMin + bounds.width * column / 8;
                painter.MoveTo(new Vector2(x, bounds.yMin));
                painter.LineTo(new Vector2(x, bounds.yMax));
            }
            for (int row = 1; row < 4; row++)
            {
                float y = bounds.yMin + bounds.height * row / 4;
                painter.MoveTo(new Vector2(bounds.xMin, y));
                painter.LineTo(new Vector2(bounds.xMax, y));
            }
            painter.Stroke();

            painter.strokeColor = Green;
            painter.lineWidth = 1.6f;
            painter.BeginPath();
            float previousY = SampleY(0);
            painter.MoveTo(new Vector2(bounds.xMin, bounds.yMin + bounds.height * previousY));
            const int sampleCount = 160;
            for (int step = 1; step <= sampleCount; step++)
            {
                float time = step / (float)sampleCount;
                float x = Mathf.Lerp(bounds.xMin, bounds.xMax, time);
                float y = SampleY(time);
                if (view == View.Timing && previousY != y)
                    painter.LineTo(new Vector2(x, bounds.yMin + bounds.height * previousY));
                painter.LineTo(new Vector2(x, bounds.yMin + bounds.height * y));
                previousY = y;
            }
            painter.Stroke();
        }

        // Synthetic baseband trace for display. Raw carrier samples would need a higher sample rate.
        float SampleY(float time)
        {
            var signal = signals[selected];
            float frequency = (float)(signal.Packet.FrequencyHz / 1000);
            float phase = (float)signal.Packet.TransmitTimeMs;
            return view switch
            {
                View.Waveform => .5f - Mathf.InverseLerp(-95, -35, signal.ReceivedDbm) * .43f
                    * Mathf.Sin(time * frequency * .85f + phase) * (.72f + .28f * Mathf.Cos(time * 13)),
                View.Strength => 1 - Mathf.InverseLerp(-95, -35,
                    signal.ReceivedDbm + 3 * Mathf.Sin(time * 23 + phase) + 1.5f * Mathf.Cos(time * 59)),
                _ => Mathf.Repeat(time * 100 - phase, 18 + selected * 2) < 3.5f ? .21f : .79f
            };
        }

        static void MarkSelected(Button button, bool active)
        {
            button.style.backgroundColor = active ? new Color(.18f, .33f, .25f) : Screen;
            button.style.color = active ? Green : Ink;
            button.style.borderBottomColor = active ? Green : Edge;
        }

        static Button AddButton(VisualElement parent, string text, System.Action action)
        {
            var button = new Button(action) { text = text };
            button.style.fontSize = 12;
            button.style.minHeight = 28;
            parent.Add(button);
            return button;
        }

        static VisualElement Row(VisualElement parent)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.justifyContent = Justify.SpaceBetween;
            parent.Add(row);
            return row;
        }

        static void AddMetric(VisualElement row, string caption, string value, int size)
        {
            var metric = new VisualElement();
            metric.style.marginRight = 12;
            row.Add(metric);
            AddLabel(metric, caption, 11, Muted);
            AddLabel(metric, value, size, Ink, true);
        }

        static Label AddLabel(VisualElement parent, string text, int size, Color color, bool bold = false)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.style.fontSize = size;
            label.style.color = color;
            label.style.whiteSpace = WhiteSpace.Normal;
            if (bold) label.style.unityFontStyleAndWeight = FontStyle.Bold;
            parent.Add(label);
            return label;
        }

        static void Frame(VisualElement element, Color background, float padding)
        {
            var style = element.style;
            style.backgroundColor = background;
            style.paddingTop = style.paddingBottom = style.paddingLeft = style.paddingRight = padding;
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 1;
            style.borderTopColor = style.borderBottomColor = style.borderLeftColor = style.borderRightColor = Edge;
        }
    }
}
