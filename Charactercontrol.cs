using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Charactercontrol: MonoBehaviour
{
    // Компоненты
    private Animator animator;
    private BoxCollider2D box2d;
    private Rigidbody2D rb2d;
    private SpriteRenderer sprite;
    private ColorSwap colorSwap;

    // Ввод
    private float keyHorizontal;
    private float keyVertical;
    private bool keyJump;
    private bool keyShoot;

    // Состояния (Bools)
    private bool isGrounded;
    private bool isClimbing;
    private bool isJumping;
    private bool isShooting;
    private bool isThrowing;
    private bool isTeleporting;
    private bool isTakingDamage;
    private bool isInvincible;
    private bool isFacingRight = true;

    // Параметры лестницы (Интегрировано из новой версии)
    [Header("Ladder Settings")]
    [SerializeField] private float climbSpeed = 0.525f;
    [SerializeField] private float climbSpriteHeight = 0.36f;
    private bool isClimbingDown;
    private bool atLaddersEnd;
    private bool hasStartedClimbing;
    private bool startedClimbTransition;
    private bool finishedClimbTransition;
    private float transformY;
    private float transformHY;
    [HideInInspector] public LadderScript ladder; // Ссылка на текущую лестницу (должна быть в триггере)

    // Параметры передвижения
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private float jumpSpeed = 3.7f;
    
    private string lastAnimationName;
    private bool jumpStarted;
    private bool freezeInput;

    // Оружие и здоровье
    public int currentHealth;
    public int maxHealth = 28;

    [Header("Audio")]
    [SerializeField] private AudioClip jumpLandedClip;
    [SerializeField] private AudioClip takingDamageClip;

    void Awake()
    {
        animator = GetComponent<Animator>();
        box2d = GetComponent<BoxCollider2D>();
        rb2d = GetComponent<Rigidbody2D>();
        sprite = GetComponent<SpriteRenderer>();
        colorSwap = GetComponent<ColorSwap>();
    }

    void Start()
    {
        currentHealth = maxHealth;
        // Если у тебя есть система выбора оружия, она инициализируется здесь
    }

    private void FixedUpdate()
    {
        // Проверка земли (Ground Check)
        isGrounded = false;
        if (!isClimbing)
        {
            float raycastDistance = 0.025f;
            int layerMask = 1 << LayerMask.NameToLayer("Ground") | 1 << LayerMask.NameToLayer("MagnetBeam");
            Vector3 box_origin = box2d.bounds.center;
            box_origin.y = box2d.bounds.min.y + (box2d.bounds.extents.y / 4f);
            Vector3 box_size = box2d.bounds.size;
            box_size.y = box2d.bounds.size.y / 4f;
            RaycastHit2D raycastHit = Physics2D.BoxCast(box_origin, box_size, 0f, Vector2.down, raycastDistance, layerMask);

            if (raycastHit.collider != null && !jumpStarted)
            {
                isGrounded = true;
                if (isJumping) { isJumping = false; }
            }
        }
    }

    void Update()
    {
        if (isTeleporting || isTakingDamage) return;

        if (!freezeInput)
        {
            keyHorizontal = Input.GetAxisRaw("Horizontal");
            keyVertical = Input.GetAxisRaw("Vertical");
            keyJump = Input.GetButtonDown("Jump");
            keyShoot = Input.GetButtonDown("Fire1");
        }

        HandleMovement();
    }

    private void HandleMovement()
    {
        transformY = transform.position.y;
        transformHY = transformY + climbSpriteHeight;

        if (isClimbing)
        {
            HandleClimbingLogic();
        }
        else
        {
            HandleNormalMovement();
        }
    }

    private void HandleNormalMovement()
    {
        // Обычное движение влево-вправо
        rb2d.velocity = new Vector2(moveSpeed * keyHorizontal, rb2d.velocity.y);

        if (keyHorizontal < 0 && isFacingRight) Flip();
        else if (keyHorizontal > 0 && !isFacingRight) Flip();

        // Анимации и прыжок
        if (isGrounded)
        {
            if (keyHorizontal != 0) PlayAnimation(isShooting ? "Player_RunShoot" : "Player_Run");
            else PlayAnimation(isShooting ? "Player_Shoot" : "Player_Idle");

            if (keyJump) { rb2d.velocity = new Vector2(rb2d.velocity.x, jumpSpeed); StartCoroutine(JumpCo()); }
        }
        else
        {
            isJumping = true;
            PlayAnimation(isShooting ? "Player_JumpShoot" : "Player_Jump");
        }

        // Проверка входа на лестницу
        if (keyVertical > 0) StartClimbingUp();
        if (keyVertical < 0) StartClimbingDown();
    }

    private void HandleClimbingLogic()
    {
        // 1. Проверка: достигли верха лестницы?
        if (transformHY > ladder.posTopHandlerY)
        {
            if (!isClimbingDown)
            {
                if (!startedClimbTransition) { startedClimbTransition = true; StartCoroutine(ClimbTransitionCo(true)); }
                else if (finishedClimbTransition)
                {
                    EndClimbOnTop();
                }
            }
        }
        // 2. Проверка: достигли низа (земли)?
        else if (transformHY < ladder.posBottomHandlerY)
        {
            ResetClimbing();
        }
        else
        {
            // 3. Процесс карабканья
            if (keyJump && keyVertical == 0) // Спрыгнуть с лестницы
            {
                ResetClimbing();
            }
            else
            {
                animator.speed = Mathf.Abs(keyVertical); // Анимация только при движении
                
                if (keyVertical != 0)
                {
                    transform.position += new Vector3(0, climbSpeed * keyVertical * Time.deltaTime, 0);
                }

                PlayAnimation(isShooting ? "Player_ClimbShoot" : "Player_Climb");
            }
        }
    }

    // --- МЕТОДЫ ЛЕСТНИЦЫ ---

    public void StartClimbingUp()
    {
        if (ladder != null && ladder.isNearLadder && keyVertical > 0 && transformHY < ladder.posTopHandlerY)
        {
            SetClimbingState(true);
            transform.position = new Vector3(ladder.posX, transform.position.y + 0.05f, 0);
        }
    }

    public void StartClimbingDown()
    {
        if (ladder != null && ladder.isNearLadder && keyVertical < 0 && isGrounded && transformHY > ladder.posTopHandlerY)
        {
            SetClimbingState(true);
            isClimbingDown = true;
            StartCoroutine(ClimbTransitionCo(false));
        }
    }

    private void SetClimbingState(bool active)
    {
        isClimbing = active;
        rb2d.bodyType = active ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic;
        rb2d.velocity = Vector2.zero;
        if (!active) animator.speed = 1;
    }

    private void EndClimbOnTop()
    {
        finishedClimbTransition = false;
        startedClimbTransition = false;
        isJumping = false;
        transform.position = new Vector2(ladder.posX, ladder.posPlatformY + 0.01f);
        ResetClimbing();
    }

    public void ResetClimbing()
    {
        SetClimbingState(false);
        isClimbingDown = false;
        atLaddersEnd = false;
        startedClimbTransition = false;
        finishedClimbTransition = false;
    }

    private IEnumerator ClimbTransitionCo(bool movingUp)
    {
        freezeInput = true;
        finishedClimbTransition = false;
        
        Vector3 targetPos = movingUp ? 
            new Vector3(ladder.posX, transformY + ladder.handlerTopOffset, 0) :
            new Vector3(ladder.posX, ladder.posTopHandlerY - climbSpriteHeight, 0);

        while (Vector3.Distance(transform.position, targetPos) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPos, climbSpeed * Time.deltaTime);
            animator.speed = 1;
            PlayAnimation("Player_ClimbTop");
            yield return null;
        }

        finishedClimbTransition = true;
        freezeInput = false;
    }

    // --- ВСПОМОГАТЕЛЬНОЕ ---

    void Flip()
    {
        isFacingRight = !isFacingRight;
        transform.Rotate(0f, 180f, 0f);
    }

    private IEnumerator JumpCo()
    {
        jumpStarted = true;
        yield return new WaitForSeconds(0.1f);
        jumpStarted = false;
    }

    void PlayAnimation(string animationName)
    {
        if (animationName != lastAnimationName)
        {
            lastAnimationName = animationName;
            animator.Play(animationName);
        }
    }

    public void FreezeInput(bool freeze) => freezeInput = freeze;
}
