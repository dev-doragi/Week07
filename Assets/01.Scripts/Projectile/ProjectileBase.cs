using System.Collections;
using UnityEngine;

/// <summary>
/// 모든 투사체의 최상위 베이스 클래스입니다. 오직 수명 관리와 풀링(회수) 로직만 담당합니다.
/// </summary>
public abstract class ProjectileBase : MonoBehaviour
{
    [Header("Lifecycle Settings")]
    [SerializeField] protected float _lifeTime = 5f;

    private Coroutine _lifeTimeCoroutine;

    protected virtual void OnEnable()
    {
        EventBus.Instance.Subscribe<StageCleanedUpEvent>(HandleStageCleanedUp);
        _lifeTimeCoroutine = StartCoroutine(LifeTimeRoutine());
    }

    protected virtual void OnDisable()
    {
        EventBus.Instance.Unsubscribe<StageCleanedUpEvent>(HandleStageCleanedUp);

        if (_lifeTimeCoroutine != null)
        {
            StopCoroutine(_lifeTimeCoroutine);
            _lifeTimeCoroutine = null;
        }
    }

    private IEnumerator LifeTimeRoutine()
    {
        yield return new WaitForSeconds(_lifeTime);
        Despawn();
    }

    private void HandleStageCleanedUp(StageCleanedUpEvent evt)
    {
        Despawn();
    }

    protected virtual void Despawn()
    {
        if (gameObject.activeInHierarchy)
        {
            PoolManager.Instance.Despawn(gameObject);
        }
    }

    public void SpawnBullet(string _bulletName)
    {
        Vector3 spawnPos = new Vector3(transform.position.x - 0.5f, transform.position.y, transform.position.z);
        PoolManager.Instance.Spawn(_bulletName, spawnPos, Quaternion.identity);
    }
}