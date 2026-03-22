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
    public int gamePlayerStartLives = 3;
    
    GameObject player;
    CameraFollow cameraFollow;

    [Header("Enemy & Item Spawning")]
    public AssetPalette assetPalette; // Обязательно назначь в инспекторе!
    GameObject[] enemyPrefabs;
    GameObject[] itemPrefabs;
    int enemyPrefabCount;

    [Header("Game State")]
    bool isGameOver;
    bool isGamePaused;
    bool canPauseGame = true;
    float gameRestartTime;
    public float gameRestartDelay = 5f;

    public enum GameScenes
    {
        TitleScreen,
        StageSelect,
        BombManStage, // Твой основной уровень
        GameOver
    };

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
        
        // Инициализация при старте
        playerLives = gamePlayerStartLives;
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Ищем игрока и камеру на новой сцене
        player = GameObject.FindGameObjectWithTag("Player");
        cameraFollow = Camera.main?.GetComponent<CameraFollow>();

        if (gameScene == GameScenes.BombManStage)
        {
            StartBombManStage();
        }
    }

    private void Update()
    {
        // Логика состояний
        switch (gameScene)
        {
            case GameScenes.BombManStage:
                BombManLoop();
                break;
            case GameScenes.TitleScreen:
                if (Input.anyKeyDown && !startNextScene) StartNextScene(GameScenes.BombManStage);
                break;
        }

        // Переход между сценами
        if (startNextScene)
        {
            startNextScene = false;
            SceneManager.LoadScene(nextSceneName);
        }

        // Пауза (без меню, просто остановка времени)
        if (Input.GetKeyDown(KeyCode.P))
        {
            ToggleSimplePause();
        }
    }

    // --- ЛОГИКА BOMB MAN STAGE ---
    private void StartBombManStage()
    {
        isGameOver = false;
        canPauseGame = true;
        
        // Заморозка игрока в самом начале (как в оригинале при телепортации)
        FreezePlayer(true);
        Invoke("FinishIntro", 1.5f); // Через 1.5 сек начинаем играть
    }

    private void FinishIntro()
    {
        FreezePlayer(false);
        TeleportPlayer(true);
    }

    private void BombManLoop()
    {
        if (!isGameOver)
        {
            GetWorldViewCoordinates();
            RepositionEnemies(); // Логика, чтобы враги не пропадали
            DestroyStrayBullets(); // Чистим память от пуль за экраном
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

    // --- УПРАВЛЕНИЕ МИРОМ (FREEZE/HIDE) ---
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
            // Пытаемся найти компонент контроллера врага
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
            var pc = player.GetComponent<Charactercontrol>(); // Твой основной скрипт
            if(pc != null) {
                pc.FreezeInput(freeze);
                pc.FreezePlayer(freeze);
            }
        }
    }

    // --- ВРАГИ И РЕСПАВН ---
    private void RepositionEnemies()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (GameObject enemy in enemies)
        {
            // Если враг упал слишком низко за экран — возвращаем или удаляем
            if (enemy.transform.position.y < worldViewCoords.Bottom - 2f)
            {
                Destroy(enemy); // В твоем случае проще удалять и ждать нового спавна
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

    private void TeleportPlayer(bool teleport)
    {
        // Вызываем телепортацию в твоем Charactercontrol, если она там есть
        // player.GetComponent<Charactercontrol>().Teleport(teleport);
    }
}
