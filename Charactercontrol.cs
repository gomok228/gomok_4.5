using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;



public class Charactercontrol : MonoBehaviour
{
    Animator animator;
    BoxCollider2D box2d;
    Rigidbody2D rb2d;
    SpriteRenderer sprite;

    ColorSwap colorSwap;
    Collider2D currentPlatform;

    float keyHorizontal;
    float keyVertical;
    bool keyJump;
    bool keyShoot;

    bool isGrounded;
    bool isJumping;
    bool isShooting;
    bool isTeleporting;
    bool isTakingDamage;
    bool isInvincible;
    bool isFacingRight;

    // ЛОГИКА ЛЕСТНИЦЫ
    bool isClimbing;
    bool isClimbingDown;
    bool atLaddersEnd;
    bool hasStartedClimbing;
    bool startedClimbTransition;
    bool finishedClimbTransition;
    float transformY;
    float transformHY;

    // --- ЛОГИКА DASH (MEGA MAN X STYLE) ---
    [Header("Dash Settings")]
    [SerializeField] float dashSpeed = 3.5f;
    [SerializeField] float dashDuration = 0.5f;
    bool isDashing;
    bool isDashJumping; 
    float dashTimer;
    Vector2 originalColliderSize;
    Vector2 originalColliderOffset;

    // --- ДОБАВЛЕНО: НАСТРОЙКИ СЛЕДА (GHOST TRAIL) ---
    [SerializeField] Color trailColor = new Color(0, 0.5f, 1f, 0.5f); // Голубой, полупрозрачный
    [SerializeField] float trailFadeSpeed = 3f; // Скорость исчезновения
    [SerializeField] float trailInterval = 0.08f; // Как часто создавать тени
    private float trailTimer;
    private GameObject trailPrefab; // Префаб, который мы создадим сами в Awake

    bool hitSideRight;
    bool freezeInput;
    bool freezePlayer;
    bool freezeBullets;

    float shootTime;
    bool keyShootRelease;
    bool jumpStarted;

    RigidbodyConstraints2D rb2dConstraints;

    [Header("Audio and Effects")]
    [SerializeField] AudioClip explodeEffectClip;
    [SerializeField] GameObject explodeEffectPrefab;
    [SerializeField] float moveSpeed = 1.5f;
    [SerializeField] float jumpSpeed = 3.7f;
    [SerializeField] float climbSpeed = 0.525f;
    [SerializeField] float climbSpriteHeight = 0.36f;
    [HideInInspector] public LadderScript ladder;

    private enum SwapIndex
    {
        Primary = 64,
        Secondary = 128
    }

    public enum PlayerWeapons { Default, MagnetBeam, HyperBomb, ThunderBeam, SuperArm, IceSlasher, RollingCutter, FireStorm };
    public PlayerWeapons playerWeapon = PlayerWeapons.Default;

    string lastAnimationName;

    void Awake()
    {
        animator = GetComponent<Animator>();
        box2d = GetComponent<BoxCollider2D>();
        rb2d = GetComponent<Rigidbody2D>();
        sprite = GetComponent<SpriteRenderer>();
        colorSwap = GetComponent<ColorSwap>();

        originalColliderSize = box2d.size;
        originalColliderOffset = box2d.offset;

        // Автоматически создаем префаб для тени при запуске, чтобы тебе не делать его вручную
        trailPrefab = new GameObject("DashTrail_Prefab");
        trailPrefab.AddComponent<DashTrailEffect>();
        trailPrefab.SetActive(false); // Прячем до поры до времени
    }

    void Start()
    {
        isFacingRight = true;
    }

    private void FixedUpdate()
    {
        if (isClimbing) return;

        isGrounded = false;
        float raycastDistance = 0.025f;
        int layerMask = 1 << LayerMask.NameToLayer("Ground");
        Vector3 box_origin = box2d.bounds.center;
        box_origin.y = box2d.bounds.min.y + (box2d.bounds.extents.y / 4f);
        Vector3 box_size = box2d.bounds.size;
        box_size.y = box2d.bounds.size.y / 4f;
        RaycastHit2D raycastHit = Physics2D.BoxCast(box_origin, box_size, 0f, Vector2.down, raycastDistance, layerMask);

        if (raycastHit.collider != null && !jumpStarted)
        {
            isGrounded = true;
            if (isJumping) 
            { 
                isJumping = false; 
                isDashJumping = false; 
            }
        }
    }

    void Update()
    {
        if (isTeleporting || isTakingDamage || freezePlayer) return;

        if (!freezeInput)
        {
            keyHorizontal = Input.GetAxisRaw("Horizontal");
            keyVertical = Input.GetAxisRaw("Vertical");
            keyJump = Input.GetButtonDown("Jump");
            keyShoot = Input.GetButtonDown("Fire1");
        }

        PlayerMovement();
        Jump(); 
    }

    void PlayerMovement()
    {
        transformY = transform.position.y;
        transformHY = transformY + climbSpriteHeight;

        if (isClimbing)
        {
            HandleClimbing();
            return;
        }

        if (isGrounded && keyVertical < 0 && keyJump && !isDashing)
        {
            StartDash();
        }

        if (isDashing)
        {
            HandleDashing();
            return; 
        }

        float currentHorizontalSpeed = (isDashJumping) ? dashSpeed : moveSpeed;
        rb2d.linearVelocity = new Vector2(currentHorizontalSpeed * keyHorizontal, rb2d.linearVelocity.y);

        if (keyHorizontal < 0 && isFacingRight) Flip();
        else if (keyHorizontal > 0 && !isFacingRight) Flip();

        if (isGrounded)
        {
            if (keyHorizontal != 0) PlayAnimation(isShooting ? "run_shoot" : "run");
            else PlayAnimation(isShooting ? "shoot" : "idle");
        }
        else
        {
            isJumping = true;
            PlayAnimation(isShooting ? "jump_shoot" : "jump");
        }

        if (keyVertical > 0) StartClimbingUp();
        if (keyVertical < 0) StartClimbingDown();
    }

    // --- ЛОГИКА DASH ---
    void StartDash()
    {
        isDashing = true;
        dashTimer = dashDuration;
        
        // Начинаем таймер следа сразу при старте Dash
        trailTimer = 0; 
        
        box2d.size = new Vector2(originalColliderSize.x, originalColliderSize.y / 1.5f);
        box2d.offset = new Vector2(originalColliderOffset.x, originalColliderOffset.y - (originalColliderSize.y / 6f));
        
        PlayAnimation("dash"); 
    }

    void HandleDashing()
    {
        dashTimer -= Time.deltaTime;

        // --- ЛОГИКА СЛЕДА (DASH TRAIL) ---
        trailTimer -= Time.deltaTime;
        if (trailTimer <= 0)
        {
            CreateTrailAfterimage(); // Создаем тень
            trailTimer = trailInterval; // Сбрасываем таймер
        }
        // --------------------------------

        float direction = isFacingRight ? 1f : -1f;
        rb2d.linearVelocity = new Vector2(direction * dashSpeed, rb2d.linearVelocity.y);

        if (keyJump)
        {
            isDashJumping = true; 
            StopDash();
            rb2d.linearVelocity = new Vector2(rb2d.linearVelocity.x, jumpSpeed);
            StartCoroutine(JumpCo());
            return;
        }

        if ((isFacingRight && keyHorizontal < 0) || (!isFacingRight && keyHorizontal > 0))
        {
            StopDash();
            return;
        }

        if (dashTimer <= 0 || !isGrounded)
        {
            if (!Physics2D.Raycast(transform.position, Vector2.up, originalColliderSize.y, 1 << LayerMask.NameToLayer("Ground")))
            {
                StopDash();
            }
        }
    }

    // --- ФУНКЦИЯ СОЗДАНИЯ ТЕНИ ---
    void CreateTrailAfterimage()
    {
        // Создаем копию нашего префаба тени
        GameObject trail = Instantiate(trailPrefab, transform.position, transform.rotation);
        
        // Скрипт DashTrailEffect сам настроит SpriteRenderer на основе нашего текущего спрайта
        trail.GetComponent<DashTrailEffect>().Initialize(
            sprite.sprite,          // Текущий спрайт игрока
            transform,              // Ссылка на нас (для sortingOrder)
            trailColor,             // Цвет из настроек
            trailFadeSpeed,         // Скорость исчезновения
            isFacingRight           // Поворот
        );
        
        trail.SetActive(true); // Показываем
    }

    void StopDash()
    {
        isDashing = false;
        box2d.size = originalColliderSize;
        box2d.offset = originalColliderOffset;
    }

    // --- ЛОГИКА ЛЕСТНИЦЫ ---
    void HandleClimbing()
    {
        if (transformHY > ladder.posTopHandlerY)
        {
            if (!isClimbingDown)
            {
                if (!startedClimbTransition) { startedClimbTransition = true; StartCoroutine(ClimbTransitionCo(true)); }
                else if (finishedClimbTransition) { EndClimbOnTop(); }
            }
        }
        else if (transformHY < ladder.posBottomHandlerY)
        {
            ResetClimbing();
        }
        else
        {
            animator.speed = Mathf.Abs(keyVertical);
            if (keyVertical != 0) transform.position += new Vector3(0, climbSpeed * keyVertical * Time.deltaTime, 0);
            PlayAnimation(isShooting ? "climb_shoot" : "climb");
            if (keyJump && keyVertical == 0) ResetClimbing();
        }
    }

    public void StartClimbingUp()
    {
        if (ladder != null && ladder.isNearLadder && keyVertical > 0 && transformHY < ladder.posTopHandlerY)
        {
            isClimbing = true;
            rb2d.bodyType = RigidbodyType2D.Kinematic;
            rb2d.linearVelocity = Vector2.zero;
            transform.position = new Vector3(ladder.posX, transform.position.y + 0.05f, 0);
        }
    }

    public void StartClimbingDown()
    {
        if (ladder != null && ladder.isNearLadder && keyVertical < 0 && isGrounded && transformHY > ladder.posTopHandlerY)
        {
            isClimbing = true;
            isClimbingDown = true;
            rb2d.bodyType = RigidbodyType2D.Kinematic;
            rb2d.linearVelocity = Vector2.zero;
            StartCoroutine(ClimbTransitionCo(false));
        }
    }

    public void ResetClimbing()
    {
        isClimbing = false;
        isClimbingDown = false;
        startedClimbTransition = false;
        finishedClimbTransition = false;
        rb2d.bodyType = RigidbodyType2D.Dynamic;
        animator.speed = 1;
    }

    void EndClimbOnTop()
    {
        ResetClimbing();
        transform.position = new Vector2(ladder.posX, ladder.posPlatformY + 0.01f);
    }

    IEnumerator ClimbTransitionCo(bool movingUp)
    {
        freezeInput = true;
        finishedClimbTransition = false;
        Vector3 target = movingUp ? 
            new Vector3(ladder.posX, transformY + ladder.handlerTopOffset, 0) :
            new Vector3(ladder.posX, ladder.posTopHandlerY - climbSpriteHeight, 0);

        while (Vector3.Distance(transform.position, target) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, climbSpeed * Time.deltaTime);
            PlayAnimation("climb_top");
            yield return null;
        }
        finishedClimbTransition = true;
        freezeInput = false;
    }

    void Jump()
    {
        if (keyJump && isGrounded && !jumpStarted && !isDashing)
        {
            rb2d.linearVelocity = new Vector2(rb2d.linearVelocity.x, jumpSpeed);
            StartCoroutine(JumpCo());
            PlayAnimation("jump");
        }
    }

    private IEnumerator JumpCo()
    {
        jumpStarted = true;
        yield return new WaitForSeconds(0.1f);
        jumpStarted = false;
    }

    void Flip() { isFacingRight = !isFacingRight; transform.Rotate(0f, 180f, 0f); }
    
    void PlayAnimation(string name) {
        if (name != lastAnimationName) { animator.Play(name); lastAnimationName = name; }
    }

    public void FreezeInput(bool freeze) => freezeInput = freeze;
    public void FreezePlayer(bool freeze) { freezePlayer = freeze; rb2d.linearVelocity = Vector2.zero; }

    public void SimulateMoveLeft() => keyHorizontal = -1.0f;
    public void SimulateMoveRight() => keyHorizontal = 1.0f;
    public void SimulateMoveStop() => keyHorizontal = 0f;
    public void SimulateJump() => StartCoroutine(MobileJump());
    public void SimulateShoot() => StartCoroutine(MobileShoot());

    private IEnumerator MobileShoot() { keyShoot = true; yield return new WaitForSeconds(0.01f); keyShoot = false; }
    private IEnumerator MobileJump() { keyJump = true; yield return new WaitForSeconds(0.01f); keyJump = false; }

    private IEnumerator StartDefeatAnimation(bool explode)
    {
        yield return new WaitForSeconds(0.5f);
        FreezeInput(true);
        FreezePlayer(true);
        if (explode)
        {
            GameObject explodeEffect = Instantiate(explodeEffectPrefab);
            explodeEffect.name = explodeEffectPrefab.name;
            explodeEffect.transform.position = sprite.bounds.center;
            explodeEffect.GetComponent<ExplosionScript>().SetDestroyDelay(5f);
        }
        SoundManager.Instance.Play(explodeEffectClip);
        Destroy(gameObject);
    }
}