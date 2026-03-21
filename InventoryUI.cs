using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    public InventoryManager manager;
    public Text[] slotTexts; // Перетащи сюда 4 текста из Canvas в инспекторе

    void Update()
    {
        for (int i = 0; i < slotTexts.Length; i++)
        {
            if (i < manager.items.Count)
                slotTexts[i].text = $"{manager.items[i].itemName} x{manager.items[i].amount}";
            else
                slotTexts[i].text = "[ ПУСТО ]";
        }
    }
}
