using UnityEngine;

public class EnemyGridAuthoring : MonoBehaviour
{
    public EnemyGridManager Grid;
    public UnitDataSO SelectedUnit;
    public Transform UnitRoot;

    private void Reset()
    {
        if (Grid == null)
        {
            Grid = GetComponent<EnemyGridManager>();
        }

        EnsureUnitRoot();
    }

    public void EnsureUnitRoot()
    {
        if (UnitRoot != null)
        {
            return;
        }

        Transform child = transform.Find("EnemyUnits");
        if (child == null)
        {
            GameObject unitRootObject = new GameObject("EnemyUnits");
            child = unitRootObject.transform;
            child.SetParent(transform, false);
        }

        UnitRoot = child;
    }
}
