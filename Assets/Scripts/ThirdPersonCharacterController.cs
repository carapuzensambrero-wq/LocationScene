using UnityEngine;

/// <summary>
/// Offline third-person controller with a CharacterController body.
/// Input and the public state are deliberately kept as simple data so the
/// movement layer can later be driven by FishNet state instead of local input.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public sealed class ThirdPersonCharacterController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private Animator animator;
    [SerializeField] private ThirdPersonCrosshairUI crosshairUI;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float walkSpeed = 2.5f;
    [SerializeField, Min(0f)] private float aimSpeed = 1.8f;
    [SerializeField, Min(0f)] private float acceleration = 12f;
    [SerializeField, Min(0f)] private float deceleration = 16f;
    [SerializeField, Min(0f)] private float rotationSpeed = 12f;
    [SerializeField, Min(0f)] private float gravity = 25f;
    [SerializeField] private bool rotateTowardsMovement = true;

    [Header("Third-person camera")]
    [Tooltip("Camera offset relative to the target: X = right/left, Y = up/down, Z = forward/back.")]
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 0f, -4f);
    [Tooltip("Additional local rotation of the camera around the target.")]
    [SerializeField] private Vector3 cameraRotationOffset;
    [SerializeField, Min(0.1f)] private float cameraDistance = 4f;
    [SerializeField, Min(0.1f)] private float cameraHeight = 1.5f;
    [SerializeField, Min(0f)] private float cameraFollowSharpness = 15f;
    [SerializeField, Min(0f)] private float cameraRotationSharpness = 18f;
    [Tooltip("Vertical camera limits in degrees. X = down, Y = up.")]
    [SerializeField] private Vector2 cameraPitchLimits = new Vector2(-35f, 70f);
    [SerializeField, Min(0f)] private float cameraCollisionRadius = 0.2f;
    [SerializeField, Min(0f)] private float cameraCollisionOffset = 0.05f;
    [SerializeField] private LayerMask cameraCollisionMask = Physics.DefaultRaycastLayers;
    [SerializeField, Min(0f)] private float lookSensitivity = 2.2f;
    [SerializeField, Min(0f)] private float cameraZoomSpeed = 5f;
    [SerializeField, Min(0.1f)] private float minimumCameraDistance = 1f;
    [SerializeField, Min(0.1f)] private float maximumCameraDistance = 8f;
    [SerializeField] private bool lockCursorInNormalMode = true;

    [Header("Aim")]
    [Tooltip("Aim camera offset relative to the target. It is blended with the normal offset.")]
    [SerializeField] private Vector3 aimCameraOffset = new Vector3(0.55f, 0.1f, -2.8f);
    [Tooltip("Additional local rotation while aiming.")]
    [SerializeField] private Vector3 aimCameraRotationOffset;
    [SerializeField] private bool aimWhileRightMouseHeld = true;
    [SerializeField, Min(0f)] private float aimCameraDistance = 2.8f;
    [SerializeField, Min(0.1f)] private float aimMinimumCameraDistance = 1f;
    [SerializeField, Min(0.1f)] private float aimMaximumCameraDistance = 4f;
    [SerializeField] private Vector3 aimShoulderOffset = new Vector3(0.55f, 0.1f, 0f);
    [SerializeField, Min(0.01f)] private float scopeDistanceThreshold = 1.05f;
    [SerializeField, Min(0f)] private float scopeForwardOffset = 0.25f;
    [SerializeField, Min(0.1f)] private float scopeFieldOfView = 25f;
    [SerializeField, Min(0.1f)] private float scopeMinimumFieldOfView = 10f;
    [SerializeField, Min(0.1f)] private float scopeMaximumFieldOfView = 25f;
    [SerializeField, Min(0f)] private float scopeZoomSpeed = 5f;
    [SerializeField, Min(0f)] private float aimTransitionSharpness = 12f;
    [SerializeField, Min(0f)] private float aimRotationSharpness = 14f;
    [SerializeField, Range(0f, 1f)] private float aimScreenDeadZone = 0.08f;
    [SerializeField, Min(0f)] private float aimYawAngle = 8f;
    [SerializeField, Min(0f)] private float aimPitchAngle = 5f;
    [SerializeField, Min(0.01f)] private float aimTurnSpeed = 10f;
    [SerializeField, Min(0.01f)] private float aimReticleTurnSpeed = 10f;
    [SerializeField, Min(0.01f)] private float aimReturnSpeed = 10f;
    [SerializeField, Range(0f, 1f)] private float aimDistanceSpeedInfluence = 0f;

    [Header("Animator parameters")]
    [SerializeField] private string moveXParameter = "MoveX";
    [SerializeField] private string moveYParameter = "MoveY";
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private string groundedParameter = "Grounded";
    [SerializeField] private string aimingParameter = "Aiming";
    [SerializeField] private string verticalSpeedParameter = "VerticalSpeed";

    private CharacterController characterController;
    private Vector3 planarVelocity;
    private float verticalVelocity;
    private float yaw;
    private float pitch = 12f;
    private float aimBlend;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool isAiming;
    private Vector2 virtualAimPosition;
    private Vector2 aimOffset;
    private float currentCameraDistance;
    private float currentAimCameraDistance;
    private float normalFieldOfView;
    private float currentScopeFieldOfView;

    public Vector2 MoveInput => moveInput;
    public Vector2 AimScreenPosition => virtualAimPosition;
    public bool IsAiming => isAiming;
    public bool IsOpticalScopeAiming => isAiming && currentAimCameraDistance <= aimMinimumCameraDistance + scopeDistanceThreshold;
    public bool IsGrounded => characterController != null && characterController.isGrounded;
    public Vector3 AimDirection => playerCamera != null ? playerCamera.transform.forward : transform.forward;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        if (playerCamera == null) playerCamera = Camera.main;
        if (cameraTarget == null) cameraTarget = transform;
        if (animator == null) animator = GetComponentInChildren<Animator>();

        currentCameraDistance = Mathf.Clamp(cameraDistance, minimumCameraDistance, maximumCameraDistance);
        currentAimCameraDistance = Mathf.Clamp(aimCameraDistance, aimMinimumCameraDistance, aimMaximumCameraDistance);
        if (playerCamera != null) normalFieldOfView = playerCamera.fieldOfView;
        currentScopeFieldOfView = Mathf.Clamp(scopeFieldOfView, scopeMinimumFieldOfView, scopeMaximumFieldOfView);

        yaw = transform.eulerAngles.y;
        if (playerCamera != null) yaw = playerCamera.transform.eulerAngles.y;
    }

    private void OnEnable()
    {
        ApplyCursorState(false);
    }

    private void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Update()
    {
        ReadInput();
        UpdateAimState();
        ApplyCursorState(isAiming);
        MoveCharacter();
        HandleFireInput();
        UpdateAnimator();
    }

    private void HandleFireInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            // Placeholder for the future weapon/fire command.
            if (crosshairUI != null) crosshairUI.NotifyShot();
        }
    }

    private void LateUpdate()
    {
        UpdateCamera();
    }

    private void ReadInput()
    {
        moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        moveInput = Vector2.ClampMagnitude(moveInput, 1f);
        lookInput = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));

        float zoom = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(zoom) > 0.0001f)
        {
            if (IsOpticalScopeAiming)
            {
                currentScopeFieldOfView = Mathf.Clamp(
                    currentScopeFieldOfView - zoom * scopeZoomSpeed,
                    scopeMinimumFieldOfView,
                    scopeMaximumFieldOfView);

                // Scrolling back to the widest scope view leaves optical mode.
                if (currentScopeFieldOfView >= scopeMaximumFieldOfView - 0.001f)
                    currentAimCameraDistance = aimMinimumCameraDistance + scopeDistanceThreshold + 0.001f;
            }
            else
            {
                currentCameraDistance = Mathf.Clamp(currentCameraDistance - zoom * cameraZoomSpeed, minimumCameraDistance, maximumCameraDistance);
                currentAimCameraDistance = Mathf.Clamp(currentAimCameraDistance - zoom * cameraZoomSpeed, aimMinimumCameraDistance, aimMaximumCameraDistance);
            }
        }

        // Outside aiming, the locked cursor controls a conventional orbit.
        if (!isAiming)
        {
            yaw += lookInput.x * lookSensitivity;
            pitch = Mathf.Clamp(pitch - lookInput.y * lookSensitivity, cameraPitchLimits.x, cameraPitchLimits.y);
        }
    }

    private void UpdateAimState()
    {
        bool requested = aimWhileRightMouseHeld && Input.GetMouseButton(1);
        if (requested && !isAiming)
        {
            aimOffset = Vector2.zero;
            virtualAimPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        isAiming = requested;
        float target = requested ? 1f : 0f;
        aimBlend = Mathf.MoveTowards(aimBlend, target, aimTransitionSharpness * Time.deltaTime);
    }

    private void ApplyCursorState(bool aiming)
    {
        if (aiming)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = false;
            return;
        }

        Cursor.lockState = lockCursorInNormalMode ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !lockCursorInNormalMode;
    }

    private void MoveCharacter()
    {
        if (playerCamera == null) return;

        Vector3 forward = playerCamera.transform.forward;
        Vector3 right = playerCamera.transform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 desiredDirection = right * moveInput.x + forward * moveInput.y;
        float targetSpeed = (isAiming ? aimSpeed : walkSpeed) * Mathf.Clamp01(moveInput.magnitude);
        Vector3 desiredVelocity = desiredDirection * targetSpeed;
        float changeRate = desiredVelocity.sqrMagnitude > planarVelocity.sqrMagnitude ? acceleration : deceleration;
        planarVelocity = Vector3.MoveTowards(planarVelocity, desiredVelocity, changeRate * Time.deltaTime);

        if (characterController.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        verticalVelocity -= gravity * Time.deltaTime;

        Vector3 motion = planarVelocity + Vector3.up * verticalVelocity;
        characterController.Move(motion * Time.deltaTime);

        Vector3 facing = isAiming ? playerCamera.transform.forward : (rotateTowardsMovement ? desiredDirection : Vector3.zero);
        facing.y = 0f;
        if (facing.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(facing);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, aimBlend > 0.5f ? aimRotationSharpness : rotationSpeed * 4f * Time.deltaTime);
        }
    }

    private void UpdateCamera()
    {
        if (playerCamera == null || cameraTarget == null) return;

        Vector3 focus = cameraTarget.position + Vector3.up * cameraHeight;
        Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
        bool scopeMode = isAiming && currentAimCameraDistance <= aimMinimumCameraDistance + scopeDistanceThreshold;
        float distance = Mathf.Lerp(currentCameraDistance, currentAimCameraDistance, aimBlend);
        Vector3 normalOffset = cameraOffset;
        normalOffset.z = -distance;
        Vector3 targetAimOffset = aimCameraOffset;
        targetAimOffset.z = -distance;
        Vector3 blendedOffset = Vector3.Lerp(normalOffset, targetAimOffset, aimBlend);
        blendedOffset += scopeMode ? Vector3.forward * scopeForwardOffset : Vector3.zero;
        blendedOffset += Vector3.Lerp(Vector3.zero, aimShoulderOffset, aimBlend);
        Vector3 desiredPosition = focus + orbit * blendedOffset;
        desiredPosition = ResolveCameraCollision(focus, desiredPosition);
        float positionT = 1f - Mathf.Exp(-cameraFollowSharpness * Time.deltaTime);
        playerCamera.transform.position = Vector3.Lerp(playerCamera.transform.position, desiredPosition, positionT);

        if (isAiming)
        {
            aimOffset += lookInput * lookSensitivity * 12f;
            aimOffset.x = Mathf.Clamp(aimOffset.x, -Screen.width * 0.5f, Screen.width * 0.5f);
            aimOffset.y = Mathf.Clamp(aimOffset.y, -Screen.height * 0.5f, Screen.height * 0.5f);

            Vector2 cursorFactor = GetAimCursorFactor();
            float cursorDistance = Mathf.Clamp01(Mathf.Max(Mathf.Abs(cursorFactor.x), Mathf.Abs(cursorFactor.y)));
            float speedMultiplier = Mathf.Lerp(1f, cursorDistance, aimDistanceSpeedInfluence);
            float turnT = 1f - Mathf.Exp(-aimTurnSpeed * speedMultiplier * Time.deltaTime);
            float reticleTurnT = 1f - Mathf.Exp(-aimReticleTurnSpeed * Time.deltaTime);
            float returnT = 1f - Mathf.Exp(-aimReturnSpeed * Time.deltaTime);

            float consumedX = cursorFactor.x * turnT;
            float consumedY = cursorFactor.y * turnT;
            aimOffset -= new Vector2(
                cursorFactor.x * reticleTurnT * Screen.width * 0.5f,
                cursorFactor.y * reticleTurnT * Screen.height * 0.5f);
            Vector2 remainingOffset = new Vector2(
                cursorFactor.x * Screen.width * 0.5f,
                cursorFactor.y * Screen.height * 0.5f);
            Vector2 returnDirection = remainingOffset.sqrMagnitude > 0.0001f
                ? remainingOffset.normalized
                : Vector2.zero;
            float returnDistance = aimReturnSpeed * Time.deltaTime;
            aimOffset -= Vector2.Scale(returnDirection, new Vector2(returnDistance, returnDistance));
            aimOffset.x = Mathf.MoveTowards(aimOffset.x, 0f, returnDistance);
            aimOffset.y = Mathf.MoveTowards(aimOffset.y, 0f, returnDistance);
            yaw += consumedX * aimYawAngle;
            pitch = Mathf.Clamp(pitch - consumedY * aimPitchAngle, cameraPitchLimits.x, cameraPitchLimits.y);
        }

        virtualAimPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) + aimOffset;

        Vector3 rotationOffset = Vector3.Lerp(cameraRotationOffset, aimCameraRotationOffset, aimBlend);
        Quaternion desiredRotation = Quaternion.Euler(pitch, yaw, 0f) * Quaternion.Euler(rotationOffset);
        float rotationT = 1f - Mathf.Exp(-cameraRotationSharpness * Time.deltaTime);
        playerCamera.transform.rotation = Quaternion.Slerp(playerCamera.transform.rotation, desiredRotation, rotationT);
        float targetFov = scopeMode ? currentScopeFieldOfView : normalFieldOfView;
        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFov, rotationT);
    }

    private Vector3 ResolveCameraCollision(Vector3 focus, Vector3 desiredPosition)
    {
        Vector3 direction = desiredPosition - focus;
        float desiredDistance = direction.magnitude;
        if (desiredDistance <= 0.001f)
            return focus;

        direction /= desiredDistance;
        float safeDistance = desiredDistance;
        RaycastHit hit;
        if (Physics.SphereCast(focus, cameraCollisionRadius, direction, out hit, desiredDistance, cameraCollisionMask, QueryTriggerInteraction.Ignore))
            safeDistance = Mathf.Max(0f, hit.distance - cameraCollisionOffset);

        return focus + direction * safeDistance;
    }

    private Vector2 GetAimCursorFactor()
    {
        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 cursor = virtualAimPosition - center;
        float halfWidth = Mathf.Max(center.x, 1f);
        float halfHeight = Mathf.Max(center.y, 1f);
        Vector2 normalized = new Vector2(cursor.x / halfWidth, cursor.y / halfHeight);
        normalized = Vector2.ClampMagnitude(normalized, 1f);
        return new Vector2(ApplyDeadZone(normalized.x, aimScreenDeadZone), ApplyDeadZone(normalized.y, aimScreenDeadZone));
    }

    private static float ApplyDeadZone(float value, float deadZone)
    {
        float magnitude = Mathf.Abs(value);
        if (magnitude <= deadZone) return 0f;
        return Mathf.Sign(value) * Mathf.InverseLerp(deadZone, 1f, magnitude);
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;

        Vector3 localVelocity = transform.InverseTransformDirection(planarVelocity);
        animator.SetFloat(moveXParameter, localVelocity.x / Mathf.Max(walkSpeed, 0.01f), 0.1f, Time.deltaTime);
        animator.SetFloat(moveYParameter, localVelocity.z / Mathf.Max(walkSpeed, 0.01f), 0.1f, Time.deltaTime);
        animator.SetFloat(speedParameter, planarVelocity.magnitude, 0.1f, Time.deltaTime);
        animator.SetFloat(verticalSpeedParameter, verticalVelocity);
        animator.SetBool(groundedParameter, characterController.isGrounded);
        animator.SetBool(aimingParameter, isAiming);
    }
}
