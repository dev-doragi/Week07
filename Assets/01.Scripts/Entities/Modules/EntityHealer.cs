using UnityEngine;

public class EntityHealer : MonoBehaviour
{
    private Unit _owner;
    private float _healAmount;
    private float _cooldown;
    private float _healRange;
    private float _timer;
    private Collider2D[] _healTargets = new Collider2D[32]; // 캐시 배열

    public void Setup(Unit owner, float healAmount, float cooldown, float healRange = 10f)
    {
        _owner = owner;
        _healAmount = healAmount;
        _cooldown = cooldown;
        _healRange = healRange;
        _timer = 0f;
    }

    private void Update()
    {
        if (_owner == null || _owner.IsDead) return;

        _timer += Time.deltaTime;
        if (_timer >= _cooldown)
        {
            HealNearbyAllies();
            _timer = 0f;
        }
    }

    private void HealNearbyAllies()
    {
        int allyLayer = (_owner.Team == TeamType.Player)
            ? LayerMask.GetMask("Ally")
            : LayerMask.GetMask("Enemy");

        ContactFilter2D filter = new ContactFilter2D
        {
            layerMask = allyLayer,
            useLayerMask = true
        };

        int count = Physics2D.OverlapCircle(_owner.transform.position, _healRange, filter, _healTargets);

        System.Text.StringBuilder healedLog = new System.Text.StringBuilder();
        int healedCount = 0;
        for (int i = 0; i < count; i++)
        {
            var unit = _healTargets[i].GetComponent<Unit>();
            if (unit != null && unit != _owner && unit.Team == _owner.Team && unit.Data.CanAttack && !unit.IsDead)
            {
                unit.Heal(_healAmount);
                healedCount++;
                healedLog.Append($"[{healedCount}] {unit.gameObject.name} (HP+{_healAmount})\n");
            }
        }

        if (healedCount > 0)
        {
            Debug.Log($"[EntityHealer] 힐 적용 대상 {healedCount}명\n" + healedLog.ToString());
        }
    }
}
