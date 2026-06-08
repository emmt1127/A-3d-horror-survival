
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Manages jumpscare effects including camera shake, audio, and player death.
/// Call TriggerJumpscare() to start the jumpscare sequence.
/// </summary>
public class JumpscareManager : MonoBehaviour
{
    public static JumpscareManager Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState()
    {
        Instance = null;
    }

    [Header("References")]
    [Tooltip("The main camera to shake")]
    public Camera mainCamera;

    [Tooltip("The monster GameObject to show during jumpscare")]
    public GameObject monsterObject;

    [Tooltip("The player's Health component")]
    public Health playerHealth;

    [Header("Jumpscare Settings")]
    [Tooltip("Duration of the jumpscare in seconds")]
    public float jumpscareDuration = 5f;

    [Tooltip("How long the camera takes to leave the player and move in front of the monster.")]
    public float cameraMoveDuration = 0.35f;

    [Tooltip("Distance in front of the monster for the jumpscare camera.")]
    public float cameraDistanceFromMonster = 2.8f;

    [Tooltip("Height of the jumpscare camera above the monster position.")]
    public float cameraHeight = 3.25f;

    [Tooltip("Height on the monster that the jumpscare camera looks at.")]
    public float monsterLookHeight = 2.85f;

    [Tooltip("How long to wait before starting the camera shake")]
    public float shakeDelay = 0.1f;

    [Tooltip("Intensity of the camera shake")]
    public float shakeIntensity = 0.5f;

    [Tooltip("Speed of the camera shake")]
    public float shakeSpeed = 20f;

    [Header("Audio")]
    [Tooltip("Jumpscare sound effect")]
    public AudioClip jumpscareSound;

    [Tooltip("Volume for the jumpscare sound")]
    [Range(0f, 1f)]
    public float jumpscareVolume = 1f;

    [Tooltip("Audio source for playing jumpscare sound (will create if null)")]
    public AudioSource audioSource;

    [Header("Events")]
    [Tooltip("Fired when jumpscare starts")]
    public UnityEvent onJumpscareStart;

    [Tooltip("Fired when jumpscare ends")]
    public UnityEvent onJumpscareEnd;

    // Internal state
    private bool isJumpscareActive = false;
    private float jumpscareTimer = 0f;
    private float shakeTimer = 0f;
    private Transform originalCameraParent;
    private int originalCameraSiblingIndex;
    private Vector3 originalCameraPosition;
    private Quaternion originalCameraRotation;
    private Vector3 jumpscareStartPosition;
    private Quaternion jumpscareStartRotation;
    private Vector3 jumpscareCameraPosition;
    private Quaternion jumpscareCameraRotation;
    private Vector3 jumpscareBasePosition;
    private Quaternion jumpscareBaseRotation;
    private bool monsterWasActive = false;
    private bool hasJumpscareCameraShot = false;
    private bool jumpscareEffectsStarted = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        // Auto-find references if not assigned
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (playerHealth == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerHealth = player.GetComponent<Health>();
        }

        // Create audio source if needed
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
    }

    void Start()
    {
        // Validate references
        if (mainCamera == null)
        {
            Debug.LogError("JumpscareManager: No camera assigned or found!");
            enabled = false;
        }
    }

    void Update()
    {
        if (!isJumpscareActive)
            return;

        // Update jumpscare timer
        jumpscareTimer += Time.deltaTime;

        if (!jumpscareEffectsStarted && jumpscareTimer >= GetCameraMoveDuration())
            StartJumpscareEffects();

        float effectTimer = Mathf.Max(0f, jumpscareTimer - GetCameraMoveDuration());

        // Start camera shake after the camera has reached the monster shot.
        if (jumpscareEffectsStarted && effectTimer >= shakeDelay && shakeTimer == 0f)
        {
            shakeTimer = shakeDelay;
        }

        // Apply camera shake
        if (shakeTimer > 0f)
        {
            ApplyCameraShake();
            shakeTimer += Time.deltaTime;
        }
        else
        {
            if (hasJumpscareCameraShot)
                UpdateJumpscareCameraMove();
        }

        // End jumpscare
        if (jumpscareEffectsStarted && effectTimer >= jumpscareDuration)
        {
            EndJumpscare();
        }
    }

    /// <summary>
    /// Triggers the jumpscare sequence.
    /// </summary>
    public void TriggerJumpscare()
    {
        if (isJumpscareActive)
            return;

        RefreshSceneReferences();
        isJumpscareActive = true;
        jumpscareTimer = 0f;
        shakeTimer = 0f;
        hasJumpscareCameraShot = false;
        jumpscareEffectsStarted = false;

        // Store original camera transform
        if (mainCamera != null)
        {
            originalCameraParent = mainCamera.transform.parent;
            originalCameraSiblingIndex = mainCamera.transform.GetSiblingIndex();
            originalCameraPosition = mainCamera.transform.localPosition;
            originalCameraRotation = mainCamera.transform.localRotation;
            jumpscareStartPosition = mainCamera.transform.position;
            jumpscareStartRotation = mainCamera.transform.rotation;
        }

        // Show monster if assigned
        if (monsterObject != null)
        {
            monsterWasActive = monsterObject.activeSelf;
            monsterObject.SetActive(true);
        }

        MoveCameraToMonsterJumpscareShot();
    }

    /// <summary>
    /// Triggers the jumpscare with a specific monster object.
    /// </summary>
    public void TriggerJumpscare(GameObject monster)
    {
        monsterObject = monster;
        TriggerJumpscare();
    }

    private void ApplyCameraShake()
    {
        if (mainCamera == null)
            return;

        if (hasJumpscareCameraShot)
            UpdateJumpscareCameraMove();
        else
        {
            jumpscareBasePosition = mainCamera.transform.position;
            jumpscareBaseRotation = mainCamera.transform.rotation;
        }

        // Calculate shake offset using sine waves for back-and-forth motion
        float time = shakeTimer * shakeSpeed;

        // Horizontal shake (left-right)
        float xOffset = Mathf.Sin(time) * shakeIntensity;

        // Vertical shake (up-down with different frequency)
        float yOffset = Mathf.Sin(time * 1.5f) * (shakeIntensity * 0.5f);

        // Rotation shake (slight rotation)
        float zRotation = Mathf.Sin(time * 0.8f) * (shakeIntensity * 2f);

        Vector3 shakePosition = jumpscareBasePosition + mainCamera.transform.right * xOffset + mainCamera.transform.up * yOffset;
        Quaternion shakeRotation = jumpscareBaseRotation * Quaternion.Euler(0f, 0f, zRotation);

        mainCamera.transform.position = shakePosition;
        mainCamera.transform.rotation = shakeRotation;
    }

    private void EndJumpscare()
    {
        isJumpscareActive = false;

        // Restore camera transform
        if (mainCamera != null)
        {
            mainCamera.transform.SetParent(originalCameraParent, false);
            if (originalCameraParent != null)
                mainCamera.transform.SetSiblingIndex(Mathf.Clamp(originalCameraSiblingIndex, 0, originalCameraParent.childCount - 1));
            mainCamera.transform.localPosition = originalCameraPosition;
            mainCamera.transform.localRotation = originalCameraRotation;
        }

        // Kill player
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(playerHealth.maxHealth);
        }

        // Restore monster active state
        if (monsterObject != null)
        {
            monsterObject.SetActive(monsterWasActive);
        }

        // Fire end event
        onJumpscareEnd?.Invoke();
    }

    void StartJumpscareEffects()
    {
        if (jumpscareEffectsStarted)
            return;

        jumpscareEffectsStarted = true;

        if (audioSource != null && jumpscareSound != null)
        {
            audioSource.clip = jumpscareSound;
            audioSource.volume = jumpscareVolume;
            audioSource.Play();
        }

        onJumpscareStart?.Invoke();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void RefreshSceneReferences()
    {
        if (mainCamera == null || !mainCamera.gameObject.scene.IsValid())
            mainCamera = Camera.main;

        if (playerHealth == null || !playerHealth.gameObject.scene.IsValid())
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerHealth = player.GetComponent<Health>();
        }
    }

    void MoveCameraToMonsterJumpscareShot()
    {
        if (mainCamera == null || monsterObject == null)
            return;

        Transform monsterTransform = monsterObject.transform;
        Vector3 lookPoint = GetMonsterLookPoint();
        Vector3 frontDirection = Vector3.ProjectOnPlane(monsterTransform.forward, Vector3.up);
        if (frontDirection.sqrMagnitude < 0.001f)
            frontDirection = Vector3.ProjectOnPlane(mainCamera.transform.position - monsterTransform.position, Vector3.up);
        if (frontDirection.sqrMagnitude < 0.001f)
            frontDirection = Vector3.forward;

        frontDirection.Normalize();
        jumpscareCameraPosition = monsterTransform.position + frontDirection * Mathf.Max(0.25f, cameraDistanceFromMonster) + Vector3.up * Mathf.Max(0f, cameraHeight);
        Vector3 lookDirection = lookPoint - jumpscareCameraPosition;
        if (lookDirection.sqrMagnitude < 0.001f)
            lookDirection = -frontDirection;

        jumpscareCameraRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        jumpscareBasePosition = jumpscareStartPosition;
        jumpscareBaseRotation = jumpscareStartRotation;
        hasJumpscareCameraShot = true;

        mainCamera.transform.SetParent(null, true);
        UpdateJumpscareCameraMove();
    }

    void UpdateJumpscareCameraMove()
    {
        if (mainCamera == null)
            return;

        float duration = GetCameraMoveDuration();
        float t = Mathf.Clamp01(jumpscareTimer / duration);
        t = t * t * (3f - 2f * t);

        jumpscareBasePosition = Vector3.Lerp(jumpscareStartPosition, jumpscareCameraPosition, t);
        jumpscareBaseRotation = Quaternion.Slerp(jumpscareStartRotation, jumpscareCameraRotation, t);
        mainCamera.transform.position = jumpscareBasePosition;
        mainCamera.transform.rotation = jumpscareBaseRotation;
    }

    Vector3 GetMonsterLookPoint()
    {
        if (monsterObject == null)
            return Vector3.zero;

        Renderer[] renderers = monsterObject.GetComponentsInChildren<Renderer>(true);
        if (renderers != null && renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    bounds.Encapsulate(renderers[i].bounds);
            }

            return new Vector3(bounds.center.x, Mathf.Lerp(bounds.center.y, bounds.max.y, 0.86f), bounds.center.z);
        }

        return monsterObject.transform.position + Vector3.up * Mathf.Max(0f, monsterLookHeight);
    }

    float GetCameraMoveDuration()
    {
        return Mathf.Max(0.01f, cameraMoveDuration);
    }

    void OnDrawGizmosSelected()
    {
        // Visualize shake intensity in scene view
        if (mainCamera != null)
        {
            Gizmos.color = Color.red;
            Vector3 center = mainCamera.transform.position;
            float size = shakeIntensity * 2f;
            Gizmos.DrawWireCube(center, new Vector3(size, size * 0.5f, 0.1f));
        }
    }
}
