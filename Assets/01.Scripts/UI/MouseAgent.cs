using UnityEngine;

public class MouseAgent : MonoBehaviour
{
    private MouseUIMovement _movement = new MouseUIMovement();
    private RectTransform _rectTransform;

    public void Setup(RectTransform bounds)
    {
        _rectTransform = GetComponent<RectTransform>();
        _movement.Init(bounds);
        _movement.ResetMotion(_rectTransform);
    }


    private void Update()
    {
        _movement.UpdateMovement(_rectTransform);
    }
}
