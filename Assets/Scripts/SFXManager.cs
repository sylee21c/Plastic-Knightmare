using UnityEngine;
using UnityEngine.Audio;

// 게임 전역 효과음 매니저.
// 클립별로 개별 볼륨/피치를 인스펙터에서 조절 가능.
// 팝업 Canvas 가 꺼져도 소리가 잘리지 않도록 별도 GameObject 로 존재.
// 루프 사운드 (걷기/움직임) 는 외부 AudioSource 가 TryGetClipData 로 데이터만 가져가서 로컬 재생.
public sealed class SFXManager : MonoBehaviour
{
    public enum Sfx
    {
        Default,
        // UI
        Increase,
        Decrease,
        Purchase,
        PurchaseFail,
        Close,
        Ready,
        Interact,
        // Player
        PlayerWalk,      // 루프
        PlayerAttack,
        PlayerHit,
        PlayerGuardHit,
        PlayerJump,
        PlayerDeath,
        // Ghost
        GhostHit,
        GhostAttack,
        GhostDeath,
        // Companion (Tank 등)
        TankMove,        // 루프
        TankFire,
        TankDeath,
        ChickenTrap,
        ChickenDeath,
        // Brick
        BrickPlace,
        BrickHit,
        BrickBreak,
        // Coin
        CoinPickup,
        // Bed
        BedHit,
    }

    [System.Serializable]
    public sealed class ClipSlot
    {
        [SerializeField] public AudioClip clip;
        [SerializeField, Range(0f, 1f)] public float volume = 1f;
        [Tooltip("피치 랜덤 범위 (min, max). 같은 값이면 고정")]
        [SerializeField] public Vector2 pitchRange = new Vector2(1f, 1f);
    }

    // 여러 클립 중 랜덤 재생용 슬롯. 배열에 클립을 여러 개 넣으면 매 재생마다 랜덤 선택.
    [System.Serializable]
    public sealed class MultiClipSlot
    {
        [Tooltip("재생할 클립들. 여러 개 넣으면 랜덤 선택.")]
        [SerializeField] public AudioClip[] clips = new AudioClip[0];
        [SerializeField, Range(0f, 1f)] public float volume = 1f;
        [Tooltip("피치 랜덤 범위 (min, max). 같은 값이면 고정")]
        [SerializeField] public Vector2 pitchRange = new Vector2(1f, 1f);

        public AudioClip PickRandom()
        {
            if (clips == null || clips.Length == 0) return null;
            int validCount = 0;
            for (int i = 0; i < clips.Length; i++) if (clips[i] != null) validCount++;
            if (validCount == 0) return null;
            int pick = Random.Range(0, validCount);
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] == null) continue;
                if (pick == 0) return clips[i];
                pick--;
            }
            return null;
        }
    }

    public static SFXManager Instance { get; private set; }

    [Header("Audio Source")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioMixerGroup outputMixerGroup;

    [Header("UI Clips")]
    [SerializeField] private ClipSlot defaultClick = new ClipSlot();
    [SerializeField] private ClipSlot increase = new ClipSlot();
    [SerializeField] private ClipSlot decrease = new ClipSlot();
    [SerializeField] private ClipSlot purchase = new ClipSlot();
    [SerializeField] private ClipSlot purchaseFail = new ClipSlot();
    [SerializeField] private ClipSlot close = new ClipSlot();
    [SerializeField] private ClipSlot ready = new ClipSlot();
    [SerializeField] private ClipSlot interact = new ClipSlot();

    [Header("Player")]
    [SerializeField] private ClipSlot playerWalk = new ClipSlot();
    [SerializeField] private ClipSlot playerAttack = new ClipSlot();
    [SerializeField] private ClipSlot playerHit = new ClipSlot();
    [SerializeField] private ClipSlot playerGuardHit = new ClipSlot();
    [SerializeField] private ClipSlot playerJump = new ClipSlot();
    [SerializeField] private ClipSlot playerDeath = new ClipSlot();

    [Header("Ghost")]
    [SerializeField] private ClipSlot ghostHit = new ClipSlot();
    [SerializeField] private ClipSlot ghostAttack = new ClipSlot();
    [SerializeField] private ClipSlot ghostDeath = new ClipSlot();

    [Header("Companion / Tank")]
    [SerializeField] private ClipSlot tankMove = new ClipSlot();
    [SerializeField] private ClipSlot tankFire = new ClipSlot();
    [SerializeField] private ClipSlot tankDeath = new ClipSlot();
    [Tooltip("여러 클립 넣으면 랜덤 재생.")]
    [SerializeField] private MultiClipSlot chickenTrap = new MultiClipSlot();
    [SerializeField] private ClipSlot chickenDeath = new ClipSlot();

    [Header("Brick")]
    [SerializeField] private ClipSlot brickPlace = new ClipSlot();
    [SerializeField] private ClipSlot brickHit = new ClipSlot();
    [SerializeField] private ClipSlot brickBreak = new ClipSlot();

    [Header("Coin")]
    [SerializeField] private ClipSlot coinPickup = new ClipSlot();

    [Header("Bed")]
    [SerializeField] private ClipSlot bedHit = new ClipSlot();

    public static void EnsureExists()
    {
        if (Instance != null) return;
        SFXManager existing = FindAnyObjectByType<SFXManager>(FindObjectsInactive.Include);
        if (existing != null) return;

        GameObject obj = new GameObject("SFXManager");
        obj.AddComponent<SFXManager>();
    }

    public static void PlayGlobal(Sfx sfx)
    {
        EnsureExists();
        Instance?.Play(sfx);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        EnsureAudioSource();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Play(Sfx sfx)
    {
        if (audioSource == null) return;

        // MultiClipSlot 특수 처리 — 여러 클립 중 랜덤 재생
        if (sfx == Sfx.ChickenTrap)
        {
            AudioClip picked = chickenTrap.PickRandom();
            if (picked == null) return;
            float origPitch = audioSource.pitch;
            audioSource.pitch = GetRandomPitch(chickenTrap.pitchRange);
            audioSource.PlayOneShot(picked, chickenTrap.volume);
            audioSource.pitch = origPitch;
            return;
        }

        ClipSlot slot = GetSlot(sfx);
        if (slot == null || slot.clip == null) return;

        float originalPitch = audioSource.pitch;
        audioSource.pitch = GetRandomPitch(slot.pitchRange);
        audioSource.PlayOneShot(slot.clip, slot.volume);
        audioSource.pitch = originalPitch;
    }

    // 루프 사운드를 로컬 AudioSource 로 재생하려는 엔티티가 클립/볼륨을 가져갈 때 사용.
    public bool TryGetClipData(Sfx sfx, out AudioClip clip, out float volume, out Vector2 pitchRange)
    {
        ClipSlot slot = GetSlot(sfx);
        if (slot == null || slot.clip == null)
        {
            clip = null; volume = 0f; pitchRange = Vector2.one;
            return false;
        }
        clip = slot.clip;
        volume = slot.volume;
        pitchRange = slot.pitchRange;
        return true;
    }

    private ClipSlot GetSlot(Sfx sfx)
    {
        switch (sfx)
        {
            case Sfx.Increase:       return increase.clip       != null ? increase       : defaultClick;
            case Sfx.Decrease:       return decrease.clip       != null ? decrease       : defaultClick;
            case Sfx.Purchase:       return purchase.clip       != null ? purchase       : defaultClick;
            case Sfx.PurchaseFail:   return purchaseFail.clip   != null ? purchaseFail   : defaultClick;
            case Sfx.Close:          return close.clip          != null ? close          : defaultClick;
            case Sfx.Ready:          return ready.clip          != null ? ready          : defaultClick;
            case Sfx.Interact:       return interact.clip       != null ? interact       : defaultClick;
            case Sfx.PlayerWalk:     return playerWalk;
            case Sfx.PlayerAttack:   return playerAttack;
            case Sfx.PlayerHit:      return playerHit;
            case Sfx.PlayerGuardHit: return playerGuardHit;
            case Sfx.PlayerJump:     return playerJump;
            case Sfx.PlayerDeath:    return playerDeath;
            case Sfx.GhostHit:       return ghostHit;
            case Sfx.GhostAttack:    return ghostAttack;
            case Sfx.GhostDeath:     return ghostDeath;
            case Sfx.TankMove:       return tankMove;
            case Sfx.TankFire:       return tankFire;
            case Sfx.TankDeath:      return tankDeath;
            // ChickenTrap 은 Play() 에서 MultiClipSlot 으로 직접 처리 (여기 안 옴)
            case Sfx.ChickenDeath:   return chickenDeath;
            case Sfx.BrickPlace:     return brickPlace;
            case Sfx.BrickHit:       return brickHit;
            case Sfx.BrickBreak:     return brickBreak;
            case Sfx.CoinPickup:     return coinPickup;
            case Sfx.BedHit:         return bedHit;
            default:                 return defaultClick;
        }
    }

    private void EnsureAudioSource()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D
        audioSource.outputAudioMixerGroup = outputMixerGroup;
    }

    private static float GetRandomPitch(Vector2 range)
    {
        float min = Mathf.Min(range.x, range.y);
        float max = Mathf.Max(range.x, range.y);
        if (Mathf.Approximately(min, max)) return min;
        return Random.Range(min, max);
    }
}
