using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Charactercontrol : MonoBehaviour
{
    // Компоненты
    Animator animator;
    BoxCollider2D box2d;
    Rigidbody2D rb2d;
    SpriteRenderer sprite;
    ColorSwap colorSwap;

    // Ввод
    float keyHorizontal;
    float keyVertical;
    bool keyJump;
    bool keyShoot;

    // Состояния
    bool isGrounded;
    bool isJumping;
    bool isShooting;
    bool isTeleporting;
    bool isTakingDamage;
    bool isInvincible;
    bool isFacingRight;

    // --- ЛОГИКА ЛЕСТНИЦ (Интегрировано) ---
    bool isClimbing;
    bool isClimbingDown;
    bool atLaddersEnd;
    bool hasStartedClimbing;
    bool startedClimbTransition;
    bool finishedClimbTransition;
    float transformY;
    float transformHY;
    
    [Header("Ladder Settings")]
    [SerializeField] float climbSpeed = 0.525f;
    [SerializeField] float climbSpriteHeight = 0.36f;
    [HideInInspector] public LadderScript ladder; // Ссылка на скрипт лестницы

    // Технические переменные
    bool hitSideRight;
    bool freezeInput;
    bool freezePlayer;
    bool freezeBullets;
    float shootTime;
    bool keyShootRelease;
    RigidbodyConstraints2D rb2dConstraints;

    // --- СИСТЕМА ОРУЖИЯ (Исправлено) ---
    public enum PlayerWeapons { Default, MagnetBeam, HyperBomb, ThunderBeam, SuperArm, IceSlasher, RollingCutter, FireStorm };
    public PlayerWeapons playerWeapon = PlayerWeapons.Default;

    [System.Serializable]
    public struct PlayerWeaponsStruct // Исправлено: добавление структуры
    {
        public PlayerWeapons weaponType;
        public bool enabled;
        public int currentEnergy;
        public int maxEnergy;
        public int energyCost;
        public int weaponDamage;
        public Vector2 weaponVelocity;
        public AudioClip weaponClip;
        public GameObject weaponPrefab;
    }

    public PlayerWeaponsStruct[] playerWeaponStructs; // Исправлено: массив структур

    [Header("Player Stats")]
    public int currentHealth;
    public int maxHealth = 28;
    [SerializeField] float moveSpeed = 1.5f;
    [SerializeField] float jumpSpeed = 3.7f;
    [SerializeField] float teleportSpeed = -10f; // Добавлено для метода Teleport
    [SerializeField] GameObject explodeEffectPrefab;

    string lastAnimationName;
    bool jumpStarted;

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
        isFacingRight = true;
        currentHealth = maxHealth;
    }

    private void FixedUpdate()
    {
        if (isClimbing) return;

        // Проверка земли
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
            if (isJumping) isJumping = false;
        }
    }

    void Update()
    {
        if (isTeleporting) { HandleTeleportLogic(); return; }
        if (isTakingDamage || freezePlayer) return;

        if (!freezeInput)
        {
            keyHorizontal = Input.GetAxisRaw("Horizontal");
            keyVertical = Input.GetAxisRaw("Vertical");
            keyJump = Input.GetButtonDown("Jump");
            keyShoot = Input.GetButtonDown("Fire1");
        }

        PlayerMovement();
    }

    void PlayerMovement()
    {
        transformY = transform.position.y;
        transformHY = transformY + climbSpriteHeight;

        if (isClimbing)
        {
            HandleClimbing();
        }
        else
        {
            // Обычное движение
            rb2d.velocity = new Vector2(moveSpeed * keyHorizontal, rb2d.velocity.y);

            if (keyHorizontal < 0 && isFacingRight) Flip();
            else if (keyHorizontal > 0 && !isFacingRight) Flip();

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

            // Вход на лестницу
            if (keyVertical > 0) StartClimbingUp();
            if (keyVertical < 0) StartClimbingDown();
        }
    }

    // --- МЕТОДЫ ЛЕСТНИЦЫ ---
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
            PlayAnimation(isShooting ? "Player_ClimbShoot" : "Player_Climb");
            if (keyJump && keyVertical == 0) ResetClimbing();
        }
    }

    public void StartClimbingUp()
    {
        if (ladder != null && ladder.isNearLadder && keyVertical > 0 && transformHY < ladder.posTopHandlerY)
        {
            isClimbing = true;
            rb2d.bodyType = RigidbodyType2D.Kinematic;
            rb2d.velocity = Vector2.zero;
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
            rb2d.velocity = Vector2.zero;
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
            PlayAnimation("Player_ClimbTop");
            yield return null;
        }
        finishedClimbTransition = true;
        freezeInput = false;
    }

    // --- ИСПРАВЛЕННЫЕ МЕТОДЫ (ОШИБКИ) ---

    public void FreezePlayer(bool freeze)
    {
        freezePlayer = freeze;
        if (freeze) {
            rb2d.velocity = Vector2.zero;
            rb2d.constraints = RigidbodyConstraints2D.FreezeAll;
        } else {
            rb2d.constraints = RigidbodyConstraints2D.FreezeRotation;
        }
    }

    public void Teleport(bool start)
    {
        if (start) {
            isTeleporting = true;
            gameObject.layer = LayerMask.NameToLayer("Teleport");
            rb2d.velocity = new Vector2(0, teleportSpeed);
            PlayAnimation("Player_Teleport");
        } else {
            isTeleporting = false;
            gameObject.layer = LayerMask.NameToLayer("Player");
            rb2d.constraints = RigidbodyConstraints2D.FreezeRotation;
        }
    }

    void HandleTeleportLogic()
    {
        // Базовая логика остановки при приземлении телепорта может быть тут
        if (isGrounded) Teleport(false);
    }

    // --- ВСПОМОГАТЕЛЬНЫЕ ---

    void Flip() { isFacingRight = !isFacingRight; transform.Rotate(0f, 180f, 0f); }
    
    void PlayAnimation(string name) {
        if (name != lastAnimationName) { animator.Play(name); lastAnimationName = name; }
    }

    IEnumerator JumpCo() { jumpStarted = true; yield return new WaitForSeconds(0.1f); jumpStarted = false; }

    public void FreezeInput(bool freeze) => freezeInput = freeze;

    // --- МОБИЛЬНОЕ УПРАВЛЕНИЕ (Из оригинального скрипта) ---
    public void SimulateMoveLeft() => keyHorizontal = -1.0f;
    public void SimulateMoveRight() => keyHorizontal = 1.0f;
    public void SimulateMoveStop() => keyHorizontal = 0f;
}
