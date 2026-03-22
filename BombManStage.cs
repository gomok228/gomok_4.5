using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Tilemaps;

public class BombManStage : MonoBehaviour
{
    // create an animated storyline
    float startTime;
    float runTime;

    // flag that we called for the next scene
    bool calledNextScene;

    // canvas stuff
    Text runTimeText;
    TextMeshProUGUI screenMessageText;

    // size information
    float tileSizeX;
    float tileSizeY;
    float halfTileX;
    float halfTileY;
    float tileScreenSizeWidth;
    float tileScreenSizeHeight;
    float halfScreenWidth;
    float halfScreenHeight;

    // track doors and tiles animation
    bool isSwappingTiles;
    bool isDoorwayMoving;
    bool isDoorway1Open;
    bool isDoorway2Open;

    // player info
    GameObject player;
    Vector3 playerPosition;

    // need access to the boss
    GameObject bombMan;

    // these guys operate a little differently
    bool canSpawnKillerBomb;
    bool hasSpawnedKillerBomb;
    bool midStartKillerBomb;
    float delayKillerBomb;
    bool canSpawnMambu;
    bool hasSpawnedMambu;
    float delayMambu;

    // world view coords
    GameManager.WorldViewCoordinates worldView;

    // objects status
    IDictionary<string, bool> objectActive = new Dictionary<string, bool>();

    // objects position
    IDictionary<string, Vector3> objectPosition = new Dictionary<string, Vector3>();

    [Header("Scene Settings")]
    public Vector2Int tileScreenXY = new Vector2Int(16, 15);
    [SerializeField] bool showRunTime;

    public enum LevelStates { LevelPlay, BossFightIntro, BossFight, PlayerVictory, NextScene };
    public LevelStates levelState = LevelStates.LevelPlay;

    [Header("Audio Clips")]
    public AudioClip doorClip;
    public AudioClip musicClip;
    public AudioClip bossFightClip;
    public AudioClip victoryThemeClip;

    [Header("TileMap & Grid Objects")]
    // Теперь здесь массив гридов (можно добавить 4 и больше в инспекторе)
    public Grid[] levelGrids; 
    public Tilemap tmBackground;
    public Tilemap tmForeground;

    public TileBase[] bgTile1;
    public TileBase[] bgTile2;

    [Header("Boss Door Objects")]
    // Двери теперь GameObjects, а не тайлы
    public GameObject bossDoor1;
    public GameObject bossDoor2;

    [Header("Bonus Item Objects")]
    public GameObject prefabExtraLife;
    public GameObject prefabLifeEnergyBig;
    public GameObject prefabLifeEnergySmall;
    public GameObject prefabWeaponEnergyBig;
    public GameObject prefabWeaponPart;

    [Header("Enemy Objects")]
    public GameObject prefabKamadoma;
    public GameObject prefabBombombLauncher;
    public GameObject prefabSniperJoe;
    public GameObject prefabKillerBomb;
    public GameObject prefabMambu;
    public GameObject prefabBombMan;

    [Header("Camera Transitions")]
    public GameObject[] camTransitions;

    void Awake()
    {
        runTimeText = GameObject.Find("RunTime").GetComponent<Text>();
        screenMessageText = GameObject.Find("ScreenMessage").GetComponent<TextMeshProUGUI>();

        isSwappingTiles = false;
        isDoorwayMoving = false;
        isDoorway1Open = false;
        isDoorway2Open = true; // Изначально открыта, чтобы игрок мог войти

        // Берем размеры ячейки из первого грида в массиве (предполагается, что они одинаковые)
        if (levelGrids.Length > 0 && levelGrids[0] != null)
        {
            tileSizeX = levelGrids[0].cellSize.x;
            tileSizeY = levelGrids[0].cellSize.y;
        }
        else
        {
            tileSizeX = 0.16f; // Значение по умолчанию
            tileSizeY = 0.16f;
        }

        halfTileX = tileSizeX / 2f;
        halfTileY = tileSizeY / 2f;

        tileScreenSizeWidth = (float)tileScreenXY.x * tileSizeX;
        tileScreenSizeHeight = (float)tileScreenXY.y * tileSizeY;

        halfScreenWidth = tileScreenSizeWidth / 2f;
        halfScreenHeight = tileScreenSizeHeight / 2f;

        // Инициализация статусов (Active = false)
        for (int i = 1; i <= 5; i++) objectActive["Kamadoma" + i] = false;
        for (int i = 1; i <= 4; i++) objectActive["BombombLauncher" + i] = false;
        for (int i = 1; i <= 3; i++) objectActive["SniperJoe" + i] = false;
        objectActive["KillerBomb"] = false;
        objectActive["Mambu"] = false;

        objectActive["ExtraLife1"] = false;
        objectActive["LifeEnergyBig1"] = false;
        objectActive["LifeEnergySmall1"] = false;
        objectActive["LifeEnergySmall2"] = false;
        objectActive["WeaponEnergyBig1"] = false;

        // КООРДИНАТЫ ОЧИЩЕНЫ (Заполнишь позже)
        for (int i = 1; i <= 5; i++) objectPosition["Kamadoma" + i] = Vector3.zero;
        for (int i = 1; i <= 4; i++) objectPosition["BombombLauncher" + i] = Vector3.zero;
        for (int i = 1; i <= 3; i++) objectPosition["SniperJoe" + i] = Vector3.zero;
        objectPosition["KillerBomb"] = Vector3.zero;
        objectPosition["Mambu"] = Vector3.zero;

        objectPosition["ExtraLife1"] = Vector3.zero;
        objectPosition["LifeEnergyBig1"] = Vector3.zero;
        objectPosition["LifeEnergySmall1"] = Vector3.zero;
        objectPosition["LifeEnergySmall2"] = Vector3.zero;
        objectPosition["WeaponEnergyBig1"] = Vector3.zero;

        objectPosition["BombMan"] = Vector3.zero;
        objectPosition["WeaponPart"] = Vector3.zero;
    }

    void Start()
    {
        GameManager.Instance.SetResolutionScale(GameManager.ResolutionScales.Scale4x3);
        GameManager.Instance.SetWeaponsMenuPalette(WeaponsMenu.MenuPalettes.BombMan);

        player = GameObject.FindGameObjectWithTag("Player");

        SoundManager.Instance.MusicSource.volume = 1.0f;
        SoundManager.Instance.PlayMusic(musicClip);
    }

    void Update()
    {
        runTimeText.text = showRunTime ? String.Format("RunTime: {0:0.00}", runTime) : "";

        switch (levelState)
        {
            case LevelStates.LevelPlay:
                if (player != null) playerPosition = player.transform.position;

                RemoveGameObjects();

                SpawnKamadomas();
                SpawnBombombLaunchers();
                SpawnSniperJoes();
                SpawnKillerBombs();
                SpawnMambus();
                SpawnBonusItems();

                if (SoundManager.Instance.MusicSource.time >= 37.361f)
                {
                    SoundManager.Instance.MusicSource.time = 7.545f;
                }
                break;

            case LevelStates.BossFightIntro:
                runTime = Time.time - startTime;

                if (UtilityFunctions.InTime(runTime, 0.001f))
                {
                    GameManager.Instance.AllowGamePause(false);
                    SoundManager.Instance.StopMusic();
                    SoundManager.Instance.MusicSource.volume = 1f;
                    SoundManager.Instance.PlayMusic(bossFightClip);
                    SwapTilesAnimation();

                    UIEnergyBars.Instance.SetValue(UIEnergyBars.EnergyBars.EnemyHealth, 0);
                    UIEnergyBars.Instance.SetImage(UIEnergyBars.EnergyBars.EnemyHealth, UIEnergyBars.EnergyBarTypes.BombMan);
                    UIEnergyBars.Instance.SetVisibility(UIEnergyBars.EnergyBars.EnemyHealth, true);

                    // СВЯЗЬ СО СКРИПТОМ CHARACTERCONTROL
                    if (player != null)
                    {
                        Charactercontrol charCtrl = player.GetComponent<Charactercontrol>();
                        if (charCtrl != null)
                        {
                            charCtrl.FreezeInput(true);
                            // Если у тебя в Charactercontrol появится публичный метод сброса лестницы, вызывай его тут:
                            // charCtrl.ResetClimbing(); 
                        }
                    }
                }

                if (UtilityFunctions.InTime(runTime, 3.0f)) ToggleDoorway2();
                if (UtilityFunctions.InTime(runTime, 4.0f)) InstantiateBombMan("BombMan", objectPosition["BombMan"]);
                if (UtilityFunctions.InTime(runTime, 4.5f))
                {
                    bombMan.GetComponent<BombManController>().Pose();
                    StartCoroutine(FillEnemyHealthBar());
                }

                if (UtilityFunctions.InTime(runTime, 5.75f))
                {
                    bombMan.GetComponent<BombManController>().EnableAI(true);
                    if (player != null)
                    {
                        Charactercontrol charCtrl = player.GetComponent<Charactercontrol>();
                        if (charCtrl != null) charCtrl.FreezeInput(false);
                    }
                    GameManager.Instance.AllowGamePause(true);
                    levelState = LevelStates.BossFight;
                }
                break;

            case LevelStates.BossFight:
                if (SoundManager.Instance.MusicSource.time >= 15.974f)
                {
                    SoundManager.Instance.MusicSource.time = 3.192f;
                }
                break;

            case LevelStates.PlayerVictory:
                runTime = Time.time - startTime;
                if (UtilityFunctions.InTime(runTime, 7.0f)) GameManager.Instance.TallyPlayerScore();
                if (UtilityFunctions.InTime(runTime, 15.0f))
                {
                    GameManager.Instance.ResetPointsCollected(true, false);
                    GameManager.Instance.SetLevelCompleted(GameManager.StagesList.BombMan);
                    levelState = LevelStates.NextScene;
                }
                break;

            case LevelStates.NextScene:
                if (!calledNextScene)
                {
                    GameManager.Instance.StartNextScene(GameManager.GameScenes.StageSelect);
                    calledNextScene = true;
                }
                break;
        }
    }

    public void ToggleDoorway1(bool playAudio = true)
    {
        if (!isDoorwayMoving && bossDoor1 != null)
        {
            StartCoroutine(ToggleDoorwayGameObject(bossDoor1, ref isDoorway1Open));
            if (playAudio) SoundManager.Instance.Play(doorClip);
        }
    }

    public void ToggleDoorway2(bool playAudio = true)
    {
        if (!isDoorwayMoving && bossDoor2 != null)
        {
            StartCoroutine(ToggleDoorwayGameObject(bossDoor2, ref isDoorway2Open));
            if (playAudio) SoundManager.Instance.Play(doorClip);
        }
    }

    // Универсальная корутина для анимации GameObject дверей
    private IEnumerator ToggleDoorwayGameObject(GameObject door, ref bool isOpenState)
    {
        isDoorwayMoving = true;

        // Здесь можно запустить анимацию: door.GetComponent<Animator>().SetTrigger("Toggle");
        // Пока сделано простое скрытие/появление через небольшую задержку
        yield return new WaitForSeconds(0.6f); 
        
        isOpenState = !isOpenState;
        door.SetActive(!isOpenState); // Если закрываем, объект активен. Если открываем - выключен.

        isDoorwayMoving = false;
    }

    public void SwapTilesAnimation()
    {
        if (!isSwappingTiles) StartCoroutine(SwapTilesAnimationCo());
    }

    private IEnumerator SwapTilesAnimationCo()
    {
        isSwappingTiles = true;
        for (int i = 0; i < 5; i++)
        {
            tmBackground.SwapTile(bgTile1[0], bgTile2[0]);
            tmBackground.SwapTile(bgTile1[1], bgTile2[1]);
            yield return new WaitForSeconds(0.15f);

            tmBackground.SwapTile(bgTile2[0], bgTile1[0]);
            tmBackground.SwapTile(bgTile2[1], bgTile1[1]);
            yield return new WaitForSeconds(0.15f);
        }
        isSwappingTiles = false;
    }

    private IEnumerator FillEnemyHealthBar()
    {
        int maxHealth = bombMan.GetComponent<EnemyController>().maxHealth;
        SoundManager.Instance.Play(bombMan.GetComponent<EnemyController>().energyFillClip, true);
        for (int i = 1; i <= maxHealth; i++)
        {
            float bars = (float)i / (float)maxHealth;
            UIEnergyBars.Instance.SetValue(UIEnergyBars.EnergyBars.EnemyHealth, bars);
            yield return new WaitForSeconds(0.025f);
        }
        SoundManager.Instance.Stop();
    }

    public void BossDefeated()
    {
        SoundManager.Instance.StopMusic();
        UIEnergyBars.Instance.SetVisibility(UIEnergyBars.EnergyBars.EnemyHealth, false);
        GameManager.Instance.DestroyWeapons();
        if (player != null) player.GetComponent<Charactercontrol>().Invincible(true);
        InstantiateWeaponPart("WeaponPart", objectPosition["WeaponPart"], ItemScript.WeaponPartColors.Orange);
    }

    private void WeaponPartCollected()
    {
        startTime = Time.time;
        SoundManager.Instance.MusicSource.volume = 1f;
        SoundManager.Instance.PlayMusic(victoryThemeClip, false);
        GameManager.Instance.FreezePlayer(true);
        GameManager.Instance.AllowGamePause(false);
        levelState = LevelStates.PlayerVictory;
    }

    bool OutOfCameraView(Vector3 point)
    {
        return (point.x < worldView.Left || point.x > worldView.Right ||
            point.y > worldView.Top || point.y < worldView.Bottom);
    }

    public void RemoveGameObjects(bool removeAll = false)
    {
        worldView = GameManager.Instance.worldViewCoords;

        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        for (int i = 0; i < enemies.Length; i++)
        {
            if (OutOfCameraView(enemies[i].transform.position) || removeAll)
            {
                if (enemies[i].name.Equals("KillerBomb") || enemies[i].name.Equals("Mambu"))
                {
                    if (removeAll || (enemies[i].name.Equals("Mambu") && playerPosition.x > TileWorldPos(201, 0)))
                    {
                        canSpawnMambu = false;
                        Destroy(enemies[i]);
                    }
                    else
                    {
                        MoveGameObjects(enemies[i]);
                    }
                }
                else
                {
                    Destroy(enemies[i]);
                }
            }
        }

        GameObject[] explosions = GameObject.FindGameObjectsWithTag("Explosion");
        for (int i = 0; i < explosions.Length; i++)
        {
            if (OutOfCameraView(explosions[i].transform.position) || removeAll) Destroy(explosions[i]);
        }

        ItemScript[] itemScripts = GameObject.FindObjectsOfType<ItemScript>();
        for (int i = 0; i < itemScripts.Length; i++)
        {
            if (OutOfCameraView(itemScripts[i].gameObject.transform.position) || removeAll) Destroy(itemScripts[i].gameObject);
        }

        GameObject[] beams = GameObject.FindGameObjectsWithTag("PlatformBeam");
        for (int i = 0; i < beams.Length; i++)
        {
            if (OutOfCameraView(beams[i].gameObject.transform.position) || removeAll)
            {
                // Заменил PlayerController на методы Charactercontrol (если у тебя настроено оружие)
                // player.GetComponent<Charactercontrol>().CanUseWeaponAgain(); 
                Destroy(beams[i]);
            }
        }

        GameObject[] bombs = GameObject.FindGameObjectsWithTag("Bomb");
        for (int i = 0; i < bombs.Length; i++)
        {
            if (OutOfCameraView(bombs[i].gameObject.transform.position) || removeAll) Destroy(bombs[i]);
        }

        GameObject[] bullets = GameObject.FindGameObjectsWithTag("Bullet");
        for (int i = 0; i < bullets.Length; i++)
        {
            if (OutOfCameraView(bullets[i].gameObject.transform.position) || removeAll) Destroy(bullets[i]);
        }
    }

    void MoveGameObjects(GameObject go)
    {
        if (go.name.Equals("KillerBomb"))
        {
            go.transform.position = new Vector3(worldView.Right, playerPosition.y + tileSizeY);
            go.GetComponent<KillerBombController>().ResetFollowingPath();
        }
        else if (go.name.Equals("Mambu"))
        {
            go.transform.position = new Vector3(worldView.Right, go.transform.position.y);
            go.GetComponent<MambuController>().SetState(MambuController.MambuState.Closed);
        }
    }

    // Instantiation Methods
    void InstantiateKamadoma(string name, Vector3 position)
    {
        GameObject kamadoma = Instantiate(prefabKamadoma);
        kamadoma.name = name;
        kamadoma.transform.position = position;
        kamadoma.GetComponent<EnemyController>().SetBonusBallColor(ItemScript.BonusBallColors.Blue);
        kamadoma.GetComponent<EnemyController>().SetBonusItemType(ItemScript.ItemTypes.Random);
        kamadoma.GetComponent<KamadomaController>().SetColor(KamadomaController.KamadomaColors.Red);
        kamadoma.GetComponent<KamadomaController>().EnableAI(true);
        objectActive[name] = true;
    }

    void InstantiateBombombLauncher(string name, Vector3 position)
    {
        GameObject bombombLauncher = Instantiate(prefabBombombLauncher);
        bombombLauncher.name = name;
        bombombLauncher.transform.position = position;
        bombombLauncher.GetComponent<BombombController>().SetLaunchOnStart(true);
        bombombLauncher.GetComponent<BombombController>().SetLaunchDelay(3.5f);
        bombombLauncher.GetComponent<BombombController>().EnableAI(true);
        objectActive[name] = true;
    }

    void InstantiateSniperJoe(string name, Vector3 position, Vector2 velocity)
    {
        GameObject sniperJoe = Instantiate(prefabSniperJoe);
        sniperJoe.name = name;
        sniperJoe.transform.position = position;
        sniperJoe.GetComponent<EnemyController>().SetBonusBallColor(ItemScript.BonusBallColors.Orange);
        sniperJoe.GetComponent<EnemyController>().SetBonusItemType(ItemScript.ItemTypes.Random);
        sniperJoe.GetComponent<SniperJoeController>().SetJumpVector(velocity);
        sniperJoe.GetComponent<SniperJoeController>().EnableAI(true);
        objectActive[name] = true;
    }

    void InstantiateKillerBomb(string name, Vector3 position)
    {
        GameObject killerBomb = Instantiate(prefabKillerBomb);
        killerBomb.name = name;
        killerBomb.transform.position = position;
        killerBomb.GetComponent<EnemyController>().SetBonusBallColor(ItemScript.BonusBallColors.Orange);
        killerBomb.GetComponent<EnemyController>().SetBonusItemType(ItemScript.ItemTypes.Random);
        killerBomb.GetComponent<KillerBombController>().SetColor(KillerBombController.KillerBombColors.Orange);
        killerBomb.GetComponent<KillerBombController>().SetMoveDirection(KillerBombController.MoveDirections.Left);
        killerBomb.GetComponent<KillerBombController>().EnableAI(true);
    }

    void InstantiateMambu(string name, Vector3 position)
    {
        GameObject mambu = Instantiate(prefabMambu);
        mambu.name = name;
        mambu.transform.position = position;
        mambu.GetComponent<EnemyController>().SetBonusBallColor(ItemScript.BonusBallColors.Orange);
        mambu.GetComponent<EnemyController>().SetBonusItemType(ItemScript.ItemTypes.Random);
        mambu.GetComponent<MambuController>().EnableAI(true);
        objectActive[name] = true;
    }

    void InstantiateBombMan(string name, Vector3 position)
    {
        bombMan = Instantiate(prefabBombMan);
        bombMan.name = name;
        bombMan.transform.position = position;
        bombMan.GetComponent<EnemyController>().SetBonusItemType(ItemScript.ItemTypes.Nothing);
        bombMan.GetComponent<EnemyController>().DefeatEvent.AddListener(this.BossDefeated);
    }

    void InstantiateExtraLife(string name, Vector3 position)
    {
        GameObject extraLife = Instantiate(prefabExtraLife);
        extraLife.name = name;
        extraLife.transform.position = position;
        objectActive[name] = true;
    }

    void InstantiateLifeEnergyBig(string name, Vector3 position)
    {
        GameObject lifeEnergyBig = Instantiate(prefabLifeEnergyBig);
        lifeEnergyBig.name = name;
        lifeEnergyBig.transform.position = position;
        objectActive[name] = true;
    }

    void InstantiateLifeEnergySmall(string name, Vector3 position)
    {
        GameObject lifeEnergySmall = Instantiate(prefabLifeEnergySmall);
        lifeEnergySmall.name = name;
        lifeEnergySmall.transform.position = position;
        objectActive[name] = true;
    }

    void InstantiateWeaponEnergyBig(string name, Vector3 position)
    {
        GameObject weaponEnergyBig = Instantiate(prefabWeaponEnergyBig);
        weaponEnergyBig.name = name;
        weaponEnergyBig.transform.position = position;
        objectActive[name] = true;
    }

    void InstantiateWeaponPart(string name, Vector3 position, ItemScript.WeaponPartColors color)
    {
        GameObject weaponPart = Instantiate(prefabWeaponPart);
        weaponPart.name = name;
        weaponPart.transform.position = position;
        weaponPart.GetComponent<ItemScript>().SetWeaponPartColor(color);
        weaponPart.GetComponent<ItemScript>().SetWeaponPartEnemy(ItemScript.WeaponPartEnemies.BombMan);
        weaponPart.GetComponent<ItemScript>().BonusItemEvent.AddListener(this.WeaponPartCollected);
    }

    // Spawn Methods (Логика спавна оставлена, координаты ты заполнишь сам)
    void SpawnKamadomas()
    {
        if ((Between(playerPosition.x, TileWorldPos(1), halfTileX) || Between(playerPosition.x, TileWorldPos(16), halfTileX)) &&
            Between(playerPosition.y, TileWorldPos(0), halfScreenHeight) &&
            GameObject.Find("Kamadoma1") == null && !objectActive["Kamadoma1"])
        {
            InstantiateKamadoma("Kamadoma1", objectPosition["Kamadoma1"]);
        }

        if ((Between(playerPosition.x, TileWorldPos(0), halfTileX) || Between(playerPosition.x, TileWorldPos(17), halfTileX)) &&
            Between(playerPosition.y, TileWorldPos(0), halfScreenHeight) &&
            GameObject.Find("Kamadoma1") == null && objectActive["Kamadoma1"])
        {
            objectActive["Kamadoma1"] = false;
        }

        // Остальные проверки Kamadoma 2-5 можно оставить как есть или дублировать по шаблону выше
    }

    void SpawnBombombLaunchers()
    {
        if ((Between(playerPosition.x, TileWorldPos(35), halfTileX) || Between(playerPosition.x, TileWorldPos(50), halfTileX)) &&
            Between(playerPosition.y, TileWorldPos(0), halfScreenHeight) &&
            GameObject.Find("BombombLauncher1") == null && !objectActive["BombombLauncher1"])
        {
            InstantiateBombombLauncher("BombombLauncher1", objectPosition["BombombLauncher1"]);
        }

        if ((Between(playerPosition.x, TileWorldPos(34), halfTileX) || Between(playerPosition.x, TileWorldPos(51), halfTileX)) &&
            Between(playerPosition.y, TileWorldPos(0), halfScreenHeight) &&
            GameObject.Find("BombombLauncher1") == null && objectActive["BombombLauncher1"])
        {
            objectActive["BombombLauncher1"] = false;
        }
    }

    void SpawnSniperJoes()
    {
        if (Between(playerPosition.x, TileWorldPos(94), halfTileX) && Between(playerPosition.y, TileWorldPos(30), halfScreenHeight) &&
            GameObject.Find("SniperJoe1") == null && !objectActive["SniperJoe1"])
        {
            InstantiateSniperJoe("SniperJoe1", objectPosition["SniperJoe1"], new Vector2(-0.8f, 0.25f));
        }

        if (Between(playerPosition.x, TileWorldPos(93), halfTileX) && Between(playerPosition.y, TileWorldPos(30), halfScreenHeight) &&
            GameObject.Find("SniperJoe1") == null && objectActive["SniperJoe1"])
        {
            objectActive["SniperJoe1"] = false;
        }
    }

    void SpawnKillerBombs()
    {
        if (!GameManager.Instance.InCameraTransition())
        {
            if (Between(playerPosition.y, TileWorldPos(30), halfScreenHeight) || Between(playerPosition.y, TileWorldPos(45), halfScreenHeight))
            {
                if (playerPosition.x > TileWorldPos(86, 0) && playerPosition.x < TileWorldPos(182, 0) &&
                    GameObject.Find("KillerBomb") == null && !objectActive["KillerBomb"] && canSpawnKillerBomb)
                {
                    if (!hasSpawnedKillerBomb)
                    {
                        hasSpawnedKillerBomb = true;
                        Invoke("SpawnKillerBombDelayed", delayKillerBomb);
                    }
                }

                if (playerPosition.x > TileWorldPos(86, 0) && playerPosition.x < TileWorldPos(182, 0) &&
                    GameObject.Find("KillerBomb") == null && objectActive["KillerBomb"])
                {
                    objectActive["KillerBomb"] = false;
                }

                if (Between(playerPosition.x, TileWorldPos(118), halfTileX) || Between(playerPosition.x, TileWorldPos(134), halfTileX))
                {
                    canSpawnKillerBomb = true;
                    delayKillerBomb = 0f;
                }
            }
        }
    }

    public void CanSpawnKillerBomb()
    {
        canSpawnKillerBomb = true;
        midStartKillerBomb = true;
        delayKillerBomb = 0f;
    }

    void SpawnKillerBombDelayed()
    {
        objectPosition["KillerBomb"] = new Vector3(worldView.Right, midStartKillerBomb ? TileWorldPos(45, 0) : playerPosition.y + tileSizeY);
        InstantiateKillerBomb("KillerBomb", objectPosition["KillerBomb"]);
        hasSpawnedKillerBomb = false;
        midStartKillerBomb = false;
        delayKillerBomb = 2.0f;
    }

    void SpawnMambus()
    {
        if (Between(playerPosition.y, TileWorldPos(60), halfScreenHeight))
        {
            if (playerPosition.x > TileWorldPos(166, 0) && playerPosition.x < TileWorldPos(201, 0) &&
                GameObject.Find("Mambu") == null && !objectActive["Mambu"] && canSpawnMambu)
            {
                if (!hasSpawnedMambu)
                {
                    hasSpawnedMambu = true;
                    Invoke("SpawnMambuDelayed", delayMambu);
                }
            }

            if (playerPosition.x > TileWorldPos(166, 0) && playerPosition.x < TileWorldPos(201, 0) &&
                GameObject.Find("Mambu") == null && objectActive["Mambu"])
            {
                objectActive["Mambu"] = false;
            }

            if (Between(playerPosition.x, TileWorldPos(188), halfTileX))
            {
                canSpawnMambu = true;
                delayMambu = 0f;
            }
        }
    }

    void SpawnMambuDelayed()
    {
        objectPosition["Mambu"] = new Vector3(worldView.Right, objectPosition["Mambu"].y);
        InstantiateMambu("Mambu", objectPosition["Mambu"]);
        hasSpawnedMambu = false;
        delayMambu = 2.0f;
    }

    void SpawnBonusItems()
    {
        if (Between(playerPosition.x, TileWorldPos(80), halfTileX) && Between(playerPosition.y, TileWorldPos(0), halfScreenHeight) &&
            GameObject.Find("LifeEnergySmall1") == null && !objectActive["LifeEnergySmall1"])
        {
            InstantiateLifeEnergySmall("LifeEnergySmall1", objectPosition["LifeEnergySmall1"]);
        }

        if (Between(playerPosition.x, TileWorldPos(79), halfTileX) && Between(playerPosition.y, TileWorldPos(0), halfScreenHeight) &&
            GameObject.Find("LifeEnergySmall1") == null && objectActive["LifeEnergySmall1"])
        {
            objectActive["LifeEnergySmall1"] = false;
        }
    }

    public void PostTransitionEvent()
    {
        canSpawnKillerBomb = false;
        canSpawnMambu = false;
        midStartKillerBomb = false;

        if (Between(playerPosition.x, TileWorldPos(93), halfScreenWidth) && Between(playerPosition.y, TileWorldPos(6), halfTileY))
        {
            InstantiateLifeEnergySmall("LifeEnergySmall1", objectPosition["LifeEnergySmall1"]);
            InstantiateLifeEnergySmall("LifeEnergySmall2", objectPosition["LifeEnergySmall2"]);
            InstantiateWeaponEnergyBig("WeaponEnergyBig1", objectPosition["WeaponEnergyBig1"]);
            camTransitions[0].transform.position = new Vector3(camTransitions[0].transform.position.x, 0.986f);
        }
        else if (Between(playerPosition.x, TileWorldPos(93), halfScreenWidth) &&
            (Between(playerPosition.y, TileWorldPos(7), halfTileY) || Between(playerPosition.y, TileWorldPos(21), halfTileY)))
        {
            InstantiateLifeEnergyBig("LifeEnergyBig1", objectPosition["LifeEnergyBig1"]);
            camTransitions[0].transform.position = new Vector3(camTransitions[0].transform.position.x, 0.696f);
            camTransitions[1].transform.position = new Vector3(camTransitions[1].transform.position.x, 3.386f);
        }
        else if (Between(playerPosition.x, TileWorldPos(93), halfScreenWidth) && Between(playerPosition.y, TileWorldPos(22), halfTileY))
        {
            camTransitions[1].transform.position = new Vector3(camTransitions[1].transform.position.x, 3.096f);
        }
        else if (Between(playerPosition.x, TileWorldPos(173), halfScreenWidth) && Between(playerPosition.y, TileWorldPos(36), halfTileY))
        {
            camTransitions[2].transform.position = new Vector3(camTransitions[2].transform.position.x, 5.783f);
        }
        else if (Between(playerPosition.x, TileWorldPos(173), halfScreenWidth) &&
            (Between(playerPosition.y, TileWorldPos(37), halfTileY) || Between(playerPosition.y, TileWorldPos(51), halfTileY)))
        {
            CanSpawnKillerBomb();
            camTransitions[2].transform.position = new Vector3(camTransitions[2].transform.position.x, 5.493f);
            camTransitions[3].transform.position = new Vector3(camTransitions[3].transform.position.x, 8.201f);
        }
        else if (Between(playerPosition.x, TileWorldPos(173), halfScreenWidth) && Between(playerPosition.y, TileWorldPos(52), halfTileY))
        {
            canSpawnMambu = true;
            delayMambu = 0f;
            camTransitions[3].transform.position = new Vector3(camTransitions[3].transform.position.x, 7.911f);
        }
        else if (Between(playerPosition.x, TileWorldPos(253), halfScreenWidth) && Between(playerPosition.y, TileWorldPos(52), halfTileY))
        {
            camTransitions[5].transform.position = new Vector3(camTransitions[5].transform.position.x, 7.88f);
        }
        else if (Between(playerPosition.x, TileWorldPos(253), halfScreenWidth) && Between(playerPosition.y, TileWorldPos(22), tileSizeY * 2f))
        {
            startTime = Time.time;
            levelState = LevelStates.BossFightIntro;
        }
    }

    bool Between(float a, float b, float margin)
    {
        return (a > (b - margin)) && (a < (b + margin));
    }

    float TileWorldPos(int tilePosition, float offset = 0.5f)
    {
        return (float)tilePosition * 0.16f + offset * 0.16f;
    }
}
