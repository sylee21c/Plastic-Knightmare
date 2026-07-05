using System.Collections;
using UnityEngine;

// 낮/밤 배경음악을 반복 재생. 페이즈 전환 시 크로스페이드.
// 게임오버 시엔 자동 페이즈 감지를 잠시 멈추고 gameOverMusic 을 1회 재생.
// Retry 로 낮 음악으로 돌아올 때 다시 크로스페이드.
public sealed class BGMManager : MonoBehaviour
{
    public static BGMManager Instance { get; private set; }

    [Header("Clips")]
    [SerializeField] private AudioClip dayMusic;
    [SerializeField] private AudioClip nightMusic;
    [SerializeField] private AudioClip gameOverMusic;

    [Header("Volume / Fade")]
    [Range(0f, 1f)]
    [SerializeField] private float maxVolume = 0.5f;
    [Range(0f, 1f)]
    [SerializeField] private float gameOverVolume = 0.7f;
    [Tooltip("크로스페이드 지속 시간. 미지정 시 DayNightManager.TransitionDuration 사용")]
    [SerializeField] private float overrideFadeDuration = 0f;
    [Tooltip("게임오버 진입 시 페이드 시간")]
    [SerializeField] private float gameOverFadeDuration = 2f;

    private AudioSource sourceA;
    private AudioSource sourceB;
    private AudioSource currentSource;
    private DayNightManager.Phase lastPhase = DayNightManager.Phase.Day;
    private Coroutine activeFade;
    private bool overrideActive; // true 면 페이즈 자동 감지 무시 (게임오버 중)

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        sourceA = gameObject.AddComponent<AudioSource>();
        sourceB = gameObject.AddComponent<AudioSource>();
        SetupSource(sourceA);
        SetupSource(sourceB);
        currentSource = sourceA;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private static void SetupSource(AudioSource s)
    {
        s.loop = true;
        s.playOnAwake = false;
        s.spatialBlend = 0f; // 2D
        s.volume = 0f;
    }

    private void Start()
    {
        DayNightManager mgr = DayNightManager.Instance;
        if (mgr != null) lastPhase = mgr.CurrentPhase;

        AudioClip startClip = lastPhase == DayNightManager.Phase.Night ? nightMusic : dayMusic;
        if (startClip != null)
        {
            currentSource.clip = startClip;
            currentSource.loop = true;
            currentSource.volume = maxVolume;
            currentSource.Play();
        }
    }

    private void Update()
    {
        if (overrideActive) return; // 게임오버 중엔 자동 전환 잠금

        DayNightManager mgr = DayNightManager.Instance;
        if (mgr == null) return;

        if (mgr.CurrentPhase != lastPhase)
        {
            lastPhase = mgr.CurrentPhase;
            AudioClip newClip = lastPhase == DayNightManager.Phase.Night ? nightMusic : dayMusic;
            Crossfade(newClip, true, maxVolume, GetFadeDuration(mgr));
        }
    }

    // ── 게임오버 진입: 현재 곡 → gameOverMusic (반복 없음) ──────────
    public void EnterGameOverMusic()
    {
        if (gameOverMusic == null) return;
        overrideActive = true;
        Crossfade(gameOverMusic, false, gameOverVolume, gameOverFadeDuration);
    }

    // ── Retry: gameOverMusic → 낮 음악 (반복) ─────────────────────
    public void ReturnToDayMusic()
    {
        overrideActive = false;
        lastPhase = DayNightManager.Phase.Day; // Update 에서 재감지 안 되게 동기화
        Crossfade(dayMusic, true, maxVolume, gameOverFadeDuration);
    }

    private float GetFadeDuration(DayNightManager mgr)
    {
        if (overrideFadeDuration > 0f) return overrideFadeDuration;
        if (mgr != null && mgr.TransitionDuration > 0f) return mgr.TransitionDuration;
        return 2f;
    }

    private void Crossfade(AudioClip newClip, bool loop, float targetVolume, float duration)
    {
        if (activeFade != null) StopCoroutine(activeFade);

        AudioSource oldSource = currentSource;
        AudioSource newSource = (currentSource == sourceA) ? sourceB : sourceA;

        if (newClip != null)
        {
            newSource.clip = newClip;
            newSource.loop = loop;
            newSource.volume = 0f;
            newSource.Play();
        }
        currentSource = newSource;

        activeFade = StartCoroutine(FadeRoutine(oldSource, newSource, targetVolume, duration));
    }

    private IEnumerator FadeRoutine(AudioSource oldS, AudioSource newS, float targetVolume, float duration)
    {
        float startOldVol = oldS.volume;
        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            oldS.volume = Mathf.Lerp(startOldVol, 0f, t);
            newS.volume = Mathf.Lerp(0f, targetVolume, t);
            yield return null;
        }
        oldS.volume = 0f;
        newS.volume = targetVolume;
        oldS.Stop();
        activeFade = null;
    }
}
