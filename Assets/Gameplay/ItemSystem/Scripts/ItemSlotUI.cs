using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ItemSlotUI : MonoBehaviour
{
    [Header("UI Bindings")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI countText;

    private ItemData _trackedItem;

    public ItemData TrackedItem => _trackedItem;

    public void Initialize(ItemData item, int initialCount = 0)
    {
        _trackedItem = item;

        if (iconImage != null && item != null && item.icon != null)
        {
            iconImage.sprite = item.icon;
            iconImage.enabled = true;
        }

        UpdateCount(initialCount);
    }

    public void UpdateCount(int newCount)
    {
        if (countText != null)
        {
            countText.text = newCount.ToString();
        }
    }
}