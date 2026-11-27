using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;
using TicketStatus = LostSignal.FieldService.ControllerFailureTicket.Status;

namespace LostSignal.FieldService
{
    [DisallowMultipleComponent, RequireComponent(typeof(UIDocument))]
    public sealed class ControllerFailureTicketBehaviour : MonoBehaviour
    {
        [SerializeField] private Transform _fieldAgent;
        [SerializeField] private Transform _nodeLocation;
        [SerializeField] private string _ticketId = "FS-001";
        [SerializeField] private string _nodeId = "HC-017";
        [SerializeField, Min(0.5f)] private float _terminalRange = 3f;
        [SerializeField] private UnityEvent<bool> _onTerminalVisibilityChanged = new UnityEvent<bool>();

        private ControllerFailureTicket _ticket;
        private VisualElement _card;
        private VisualElement _terminal;
        private Label _status;
        private Label _distance;
        private Label _feedback;
        private Label _transcript;
        private Button _connect;
        private Button _close;
        private ScrollView _transcriptScroll;
        private TextField _command;
        private string _terminalLog;
        private bool _terminalOpen;
        private int _distanceMetres = -1;

        public Transform TargetNode => _nodeLocation;
        public bool IsClosed => _ticket != null && _ticket.State == TicketStatus.Closed;

        public void SetFieldAgent(Transform fieldAgent)
        {
            _fieldAgent = fieldAgent;
        }

        public void SetNodeLocation(Transform nodeLocation)
        {
            _nodeLocation = nodeLocation;
        }

        private void OnEnable()
        {
            if (_ticket != null)
            {
                BuildInterface();
            }
        }

        private void Start()
        {
            _ticket = new ControllerFailureTicket();
            _terminalLog = $"Connected to {_nodeId} service interface.\nType system to check the controller.";
            BuildInterface();
        }

        private void OnDisable()
        {
            SetTerminalOpen(false);
            _card?.RemoveFromHierarchy();
            _card = null;
        }

        private void LateUpdate()
        {
            TicketStatus previousStatus = _ticket.State;
            _ticket.Advance(Time.unscaledDeltaTime, AppendOutput);

            if (_card == null)
            {
                return;
            }

            if (_ticket.State != previousStatus)
            {
                _feedback.text = string.Empty;
                UpdateStatus();
            }

            UpdateDistance();

            if (_terminalOpen && !IsInTerminalRange())
            {
                SetTerminalOpen(false);
                _feedback.text = "Move closer to the node to reconnect.";
            }
        }

        private void BuildInterface()
        {
            UIDocument document = GetComponent<UIDocument>();
            VisualElement root = document.rootVisualElement;
            if (root == null || document.panelSettings == null)
            {
                Debug.LogWarning("Enable the ticket's UI Document and assign its Panel Settings.", this);
                return;
            }

            root.pickingMode = PickingMode.Ignore;
            _card = new VisualElement();
            _card.style.position = Position.Absolute;
            _card.style.top = 16;
            _card.style.right = 16;
            _card.style.width = new Length(90, LengthUnit.Percent);
            _card.style.maxWidth = 400;
            _card.style.paddingTop = 10;
            _card.style.paddingBottom = 10;
            _card.style.paddingLeft = 12;
            _card.style.paddingRight = 12;
            _card.style.backgroundColor = (Color)new Color32(24, 31, 33, 240);
            _card.style.color = (Color)new Color32(232, 241, 237, 255);
            root.Add(_card);

            Label title = AddLabel(_card, $"Dispatch · {_ticketId} · {_nodeId}");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            AddLabel(_card, "Intermittent check-ins. Three heartbeats missed.");
            _status = AddLabel(_card, string.Empty);
            _distance = AddLabel(_card, string.Empty);
            _distanceMetres = -1;

            VisualElement actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            _connect = new Button(() => SetTerminalOpen(true)) { text = "Connect terminal" };
            _close = new Button(CloseTicket) { text = "Close ticket" };
            actions.Add(_connect);
            actions.Add(_close);
            _card.Add(actions);
            _feedback = AddLabel(_card, string.Empty);

            _terminal = new VisualElement();
            _terminal.style.marginTop = 8;
            _terminal.style.display = DisplayStyle.None;
            _card.Add(_terminal);

            _transcriptScroll = new ScrollView(ScrollViewMode.Vertical);
            _transcriptScroll.style.height = 156;
            _transcript = AddLabel(_transcriptScroll, _terminalLog);
            _transcript.RegisterCallback<GeometryChangedEvent>(OnTranscriptLayoutChanged);
            _terminal.Add(_transcriptScroll);

            _command = new TextField("Command") { maxLength = 64 };
            _command.RegisterCallback<KeyDownEvent>(OnCommandKeyDown, TrickleDown.TrickleDown);
            _terminal.Add(_command);

            VisualElement terminalActions = new VisualElement();
            terminalActions.style.flexDirection = FlexDirection.Row;
            terminalActions.Add(new Button(SubmitCommand) { text = "Run" });
            terminalActions.Add(new Button(() => SetTerminalOpen(false)) { text = "Disconnect" });
            _terminal.Add(terminalActions);

            UpdateStatus();
            UpdateDistance();
        }

        private void UpdateStatus()
        {
            switch (_ticket.State)
            {
                case TicketStatus.Assigned:
                    _status.text = "Assigned to you · Visit the node and run system";
                    break;
                case TicketStatus.Rebooting:
                    _status.text = "Rebooting · Waiting for dispatch check-ins";
                    break;
                case TicketStatus.ReadyToClose:
                    _status.text = "Link restored · Ready to close";
                    break;
                case TicketStatus.Closed:
                    _status.text = "Closed";
                    break;
            }

            _close.SetEnabled(!IsClosed);
        }

        private void UpdateDistance()
        {
            bool locationAvailable = _fieldAgent != null && _nodeLocation != null;
            int metres = locationAvailable
                ? Mathf.RoundToInt(Vector3.Distance(_fieldAgent.position, _nodeLocation.position))
                : -1;

            if (_distanceMetres != metres || string.IsNullOrEmpty(_distance.text))
            {
                _distanceMetres = metres;
                _distance.text = locationAvailable ? $"Distance to node · {metres:N0} m" : "Node location unavailable";
            }

            _connect.SetEnabled(!_terminalOpen && IsInTerminalRange());
        }

        private bool IsInTerminalRange()
        {
            return _fieldAgent != null && _nodeLocation != null &&
                   (_fieldAgent.position - _nodeLocation.position).sqrMagnitude <= _terminalRange * _terminalRange;
        }

        private void SetTerminalOpen(bool open)
        {
            if (_terminalOpen == open || (open && !IsInTerminalRange()))
            {
                return;
            }

            _terminalOpen = open;
            _terminal.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;

            if (open)
            {
                _feedback.text = string.Empty;
                TextField input = _command;
                input.schedule.Execute(() =>
                {
                    if (_terminalOpen && _command == input)
                    {
                        input.Focus();
                    }
                });
            }
            else
            {
                _command.Blur();
            }

            _onTerminalVisibilityChanged.Invoke(open);
        }

        private void OnCommandKeyDown(KeyDownEvent keyEvent)
        {
            if (keyEvent.keyCode == KeyCode.Return || keyEvent.keyCode == KeyCode.KeypadEnter)
            {
                keyEvent.StopImmediatePropagation();
                SubmitCommand();
            }
        }

        private void SubmitCommand()
        {
            if (!_terminalOpen || !IsInTerminalRange())
            {
                return;
            }

            string command = _command.value;
            if (string.IsNullOrWhiteSpace(command))
            {
                return;
            }

            command = command.Trim();
            _command.SetValueWithoutNotify(string.Empty);
            AppendOutput($"> {command}\n{_ticket.RunCommand(command)}");
            _feedback.text = string.Empty;
            UpdateStatus();
            _command.Focus();
        }

        private void CloseTicket()
        {
            _feedback.text = _ticket.Close();
            UpdateStatus();
        }

        private void AppendOutput(string line)
        {
            _terminalLog += "\n" + line;
            if (_transcript != null)
            {
                _transcript.text = _terminalLog;
            }
        }

        private void OnTranscriptLayoutChanged(GeometryChangedEvent layoutEvent)
        {
            if (layoutEvent.newRect.height != layoutEvent.oldRect.height)
            {
                ScrollView scroll = _transcriptScroll;
                scroll.schedule.Execute(() => scroll.verticalScroller.value = scroll.verticalScroller.highValue);
            }
        }

        private static Label AddLabel(VisualElement parent, string value)
        {
            Label label = new Label(value);
            label.enableRichText = false;
            label.style.whiteSpace = WhiteSpace.Normal;
            parent.Add(label);
            return label;
        }
    }
}
