using UnityEngine;

public class DoctrineEffectApplier : MonoBehaviour
{
    public void ApplyEffect(string effectId)
    {
        if (string.IsNullOrWhiteSpace(effectId))
        {
            Debug.Log("[DoctrineEffectApplier] Empty effectId. No effect applied.");
            return;
        }

        Debug.Log($"[DoctrineEffectApplier] Doctrine Effect Applied: {effectId}");
    }
}
