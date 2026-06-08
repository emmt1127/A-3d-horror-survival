using UnityEngine;

public class BunnyAI : MonoBehaviour
{
    enum BunnyState
    {
        Walking,
        Waiting,
        Fleeing
    }

    [Header("Walk / Stop")]
    public float wanderRadius = 4f;
    public float walkSpeed = 0.45f;
    public float minWalkDuration = 1.25f;
    public float maxWalkDuration = 2.8f;
    public float minStopDuration = 0.7f;
    public float maxStopDuration = 1.6f;
    public float arriveDistance = 0.3f;
    public float minTargetDistance = 0.7f;

    [Header("Hit Flee")]
    public float fleeSpeed = 0.8f;
    public float hitFleeDuration = 2.8f;
    public float fleeRetargetInterval = 0.4f;
    public float fleeStepDistance = 2.5f;

    [Header("Turning")]
    public float turnSpeed = 5f;

    [Header("Grounding")]
    public LayerMask groundMask = ~0;
    public float groundRayStartHeight = 3f;
    public float groundRayDistance = 12f;
    public float groundOffset = 0.02f;

    [Header("Visuals")]
    [Tooltip("When false, this bunny keeps the model from its prefab instead of generating the simple runtime bunny parts.")]
    public bool useGeneratedVisuals = true;

    public Transform player;

    Rigidbody _rb;
    BunnyState _state;
    Vector3 _targetPos;
    Vector3 _homePosition;
    Vector3 _lastFleeDirection = Vector3.forward;
    float _stateTimer;
    float _fleeRetargetTimer;
    bool _hasGroundPoint;

    void Awake()
    {
        ValidateSettings();

        if (useGeneratedVisuals && ShouldCreateGeneratedVisuals())
        {
            BunnyVisuals visuals = GetComponent<BunnyVisuals>();
            if (visuals == null)
                visuals = gameObject.AddComponent<BunnyVisuals>();
            visuals.EnsureVisuals();
        }

        _rb = GetComponent<Rigidbody>();
        if (_rb != null)
        {
            // This AI moves the bunny manually, so physics should not also try to drive it.
            _rb.isKinematic = false;
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.useGravity = false;
            _rb.isKinematic = true;
            _rb.constraints = RigidbodyConstraints.FreezeRotation;
            _rb.interpolation = RigidbodyInterpolation.None;
        }
    }

    bool ShouldCreateGeneratedVisuals()
    {
        Transform generatedRoot = transform.Find("GeneratedBunnyVisual");
        if (generatedRoot != null)
            return true;

        return gameObject.name.StartsWith("Runtime Bunny");
    }

    void OnValidate()
    {
        ValidateSettings();
    }

    void Start()
    {
        if (player == null && Camera.main != null)
            player = Camera.main.transform;

        if (TrySnapToGround(transform.position, out Vector3 groundedStart))
        {
            SetWorldPosition(groundedStart);
            _homePosition = groundedStart;
            _hasGroundPoint = true;
        }
        else
        {
            _homePosition = transform.position;
        }

        BeginWaiting(Random.Range(minStopDuration * 0.5f, minStopDuration));
    }

    public void FleeFromHit(Vector3 hitPoint)
    {
        _lastFleeDirection = GetFleeDirection(hitPoint);
        BeginFleeing();
    }

    void Update()
    {
        _stateTimer -= Time.deltaTime;
        _fleeRetargetTimer -= Time.deltaTime;

        KeepGrounded();

        switch (_state)
        {
            case BunnyState.Walking:
                UpdateWalking();
                break;
            case BunnyState.Waiting:
                UpdateWaiting();
                break;
            case BunnyState.Fleeing:
                UpdateFleeing();
                break;
        }
    }

    void UpdateWalking()
    {
        MoveTowardsTarget(walkSpeed);

        if (_stateTimer <= 0f || HasReachedTarget())
            BeginWaiting(Random.Range(minStopDuration, maxStopDuration));
    }

    void UpdateWaiting()
    {
        if (_stateTimer <= 0f)
            BeginWalking();
    }

    void UpdateFleeing()
    {
        if (player != null)
            _lastFleeDirection = GetFleeDirection(player.position);

        if (_fleeRetargetTimer <= 0f || HasReachedTarget())
            ChooseFleeTarget();

        MoveTowardsTarget(fleeSpeed);

        if (_stateTimer <= 0f)
            BeginWaiting(Random.Range(minStopDuration, maxStopDuration));
    }

    void BeginWalking()
    {
        _state = BunnyState.Walking;
        _stateTimer = Random.Range(minWalkDuration, maxWalkDuration);
        ChooseWalkTarget();
    }

    void BeginWaiting(float duration)
    {
        _state = BunnyState.Waiting;
        _stateTimer = Mathf.Max(0.05f, duration);
        _fleeRetargetTimer = 0f;
        _targetPos = transform.position;
    }

    void BeginFleeing()
    {
        _state = BunnyState.Fleeing;
        _stateTimer = hitFleeDuration;
        _fleeRetargetTimer = 0f;
        ChooseFleeTarget();
    }

    void ChooseWalkTarget()
    {
        Vector3 origin = _hasGroundPoint ? _homePosition : transform.position;

        for (int attempt = 0; attempt < 10; attempt++)
        {
            Vector2 offset = Random.insideUnitCircle * wanderRadius;
            Vector3 candidate = origin + new Vector3(offset.x, 0f, offset.y);

            if (Vector3.Distance(transform.position, candidate) < minTargetDistance)
                continue;

            if (TrySnapToGround(candidate, out Vector3 groundedCandidate))
            {
                _targetPos = groundedCandidate;
                return;
            }
        }

        _targetPos = transform.position;
    }

    void ChooseFleeTarget()
    {
        _fleeRetargetTimer = fleeRetargetInterval;
        SetTargetInDirection(_lastFleeDirection, fleeStepDistance);
    }

    Vector3 GetFleeDirection(Vector3 hitPoint)
    {
        Vector3 source = hitPoint;
        if (player != null)
            source = player.position;

        Vector3 direction = transform.position - source;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            direction = -transform.forward;

        return direction.normalized;
    }

    void SetTargetInDirection(Vector3 direction, float distance)
    {
        Vector3 flatDirection = direction;
        flatDirection.y = 0f;

        if (flatDirection.sqrMagnitude < 0.0001f)
            flatDirection = -transform.forward;

        flatDirection.Normalize();

        Vector3 candidate = transform.position + flatDirection * distance;
        if (TrySnapToGround(candidate, out Vector3 groundedCandidate))
            _targetPos = groundedCandidate;
        else
            _targetPos = transform.position + flatDirection * Mathf.Max(minTargetDistance, distance * 0.5f);
    }

    void MoveTowardsTarget(float speed)
    {
        Vector3 flatTarget = _targetPos;
        flatTarget.y = transform.position.y;

        Vector3 direction = flatTarget - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= arriveDistance * arriveDistance || speed <= 0f)
            return;

        Vector3 moveDir = direction.normalized;
        float moveDistance = Mathf.Min(speed * Time.deltaTime, direction.magnitude);
        Vector3 candidatePosition = transform.position + moveDir * moveDistance;

        if (TrySnapToGround(candidatePosition, out Vector3 groundedMove))
        {
            SetWorldPosition(groundedMove);
            _hasGroundPoint = true;
        }
        else
        {
            SetWorldPosition(candidatePosition);
        }

        transform.forward = Vector3.Slerp(transform.forward, moveDir, Time.deltaTime * turnSpeed);
    }

    bool HasReachedTarget()
    {
        Vector3 flatTarget = _targetPos;
        flatTarget.y = transform.position.y;
        return Vector3.Distance(transform.position, flatTarget) <= arriveDistance;
    }

    void KeepGrounded()
    {
        if (!TrySnapToGround(transform.position, out Vector3 groundedPosition))
            return;

        SetWorldPosition(groundedPosition);
        if (!_hasGroundPoint)
            _homePosition = groundedPosition;
        _hasGroundPoint = true;
    }

    void SetWorldPosition(Vector3 position)
    {
        if (_rb != null)
            _rb.position = position;
        else
            transform.position = position;
    }

    void ValidateSettings()
    {
        if (groundMask.value == 0)
            groundMask = ~0;

        wanderRadius = Mathf.Max(1f, wanderRadius);
        walkSpeed = Mathf.Max(0.05f, walkSpeed);
        minWalkDuration = Mathf.Max(0.1f, minWalkDuration);
        maxWalkDuration = Mathf.Max(minWalkDuration, maxWalkDuration);
        minStopDuration = Mathf.Max(0.05f, minStopDuration);
        maxStopDuration = Mathf.Max(minStopDuration, maxStopDuration);
        arriveDistance = Mathf.Clamp(arriveDistance, 0.05f, 1.5f);
        minTargetDistance = Mathf.Max(arriveDistance + 0.1f, minTargetDistance);
        fleeSpeed = Mathf.Max(walkSpeed, fleeSpeed);
        hitFleeDuration = Mathf.Max(0.2f, hitFleeDuration);
        fleeRetargetInterval = Mathf.Max(0.05f, fleeRetargetInterval);
        fleeStepDistance = Mathf.Max(minTargetDistance, fleeStepDistance);
        turnSpeed = Mathf.Max(0.1f, turnSpeed);
        groundRayStartHeight = Mathf.Max(2f, groundRayStartHeight);
        groundRayDistance = Mathf.Max(8f, groundRayDistance);
        groundOffset = Mathf.Clamp(groundOffset, 0.01f, 0.08f);
    }

    bool TrySnapToGround(Vector3 worldPos, out Vector3 groundedPosition)
    {
        Vector3 origin = new Vector3(worldPos.x, worldPos.y + groundRayStartHeight, worldPos.z);
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, groundRayDistance, groundMask, QueryTriggerInteraction.Ignore);
        float bestDistance = float.MaxValue;
        bool found = false;
        Vector3 bestPoint = worldPos;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                continue;

            // Ignore moving rigidbodies so the bunny only grounds on stable world geometry.
            if (hit.rigidbody != null && hit.rigidbody != _rb)
                continue;

            // Ignore walls / steep faces that can yank the bunny up or underground.
            if (hit.normal.y < 0.55f)
                continue;

            if (hit.distance < bestDistance)
            {
                bestDistance = hit.distance;
                bestPoint = hit.point;
                found = true;
            }
        }

        if (found)
        {
            groundedPosition = bestPoint + Vector3.up * groundOffset;
            return true;
        }

        groundedPosition = worldPos;
        return false;
    }
}
