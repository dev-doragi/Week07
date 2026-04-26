using UnityEngine;
using UnityEngine.UI;

public class SkillButtonCooldownView : MonoBehaviour
{
    [SerializeField] private int _skillIndex = 1;
    [SerializeField] private Button _button;
    [SerializeField] private Image _lockedOverlay;
    [SerializeField] private Image _cooldownOverlay;
    [SerializeField] private Color _lockedColor = new Color(0f, 0f, 0f, 0.78f);
    [SerializeField] private Color _cooldownColor = new Color(0f, 0f, 0f, 0.58f);

    private RitualSystem _ritualSystem;

    public void Initialize(int skillIndex, RitualSystem ritualSystem)
    {
        _skillIndex = skillIndex;
        _ritualSystem = ritualSystem;
        EnsureReferences();
        Refresh();
    }

    private void Awake()
    {
        EnsureReferences();
    }

    private void OnEnable()
    {
        if (UnlockManager.Instance != null)
            UnlockManager.Instance.SkillUnlocked += OnSkillUnlocked;

        Refresh();
    }

    private void OnDisable()
    {
        if (UnlockManager.Instance != null)
            UnlockManager.Instance.SkillUnlocked -= OnSkillUnlocked;
    }

    private void Update()
    {
        Refresh();
    }

    private void OnSkillUnlocked(int skillIndex)
    {
        if (skillIndex == _skillIndex)
            Refresh();
    }

    private void Refresh()
    {
        if (_ritualSystem == null)
            _ritualSystem = FindFirstObjectByType<RitualSystem>();

        EnsureReferences();

        bool isUnlocked = _ritualSystem == null || _ritualSystem.IsSkillUnlocked(_skillIndex);
        float duration = _ritualSystem != null ? _ritualSystem.GetSkillCooldownDuration(_skillIndex) : 0f;
        float remaining = _ritualSystem != null ? _ritualSystem.GetSkillCooldownRemaining(_skillIndex) : 0f;
        bool isCoolingDown = isUnlocked && duration > 0f && remaining > 0f;

        if (_button != null)
            _button.interactable = isUnlocked && !isCoolingDown;

        if (_lockedOverlay != null)
            _lockedOverlay.gameObject.SetActive(!isUnlocked);

        if (_cooldownOverlay != null)
        {
            _cooldownOverlay.gameObject.SetActive(isCoolingDown);
            _cooldownOverlay.fillAmount = duration > 0f ? Mathf.Clamp01(remaining / duration) : 0f;
        }
    }

    private void EnsureReferences()
    {
        if (_button == null)
            _button = GetComponent<Button>();

        if (_lockedOverlay == null)
            _lockedOverlay = EnsureOverlay("LockedOverlay", _lockedColor, false);

        if (_cooldownOverlay == null)
        {
            _cooldownOverlay = EnsureOverlay("CooldownRadial", _cooldownColor, true);
            _cooldownOverlay.type = Image.Type.Filled;
            _cooldownOverlay.fillMethod = Image.FillMethod.Radial360;
            _cooldownOverlay.fillOrigin = 2;
            _cooldownOverlay.fillClockwise = false;
            _cooldownOverlay.fillAmount = 0f;
        }
    }

    private Image EnsureOverlay(string childName, Color color, bool cooldown)
    {
        Transform existing = transform.Find(childName);
        if (existing != null && existing.TryGetComponent(out Image existingImage))
            return existingImage;

        GameObject obj = new GameObject(childName, typeof(RectTransform), typeof(Image));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = obj.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        Image buttonImage = _button != null ? _button.targetGraphic as Image : GetComponent<Image>();
        if (buttonImage != null)
            image.sprite = buttonImage.sprite;

        image.type = cooldown ? Image.Type.Filled : Image.Type.Simple;
        obj.SetActive(false);
        return image;
    }
}
