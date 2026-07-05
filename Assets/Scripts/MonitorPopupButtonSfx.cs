using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 팝업 내 버튼을 자동으로 찾아 이름 기반으로 SFXManager 에 재생 요청.
// 실제 클립/볼륨은 SFXManager 에서 관리 (개별 조절).
public sealed class MonitorPopupButtonSfx : MonoBehaviour
{
    [System.Serializable]
    private sealed class ButtonOverride
    {
        [SerializeField] public Button button;
        [SerializeField] public SFXManager.Sfx sfx = SFXManager.Sfx.Default;
    }

    [Header("Binding")]
    [Tooltip("켜두면 이 오브젝트 하위의 모든 Button을 자동으로 찾아 이름 기준으로 효과음을 재생합니다.")]
    [SerializeField] private bool autoBindChildButtons = true;
    [Tooltip("특정 버튼만 다른 효과음을 쓰고 싶을 때 여기에 직접 지정합니다.")]
    [SerializeField] private ButtonOverride[] overrides;

    private readonly Dictionary<Button, UnityAction> boundButtons = new Dictionary<Button, UnityAction>();

    private void Awake()
    {
        SFXManager.EnsureExists();
    }

    private void OnEnable()
    {
        BindButtons();
    }

    private void OnDisable()
    {
        UnbindButtons();
    }

    [ContextMenu("Rebind Child Buttons")]
    private void BindButtons()
    {
        UnbindButtons();

        if (autoBindChildButtons)
        {
            foreach (Button button in GetComponentsInChildren<Button>(true))
                Bind(button);
        }

        if (overrides == null) return;
        foreach (ButtonOverride entry in overrides)
        {
            if (entry != null && entry.button != null)
                Bind(entry.button);
        }
    }

    private void Bind(Button button)
    {
        if (button == null || boundButtons.ContainsKey(button)) return;

        // 구매 버튼은 상점 스크립트가 성공/실패에 따라 SFX 를 직접 재생하므로 자동 바인딩 스킵.
        string lowered = button.gameObject.name.ToLowerInvariant();
        if (lowered.Contains("purchase") || lowered.Contains("buy")) return;

        UnityAction action = () => PlayForButton(button);
        button.onClick.AddListener(action);
        boundButtons.Add(button, action);
    }

    private void UnbindButtons()
    {
        foreach (KeyValuePair<Button, UnityAction> pair in boundButtons)
        {
            if (pair.Key != null)
                pair.Key.onClick.RemoveListener(pair.Value);
        }

        boundButtons.Clear();
    }

    private void PlayForButton(Button button)
    {
        SFXManager.Sfx sfx = GetOverrideSfx(button) ?? GetSfxFromName(button);
        SFXManager.PlayGlobal(sfx);
    }

    private SFXManager.Sfx? GetOverrideSfx(Button button)
    {
        if (button == null || overrides == null) return null;
        foreach (ButtonOverride entry in overrides)
        {
            if (entry != null && entry.button == button)
                return entry.sfx;
        }
        return null;
    }

    private static SFXManager.Sfx GetSfxFromName(Button button)
    {
        string name = button != null ? button.gameObject.name.ToLowerInvariant() : "";

        if (name.Contains("increase") || name.Contains("plus") || name.Contains("+"))
            return SFXManager.Sfx.Increase;
        if (name.Contains("decrease") || name.Contains("minus") || name.Contains("-"))
            return SFXManager.Sfx.Decrease;
        // NOTE: purchase/buy 는 여기서 처리하지 않음.
        // 상점 스크립트가 성공/실패에 따라 Purchase/PurchaseFail 을 직접 재생 → 중복 방지.
        if (name == "x" || name.Contains("x button") || name.Contains("close"))
            return SFXManager.Sfx.Close;
        if (name.Contains("ready"))
            return SFXManager.Sfx.Ready;

        return SFXManager.Sfx.Default;
    }
}
