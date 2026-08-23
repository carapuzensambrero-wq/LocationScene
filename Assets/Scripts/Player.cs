using UnityEngine;

/// <summary>
/// Самодостаточный пример качественного 2D-контроллера персонажа.
/// Скрипт вешается на объект игрока с Rigidbody2D. В Rigidbody2D желательно
/// заранее включить Freeze Rotation по Z, чтобы физика не заваливала персонажа.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public sealed class Player : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Максимальная скорость горизонтального движения в юнитах Unity за секунду.")]
    [SerializeField, Min(0f)] private float moveSpeed = 7f;

    [Tooltip("Насколько быстро персонаж набирает скорость при нажатии A/D или стрелок.")]
    [SerializeField, Min(0f)] private float acceleration = 60f;

    [Tooltip("Насколько быстро персонаж останавливается, когда кнопки движения отпущены.")]
    [SerializeField, Min(0f)] private float deceleration = 70f;

    [Header("Jump")]
    [Tooltip("Начальная вертикальная скорость прыжка.")]
    [SerializeField, Min(0f)] private float jumpForce = 12f;

    [Tooltip("Сколько прыжков доступно до следующего касания земли: 2 = обычный прыжок + двойной.")]
    [SerializeField, Min(1)] private int maxJumpCount = 2;

    [Tooltip("Короткое окно после схода с платформы, в течение которого еще можно сделать первый прыжок.")]
    [SerializeField, Min(0f)] private float coyoteTime = 0.12f;

    [Tooltip("Короткое окно запоминания нажатия прыжка перед приземлением.")]
    [SerializeField, Min(0f)] private float jumpBufferTime = 0.12f;

    [Tooltip("Когда кнопка прыжка отпущена раньше, подъем гасится этим множителем. Так получается короткий прыжок.")]
    [SerializeField, Range(0f, 1f)] private float jumpCutMultiplier = 0.5f;

    [Header("Ground Check")]
    [Tooltip("Точка проверки земли. Если не указана, скрипт будет проверять землю чуть ниже центра игрока.")]
    [SerializeField] private Transform groundCheckPoint;

    [Tooltip("Радиус круга, которым проверяем землю под игроком.")]
    [SerializeField, Min(0.01f)] private float groundCheckRadius = 0.16f;

    [Tooltip("Слои, которые считаются землей. Назначь сюда слой платформ/пола.")]
    [SerializeField] private LayerMask groundLayerMask = ~0;

    [Header("Smooth Camera")]
    [Tooltip("Камера, которую нужно плавно вести за игроком. Поле можно оставить пустым, если камера не нужна.")]
    [SerializeField] private Camera linkedCamera;

    [Tooltip("Смещение камеры относительно игрока. Z обычно отрицательный, например -10 для 2D.")]
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 1.5f, -10f);

    [Tooltip("Время сглаживания камеры. Меньше значение = камера быстрее догоняет игрока.")]
    [SerializeField, Min(0.01f)] private float cameraSmoothTime = 0.15f;

    [Tooltip("Если включено, персонаж поворачивается лицом в сторону движения.")]
    [SerializeField] private bool flipToMoveDirection = true;

    private Rigidbody2D rb2d;
    private Collider2D[] ownColliders;
    private Vector3 cameraVelocity;
    private float horizontalInput;
    private float jumpBufferCounter;
    private float coyoteCounter;
    private int jumpsUsed;
    private bool isGrounded;

    private void Awake()
    {
        // Берем Rigidbody2D с того же объекта. RequireComponent гарантирует, что компонент существует.
        rb2d = GetComponent<Rigidbody2D>();

        // Запоминаем свои коллайдеры, чтобы проверка земли не принимала самого игрока за пол.
        ownColliders = GetComponents<Collider2D>();

        // Для 2D-платформера персонаж не должен вращаться от столкновений, поэтому фиксируем вращение программно тоже.
        rb2d.freezeRotation = true;

        // Если камеру забыли назначить в инспекторе, пробуем автоматически взять Main Camera.
        if (linkedCamera == null)
            linkedCamera = Camera.main;
    }

    private void Update()
    {
        // Ввод читаем в Update, потому что GetKeyDown/GetKeyUp живут один кадр и могут потеряться в FixedUpdate.
        ReadDirectKeyboardInput();

        // Проверку земли тоже удобно держать каждый кадр, чтобы таймеры прыжка были отзывчивыми.
        UpdateGroundState();

        // Обновляем таймеры coyote time и буфера прыжка.
        UpdateJumpTimers();

        // Если прыжок доступен сейчас или был заранее буферизован, выполняем его.
        TryConsumeBufferedJump();

        // Если игрок отпустил кнопку прыжка во время подъема, делаем прыжок ниже и управляемее.
        CutJumpOnKeyRelease();

        // Визуально разворачиваем персонажа в сторону движения, не трогая физическое вращение Rigidbody2D.
        FlipVisualDirection();
    }

    private void FixedUpdate()
    {
        // Физическую скорость меняем только в FixedUpdate, чтобы движение было стабильным на разных FPS.
        MoveHorizontally();
    }

    private void LateUpdate()
    {
        // Камеру двигаем после движения персонажа, чтобы она следовала за уже обновленной позицией.
        FollowWithCamera();
    }

    private void ReadDirectKeyboardInput()
    {
        // Движение влево: A или стрелка влево. Input Manager и оси Unity здесь не используются.
        bool moveLeft = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);

        // Движение вправо: D или стрелка вправо. Можно оставить только A/D, если нужны строго эти клавиши.
        bool moveRight = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);

        // Получаем -1, 0 или 1. Если зажаты обе стороны одновременно, они взаимно гасятся.
        horizontalInput = 0f;
        if (moveLeft) horizontalInput -= 1f;
        if (moveRight) horizontalInput += 1f;

        // Прыжок: Space или W. Нажатие кладется в буфер, чтобы прыжок не терялся перед касанием земли.
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W))
            jumpBufferCounter = jumpBufferTime;
    }

    private void UpdateGroundState()
    {
        // Если отдельная точка проверки не назначена, проверяем немного ниже центра объекта.
        Vector2 checkPosition = groundCheckPoint != null
            ? groundCheckPoint.position
            : transform.position + Vector3.down * 0.55f;

        // OverlapCircleAll надежнее одиночного луча на краях платформ и позволяет отфильтровать свои коллайдеры.
        Collider2D[] hits = Physics2D.OverlapCircleAll(checkPosition, groundCheckRadius, groundLayerMask);
        isGrounded = false;
        foreach (Collider2D hit in hits)
        {
            // Пропускаем пустые попадания и коллайдеры, которые принадлежат самому игроку.
            if (hit == null || IsOwnCollider(hit))
                continue;

            isGrounded = true;
            break;
        }

        // Сбрасывать прыжки нужно только при реальном контакте снизу, а не во время взлета через платформу.
        if (!isGrounded || rb2d.velocity.y > 0.01f)
            return;

        // При касании земли снова разрешаем полный набор прыжков.
        jumpsUsed = 0;

        // Coyote timer обновляется на земле, чтобы после схода с края было маленькое окно для прыжка.
        coyoteCounter = coyoteTime;
    }

    private void UpdateJumpTimers()
    {
        // Пока персонаж не на земле, coyote time постепенно истекает.
        if (!isGrounded)
            coyoteCounter -= Time.deltaTime;

        // Если игрок просто упал с края и coyote time закончился, считаем первый прыжок потраченным.
        // Так после падения остается только воздушный прыжок, а не два прыжка подряд в воздухе.
        if (!isGrounded && coyoteCounter <= 0f && jumpsUsed == 0)
            jumpsUsed = 1;

        // Буфер прыжка тоже постепенно истекает, если прыжок еще не был выполнен.
        if (jumpBufferCounter > 0f)
            jumpBufferCounter -= Time.deltaTime;
    }

    private void TryConsumeBufferedJump()
    {
        // Нет сохраненного нажатия — прыгать не надо.
        if (jumpBufferCounter <= 0f)
            return;

        // Первый прыжок можно сделать с земли или в пределах coyote time.
        bool canUseFirstJump = jumpsUsed == 0 && (isGrounded || coyoteCounter > 0f);

        // Дополнительные прыжки работают в воздухе до maxJumpCount. При maxJumpCount = 2 это двойной прыжок.
        bool canUseAirJump = jumpsUsed > 0 && jumpsUsed < maxJumpCount;

        if (!canUseFirstJump && !canUseAirJump)
            return;

        // Перед прыжком сбрасываем текущую вертикальную скорость, чтобы второй прыжок был одинаково сильным.
        rb2d.velocity = new Vector2(rb2d.velocity.x, 0f);

        // Импульс дает мгновенный толчок вверх и хорошо подходит для прыжка на Rigidbody2D.
        rb2d.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);

        // Фиксируем, что один прыжок потрачен, и очищаем буфер, чтобы одно нажатие не сработало дважды.
        jumpsUsed++;
        jumpBufferCounter = 0f;
        coyoteCounter = 0f;
        isGrounded = false;
    }

    private void CutJumpOnKeyRelease()
    {
        // Реагируем только на отпускание клавиши прыжка.
        bool jumpReleased = Input.GetKeyUp(KeyCode.Space) || Input.GetKeyUp(KeyCode.W);
        if (!jumpReleased)
            return;

        // Если персонаж еще летит вверх, уменьшаем вертикальную скорость — так высота прыжка зависит от удержания кнопки.
        if (rb2d.velocity.y > 0f)
            rb2d.velocity = new Vector2(rb2d.velocity.x, rb2d.velocity.y * jumpCutMultiplier);
    }

    private void MoveHorizontally()
    {
        // Целевая скорость зависит только от прямого клавиатурного ввода.
        float targetVelocityX = horizontalInput * moveSpeed;

        // На разгоне и торможении используем разные значения, чтобы движение ощущалось плотнее.
        bool hasMoveInput = Mathf.Abs(horizontalInput) > 0.01f;
        float speedChange = (hasMoveInput ? acceleration : deceleration) * Time.fixedDeltaTime;

        // MoveTowards дает контролируемое ускорение без рывков и без скольжения сверх заданной скорости.
        float newVelocityX = Mathf.MoveTowards(rb2d.velocity.x, targetVelocityX, speedChange);

        // Горизонтальную скорость контролируем сами, вертикальную оставляем физике и прыжкам.
        rb2d.velocity = new Vector2(newVelocityX, rb2d.velocity.y);
    }

    private void FlipVisualDirection()
    {
        if (!flipToMoveDirection || Mathf.Abs(horizontalInput) < 0.01f)
            return;

        // Меняем знак масштаба X. Это простой способ развернуть 2D-спрайт без вращения Rigidbody2D.
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * Mathf.Sign(horizontalInput);
        transform.localScale = scale;
    }

    private bool IsOwnCollider(Collider2D hit)
    {
        // Проверяем список коллайдеров на объекте игрока. Это защищает от ложного isGrounded при маске Everything.
        for (int i = 0; i < ownColliders.Length; i++)
        {
            if (hit == ownColliders[i])
                return true;
        }

        return false;
    }

    private void FollowWithCamera()
    {
        if (linkedCamera == null)
            return;

        // Целевая точка камеры — позиция игрока плюс настраиваемое смещение.
        Vector3 targetPosition = transform.position + cameraOffset;

        // В 2D важно сохранить отрицательный Z, иначе камера может оказаться в плоскости персонажа.
        targetPosition.z = cameraOffset.z;

        // SmoothDamp делает плавное следование без дрожания и без резкого прилипания к игроку.
        linkedCamera.transform.position = Vector3.SmoothDamp(
            linkedCamera.transform.position,
            targetPosition,
            ref cameraVelocity,
            cameraSmoothTime);
    }

    private void OnDrawGizmosSelected()
    {
        // В редакторе показываем радиус проверки земли, чтобы его было легко настроить под размер коллайдера.
        Gizmos.color = Color.yellow;
        Vector3 checkPosition = groundCheckPoint != null
            ? groundCheckPoint.position
            : transform.position + Vector3.down * 0.55f;
        Gizmos.DrawWireSphere(checkPosition, groundCheckRadius);
    }
}
