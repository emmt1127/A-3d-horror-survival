using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// AI controller for the monster that stalks the player and attacks when detected.
/// During the day the monster fully disappears. At night it can manifest and chase the player.
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(NavMeshAgent))]
public class MonsterAI : MonoBehaviour
{
    [Header("References")]
    public Transform playerTransform;
    public Transform playerCamera;
    public Health playerHealth;
    public JumpscareManager jumpscareManager;
    public Campfire campfire;
    public Transform protectedCampfireCenter;
    public Transform protectedTentCenter;

    [Header("Movement Settings")]
    public float stalkSpeed = 3.5f;
    public float attackSpeed = 8f;
    public float rotationSpeed = 5f;
    [Tooltip("When the player is this close, the monster approaches with a walk instead of a full sprint.")]
    public float closePlayerWalkRadius = 24f;
    [Tooltip("When close and facing a different way than the player, the monster copies the player's direction while walking toward them.")]
    public bool mimicPlayerFacingWhenClose = true;
    [Range(-1f, 1f)]
    public float sameFacingDotThreshold = 0.65f;
    public float stuckRepathDelay = 1.25f;
    [Tooltip("When true, nighttime spawns immediately run toward the player instead of hiding first.")]
    public bool chasePlayerOnNightSpawn = true;

    [Header("Grounding")]
    [Tooltip("Keeps the monster's feet on the terrain instead of floating above it.")]
    public bool snapToGround = true;
    public LayerMask groundMask = ~0;
    public float groundRayStartHeight = 6f;
    public float groundRayDistance = 24f;
    public float groundOffset = 0.02f;

    [Header("Stalking / Tree Hiding")]
    public float stalkDistance = 12f;
    public float minStalkDistance = 3f;
    public float hideSpotCheckRadius = 25f;
    public float minTreeDistance = 5f;
    public float maxTreeDistance = 20f;
    public float treeHidingOffset = 2.5f;

    [Header("Detection Settings")]
    public float detectionAngle = 45f;
    public float detectionDistance = 15f;
    public LayerMask lineOfSightLayers;

    [Header("Attack Settings")]
    public float attackDamage = 100f;
    public float attackDelay = 0.5f;
    public float lungeDistance = 3f;

    [Header("Protected Zones")]
    public float campfireProtectedRadius = 28f;
    public float tentProtectedRadius = 18f;
    public float protectedZoneBuffer = 1.5f;
    [Tooltip("Use the Campfire Safe Radius as the actual fire fear circle when a Campfire exists.")]
    public bool useCampfireSafeRadiusForFear = true;

    [Header("Animation Parameters")]
    public string speedParam = "Speed";
    public string attackTrigger = "Attack";
    public string idleStateName = "Idle";
    public string walkStateName = "Walk";
    public string runStateName = "Run";
    public string attackStateName = "Attack";
    public float animationCrossFade = 0.12f;
    public float walkAnimationSpeed = 1f;
    public float runAnimationSpeed = 1.18f;

    static readonly int IsAttackingHash = Animator.StringToHash("IsAttacking");

    Animator animator;
    NavMeshAgent navAgent;
    DayNightCycle dayNightCycle;
    Renderer[] cachedRenderers;
    Collider[] cachedColliders;

    enum MonsterState { Stalking, Chasing, Detected, Attacking }

    MonsterState currentState = MonsterState.Stalking;
    float attackTimer;
    bool isAttacking;
    bool isManifested = false;
    bool agentIsPlaced;
    bool hasSeenPlayer;
    bool jumpscareSequenceStarted;
    string currentAnimationState = string.Empty;
    float desiredMoveSpeed;
    float stuckTimer;

    Vector3? currentHideSpot;
    float nextSpotPickTime;

    void Awake()
    {
        animator = GetComponent<Animator>();
        navAgent = GetComponent<NavMeshAgent>();
        dayNightCycle = FindObjectOfType<DayNightCycle>();
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        cachedColliders = GetComponentsInChildren<Collider>(true);

        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
                playerCamera = player.GetComponentInChildren<Camera>()?.transform;
                playerHealth = player.GetComponent<Health>();
            }
        }

        if (navAgent != null)
        {
            navAgent.speed = stalkSpeed;
            navAgent.baseOffset = groundOffset;
            navAgent.autoBraking = false;
            navAgent.updateRotation = true;
            navAgent.enabled = false;
        }

        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.Normal;
        }

        if (campfire == null)
            campfire = FindObjectOfType<Campfire>();
        if (campfire != null && protectedCampfireCenter == null)
            protectedCampfireCenter = campfire.transform;

        if (lineOfSightLayers.value == 0)
            lineOfSightLayers = ~0;
    }

    void Start()
    {
        if (playerTransform == null)
        {
            Debug.LogError("MonsterAI: Player transform not found!");
            enabled = false;
            return;
        }

        if (animator == null)
        {
            Debug.LogError("MonsterAI: Animator component missing!");
            enabled = false;
            return;
        }

        if (navAgent == null)
        {
            Debug.LogError("MonsterAI: NavMeshAgent component missing!");
            enabled = false;
            return;
        }

        bool shouldManifest = dayNightCycle != null && !dayNightCycle.IsDay;
        if (shouldManifest)
        {
            SetManifested(true, true);
        }
        else
        {
            SetVisible(false);
            OnDespawn();
        }
    }

    void Update()
    {
        bool shouldManifest = dayNightCycle != null && !dayNightCycle.IsDay;
        if (shouldManifest != isManifested)
            SetManifested(shouldManifest, shouldManifest);

        if (!isManifested)
            return;

        EnforceProtectedZones();
        SnapToGroundIfNeeded();

        if (playerTransform == null)
            return;

        if (currentState == MonsterState.Stalking || currentState == MonsterState.Detected)
            BeginChasingPlayer();

        CheckForDetection();

        switch (currentState)
        {
            case MonsterState.Stalking:
                UpdateStalking();
                break;
            case MonsterState.Chasing:
                UpdateChasing();
                break;
            case MonsterState.Detected:
                UpdateDetected();
                break;
            case MonsterState.Attacking:
                UpdateAttacking();
                break;
        }

        UpdateAnimation();
    }

    void UpdateStalking()
    {
        if (!navAgent.enabled)
            return;

        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        if (distToPlayer <= minStalkDistance)
        {
            TriggerAttack();
            return;
        }

        bool needNewSpot = false;

        if (currentHideSpot == null)
        {
            needNewSpot = true;
        }
        else
        {
            float spotDistToPlayer = Vector3.Distance(currentHideSpot.Value, playerTransform.position);
            if (spotDistToPlayer > maxTreeDistance * 1.5f)
            {
                needNewSpot = true;
            }
            else if (navAgent.hasPath && !navAgent.pathPending &&
                     navAgent.remainingDistance <= navAgent.stoppingDistance + 0.5f &&
                     !navAgent.isStopped)
            {
                navAgent.isStopped = true;
                nextSpotPickTime = Time.time + Random.Range(2f, 4f);
            }

            if (navAgent.isStopped && Time.time >= nextSpotPickTime)
                needNewSpot = true;
        }

        if (!needNewSpot)
            return;

        PickNewHideSpot();
        if (currentHideSpot.HasValue && navAgent.enabled)
        {
            navAgent.isStopped = false;
            navAgent.SetDestination(currentHideSpot.Value);
            navAgent.speed = stalkSpeed;
        }
    }

    void PickNewHideSpot()
    {
        currentHideSpot = null;
        GameObject[] trees = GameObject.FindGameObjectsWithTag("tree");

        float bestScore = float.MaxValue;
        Vector3 bestSpot = Vector3.zero;
        bool found = false;

        if (trees.Length == 0)
        {
            Vector3 fallback = playerTransform.position - playerTransform.forward * 5f;
            fallback.y = transform.position.y;
            if (IsInsideProtectedZone(fallback))
                fallback = PushOutsideProtectedZones(fallback);
            currentHideSpot = fallback;
            nextSpotPickTime = Time.time + Random.Range(2f, 4f);
            return;
        }

        foreach (GameObject tree in trees)
        {
            Vector3 treePos = tree.transform.position;
            float distPlayerToTree = Vector3.Distance(playerTransform.position, treePos);

            if (distPlayerToTree < minTreeDistance || distPlayerToTree > maxTreeDistance)
                continue;

            Vector3 dirFromPlayer = (treePos - playerTransform.position).normalized;
            Vector3 hidePos = treePos + dirFromPlayer * treeHidingOffset;
            hidePos.y = transform.position.y;

            NavMeshHit hit;
            if (!NavMesh.SamplePosition(hidePos, out hit, 2f, NavMesh.AllAreas))
                continue;

            hidePos = hit.position;
            if (IsInsideProtectedZone(hidePos))
                continue;

            float distToPlayer = Vector3.Distance(playerTransform.position, hidePos);
            float currentDistToPlayer = Vector3.Distance(transform.position, playerTransform.position);

            if (distToPlayer >= currentDistToPlayer)
                continue;

            if (distToPlayer < bestScore)
            {
                bestScore = distToPlayer;
                bestSpot = hidePos;
                found = true;
            }
        }

        Vector3 destination = found ? bestSpot : playerTransform.position - playerTransform.forward * 5f;
        destination.y = transform.position.y;
        if (IsInsideProtectedZone(destination))
            destination = PushOutsideProtectedZones(destination);
        currentHideSpot = destination;

        if (navAgent != null && navAgent.enabled)
            navAgent.SetDestination(destination);

        nextSpotPickTime = Time.time + Random.Range(3f, 6f);
    }

    void TriggerAttack()
    {
        if (jumpscareSequenceStarted)
            return;

        jumpscareSequenceStarted = true;

        if (jumpscareManager == null)
            jumpscareManager = FindObjectOfType<JumpscareManager>();

        if (jumpscareManager != null)
            jumpscareManager.TriggerJumpscare(gameObject);

        currentState = MonsterState.Attacking;
        isAttacking = true;

        if (animator != null)
        {
            animator.speed = 1f;
            animator.ResetTrigger(attackTrigger);
            animator.SetTrigger(attackTrigger);
            animator.SetBool(IsAttackingHash, true);
            PlayAnimationState(attackStateName, true);
        }

        if (navAgent != null && navAgent.enabled)
        {
            navAgent.isStopped = true;
            if (playerTransform != null)
                navAgent.SetDestination(playerTransform.position);
            navAgent.speed = attackSpeed;
        }
    }

    void UpdateChasing()
    {
        if (playerTransform == null)
            return;

        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        if (!IsPlayerInFireFearRadius() && distToPlayer <= lungeDistance)
        {
            TriggerAttack();
            return;
        }

        Vector3 destination = GetChaseDestination();
        float moveSpeed = GetChaseMoveSpeed(distToPlayer);
        desiredMoveSpeed = moveSpeed;
        bool closeToPlayer = distToPlayer <= Mathf.Max(lungeDistance, closePlayerWalkRadius);
        if (navAgent != null && navAgent.enabled && agentIsPlaced)
        {
            navAgent.isStopped = false;
            navAgent.speed = moveSpeed;
            navAgent.SetDestination(destination);
            UpdateCloseFacingBehavior(closeToPlayer, destination);
            RecoverIfStuck(destination, distToPlayer);
        }
        else
        {
            UpdateCloseFacingBehavior(closeToPlayer, destination);
            transform.position = Vector3.MoveTowards(transform.position, destination, moveSpeed * Time.deltaTime);
        }
    }

    void UpdateCloseFacingBehavior(bool closeToPlayer, Vector3 destination)
    {
        if (!mimicPlayerFacingWhenClose || playerTransform == null)
        {
            SetAgentRotationEnabled(true);
            RotateTowardDestination(destination);
            return;
        }

        if (!closeToPlayer || IsFacingSameDirectionAsPlayer())
        {
            SetAgentRotationEnabled(true);
            RotateTowardDestination(destination);
            return;
        }

        SetAgentRotationEnabled(false);
        RotateTowardPlayerFacing();
    }

    bool IsFacingSameDirectionAsPlayer()
    {
        Vector3 monsterForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 playerForward = Vector3.ProjectOnPlane(playerTransform.forward, Vector3.up).normalized;
        if (monsterForward.sqrMagnitude < 0.001f || playerForward.sqrMagnitude < 0.001f)
            return true;

        return Vector3.Dot(monsterForward, playerForward) >= sameFacingDotThreshold;
    }

    void RotateTowardPlayerFacing()
    {
        Vector3 playerForward = Vector3.ProjectOnPlane(playerTransform.forward, Vector3.up);
        if (playerForward.sqrMagnitude < 0.001f)
            return;

        RotateTowardDirection(playerForward);
    }

    void RotateTowardDestination(Vector3 destination)
    {
        if (navAgent != null && navAgent.enabled && agentIsPlaced && navAgent.updateRotation)
            return;

        Vector3 direction = Vector3.ProjectOnPlane(destination - transform.position, Vector3.up);
        if (direction.sqrMagnitude < 0.001f)
            return;

        RotateTowardDirection(direction);
    }

    void RotateTowardDirection(Vector3 direction)
    {
        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Mathf.Max(0.1f, rotationSpeed) * Time.deltaTime);
    }

    void SetAgentRotationEnabled(bool enabled)
    {
        if (navAgent != null && navAgent.enabled)
            navAgent.updateRotation = enabled;
    }

    float GetChaseMoveSpeed(float distanceToPlayer)
    {
        if (distanceToPlayer <= Mathf.Max(lungeDistance, closePlayerWalkRadius))
            return hasSeenPlayer ? attackSpeed : stalkSpeed;

        return attackSpeed;
    }

    void RecoverIfStuck(Vector3 destination, float distanceToPlayer)
    {
        if (navAgent == null || !navAgent.enabled || !agentIsPlaced)
            return;

        if (distanceToPlayer <= lungeDistance + 0.5f)
        {
            stuckTimer = 0f;
            return;
        }

        bool wantsMovement = desiredMoveSpeed > 0.1f && !navAgent.pathPending;
        bool barelyMoving = navAgent.velocity.sqrMagnitude < 0.04f;
        bool hasBadPath = navAgent.pathStatus == NavMeshPathStatus.PathInvalid;

        if (!wantsMovement || (!barelyMoving && !hasBadPath))
        {
            stuckTimer = 0f;
            return;
        }

        stuckTimer += Time.deltaTime;
        if (stuckTimer < Mathf.Max(0.1f, stuckRepathDelay))
            return;

        stuckTimer = 0f;
        if (navAgent.isOnNavMesh)
            navAgent.ResetPath();

        if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 12f, NavMesh.AllAreas))
            navAgent.SetDestination(hit.position);
        else
            navAgent.SetDestination(destination);
    }

    void CheckForDetection()
    {
        if (currentState == MonsterState.Chasing)
            return;

        if (playerCamera == null)
            return;

        Vector3 directionToMonster = transform.position - playerCamera.position;
        float angle = Vector3.Angle(playerCamera.forward, directionToMonster);
        float distance = directionToMonster.magnitude;

        // `detectionAngle` is treated as the full cone angle in the inspector,
        // so compare against the half-angle here (cone half-angle).
        float halfAngle = detectionAngle * 0.5f;

        // Use strict greater-than checks so being exactly on the edge still counts as detected.
        if (angle > halfAngle || distance > detectionDistance)
            return;

        RaycastHit hit;
        // Raycast only as far as the measured distance to avoid hitting farther geometry.
        if (Physics.Raycast(playerCamera.position, directionToMonster.normalized, out hit, distance, lineOfSightLayers))
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                hasSeenPlayer = true;
                BeginChasingPlayer();
            }
        }
    }

    void UpdateDetected()
    {
        attackTimer -= Time.deltaTime;
        if (attackTimer > 0f)
            return;

        hasSeenPlayer = true;
        BeginChasingPlayer();
    }

    void UpdateAttacking()
    {
        if (playerTransform == null)
            return;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        if (distanceToPlayer <= lungeDistance && isAttacking)
        {
            isAttacking = false;
        }
    }

    void UpdateAnimation()
    {
        if (animator == null || currentState == MonsterState.Attacking)
            return;

        float speed = 0f;
        if (navAgent != null && navAgent.enabled && !navAgent.isStopped)
            speed = Mathf.Max(navAgent.velocity.magnitude, navAgent.desiredVelocity.magnitude, desiredMoveSpeed);
        else if (currentState == MonsterState.Chasing)
            speed = desiredMoveSpeed > 0f ? desiredMoveSpeed : attackSpeed;

        int moveState = 0;
        bool wantsRun = currentState == MonsterState.Chasing && desiredMoveSpeed > stalkSpeed + 0.25f;
        if (wantsRun)
            moveState = 2;
        else if (speed > 0.1f)
            moveState = 1;

        animator.SetInteger(speedParam, moveState);
        animator.speed = moveState >= 2 ? Mathf.Max(0.1f, runAnimationSpeed) : Mathf.Max(0.1f, walkAnimationSpeed);

        string targetState = idleStateName;
        if (moveState >= 2)
            targetState = runStateName;
        else if (moveState == 1)
            targetState = walkStateName;

        PlayAnimationState(targetState);
    }

    public void OnAttackHit()
    {
        if (playerHealth != null && isAttacking)
        {
            isAttacking = false;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision != null && IsPlayerCollider(collision.collider))
            TriggerAttack();
    }

    void OnTriggerEnter(Collider other)
    {
        if (IsPlayerCollider(other))
            TriggerAttack();
    }

    bool IsPlayerCollider(Collider other)
    {
        if (other == null)
            return false;

        if (playerTransform != null && other.transform.root == playerTransform.root)
            return true;

        if (other.CompareTag("Player"))
            return true;

        Health touchedHealth = other.GetComponentInParent<Health>();
        return touchedHealth != null && touchedHealth == playerHealth;
    }

    public void OnAttackComplete()
    {
        if (currentState != MonsterState.Attacking)
            return;

        currentState = MonsterState.Chasing;
        isAttacking = false;
        animator.ResetTrigger(attackTrigger);
        animator.SetBool(IsAttackingHash, false);
        currentAnimationState = string.Empty;
        BeginChasingPlayer();
        UpdateAnimation();
    }

    bool IsPlayerInFireFearRadius()
    {
        if (playerTransform == null)
            return false;

        if (DistanceXZ(playerTransform.position, protectedCampfireCenter) < GetCampfireFearRadius())
            return true;

        if (campfire == null || !campfire.IsSafeZoneActive)
            return false;

        float dist = Vector3.Distance(playerTransform.position, campfire.transform.position);
        return dist <= campfire.safeRadius;
    }

    public void InitializeMonster(Transform playerTransform, Camera playerCamera, Health playerHealth, JumpscareManager jumpscareManager, Campfire campfire)
    {
        if (playerTransform != null)
            this.playerTransform = playerTransform;

        if (playerCamera != null)
            this.playerCamera = playerCamera.transform;

        if (playerHealth != null)
            this.playerHealth = playerHealth;

        if (jumpscareManager != null)
            this.jumpscareManager = jumpscareManager;

        if (campfire != null)
        {
            this.campfire = campfire;
            protectedCampfireCenter = campfire.transform;
        }
    }

    public void ConfigureProtectedZones(Transform campfireCenter, float campfireRadius, Transform tentCenter, float tentRadius)
    {
        if (campfireCenter != null)
            protectedCampfireCenter = campfireCenter;

        if (campfireRadius > 0f)
            campfireProtectedRadius = campfireRadius;

        if (tentCenter != null)
            protectedTentCenter = tentCenter;

        if (tentRadius > 0f)
            tentProtectedRadius = tentRadius;
    }

    public void SpawnNearPlayer(float minDist = 10f, float maxDist = 25f)
    {
        SpawnAtRandomDistance(minDist, maxDist, false);
    }

    public void SpawnRandomAroundMap(float minDist = 70f, float maxDist = 160f)
    {
        SpawnAtRandomDistance(minDist, maxDist, chasePlayerOnNightSpawn);
    }

    void SpawnAtRandomDistance(float minDist, float maxDist, bool startChasing)
    {
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                playerTransform = playerObj.transform;
        }

        if (playerTransform == null)
            return;

        if (!HasUsableNavMesh())
        {
            Debug.LogWarning("MonsterAI: No valid NavMesh found in the scene; spawning the monster without pathfinding.");
            agentIsPlaced = false;
            if (navAgent != null)
                navAgent.enabled = false;

            if (TryFindFallbackSpawnPosition(minDist, maxDist, out Vector3 fallbackSpawn))
            {
                transform.position = fallbackSpawn;
                SetVisible(true);
                isManifested = true;
                BeginChasingPlayer();
            }
            return;
        }

        if (!TryFindSpawnPosition(minDist, maxDist, out Vector3 spawnPos))
        {
            Debug.LogWarning("MonsterAI: Could not find a nearby NavMesh spawn point for the monster; using fallback placement.");
            if (TryFindFallbackSpawnPosition(minDist, maxDist, out spawnPos))
            {
                transform.position = spawnPos;
                // Make sure renderers are visible even if navmesh placement failed
                SetVisible(true);
                EnsureRenderersEnabled();
            }
            else
            {
                agentIsPlaced = false;
                if (navAgent != null)
                    navAgent.enabled = false;
                return;
            }
        }

        transform.position = spawnPos;
        // Ensure monster is visible immediately after placement
        SetVisible(true);
        EnsureRenderersEnabled();

        if (navAgent != null)
        {
            if (navAgent.enabled)
                navAgent.enabled = false;

            navAgent.enabled = true;
            navAgent.baseOffset = groundOffset;
            bool warped = navAgent.Warp(spawnPos);
            if (!warped)
            {
                Debug.LogWarning("MonsterAI: Failed to warp NavMeshAgent to the chosen spawn point. Retrying near player.");

                if (!TryFindSpawnPosition(2f, maxDist, out spawnPos))
                {
                    agentIsPlaced = false;
                    navAgent.enabled = false;
                    return;
                }

                transform.position = spawnPos;
                warped = navAgent.Warp(spawnPos);
            }

            if (!warped)
            {
                Debug.LogError("MonsterAI: Failed to warp NavMeshAgent to NavMesh after retry.");
                agentIsPlaced = false;
                navAgent.enabled = false;
                return;
            }

            agentIsPlaced = true;
            SnapToGroundIfNeeded();
            navAgent.isStopped = false;
            navAgent.speed = startChasing ? attackSpeed : stalkSpeed;
        }

        isManifested = true;
        currentState = startChasing ? MonsterState.Chasing : MonsterState.Stalking;
        isAttacking = false;
        attackTimer = 0f;
        currentHideSpot = null;
        nextSpotPickTime = 0f;

        if (animator != null)
        {
            animator.Rebind();
            animator.Update(0f);
            animator.ResetTrigger(attackTrigger);
            animator.SetBool(IsAttackingHash, false);
            animator.SetInteger(speedParam, 0);
            currentAnimationState = string.Empty;
            PlayAnimationState(idleStateName, true);
        }

        if (startChasing)
            BeginChasingPlayer();
    }

    public void BeginChasingPlayer()
    {
        if (playerTransform == null)
            return;

        hasSeenPlayer = true;
        isManifested = true;
        currentState = MonsterState.Chasing;
        currentHideSpot = null;
        isAttacking = false;
        attackTimer = 0f;
        jumpscareSequenceStarted = false;

        if (animator != null)
        {
            animator.ResetTrigger(attackTrigger);
            animator.SetBool(IsAttackingHash, false);
            currentAnimationState = string.Empty;
        }

        if (navAgent != null && navAgent.enabled && agentIsPlaced)
        {
            navAgent.isStopped = false;
            navAgent.baseOffset = groundOffset;
            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            desiredMoveSpeed = GetChaseMoveSpeed(distanceToPlayer);
            navAgent.speed = desiredMoveSpeed;
            navAgent.SetDestination(GetChaseDestination());
        }
        else
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            desiredMoveSpeed = GetChaseMoveSpeed(distanceToPlayer);
        }
    }

    Vector3 GetChaseDestination()
    {
        if (playerTransform == null)
            return transform.position;

        Vector3 destination = playerTransform.position;
        if (IsPlayerInFireFearRadius())
            destination = PushOutsideZoneFromReference(destination, protectedCampfireCenter, GetCampfireFearRadius() + protectedZoneBuffer, transform.position);
        destination = PushOutsideZoneFromReference(destination, protectedTentCenter, tentProtectedRadius + protectedZoneBuffer, transform.position);
        return destination;
    }

    bool TryFindSpawnPosition(float minDist, float maxDist, out Vector3 spawnPos)
    {
        Vector3 playerPosition = playerTransform.position;
        float searchRadius = Mathf.Max(1f, maxDist * 0.35f);

        for (int attempt = 0; attempt < 36; attempt++)
        {
            Vector2 circle = Random.insideUnitCircle.normalized;
            if (circle.sqrMagnitude < 0.0001f)
                circle = Random.insideUnitCircle.normalized;

            float distance = Random.Range(minDist, maxDist);
            Vector3 candidate = playerPosition + new Vector3(circle.x, 0f, circle.y) * distance;

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, searchRadius, NavMesh.AllAreas))
            {
                if (IsInsideProtectedZone(hit.position))
                    continue;

                Vector3 flatHit = hit.position;
                flatHit.y = hit.position.y;
                float playerDistance = Vector3.Distance(new Vector3(flatHit.x, 0f, flatHit.z), new Vector3(playerPosition.x, 0f, playerPosition.z));
                if (playerDistance >= minDist * 0.75f)
                {
                    spawnPos = hit.position;
                    return true;
                }
            }
        }

        if (NavMesh.SamplePosition(playerPosition, out NavMeshHit nearbyHit, maxDist, NavMesh.AllAreas))
        {
            Vector3 flatHit = nearbyHit.position;
            float playerDistance = Vector3.Distance(new Vector3(flatHit.x, 0f, flatHit.z), new Vector3(playerPosition.x, 0f, playerPosition.z));
            if (!IsInsideProtectedZone(nearbyHit.position) && playerDistance >= minDist * 0.75f)
            {
                spawnPos = nearbyHit.position;
                return true;
            }
        }

        spawnPos = default;
        return false;
    }

    bool TryFindFallbackSpawnPosition(float minDist, float maxDist, out Vector3 spawnPos)
    {
        Vector3 playerPosition = playerTransform.position;

        for (int attempt = 0; attempt < 24; attempt++)
        {
            Vector2 circle = Random.insideUnitCircle.normalized;
            if (circle.sqrMagnitude < 0.0001f)
                circle = Vector2.right;

            float distance = Random.Range(minDist, maxDist);
            Vector3 candidate = playerPosition + new Vector3(circle.x, 0f, circle.y) * distance;
            float sampleRadius = Mathf.Max(8f, maxDist * 0.2f);

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas) && !IsInsideProtectedZone(hit.position))
            {
                spawnPos = hit.position;
                return true;
            }
        }

        // Try a direct player-facing fallback if no distant navmesh sample exists.
        spawnPos = playerPosition + (playerTransform.forward * Mathf.Max(minDist, 5f));
        spawnPos.y = playerTransform.position.y;
        spawnPos = PushOutsideProtectedZones(spawnPos);
        return true;
    }

    bool IsInsideProtectedZone(Vector3 position)
    {
        return DistanceXZ(position, protectedCampfireCenter) < GetCampfireFearRadius() ||
               DistanceXZ(position, protectedTentCenter) < tentProtectedRadius;
    }

    Vector3 PushOutsideProtectedZones(Vector3 position)
    {
        position = PushOutsideZoneFromReference(position, protectedCampfireCenter, GetCampfireFearRadius() + protectedZoneBuffer, transform.position);
        position = PushOutsideZoneFromReference(position, protectedTentCenter, tentProtectedRadius + protectedZoneBuffer, transform.position);
        return position;
    }

    float GetCampfireFearRadius()
    {
        if (useCampfireSafeRadiusForFear && campfire != null && campfire.safeRadius > 0f)
            return campfire.safeRadius;

        return campfireProtectedRadius;
    }

    Vector3 PushOutsideZone(Vector3 position, Transform center, float radius)
    {
        return PushOutsideZoneFromReference(position, center, radius, playerTransform != null ? playerTransform.position : transform.position);
    }

    Vector3 PushOutsideZoneFromReference(Vector3 position, Transform center, float radius, Vector3 referencePosition)
    {
        if (center == null || radius <= 0f)
            return position;

        Vector3 centerPos = center.position;
        Vector3 flatDelta = new Vector3(position.x - centerPos.x, 0f, position.z - centerPos.z);
        if (flatDelta.sqrMagnitude >= radius * radius)
            return position;

        if (flatDelta.sqrMagnitude < 0.001f)
        {
            Vector3 awayFromReference = referencePosition - centerPos;
            flatDelta = new Vector3(awayFromReference.x, 0f, awayFromReference.z);
            if (flatDelta.sqrMagnitude < 0.001f)
                flatDelta = Vector3.forward;
        }

        flatDelta.Normalize();
        Vector3 result = centerPos + flatDelta * radius;
        result.y = position.y;

        if (NavMesh.SamplePosition(result, out NavMeshHit hit, 8f, NavMesh.AllAreas) && !IsInsideProtectedZone(hit.position))
            result = hit.position;

        return result;
    }

    void SnapToGroundIfNeeded()
    {
        if (!snapToGround)
            return;

        if (navAgent != null)
            navAgent.baseOffset = groundOffset;

        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
            return;

        Vector3 origin = transform.position + Vector3.up * Mathf.Max(0.1f, groundRayStartHeight);
        float distance = Mathf.Max(0.1f, groundRayStartHeight + groundRayDistance);
        if (!TryFindGroundBelow(origin, distance, out RaycastHit hit))
            return;

        Vector3 grounded = transform.position;
        grounded.y = hit.point.y + groundOffset;
        transform.position = grounded;
    }

    bool TryFindGroundBelow(Vector3 origin, float distance, out RaycastHit bestHit)
    {
        bestHit = default;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, distance, groundMask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
            return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;
            if (hit.collider.transform.root == transform.root)
                continue;

            bestHit = hit;
            return true;
        }

        return false;
    }

    float DistanceXZ(Vector3 position, Transform center)
    {
        if (center == null)
            return float.MaxValue;

        Vector3 c = center.position;
        float dx = position.x - c.x;
        float dz = position.z - c.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    void EnforceProtectedZones()
    {
        if (!IsInsideProtectedZone(transform.position))
            return;

        Vector3 safePosition = PushOutsideProtectedZones(transform.position);
        if (navAgent != null && navAgent.enabled)
        {
            if (navAgent.isOnNavMesh)
                navAgent.ResetPath();
            bool warped = navAgent.Warp(safePosition);
            if (!warped)
                transform.position = safePosition;
            navAgent.isStopped = false;
        }
        else
        {
            transform.position = safePosition;
        }

        currentHideSpot = null;
        currentState = MonsterState.Chasing;
        isAttacking = false;
        if (animator != null)
        {
            animator.ResetTrigger(attackTrigger);
            animator.SetBool(IsAttackingHash, false);
        }

        BeginChasingPlayer();
    }

    bool HasUsableNavMesh()
    {
        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
        if (triangulation.vertices != null && triangulation.vertices.Length > 0)
            return true;

        if (playerTransform != null && NavMesh.SamplePosition(playerTransform.position, out _, 12f, NavMesh.AllAreas))
            return true;

        return NavMesh.SamplePosition(transform.position, out _, 12f, NavMesh.AllAreas);
    }

    public void OnDespawn()
    {
        agentIsPlaced = false;
        currentState = MonsterState.Stalking;
        isAttacking = false;
        hasSeenPlayer = false;
        jumpscareSequenceStarted = false;
        currentHideSpot = null;
        nextSpotPickTime = 0f;

        if (navAgent != null && navAgent.enabled)
        {
            if (navAgent.isOnNavMesh)
                navAgent.ResetPath();

            navAgent.enabled = false;
        }

        if (animator != null)
        {
            animator.ResetTrigger(attackTrigger);
            animator.SetBool(IsAttackingHash, false);
            animator.SetInteger(speedParam, 0);
            currentAnimationState = string.Empty;
            PlayAnimationState(idleStateName, true);
        }
    }

    void SetManifested(bool manifest, bool respawnNearPlayer)
    {
        if (isManifested == manifest)
            return;

        isManifested = manifest;

        if (!manifest)
        {
            OnDespawn();
            SetVisible(false);
            return;
        }

        SetVisible(true);
        if (respawnNearPlayer)
            SpawnRandomAroundMap();
    }

    void SetVisible(bool visible)
    {
        if (cachedRenderers != null)
        {
            foreach (Renderer rendererComponent in cachedRenderers)
            {
                if (rendererComponent != null)
                    rendererComponent.enabled = visible;
            }
        }

        if (cachedColliders != null)
        {
            foreach (Collider colliderComponent in cachedColliders)
            {
                if (colliderComponent != null)
                    colliderComponent.enabled = visible;
            }
        }
    }

    void EnsureRenderersEnabled()
    {
        if (cachedRenderers == null || cachedRenderers.Length == 0)
            cachedRenderers = GetComponentsInChildren<Renderer>(true);

        if (cachedRenderers != null)
        {
            foreach (Renderer r in cachedRenderers)
            {
                if (r == null) continue;
                try
                {
                    r.enabled = true;
                }
                catch { }
            }
        }

        if (cachedColliders == null || cachedColliders.Length == 0)
            cachedColliders = GetComponentsInChildren<Collider>(true);

        if (cachedColliders != null)
        {
            foreach (Collider c in cachedColliders)
            {
                if (c == null) continue;
                try
                {
                    c.enabled = true;
                }
                catch { }
            }
        }
    }

    // Public helper to force the monster visible from external callers (manager/test hooks)
    public void EnsureVisible()
    {
        gameObject.SetActive(true);
        SetVisible(true);
        EnsureRenderersEnabled();
    }
    void PlayAnimationState(string stateName, bool force = false)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
            return;

        int stateHash = Animator.StringToHash(stateName);
        if (!force && currentAnimationState == stateName)
            return;

        if (!animator.HasState(0, stateHash))
            return;

        animator.CrossFade(stateHash, Mathf.Max(0f, animationCrossFade), 0);
        currentAnimationState = stateName;
    }

    void OnDrawGizmosSelected()
    {
        if (playerTransform == null)
            return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionDistance);

        if (playerCamera != null)
        {
            Gizmos.color = Color.red;
            Vector3 direction = transform.position - playerCamera.position;
            float halfAngle = detectionAngle * 0.5f;
            Quaternion leftRotation = Quaternion.AngleAxis(-halfAngle, Vector3.up);
            Quaternion rightRotation = Quaternion.AngleAxis(halfAngle, Vector3.up);
            Vector3 leftDirection = leftRotation * playerCamera.forward;
            Vector3 rightDirection = rightRotation * playerCamera.forward;
            Gizmos.DrawLine(playerCamera.position, playerCamera.position + leftDirection * detectionDistance);
            Gizmos.DrawLine(playerCamera.position, playerCamera.position + rightDirection * detectionDistance);
        }
    }
}
