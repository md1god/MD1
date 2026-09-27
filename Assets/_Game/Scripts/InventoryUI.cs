using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// InventoryUI: Grid-based inventory display with drag-drop.
/// Press 'I' to toggle. WebGL-compatible.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [Header("References")]
    public Inventory playerInventory;
    public GameObject inventoryPanel;
    public GameObject slotPrefab;
    public Transform slotsContainer;
    public GameObject itemTooltip;
    public Text tooltipNameText;
    public Text tooltipDescText;

    [Header("Settings")]
    public KeyCode toggleKey = KeyCode.I;

    List<InventorySlotUI> _slotUIs = new();
    bool _isOpen;
    int _hoveredIndex = -1;

    void Awake()
    {
        if (!playerInventory) playerInventory = FindFirstObjectByType<Inventory>();
        if (!inventoryPanel) Debug.LogError("[InventoryUI] inventoryPanel not assigned!");
        if (!slotPrefab) Debug.LogError("[InventoryUI] slotPrefab not assigned!");
        if (!slotsContainer) Debug.LogError("[InventoryUI] slotsContainer not assigned!");

        if (inventoryPanel) inventoryPanel.SetActive(false);
        if (itemTooltip) itemTooltip.SetActive(false);

        if (playerInventory != null)
            playerInventory.OnInventoryChanged += RefreshUI;
    }

    void Start()
    {
        BuildSlots();
        RefreshUI();
    }

    void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.iKey.wasPressedThisFrame)
            ToggleInventory();

        if (_isOpen && Keyboard.current.escapeKey.wasPressedThisFrame)
            CloseInventory();
    }

    void BuildSlots()
    {
        if (!slotsContainer || !slotPrefab) return;

        // Clear existing
        foreach (var s in _slotUIs) if (s) Destroy(s.gameObject);
        _slotUIs.Clear();

        int count = playerInventory ? playerInventory.slots.Count : 32;
        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(slotPrefab, slotsContainer);
            var slotUI = go.GetComponent<InventorySlotUI>();
            if (!slotUI)
            {
                slotUI = go.AddComponent<InventorySlotUI>();
                SetupSlotUI(go, slotUI);
            }
            slotUI.Initialize(this, i);
            _slotUIs.Add(slotUI);
        }
    }

    void SetupSlotUI(GameObject go, InventorySlotUI slotUI)
    {
        // Add Image for icon
        var img = go.GetComponent<Image>();
        if (!img) img = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

        // Add icon child
        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(go.transform, false);
        var iconImg = iconGO.AddComponent<Image>();
        iconImg.raycastTarget = false;
        iconImg.color = Color.white;
        var rt = iconImg.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.one * 4;

        slotUI.iconImage = iconImg;

        // Add quantity text
        var qtyGO = new GameObject("Quantity");
        qtyGO.transform.SetParent(go.transform, false);
        var qtyText = qtyGO.AddComponent<Text>();
        qtyText.font = Font.CreateDynamicFontFromOSFont("Arial", 16);
        qtyText.color = Color.white;
        qtyText.alignment = TextAnchor.LowerRight;
        qtyText.rectTransform.anchorMin = Vector2.zero;
        qtyText.rectTransform.anchorMax = Vector2.one;
        qtyText.rectTransform.offsetMin = Vector2.zero;
        qtyText.rectTransform.offsetMax = new Vector2(-4, 4);
        slotUI.quantityText = qtyText;

        // Event triggers for drag-drop
        var trigger = go.AddComponent<EventTrigger>();
        AddEventTrigger(trigger, EventTriggerType.PointerEnter, (e) => OnSlotHover(slotUI.slotIndex));
        AddEventTrigger(trigger, EventTriggerType.PointerExit, (e) => OnSlotHover(-1));
        AddEventTrigger(trigger, EventTriggerType.BeginDrag, (e) => OnBeginDrag(slotUI.slotIndex));
        AddEventTrigger(trigger, EventTriggerType.Drag, (e) => OnDrag());
        AddEventTrigger(trigger, EventTriggerType.EndDrag, (e) => OnEndDrag(slotUI.slotIndex));
    }

    void AddEventTrigger(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(action);
        trigger.triggers.Add(entry);
    }

    public void ToggleInventory()
    {
        if (_isOpen) CloseInventory(); else OpenInventory();
    }

    public void OpenInventory()
    {
        _isOpen = true;
        if (inventoryPanel) inventoryPanel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
        RefreshUI();
    }

    public void CloseInventory()
    {
        _isOpen = false;
        if (inventoryPanel) inventoryPanel.SetActive(false);
        if (itemTooltip) itemTooltip.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Time.timeScale = 1f;
    }

    void RefreshUI()
    {
        if (!playerInventory) return;

        for (int i = 0; i < _slotUIs.Count && i < playerInventory.slots.Count; i++)
        {
            var slot = playerInventory.slots[i];
            var slotUI = _slotUIs[i];
            if (slot.IsEmpty)
            {
                slotUI.SetEmpty();
            }
            else
            {
                slotUI.SetItem(slot.item, slot.quantity);
            }
        }
    }

    void OnSlotHover(int index)
    {
        _hoveredIndex = index;
        if (itemTooltip && index >= 0 && playerInventory && index < playerInventory.slots.Count)
        {
            var slot = playerInventory.slots[index];
            if (!slot.IsEmpty)
            {
                tooltipNameText.text = slot.item.itemName;
                tooltipDescText.text = slot.item.description;
                itemTooltip.SetActive(true);
                // Position near mouse
                itemTooltip.transform.position = Input.mousePosition + new Vector3(20, -20, 0);
            }
            else itemTooltip.SetActive(false);
        }
        else if (itemTooltip) itemTooltip.SetActive(false);
    }

    int _dragSourceIndex = -1;
    void OnBeginDrag(int index)
    {
        if (index < 0 || !playerInventory || index >= playerInventory.slots.Count) return;
        var slot = playerInventory.slots[index];
        if (slot.IsEmpty) return;
        _dragSourceIndex = index;
        // Visual feedback
        _slotUIs[index].SetDragging(true);
    }

    void OnDrag() { /* Could add drag preview */ }

    void OnEndDrag(int targetIndex)
    {
        if (_dragSourceIndex < 0) return;
        _slotUIs[_dragSourceIndex].SetDragging(false);

        if (targetIndex >= 0 && targetIndex != _dragSourceIndex && playerInventory)
        {
            playerInventory.SwapSlots(_dragSourceIndex, targetIndex);
        }
        _dragSourceIndex = -1;
    }

    void OnDestroy()
    {
        if (playerInventory != null)
            playerInventory.OnInventoryChanged -= RefreshUI;
    }
}

/// <summary>
/// InventorySlotUI: Visual representation of an inventory slot.
/// </summary>
public class InventorySlotUI : MonoBehaviour
{
    public Image iconImage;
    public Text quantityText;
    public int slotIndex;
    InventoryUI _parentUI;

    public void Initialize(InventoryUI parent, int index)
    {
        _parentUI = parent;
        slotIndex = index;
        SetEmpty();
    }

    public void SetItem(ItemData item, int quantity)
    {
        if (iconImage) { iconImage.sprite = item.icon; iconImage.enabled = true; }
        if (quantityText)
        {
            quantityText.text = quantity > 1 ? quantity.ToString() : "";
            quantityText.enabled = quantity > 1;
        }
    }

    public void SetEmpty()
    {
        if (iconImage) { iconImage.sprite = null; iconImage.enabled = false; }
        if (quantityText) { quantityText.text = ""; quantityText.enabled = false; }
    }

    public void SetDragging(bool dragging)
    {
        var img = GetComponent<Image>();
        if (img) img.color = dragging ? new Color(0.5f, 0.5f, 1f, 0.5f) : new Color(0.2f, 0.2f, 0.2f, 0.8f);
    }
}