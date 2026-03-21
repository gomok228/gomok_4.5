using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class InventoryManager : MonoBehaviour
{
    public List<Item> items = new List<Item>();
    private const int MAX_SLOTS = 4; // Лимит по условию конкурса
    private string savePath;

    void Awake()
    {
        // Путь к файлу на ПК: C:/Users/Имя/AppData/LocalLow/Company/Game/inventory.json
        savePath = Path.Combine(Application.persistentDataPath, "inventory.json");
        LoadInventory();
    }

    public bool AddItem(string name, int id, int count)
    {
        if (items.Count >= MAX_SLOTS)
        {
            Debug.LogWarning("Инвентарь полон! (Макс. 4 предмета)");
            return false;
        }

        items.Add(new Item(name, id, count));
        SaveInventory();
        return true;
    }

    public void SaveInventory()
    {
        InventoryData data = new InventoryData { itemList = items };
        string json = JsonUtility.ToJson(data, true); // Генерируем красивый JSON
        File.WriteAllText(savePath, json);
        Debug.Log("Данные сохранены в JSON!");
    }

    public void LoadInventory()
    {
        if (File.Exists(savePath))
        {
            string json = File.ReadAllText(savePath);
            InventoryData data = JsonUtility.FromJson<InventoryData>(json);
            items = data.itemList;
            Debug.Log("Данные загружены из JSON!");
        }
    }
}

// Обертка, так как JsonUtility не ест List напрямую
[System.Serializable]
public class InventoryData { public List<Item> itemList; }
