using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraManager : MonoBehaviour
{
    [Header("Bounds")]
    [SerializeField] private BoxCollider2D _cameraBounds;

    [Header("Zoom Settings")]
    [SerializeField] private float _zoomStep = 2f;
    [SerializeField] private float _minZoom = 3f;
    [SerializeField] private float _maxZoom = 15f;

    private Camera _mainCamera;
    // v1.9 규칙: InputReader.Instance 직접 사용

    private float _targetZoom;
    private float _initialZoom;
    private bool _isDragging;

    private void Awake()
    {
        _mainCamera = GetComponent<Camera>();

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

    private void Start()
    {
        transform.position = ClampCameraPosition(transform.position, _mainCamera.orthographicSize);
    }

    private void OnEnable()
    {
        if (EventBus.Instance == null) return;

        EventBus.Instance.Subscribe<RightClickEvent>(HandleRightClick);
        EventBus.Instance.Subscribe<ScrollEvent>(HandleScroll);
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
        // Lerp zoom for smooth transition
        float currentZoom = _mainCamera.orthographicSize;
        if (Mathf.Abs(currentZoom - _targetZoom) > 0.01f)
        {
            float lerped = Mathf.Lerp(currentZoom, _targetZoom, Time.deltaTime * 10f);
            _mainCamera.orthographicSize = lerped;
            // 위치 보정 (줌 중 마우스 위치 기준)
            transform.position = ClampCameraPosition(transform.position, _mainCamera.orthographicSize);
        }
    }

    private void HandlePanning()
    {
        if (_isDragging == false || InputReader.Instance == null) return;

        if (Mathf.Abs(_mainCamera.orthographicSize - _maxZoom) < 0.001f) return;

        Vector2 mouseDelta = InputReader.Instance.GetMouseDelta();
        if (mouseDelta.sqrMagnitude <= 0.01f) return;

        float unitsPerPixel = (_mainCamera.orthographicSize * 2f) / Screen.height;
        Vector3 move = new Vector3(-mouseDelta.x, -mouseDelta.y, 0f) * unitsPerPixel;

        Vector3 nextPos = transform.position + move;
        transform.position = ClampCameraPosition(nextPos, _mainCamera.orthographicSize);
    }

    private void HandleRightClick(RightClickEvent e)
    {
        if (InputReader.Instance != null && InputReader.Instance.IsPointerOverUI && e.IsStarted) return;
        _isDragging = e.IsStarted;
    }

    private void HandleScroll(ScrollEvent e)
    {
        if (InputReader.Instance != null && InputReader.Instance.IsPointerOverUI) return;

        // direction: -1 = zoom in (smaller size), +1 = zoom out (larger size)
        float direction = e.Delta > 0 ? -1f : 1f;
        float nextZoom = _targetZoom + direction * _zoomStep;

        // Detect whether user is zooming out (increasing orthographicSize)
        bool isZoomingOut = direction > 0f;

        // Will cross initial zoom this tick?
        bool willCrossInitial =
            (_targetZoom < _initialZoom && nextZoom >= _initialZoom) ||
            (_targetZoom > _initialZoom && nextZoom <= _initialZoom);

        // If zooming out and we will cross the initial zoom, snap to initial once
        if (isZoomingOut && willCrossInitial && Mathf.Abs(_targetZoom - _initialZoom) > 0.01f)
        {
            _targetZoom = _initialZoom;
            ApplyZoomAtMousePosition(_targetZoom);
            return;
        }

        // Existing behavior: handle crossing in other direction or normal clamping
        bool isCrossingInitial =
            (_targetZoom > _initialZoom && nextZoom < _initialZoom) ||
            (_targetZoom < _initialZoom && nextZoom > _initialZoom);

        bool isAlreadyAtInitial = Mathf.Abs(_targetZoom - _initialZoom) < 0.01f;

        if (isCrossingInitial && isAlreadyAtInitial == false)
            _targetZoom = _initialZoom;
        else
            _targetZoom = Mathf.Clamp(nextZoom, _minZoom, _maxZoom);

        ApplyZoomAtMousePosition(_targetZoom);
    }

    private void ApplyZoomAtMousePosition(float newZoom)
    {
        if (InputReader.Instance == null) return;

        Vector2 mousePos = InputReader.Instance.GetMousePosition();
        Vector3 mouseScreen = new Vector3(mousePos.x, mousePos.y, _mainCamera.nearClipPlane);

        Vector3 beforeWorld = _mainCamera.ScreenToWorldPoint(mouseScreen);

        // orthographicSize는 LateUpdate에서 Lerp로 적용
        // 여기서는 _targetZoom만 갱신
        _targetZoom = newZoom;

        // Lerp 후 위치 보정이 LateUpdate에서 이뤄짐
        // (즉시 위치 보정 필요시 아래 코드 유지)
        Vector3 afterWorld = _mainCamera.ScreenToWorldPoint(mouseScreen);
        Vector3 delta = beforeWorld - afterWorld;
        Vector3 nextPos = transform.position + new Vector3(delta.x, delta.y, 0f);
        transform.position = ClampCameraPosition(nextPos, _mainCamera.orthographicSize);
    }

    private Vector3 ClampCameraPosition(Vector3 targetPos, float zoomSize)
    {
        if (_cameraBounds == null)
            return new Vector3(targetPos.x, targetPos.y, transform.position.z);

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

        return new Vector3(clampedX, clampedY, transform.position.z);
    }
}