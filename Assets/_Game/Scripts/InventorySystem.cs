using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ItemData: ScriptableObject for item definitions.
/// </summary>
[CreateAssetMenu(fileName = "New Item", menuName = "Game/Item")]
public class ItemData : ScriptableObject
{
    public string itemName;
    public string description;
    public Sprite icon;
    public ItemType type;
    public int maxStack = 99;
    public float weight = 1f;
    public GameObject worldPrefab; // Prefab to spawn when dropped
}

public enum ItemType
{
    Consumable,
    Weapon,
    Tool,
    Material,
    Quest,
    Misc
}

/// <summary>
/// InventorySlot: Single slot in inventory.
/// </summary>
[System.Serializable]
public class InventorySlot
{
    public ItemData item;
    public int quantity;

    public bool IsEmpty => item == null || quantity <= 0;
    public bool CanAdd(ItemData otherItem) => item == null || (item == otherItem && quantity < otherItem.maxStack);

    public void Clear() { item = null; quantity = 0; }
}

/// <summary>
/// Inventory: Grid-based inventory system.
/// </summary>
public class Inventory : MonoBehaviour
{
    public int width = 8;
    public int height = 4;
    public List<InventorySlot> slots = new();

    public event System.Action OnInventoryChanged;

    public void NotifyChanged() => OnInventoryChanged?.Invoke();

    void Awake()
    {
        slots.Clear();
        for (int i = 0; i < width * height; i++)
            slots.Add(new InventorySlot());
    }

    public bool AddItem(ItemData item, int amount = 1)
    {
        if (item == null || amount <= 0) return false;

        // Try stacking first
        if (item.maxStack > 1)
        {
            foreach (var slot in slots)
            {
                if (slot.item == item && slot.quantity < item.maxStack)
                {
                    int canAdd = Mathf.Min(amount, item.maxStack - slot.quantity);
                    slot.quantity += canAdd;
                    amount -= canAdd;
                    if (amount <= 0)
                    {
                        OnInventoryChanged?.Invoke();
                        return true;
                    }
                }
            }
        }

        // Find empty slots
        foreach (var slot in slots)
        {
            if (slot.IsEmpty)
            {
                int canAdd = Mathf.Min(amount, item.maxStack);
                slot.item = item;
                slot.quantity = canAdd;
                amount -= canAdd;
                if (amount <= 0)
                {
                    OnInventoryChanged?.Invoke();
                    return true;
                }
            }
        }

        // Could not fit all
        if (amount > 0)
        {
            Debug.LogWarning($"[Inventory] Could not fit {amount}x {item.itemName}");
            return false;
        }

        OnInventoryChanged?.Invoke();
        return true;
    }

    public bool RemoveItem(ItemData item, int amount = 1)
    {
        if (item == null) return false;

        int remaining = amount;
        foreach (var slot in slots)
        {
            if (slot.item == item)
            {
                int canRemove = Mathf.Min(remaining, slot.quantity);
                slot.quantity -= canRemove;
                remaining -= canRemove;
                if (slot.quantity <= 0) slot.Clear();
                if (remaining <= 0)
                {
                    OnInventoryChanged?.Invoke();
                    return true;
                }
            }
        }
        return false;
    }

    public int GetItemCount(ItemData item)
    {
        if (item == null) return 0;
        int count = 0;
        foreach (var slot in slots)
            if (slot.item == item) count += slot.quantity;
        return count;
    }

    public bool HasItem(ItemData item, int amount = 1) => GetItemCount(item) >= amount;

    public InventorySlot GetSlot(int index)
    {
        if (index >= 0 && index < slots.Count) return slots[index];
        return null;
    }

    public void SwapSlots(int indexA, int indexB)
    {
        if (indexA < 0 || indexA >= slots.Count || indexB < 0 || indexB >= slots.Count) return;
        (slots[indexA], slots[indexB]) = (slots[indexB], slots[indexA]);
        OnInventoryChanged?.Invoke();
    }
}

/// <summary>
/// PickupItem: World item that can be picked up.
/// </summary>
public class PickupItem : MonoBehaviour
{
    public ItemData itemData;
    public int quantity = 1;
    public float pickupRange = 2f;
    public float bobSpeed = 2f;
    public float bobHeight = 0.3f;

    Vector3 _startPos;
    Collider _collider;

    void Awake()
    {
        _startPos = transform.position;
        _collider = GetComponent<Collider>();
        if (!_collider)
        {
            _collider = gameObject.AddComponent<SphereCollider>();
            (_collider as SphereCollider).radius = 0.5f;
        }
        _collider.isTrigger = true;
    }

    void Update()
    {
        // Bob animation
        transform.position = _startPos + Vector3.up * Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.Rotate(0, 30f * Time.deltaTime, 0);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        var inventory = other.GetComponent<Inventory>();
        if (inventory && inventory.AddItem(itemData, quantity))
        {
            inventory.NotifyChanged();
            Destroy(gameObject);
        }
    }
}