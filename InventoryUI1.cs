using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    public InventoryManager manager; // Ссылка на наш менеджер

    [Header("UI Elements")]
    public GameObject[] slots;      // Сюда перетащи 4 объекта Slot_0, Slot_1 и т.д.
    public Text[] itemTexts;        // Сюда перетащи 4 текста из этих слотов
    
    public Color activeColor = Color.white;    // Цвет когда есть предмет
    public Color emptyColor = new Color(1, 1, 1, 0.2f); // Прозрачный, когда пусто

    void Update()
    {
        UpdateVisuals();
    }

    void UpdateVisuals()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            // Проверяем, есть ли предмет для этого слота в списке менеджера
            if (i < manager.items.Count)
            {
                // Слот занят
                slots[i].GetComponent<Image>().color = activeColor;
                itemTexts[i].text = manager.items[i].itemName + "\n x" + manager.items[i].amount;
                itemTexts[i].enabled = true;
            }
            else
            {
                // Слот пуст
                slots[i].GetComponent<Image>().color = emptyColor;
                itemTexts[i].text = "";
                itemTexts[i].enabled = false;
            }
        }
    }
}
