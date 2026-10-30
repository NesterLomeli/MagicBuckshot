using UnityEngine;

// plays every sound in the game from a pool of audio sources so a bunch of gunshots at once
// don't cut each other off. sticks around between levels and makes itself if a scene doesn't have one
public class AudioManager : MonoBehaviour
{
    private static AudioManager instance;

    [Tooltip("How many sounds can overlap at once.")]
    [SerializeField] private int poolSize = 16;

    [Tooltip("Master volume for all sound effects.")]
    [Range(0f, 1f)] [SerializeField] private float masterVolume = 0.8f;

    private AudioSource[] pool;
    private int nextSource;

    private void Awake()
    {
        // only ever keep one
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        pool = new AudioSource[poolSize];
        for (int i = 0; i < poolSize; i++)
        {
            pool[i] = gameObject.AddComponent<AudioSource>();
            pool[i].playOnAwake = false;
            pool[i].spatialBlend = 0f;
        }
    }

    private static AudioManager GetOrCreate()
    {
        if (instance == null)
        {
            instance = new GameObject("AudioManager (auto)").AddComponent<AudioManager>();
        }
        return instance;
    }

    // bullet time turns this down so everything sounds deeper
    public static float PitchScale = 1f;

    public static void Play(AudioClip clip, float volume = 1f, float pitchVariance = 0.06f)
    {
        if (clip == null) return;
        AudioManager mgr = GetOrCreate();

        AudioSource source = mgr.pool[mgr.nextSource];
        // cycle through the pool
        mgr.nextSource = (mgr.nextSource + 1) % mgr.pool.Length;

        // little bit of random pitch so the same shot doesn't sound robotic
        source.pitch = (1f + Random.Range(-pitchVariance, pitchVariance)) * PitchScale;
        source.volume = volume * mgr.masterVolume;
        source.clip = clip;
        source.Play();
    }

    public static void PlayRandom(AudioClip[] clips, float volume = 1f, float pitchVariance = 0.06f)
    {
        if (clips == null || clips.Length == 0) return;
        Play(clips[Random.Range(0, clips.Length)], volume, pitchVariance);
    }
}
