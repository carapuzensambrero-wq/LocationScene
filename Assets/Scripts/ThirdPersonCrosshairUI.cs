using UnityEngine;

/// <summary>
/// World-of-Tanks-style aiming reticle. The center marker stays fixed while the
/// cursor/ring expands according to its movement speed, then contracts smoothly.
/// Assign a UI Image with Type = Filled and Fill Method = Radial 360.
/// </summary>
public sealed class ThirdPersonCrosshairUI : MonoBehaviour
{
    [Header("Reticle")]
    [SerializeField] private RectTransform reticle;
    [SerializeField] private RectTransform innerDot;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Optical scope")]
    [SerializeField] private CanvasGroup opticalScopeCanvasGroup;
    [SerializeField, Min(0.01f)] private float opticalScopeTransitionTime = 0.2f;

    [Header("Size")]
    [SerializeField, Min(0f)] private float minimumRadius = 18f;
    [SerializeField, Min(0f)] private float movementRadius = 22f;
    [SerializeField, Min(0f)] private float cursorMovementRadius = 12f;
    [SerializeField, Min(0.01f)] private float cursorSpeedForMaximumExpansion = 1200f;
    [SerializeField, Min(0f)] private float shotRadius = 32f;
    [SerializeField, Min(0f)] private float radiusChangeSharpness = 10f;
    [SerializeField, Min(0f)] private float shotRecoverySharpness = 7f;

    [Header("Visibility")]
    [SerializeField, Min(0.01f)] private float visibilityTransitionTime = 0.2f;

    private ThirdPersonCharacterController controller;
    private float shotImpulse;
    private float currentRadius;
    private Vector2 previousCursorPosition;
    private bool hasPreviousCursorPosition;

    public float CurrentRadius => currentRadius;
    public Vector2 AimScreenPosition => reticle != null ? reticle.position : Input.mousePosition;

    private void Awake()
    {
        controller = Object.FindObjectOfType<ThirdPersonCharacterController>();
        currentRadius = minimumRadius;
        ApplyRadius(currentRadius);
        previousCursorPosition = reticle != null ? reticle.position : Input.mousePosition;
        hasPreviousCursorPosition = true;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void Update()
    {
        if (controller == null)
            controller = Object.FindObjectOfType<ThirdPersonCharacterController>();

        UpdateVisibility();

        UpdateOpticalScopeVisibility();

        if (controller != null && controller.IsAiming && reticle != null)
        {
            reticle.position = controller.AimScreenPosition;
        }

        float movement = controller != null ? controller.MoveInput.magnitude : 0f;
        float cursorSpeed = 0f;
        if (controller != null)
        {
            Vector2 cursorPosition = controller.AimScreenPosition;
            cursorSpeed = hasPreviousCursorPosition
                ? Vector2.Distance(previousCursorPosition, cursorPosition)
                    / Mathf.Max(Time.unscaledDeltaTime, 0.0001f)
                : 0f;
            previousCursorPosition = cursorPosition;
            hasPreviousCursorPosition = true;
        }

        float targetRadius = minimumRadius
            + movement * movementRadius
            + Mathf.Clamp01(cursorSpeed / cursorSpeedForMaximumExpansion) * cursorMovementRadius
            + shotImpulse * shotRadius;

        float radiusT = 1f - Mathf.Exp(-radiusChangeSharpness * Time.unscaledDeltaTime);
        currentRadius = Mathf.Lerp(currentRadius, targetRadius, radiusT);
        shotImpulse = Mathf.MoveTowards(shotImpulse, 0f, shotRecoverySharpness * Time.unscaledDeltaTime);
        ApplyRadius(currentRadius);

    }

    public Vector2 GetAimScreenPosition()
    {
        return reticle != null ? (Vector2)reticle.position : (Vector2)Input.mousePosition;
    }

    /// <summary>Called by the controller now and by the weapon system later.</summary>
    public void NotifyShot()
    {
        shotImpulse = 1f;
    }

    private void ApplyRadius(float radius)
    {
        if (reticle != null)
            reticle.sizeDelta = new Vector2(radius * 2f, radius * 2f);

        // The center marker remains unchanged; only the cursor/ring expands.
        if (innerDot != null)
            innerDot.localScale = Vector3.one;
    }

    private void UpdateVisibility()
    {
        if (canvasGroup == null)
            return;

        float targetAlpha = controller != null && controller.IsAiming ? 1f : 0f;
        float alphaStep = Time.unscaledDeltaTime / visibilityTransitionTime;
        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, alphaStep);
    }

    private void UpdateOpticalScopeVisibility()
    {
        if (opticalScopeCanvasGroup == null)
            return;

        float targetAlpha = controller != null && controller.IsOpticalScopeAiming ? 1f : 0f;
        float alphaStep = Time.unscaledDeltaTime / opticalScopeTransitionTime;
        opticalScopeCanvasGroup.alpha = Mathf.MoveTowards(opticalScopeCanvasGroup.alpha, targetAlpha, alphaStep);
        opticalScopeCanvasGroup.interactable = false;
        opticalScopeCanvasGroup.blocksRaycasts = false;
    }

}
