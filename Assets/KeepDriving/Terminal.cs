using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace KeepDriving
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Keep Driving/Terminal")]
    public sealed class Terminal : MonoBehaviour
    {
        [SerializeField] private string terminalTitle = "Terminal";
        [SerializeField, Min(.1f)] private float interactionRange = 2.5f;
        [SerializeField, Tooltip("Optional reference point for the proximity prompt.")]
        private Transform interactionPoint;

        private static readonly HashSet<Terminal> ActiveTerminals = new();

        internal string Title => string.IsNullOrWhiteSpace(terminalTitle) ? "Terminal" : terminalTitle;

        private void OnEnable() => ActiveTerminals.Add(this);
        private void OnDisable() => ActiveTerminals.Remove(this);

        internal static Terminal FindNearby(Vector3 position)
        {
            Terminal nearest = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (var terminal in ActiveTerminals)
            {
                Vector3 point = terminal.interactionPoint ? terminal.interactionPoint.position : terminal.transform.position;
                float distance = (point - position).sqrMagnitude;
                if (distance > terminal.interactionRange * terminal.interactionRange || distance >= nearestDistance)
                    continue;
                nearest = terminal;
                nearestDistance = distance;
            }
            return nearest;
        }
    }

    internal sealed class TerminalScreen
    {
        private const string Greeting = "Type help for help.";
        private const int TranscriptLimit = 160;
        private static readonly Color Background = new Color32(12, 45, 30, 255);
        private static readonly Color Header = new Color32(9, 32, 22, 255);
        private static readonly Color Text = new Color32(205, 255, 211, 255);
        private static readonly Color Accent = new Color32(143, 237, 153, 255);
        private static readonly Color Edge = new Color32(66, 123, 82, 255);
        private readonly Action _onExit;
        private readonly PanelSettings _settings;
        private readonly GameObject _host;
        private readonly Font _font;
        private readonly VisualElement _overlay;
        private readonly Label _title;
        private readonly ScrollView _transcript;
        private readonly TextField _command;

        public bool IsOpen { get; private set; }

        public TerminalScreen(Transform parent, Action onExit)
        {
            _onExit = onExit;
            _settings = ScriptableObject.CreateInstance<PanelSettings>();
            _settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            _settings.referenceResolution = new Vector2Int(960, 600);
            _settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            _settings.match = .5f;
            _settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("FieldReceiverTheme");

            _host = new GameObject("Terminal Screen");
            _host.SetActive(false);
            _host.transform.SetParent(parent, false);
            var document = _host.AddComponent<UIDocument>();
            document.panelSettings = _settings;
            _host.SetActive(true);
            _overlay = document.rootVisualElement;
            var styleSheet = Resources.Load<StyleSheet>("Terminal");
            if (styleSheet) _overlay.styleSheets.Add(styleSheet);
            _overlay.style.alignItems = Align.Center;
            _overlay.style.justifyContent = Justify.Center;
            _overlay.style.backgroundColor = new Color(0, 0, 0, .74f);
            _overlay.style.display = DisplayStyle.None;

            var screen = new VisualElement();
            screen.style.width = Length.Percent(92);
            screen.style.height = Length.Percent(80);
            screen.style.maxWidth = 760;
            screen.style.maxHeight = 500;
            screen.style.backgroundColor = Background;
            var frame = screen.style;
            frame.borderTopWidth = frame.borderBottomWidth = frame.borderLeftWidth = frame.borderRightWidth = 1;
            frame.borderTopColor = frame.borderBottomColor = frame.borderLeftColor = frame.borderRightColor = Edge;
            _font = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Liberation Mono", "Courier New" }, 16);
            if (_font) screen.style.unityFontDefinition = FontDefinition.FromFont(_font);
            _overlay.Add(screen);

            var heading = new VisualElement();
            heading.style.flexDirection = FlexDirection.Row;
            heading.style.justifyContent = Justify.SpaceBetween;
            heading.style.alignItems = Align.Center;
            heading.style.backgroundColor = Header;
            heading.style.paddingLeft = heading.style.paddingRight = 16;
            heading.style.paddingTop = heading.style.paddingBottom = 9;
            heading.style.borderBottomWidth = 1;
            heading.style.borderBottomColor = Edge;
            screen.Add(heading);
            _title = MakeLabel(string.Empty, Text, 17);
            _title.style.flexShrink = 1;
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            heading.Add(_title);
            heading.Add(MakeLabel("ESC  Close", Accent, 12));

            _transcript = new ScrollView(ScrollViewMode.Vertical);
            _transcript.style.flexGrow = 1;
            _transcript.style.paddingLeft = _transcript.style.paddingRight = 16;
            _transcript.style.paddingTop = 12;
            screen.Add(_transcript);

            var commandRow = new VisualElement();
            commandRow.style.flexDirection = FlexDirection.Row;
            commandRow.style.alignItems = Align.Center;
            commandRow.style.paddingLeft = commandRow.style.paddingRight = 16;
            commandRow.style.paddingTop = commandRow.style.paddingBottom = 8;
            commandRow.style.borderTopWidth = 1;
            commandRow.style.borderTopColor = Edge;
            commandRow.style.backgroundColor = Header;
            screen.Add(commandRow);
            commandRow.Add(MakeLabel(">", Accent, 16));
            _command = new TextField { name = "terminal-command", maxLength = 80 };
            _command.style.flexGrow = 1;
            _command.style.fontSize = 16;
            _command.style.color = Text;
            _command.RegisterCallback<KeyDownEvent>(SubmitOnEnter, TrickleDown.TrickleDown);
            commandRow.Add(_command);
        }

        public void Open(string title)
        {
            _title.text = title;
            _transcript.Clear();
            AppendLine(Greeting, Text);
            _command.SetValueWithoutNotify(string.Empty);
            _overlay.style.display = DisplayStyle.Flex;
            IsOpen = true;
            FocusCommand();
        }

        public void FocusCommand() => _command.schedule.Execute(() =>
        {
            if (IsOpen) _command.Focus();
        });

        public void Close()
        {
            IsOpen = false;
            _command.Blur();
            _overlay.style.display = DisplayStyle.None;
        }

        public void Dispose()
        {
            Object.Destroy(_host);
            Object.Destroy(_settings);
            if (_font) Object.Destroy(_font);
        }

        private void SubmitOnEnter(KeyDownEvent evt)
        {
            if (!IsOpen || (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter)) return;
            evt.StopImmediatePropagation();
            string command = _command.value.Trim();
            _command.SetValueWithoutNotify(string.Empty);
            if (command.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                _onExit();
                return;
            }
            if (command.Length == 0) return;

            AppendLine("> " + command, Accent);
            if (command.Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                AppendLine("help  Show available commands.", Text);
                AppendLine("exit  Close the terminal.", Text);
            }
            else
            {
                AppendLine("Unknown command. " + Greeting, Text);
            }
            FocusCommand();
        }

        private void AppendLine(string text, Color color)
        {
            if (_transcript.childCount >= TranscriptLimit)
                _transcript.RemoveAt(0);
            var line = MakeLabel(text, color, 16);
            _transcript.Add(line);
            _transcript.schedule.Execute(() =>
            {
                if (IsOpen && line.parent == _transcript.contentContainer) _transcript.ScrollTo(line);
            });
        }

        private static Label MakeLabel(string text, Color color, int fontSize)
        {
            var label = new Label(text) { enableRichText = false };
            label.style.color = color;
            label.style.fontSize = fontSize;
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }
    }
}
