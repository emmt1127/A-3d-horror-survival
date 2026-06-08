using UnityEngine;

public class BulletProjectile : MonoBehaviour
{
    float _damage;
    float _maxDistance;
    float _lifeTime;
    GameObject _impactEffectPrefab;
    Transform _ownerRoot;
    Vector3 _startPosition;
    bool _hasHit;

    public void Initialize(float damage, float maxDistance, float lifeTime, GameObject impactEffectPrefab, Transform ownerRoot)
    {
        _damage = Mathf.Max(0f, damage);
        _maxDistance = Mathf.Max(1f, maxDistance);
        _lifeTime = Mathf.Max(0.1f, lifeTime);
        _impactEffectPrefab = impactEffectPrefab;
        _ownerRoot = ownerRoot;
        _startPosition = transform.position;

        IgnoreOwnerColliders();
        Destroy(gameObject, _lifeTime);
    }

    void Update()
    {
        if (Vector3.Distance(_startPosition, transform.position) >= _maxDistance)
            Destroy(gameObject);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (_hasHit || collision.collider == null)
            return;

        HandleHit(collision.collider, collision.GetContact(0).point, collision.GetContact(0).normal);
    }

    void OnTriggerEnter(Collider other)
    {
        if (_hasHit || other == null)
            return;

        HandleHit(other, transform.position, -transform.forward);
    }

    void HandleHit(Collider hitCollider, Vector3 point, Vector3 normal)
    {
        if (_ownerRoot != null && hitCollider.transform.root == _ownerRoot)
            return;

        _hasHit = true;

        BunnyHealth bunny = hitCollider.GetComponentInParent<BunnyHealth>();
        if (bunny != null)
        {
            bunny.KillInstantly(point);
        }
        else
        {
            Health health = hitCollider.GetComponentInParent<Health>();
            if (health != null && (_ownerRoot == null || health.transform.root != _ownerRoot))
                health.TakeDamage(_damage);
        }

        SpawnImpact(point, normal);
        Destroy(gameObject);
    }

    void IgnoreOwnerColliders()
    {
        if (_ownerRoot == null)
            return;

        Collider ownCollider = GetComponent<Collider>();
        if (ownCollider == null)
            return;

        Collider[] ownerColliders = _ownerRoot.GetComponentsInChildren<Collider>(true);
        foreach (Collider ownerCollider in ownerColliders)
        {
            if (ownerCollider != null)
                Physics.IgnoreCollision(ownCollider, ownerCollider, true);
        }
    }

    void SpawnImpact(Vector3 point, Vector3 normal)
    {
        if (_impactEffectPrefab == null)
            return;

        Quaternion rotation = normal.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(normal)
            : Quaternion.identity;
        Destroy(Instantiate(_impactEffectPrefab, point + normal.normalized * 0.01f, rotation), 2f);
    }
}
