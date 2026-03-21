using UnityEngine;

public class InventoryTester : MonoBehaviour
{
    public InventoryManager manager;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) manager.AddItem("Меч", 1, 1);
        if (Input.GetKeyDown(KeyCode.Alpha2)) manager.AddItem("Зелье", 2, 5);
        if (Input.GetKeyDown(KeyCode.Alpha3)) manager.AddItem("Щит", 3, 1);
        if (Input.GetKeyDown(KeyCode.Alpha4)) manager.AddItem("Яблоко", 4, 10);
        
        if (Input.GetKeyDown(KeyCode.C)) // Очистить всё
        {
            manager.items.Clear();
            manager.SaveInventory();
        }
    }
}
