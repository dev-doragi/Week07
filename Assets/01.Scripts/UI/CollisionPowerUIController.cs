using TMPro;
using UnityEngine;

public class CollisionPowerUIController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _playerCPText;
    [SerializeField] private TextMeshProUGUI _enemyCPText;

    private void OnEnable()
    {
        EventBus.Instance?.Subscribe<CollisionPowerUpdatedEvent>(OnCollisionPowerUpdated);
    }

    private void OnDisable()
    {
        EventBus.Instance?.Unsubscribe<CollisionPowerUpdatedEvent>(OnCollisionPowerUpdated);
    }

    private void OnCollisionPowerUpdated(CollisionPowerUpdatedEvent e)
    {
        _playerCPText.text = $"아군 공성력: {e.PlayerCP:F1}";
        _enemyCPText.text = $"적군 공성력: {e.EnemyCP:F1}";
    }
}
