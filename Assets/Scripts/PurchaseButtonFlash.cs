using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class PurchaseButtonFlash : MonoBehaviour
{
    [SerializeField] private Color successColor = new Color(0.18f, 0.8f, 0.28f, 1f);
    [SerializeField] private Color failureColor = new Color(0.9f, 0.18f, 0.16f, 1f);
    [SerializeField] private float flashDuration = 0.18f;
    [SerializeField] private float returnDuration = 0.22f;

    private Coroutine routine;

    public void Flash(Button button, bool success)
    {
        if (button == null) return;

        Graphic graphic = button.targetGraphic;
        if (graphic == null) graphic = button.GetComponent<Graphic>();
        if (graphic == null) return;

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(FlashRoutine(graphic, success ? successColor : failureColor));
    }

    private IEnumerator FlashRoutine(Graphic graphic, Color flashColor)
    {
        Color original = graphic.color;
        graphic.color = flashColor;

        yield return new WaitForSecondsRealtime(flashDuration);

        float elapsed = 0f;
        while (elapsed < returnDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / returnDuration);
            graphic.color = Color.Lerp(flashColor, original, t);
            yield return null;
        }

        graphic.color = original;
        routine = null;
    }
}
