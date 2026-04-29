using UnityEngine;

[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(-110)]
public class CameraManager : Singleton<CameraManager>
{
    [Header("Bounds")]
    [SerializeField] private BoxCollider2D _cameraBounds;

    [Header("Zoom Settings")]
    [SerializeField] private float _zoomStep = 2f;
    [SerializeField] private float _minZoom = 3f;
    [SerializeField] private float _maxZoom = 15f;

    private Camera _mainCamera;
    private Vector3 _originalPos;
    private Coroutine _shakeCoroutine;

    private float _targetZoom;
    private float _initialZoom;
    private bool _isDragging;
    private bool _tutorialCameraEventPublished;

    public Vector3 OriginalPosition => _originalPos;
    public float InitialZoom => _initialZoom;

    public void ResetCameraState()
    {
        _isDragging = false;
        _targetZoom = _initialZoom;
        _mainCamera.orthographicSize = _initialZoom;
        transform.position = ClampCameraPosition(_originalPos, _initialZoom);
    }

    protected override void Awake()
    {
        base.Awake();

        _mainCamera = GetComponent<Camera>();
        _originalPos = transform.localPosition;

        if (_mainCamera == null)
        {
            Debug.LogError("CameraManager: Camera not found");
            enabled = false;
            return;
        }

        if (_mainCamera.orthographic == false)
        {
            Debug.LogError("CameraManager: Orthographic Camera only");
            enabled = false;
            return;
        }

        _initialZoom = _mainCamera.orthographicSize;
        _targetZoom = _initialZoom;
    }

    protected override void OnBootstrap()
    {
        transform.position = ClampCameraPosition(transform.position, _mainCamera.orthographicSize);

        if (EventBus.Instance != null)
        {
            EventBus.Instance.Subscribe<RightClickEvent>(HandleRightClick);
            EventBus.Instance.Subscribe<ScrollEvent>(HandleScroll);
        }
    }

    private void OnDisable()
    {
        if (EventBus.Instance == null) return;

        EventBus.Instance.Unsubscribe<RightClickEvent>(HandleRightClick);
        EventBus.Instance.Unsubscribe<ScrollEvent>(HandleScroll);
    }

    private void Update()
    {
        if (Time.timeScale <= 0f) return;
        HandlePanning();
    }

    private void LateUpdate()
    {
        float currentZoom = _mainCamera.orthographicSize;
        if (Mathf.Abs(currentZoom - _targetZoom) > 0.01f)
        {
            float lerped = Mathf.Lerp(currentZoom, _targetZoom, Time.deltaTime * 10f);
            _mainCamera.orthographicSize = lerped;
            transform.position = ClampCameraPosition(transform.position, _mainCamera.orthographicSize);
        }
    }

    private void HandlePanning()
    {
        if (!_isDragging || InputReader.Instance == null) return;
        if (Mathf.Abs(_mainCamera.orthographicSize - _maxZoom) < 0.001f) return;

        Vector2 mouseDelta = InputReader.Instance.GetMouseDelta();
        if (mouseDelta.sqrMagnitude <= 0.01f) return;

        float unitsPerPixel = (_mainCamera.orthographicSize * 2f) / Screen.height;
        Vector3 move = new Vector3(-mouseDelta.x, -mouseDelta.y, 0f) * unitsPerPixel;

        Vector3 nextPos = transform.localPosition + move;
        transform.localPosition = ClampCameraPosition(nextPos, _mainCamera.orthographicSize);
    }

    private void HandleRightClick(RightClickEvent e)
    {
        if (InputReader.Instance != null && InputReader.Instance.IsPointerOverUI && e.IsStarted) return;
        _isDragging = e.IsStarted;
        if (e.IsStarted)
            TryPublishTutorialCameraManipulated();
    }

    private void HandleScroll(ScrollEvent e)
    {
        if (InputReader.Instance != null && InputReader.Instance.IsPointerOverUI) return;

        if (Mathf.Abs(e.Delta) > 0.001f)
            TryPublishTutorialCameraManipulated();

        float direction = e.Delta > 0 ? -1f : 1f;
        float nextZoom = _targetZoom + direction * _zoomStep;

        bool isZoomingOut = direction > 0f;
        bool willCrossInitial =
            (_targetZoom < _initialZoom && nextZoom >= _initialZoom) ||
            (_targetZoom > _initialZoom && nextZoom <= _initialZoom);

        if (isZoomingOut && willCrossInitial && Mathf.Abs(_targetZoom - _initialZoom) > 0.01f)
        {
            _targetZoom = _initialZoom;
            ApplyZoomAtMousePosition(_targetZoom);
            return;
        }

        bool isCrossingInitial =
            (_targetZoom > _initialZoom && nextZoom < _initialZoom) ||
            (_targetZoom < _initialZoom && nextZoom > _initialZoom);

        bool isAlreadyAtInitial = Mathf.Abs(_targetZoom - _initialZoom) < 0.01f;

        if (isCrossingInitial && !isAlreadyAtInitial)
            _targetZoom = _initialZoom;
        else
            _targetZoom = Mathf.Clamp(nextZoom, _minZoom, _maxZoom);

        ApplyZoomAtMousePosition(_targetZoom);
    }

    private void TryPublishTutorialCameraManipulated()
    {
        if (_tutorialCameraEventPublished) return;

        _tutorialCameraEventPublished = true;
        EventBus.Instance?.Publish(new CameraManipulationEvent());
    }

    private void ApplyZoomAtMousePosition(float newZoom)
    {
        if (InputReader.Instance == null) return;

        Vector2 mousePos = InputReader.Instance.GetMousePosition();
        Vector3 mouseScreen = new Vector3(mousePos.x, mousePos.y, _mainCamera.nearClipPlane);

        Vector3 beforeWorld = _mainCamera.ScreenToWorldPoint(mouseScreen);

        _targetZoom = newZoom;

        Vector3 afterWorld = _mainCamera.ScreenToWorldPoint(mouseScreen);
        Vector3 delta = beforeWorld - afterWorld;
        Vector3 nextPos = transform.localPosition + new Vector3(delta.x, delta.y, 0f);
        transform.localPosition = ClampCameraPosition(nextPos, _mainCamera.orthographicSize);
    }

    private Vector3 ClampCameraPosition(Vector3 targetPos, float zoomSize)
    {
        if (_cameraBounds == null)
            return new Vector3(targetPos.x, targetPos.y, transform.localPosition.z);

        Bounds bounds = _cameraBounds.bounds;

        float camHalfHeight = zoomSize;
        float camHalfWidth = zoomSize * _mainCamera.aspect;

        float minX = bounds.min.x + camHalfWidth;
        float maxX = bounds.max.x - camHalfWidth;
        float minY = bounds.min.y + camHalfHeight;
        float maxY = bounds.max.y - camHalfHeight;

        if (minX > maxX)
        {
            float midX = (bounds.min.x + bounds.max.x) * 0.5f;
            minX = midX;
            maxX = midX;
        }

        if (minY > maxY)
        {
            float midY = (bounds.min.y + bounds.max.y) * 0.5f;
            minY = midY;
            maxY = midY;
        }

        float clampedX = Mathf.Clamp(targetPos.x, minX, maxX);
        float clampedY = Mathf.Clamp(targetPos.y, minY, maxY);

        return new Vector3(clampedX, clampedY, transform.localPosition.z);
    }

    // --- 카메라 쉐이크 구현 (강도별) ---
    public void ShakeWeak()   => StartShake(0.18f, 0.08f);
    public void ShakeMedium() => StartShake(0.18f, 0.15f);
    public void ShakeStrong() => StartShake(0.18f, 0.25f);

    private void StartShake(float duration, float magnitude)
    {
        if (_shakeCoroutine != null)
            return; // 이미 쉐이크 중이면 무시
        _shakeCoroutine = StartCoroutine(ShakeCoroutine(duration, magnitude));
    }

    private System.Collections.IEnumerator ShakeCoroutine(float duration, float magnitude)
    {
        float elapsed = 0f;
        Vector3 basePos = transform.localPosition;
        while (elapsed < duration)
        {
            float offsetX = Random.Range(-1f, 1f) * magnitude;
            float offsetY = Random.Range(-1f, 1f) * magnitude;
            transform.localPosition = basePos + new Vector3(offsetX, offsetY, 0f);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        transform.localPosition = basePos;
        _shakeCoroutine = null;
    }
}
