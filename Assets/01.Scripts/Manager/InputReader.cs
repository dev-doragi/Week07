using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-140)]
[RequireComponent(typeof(PlayerInput))]
public class InputReader : Singleton<InputReader>
{
    private PlayerInput _playerInput;

    private InputActionMap _playerMap;
    private InputActionMap _systemMap;

    private InputAction _clickAction;
    private InputAction _rightClickAction;
    private InputAction _rotateAction;
    private InputAction _pointAction;
    private InputAction _scrollAction;
    private InputAction _pauseAction;

    public bool IsPointerOverUI { get; private set; }

    private bool _isInputBlocked = false;

    public bool IsInputBlocked => _isInputBlocked;

    protected override void OnBootstrap()
    {
        _playerInput = GetComponent<PlayerInput>();

        _playerMap = _playerInput.actions.FindActionMap("Player", true);
        _systemMap = _playerInput.actions.FindActionMap("System", true);

        _clickAction = _playerMap.FindAction("Click", true);
        _rightClickAction = _playerMap.FindAction("RightClick", true);
        _rotateAction = _playerMap.FindAction("Rotate", true);
        _pointAction = _playerMap.FindAction("Point", true);
        _scrollAction = _playerMap.FindAction("Scroll", true);
        _pauseAction = _systemMap.FindAction("Pause", true);

        BindEvents();
        _playerMap.Enable();
        _systemMap.Enable();

        EventBus.Instance.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
    }



    private void Update()
    {
        if (EventSystem.current != null)
        {
            IsPointerOverUI = EventSystem.current.IsPointerOverGameObject();
        }
    }

    private void BindEvents()
    {
        _clickAction.started += _ => PublishIfAllowed(new ClickEvent { IsStarted = true });
        _clickAction.canceled += _ => PublishIfAllowed(new ClickEvent { IsStarted = false });

        _rightClickAction.started += _ => PublishIfAllowed(new RightClickEvent { IsStarted = true });
        _rightClickAction.canceled += _ => PublishIfAllowed(new RightClickEvent { IsStarted = false });

        _rotateAction.performed += _ => PublishIfAllowed(new RotateEvent());

        _scrollAction.performed += ctx =>
        {
            float scrollValue = ctx.ReadValue<Vector2>().y;
            if (Mathf.Abs(scrollValue) > 0.01f)
                PublishIfAllowed(new ScrollEvent { Delta = scrollValue });
        };

        _pauseAction.performed += _ => EventBus.Instance.Publish(new PausePressedEvent());
    }

    private void PublishIfAllowed<T>(T evt) where T : struct
    {
        if (_isInputBlocked) return;
        EventBus.Instance.Publish(evt);
    }

    private void OnGameStateChanged(GameStateChangedEvent evt)
    {
        if (evt.NewState == GameState.Playing) _playerMap.Enable();
        else _playerMap.Disable();
    }

    public void SetInputBlocked(bool blocked)
    {
        _isInputBlocked = blocked;
    }

    public Vector2 GetMousePosition() => _pointAction?.ReadValue<Vector2>() ?? Vector2.zero;
    public Vector2 GetMouseDelta() => Mouse.current.delta.ReadValue();
}