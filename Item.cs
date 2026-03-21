using System;

[Serializable]
public class Item
{
    public string itemName;
    public int itemID;
    public int amount;

    public Item(string name, int id, int count)
    {
        itemName = name;
        itemID = id;
        amount = count;
    }
}
