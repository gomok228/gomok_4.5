using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance = null;

    [Header("Scene Management")]
    public GameScenes gameScene = GameScenes.TitleScreen;
    bool startNextScene;
    string nextSceneName;
    string lastSceneName;

    [Header("Player Stats")]
    public int playerLives = 3;
    public int playerScore = 0;
    public int levelPoints = 0;
    public int gamePlayerStartLives = 3;
    private List<int> bonusScore = new List<int>();
    
    GameObject player;
    CameraFollow cameraFollow;

    [Header("Enemy & Item Spawning")]
    public AssetPalette assetPalette; // Обязательно назначь в инспекторе!
    GameObject[] itemPrefabs;

    [Header("Game State")]
    bool isGameOver;
    bool isGamePaused;
    bool canPauseGame = true;
    float gameRestartTime;
    public float gameRestartDelay = 5f;

    public enum ResolutionScales { Scale16x9, Scale4x3 };
    public ResolutionScales resolutionScale = ResolutionScales.Scale16x9;

    public enum GameScenes
    {
        TitleScreen,
        StageSelect,
        BombManStage, 
        GameOver
    };

    public enum StagesList
    {
        BombMan,
        // Сюда добавишь остальных боссов потом
    };

    [System.Serializable]
    public struct StagesStruct
    {
        public GameScenes GameScene;
        public bool Completed;
    }
    public StagesStruct[] GameStages;

    [System.Serializable]
    public struct WorldViewCoordinates
    {
        public float Top;
        public float Right;
        public float Bottom;
        public float Left;
    }
    public WorldViewCoordinates worldViewCoords;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);

        DontDestroyOnLoad(gameObject);

        if (assetPalette == null)
        {
            assetPalette = GetComponent<AssetPalette>();
        }
        
        playerLives = gamePlayerStartLives;
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        player = GameObject.FindGameObjectWithTag("Player");
        if (Camera.main != null) cameraFollow = Camera.main.GetComponent<CameraFollow>();

        if (gameScene == GameScenes.BombManStage)
        {
            StartBombManStage();
        }
    }

    private void Update()
    {
        switch (gameScene)
        {
            case GameScenes.BombManStage:
                BombManLoop();
                break;
            case GameScenes.TitleScreen:
                if (Input.anyKeyDown && !startNextScene) StartNextScene(GameScenes.BombManStage);
                break;
        }

        if (startNextScene)
        {
            startNextScene = false;
            SceneManager.LoadScene(nextSceneName);
        }

        if (Input.GetKeyDown(KeyCode.P)) ToggleSimplePause();
    }

    // --- ЛОГИКА BOMB MAN STAGE ---
    private void StartBombManStage()
    {
        isGameOver = false;
        canPauseGame = true;
        FreezePlayer(true);
        Invoke("FinishIntro", 1.5f);
    }

    private void FinishIntro()
    {
        FreezePlayer(false);
    }

    private void BombManLoop()
    {
        if (!isGameOver)
        {
            GetWorldViewCoordinates();
            RepositionEnemies(); 
            DestroyStrayBullets(); 
        }
        else
        {
            gameRestartTime -= Time.deltaTime;
            if (gameRestartTime < 0)
            {
                if (playerLives > 0) SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                else StartNextScene(GameScenes.TitleScreen);
            }
        }
    }

    // --- УПРАВЛЕНИЕ МИРОМ (FREEZE/HIDE/DESTROY) ---
    public void FreezeEverything(bool freeze)
    {
        FreezeEnemies(freeze);
        FreezeWeapons(freeze);
    }

    public void FreezeEnemies(bool freeze)
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (GameObject enemy in enemies)
        {
            var controller = enemy.GetComponent<EnemyController>(); 
            if(controller != null) controller.FreezeEnemy(freeze);
        }
    }

    public void FreezeWeapons(bool freeze)
    {
        GameObject[] bullets = GameObject.FindGameObjectsWithTag("Bullet");
        foreach (GameObject b in bullets) 
        {
            var bs = b.GetComponent<BulletScript>();
            if(bs != null) bs.FreezeBullet(freeze);
        }
    }

    public void FreezePlayer(bool freeze)
    {
        if (player != null)
        {
            var pc = player.GetComponent<Charactercontrol>(); 
            if(pc != null) {
                pc.FreezeInput(freeze);
                pc.FreezePlayer(freeze);
            }
        }
    }

    public void DestroyWeapons()
    {
        GameObject[] beams = GameObject.FindGameObjectsWithTag("PlatformBeam");
        foreach (GameObject beam in beams) Destroy(beam);

        GameObject[] bombs = GameObject.FindGameObjectsWithTag("Bomb");
        foreach (GameObject bomb in bombs) Destroy(bomb);

        GameObject[] bullets = GameObject.FindGameObjectsWithTag("Bullet");
        foreach (GameObject bullet in bullets) Destroy(bullet);

        GameObject[] explosions = GameObject.FindGameObjectsWithTag("Explosion");
        foreach (GameObject explosion in explosions) Destroy(explosion);
    }

    // --- СИСТЕМА ЛУТА И ВЕЩЕЙ (BONUS ITEMS) ---
    
    public void SetResolutionScale(ResolutionScales scale)
    {
        this.resolutionScale = scale;
    }

    public void SetWeaponsMenuPalette(WeaponsMenu.MenuPalettes palette)
    {
        // Заглушка, чтобы не ругалось, раз у нас нет меню
    }

    private ItemScript.ItemTypes PickRandomBonusItem()
    {
        // Вероятности дропа как в оригинале
        float[] probabilities = { 12, 53, 15, 15, 2, 2, 1, 12, 16 };
        float total = probabilities.Sum();

        ItemScript.ItemTypes[] items = {
            ItemScript.ItemTypes.Nothing,
            ItemScript.ItemTypes.BonusBall,
            ItemScript.ItemTypes.WeaponEnergySmall,
            ItemScript.ItemTypes.LifeEnergySmall,
            ItemScript.ItemTypes.WeaponEnergyBig,
            ItemScript.ItemTypes.LifeEnergyBig,
            ItemScript.ItemTypes.ExtraLife,
            ItemScript.ItemTypes.Nothing,
            ItemScript.ItemTypes.BonusBall
        };

        float randomPoint = UnityEngine.Random.value * total;

        for (int i = 0; i < probabilities.Length; i++)
        {
            if (randomPoint < probabilities[i]) return items[i];
            else randomPoint -= probabilities[i];
        }
        return items[probabilities.Length - 1];
    }

    public GameObject GetBonusItem(ItemScript.ItemTypes itemType)
    {
        GameObject bonusItem = null;

        if (itemType == ItemScript.ItemTypes.Random)
        {
            itemType = PickRandomBonusItem();
        }

        switch (resolutionScale)
        {
            case ResolutionScales.Scale16x9:
                itemPrefabs = assetPalette.itemPrefabs_16x9;
                break;
            case ResolutionScales.Scale4x3:
                itemPrefabs = assetPalette.itemPrefabs_4x3;
                break;
        }

        if (itemPrefabs == null || itemPrefabs.Length == 0) return null; // Защита от ошибок

        switch (itemType)
        {
            case ItemScript.ItemTypes.Nothing:
                bonusItem = null;
                break;
            case ItemScript.ItemTypes.BonusBall:
                bonusItem = itemPrefabs[(int)AssetPalette.ItemList.BonusBall];
                break;
            case ItemScript.ItemTypes.ExtraLife:
                bonusItem = itemPrefabs[(int)AssetPalette.ItemList.ExtraLife];
                break;
            case ItemScript.ItemTypes.LifeEnergyBig:
                bonusItem = itemPrefabs[(int)AssetPalette.ItemList.LifeEnergyBig];
                break;
            case ItemScript.ItemTypes.LifeEnergySmall:
                bonusItem = itemPrefabs[(int)AssetPalette.ItemList.LifeEnergySmall];
                break;
            case ItemScript.ItemTypes.WeaponEnergyBig:
                bonusItem = itemPrefabs[(int)AssetPalette.ItemList.WeaponEnergyBig];
                break;
            case ItemScript.ItemTypes.WeaponEnergySmall:
                bonusItem = itemPrefabs[(int)AssetPalette.ItemList.WeaponEnergySmall];
                break;
            case ItemScript.ItemTypes.MagnetBeam:
                bonusItem = itemPrefabs[(int)AssetPalette.ItemList.MagnetBeam];
                break;
            case ItemScript.ItemTypes.WeaponPart:
                bonusItem = itemPrefabs[(int)AssetPalette.ItemList.WeaponPart];
                break;
            case ItemScript.ItemTypes.Yashichi:
                bonusItem = itemPrefabs[(int)AssetPalette.ItemList.Yashichi];
                break;
        }

        return bonusItem;
    }

    public void SetBonusItemsColorPalette()
    {
        ItemScript[] itemScripts = GameObject.FindObjectsOfType<ItemScript>();
        foreach (ItemScript itemScript in itemScripts)
        {
            itemScript.SetColorPalette();
        }
    }

    // --- ОЧКИ И ЗАВЕРШЕНИЕ УРОВНЯ ---
    
    public void ResetPointsCollected(bool resetLevelPoints = true, bool resetPlayerScore = true)
    {
        bonusScore.Clear();
        if (resetLevelPoints) levelPoints = 0;
        if (resetPlayerScore) playerScore = 0;
    }

    public void SetLevelCompleted(StagesList stage)
    {
        // Защита от выхода за пределы массива
        if (GameStages != null && (int)stage < GameStages.Length)
        {
            GameStages[(int)stage].Completed = true;
        }
    }

    public void TallyPlayerScore()
    {
        // Облегченный подсчет очков без UI Canvas
        playerScore += levelPoints;
        foreach (int bonus in bonusScore)
        {
            playerScore += bonus;
        }
        
        // Звук завершения подсчета
        if(SoundManager.Instance != null && assetPalette != null)
            SoundManager.Instance.Play(assetPalette.pointTallyEndClip);
    }

    public void AllowGamePause(bool pause)
    {
        canPauseGame = pause;
    }

    // --- ВРАГИ И РЕСПАВН ---
    private void RepositionEnemies()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (GameObject enemy in enemies)
        {
            if (enemy.transform.position.y < worldViewCoords.Bottom - 2f)
            {
                Destroy(enemy); 
            }
        }
    }

    private void DestroyStrayBullets()
    {
        GameObject[] bullets = GameObject.FindGameObjectsWithTag("Bullet");
        foreach (GameObject bullet in bullets)
        {
            if (bullet.transform.position.x < worldViewCoords.Left - 1f ||
                bullet.transform.position.x > worldViewCoords.Right + 1f)
            {
                Destroy(bullet);
            }
        }
    }

    // --- СИСТЕМА ЖИЗНЕЙ ---
    public void PlayerDefeated()
    {
        if (isGameOver) return;
        
        isGameOver = true;
        playerLives--;
        gameRestartTime = gameRestartDelay;
        
        FreezeEverything(true);
        if(SoundManager.Instance != null) SoundManager.Instance.StopMusic();
    }

    // --- ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ---
    public void StartNextScene(GameScenes scene)
    {
        startNextScene = true;
        nextSceneName = scene.ToString();
        gameScene = scene;
    }

    private void ToggleSimplePause()
    {
        if (!canPauseGame) return;

        isGamePaused = !isGamePaused;
        Time.timeScale = isGamePaused ? 0 : 1;
        if(SoundManager.Instance != null) {
            if (isGamePaused) SoundManager.Instance.MusicSource.Pause();
            else SoundManager.Instance.MusicSource.Play();
        }
    }

    private void GetWorldViewCoordinates()
    {
        Vector3 wv0 = Camera.main.ViewportToWorldPoint(new Vector3(0, 0, 0));
        Vector3 wv1 = Camera.main.ViewportToWorldPoint(new Vector3(1, 1, 0));
        worldViewCoords.Left = wv0.x;
        worldViewCoords.Bottom = wv0.y;
        worldViewCoords.Right = wv1.x;
        worldViewCoords.Top = wv1.y;
    }
}
