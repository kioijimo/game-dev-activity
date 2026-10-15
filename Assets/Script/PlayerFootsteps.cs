using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plays looping walking / running footsteps while the player moves on the ground.
/// The surface is picked in this order:
///   1. Water      - the player is inside a trigger collider tagged "Water" (e.g. a plane with a Box Collider set to Is Trigger)
///   2. Terrain    - the ground is a Terrain: the strongest texture layer under the player is used (matched by Terrain Layer asset name)
///   3. Tag        - any other ground: the object's tag (or the nearest tagged parent) is used
///   4. Default    - the default sounds below
/// Attach to the Player (the object with the CharacterController).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerFootsteps : MonoBehaviour
{
    [System.Serializable]
    public class Surface
    {
        [Tooltip("Object tag (e.g. Stone, Water) OR the exact name of a Terrain Layer asset (e.g. Grass, Dirt). Case-insensitive.")]
        public string tag;
        public AudioClip walkSound;     // loopable footsteps on this surface
        public AudioClip runSound;      // faster loopable footsteps on this surface

        [Tooltip("Playback speed for walking on this surface. 0 = use the default walk speed.")]
        [Range(0f, 3f)] public float walkPlaybackSpeed = 1f;

        [Tooltip("Playback speed for running on this surface. 0 = use the default run speed.")]
        [Range(0f, 3f)] public float runPlaybackSpeed = 1.3f;
    }

    [Header("Default Sounds (used when nothing matches)")]
    [SerializeField] private AudioClip walkSound;
    [SerializeField] private AudioClip runSound;
    [SerializeField, Range(0f, 1f)] private float volume = 0.7f;

    [Header("Surfaces")]
    [SerializeField] private Surface[] surfaces;

    [Header("Default Playback Speed (used when no surface matches, or a surface speed is 0)")]
    [SerializeField, Range(0.5f, 3f)] private float walkPlaybackSpeed = 1f;
    [SerializeField, Range(0.5f, 3f)] private float runPlaybackSpeed = 1.3f;
    [SerializeField, Range(0.5f, 3f)] private float crouchPitch = 0.7f;

    [Header("Settings")]
    [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode crouchKey = KeyCode.LeftControl;
    [SerializeField] private float minMoveSpeed = 0.1f;
    [SerializeField] private float stopDelay = 0.15f;
    [SerializeField] private float airGrace = 0.1f;

    [Header("Water")]
    [Tooltip("Tag of the trigger collider that counts as water. Must exist in the Tag Manager.")]
    [SerializeField] private string waterTag = "Water";

    [Header("Ground Check")]
    [SerializeField] private bool debug = true;
    [SerializeField] private float rayStartHeight = 0.5f;
    [SerializeField] private float rayLength = 1.5f;
    [SerializeField] private LayerMask groundMask = ~0;

    private CharacterController cc;
    private AudioSource source;
    private float lastGroundedTime;
    private float lastMovingTime = -999f;
    private string lastLoggedGround = "";
    private bool warnedNoClip;

    // Water trigger colliders the player is currently inside
    private readonly HashSet<Collider> waterZones = new HashSet<Collider>();

    private void Awake()
    {
        cc = GetComponent<CharacterController>();

        source = gameObject.AddComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = volume;
    }

    private void Start()
    {
        if (debug)
        {
            bool hasListener = Camera.main != null && Camera.main.GetComponent<AudioListener>() != null;
            Debug.Log("[Footsteps] Script is RUNNING on '" + gameObject.name + "'. " +
                      "defaultWalk=" + (walkSound ? walkSound.name : "NONE") +
                      ", mainCamHasAudioListener=" + hasListener, this);
        }
    }

    private void OnDisable()
    {
        waterZones.Clear();
        if (source != null) source.Stop();
    }

    // ---------------- Main loop ----------------

    private void Update()
    {
        // ---- AUDIO TEST: press P in Play mode (plays a clip once) ----
        if (debug && Input.GetKeyDown(KeyCode.P))
        {
            AudioClip testClip = FindAnyClip();
            if (testClip == null)
            {
                Debug.LogWarning("[Footsteps] P test: no clip assigned anywhere (defaults or surfaces).", this);
            }
            else
            {
                Vector3 pos = Camera.main != null ? Camera.main.transform.position : transform.position;
                AudioSource.PlayClipAtPoint(testClip, pos, 1f);
                Debug.Log("[Footsteps] P test: played '" + testClip.name + "' (length " + testClip.length.ToString("F2") + "s)", this);
            }
        }

        if (cc.isGrounded) lastGroundedTime = Time.time;
        bool grounded = Time.time - lastGroundedTime <= airGrace;

        Vector3 flat = new Vector3(cc.velocity.x, 0f, cc.velocity.z);
        bool movingNow = flat.magnitude > minMoveSpeed;
        if (movingNow) lastMovingTime = Time.time;
        bool moving = Time.time - lastMovingTime <= stopDelay;

        if (debug && Time.frameCount % 30 == 0)
        {
            Debug.Log("[Footsteps] speed=" + flat.magnitude.ToString("F2") +
                      " grounded=" + grounded +
                      " moving=" + moving +
                      " playing=" + source.isPlaying +
                      " clip=" + (source.clip ? source.clip.name : "none") +
                      " pitch=" + source.pitch.ToString("F2"), this);
        }

        if (!grounded || !moving)
        {
            if (source.isPlaying) source.Stop();
            return;
        }

        bool sprinting = Input.GetKey(sprintKey);
        bool crouching = Input.GetKey(crouchKey);
        bool running = sprinting && !crouching;

        AudioClip wanted = GetClip(running, out float surfacePlaybackSpeed);
        if (wanted == null)
        {
            if (!warnedNoClip)
            {
                Debug.LogWarning("Footsteps: no clip found. Assign a default Walk Sound on the Player, or check the Surfaces list.", this);
                warnedNoClip = true;
            }
            return;
        }
        warnedNoClip = false;

        float targetPitch = crouching ? crouchPitch : surfacePlaybackSpeed;

        source.mute = false;
        source.pitch = targetPitch;
        source.volume = volume;

        if (source.clip != wanted)
        {
            source.clip = wanted;
            source.Play();
            if (debug) Debug.Log("[Footsteps] Play() called with '" + wanted.name + "' isPlaying=" + source.isPlaying, this);
        }
        else if (!source.isPlaying)
        {
            source.Play();
            if (debug) Debug.Log("[Footsteps] Play() restarted '" + wanted.name + "' isPlaying=" + source.isPlaying, this);
        }
    }

    // ---------------- Helpers ----------------

    // Finds any assigned clip, used only by the P test
    private AudioClip FindAnyClip()
    {
        if (runSound != null) return runSound;
        if (walkSound != null) return walkSound;
        if (surfaces != null)
        {
            foreach (Surface s in surfaces)
            {
                if (s.runSound != null) return s.runSound;
                if (s.walkSound != null) return s.walkSound;
            }
        }
        return null;
    }

    // Casts down and returns the closest solid hit that is NOT part of the player itself
    private bool TryGetGround(Vector3 origin, out RaycastHit result)
    {
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, rayLength, groundMask, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        result = default;
        bool found = false;

        foreach (RaycastHit h in hits)
        {
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (h.distance < best)
            {
                best = h.distance;
                result = h;
                found = true;
            }
        }
        return found;
    }

    // Walks up the hierarchy until it finds a tag that isn't "Untagged"
    private string FindTag(Transform t)
    {
        while (t != null)
        {
            if (!t.CompareTag("Untagged")) return t.tag;
            t = t.parent;
        }
        return "Untagged";
    }

    // Blend of every terrain texture layer at a world position (values add up to ~1)
    private float[] GetTerrainCellMix(Vector3 worldPos, Terrain terrain)
    {
        TerrainData data = terrain.terrainData;
        Vector3 terrainPos = terrain.transform.position;

        // World position -> 0..1 across the terrain -> alphamap pixel
        float relX = (worldPos.x - terrainPos.x) / data.size.x;
        float relZ = (worldPos.z - terrainPos.z) / data.size.z;

        int mapX = Mathf.Clamp((int)(relX * data.alphamapWidth), 0, data.alphamapWidth - 1);
        int mapZ = Mathf.Clamp((int)(relZ * data.alphamapHeight), 0, data.alphamapHeight - 1);

        // Result is indexed [z, x, layer]
        float[,,] splat = data.GetAlphamaps(mapX, mapZ, 1, 1);

        float[] mix = new float[splat.GetLength(2)];
        for (int i = 0; i < mix.Length; i++)
            mix[i] = splat[0, 0, i];
        return mix;
    }

    // Name of the strongest terrain layer at a world position, or null if there is none
    private string GetTerrainLayerName(Vector3 worldPos, Terrain terrain)
    {
        TerrainLayer[] layers = terrain.terrainData.terrainLayers;
        if (layers == null || layers.Length == 0) return null;

        float[] mix = GetTerrainCellMix(worldPos, terrain);
        int maxIndex = 0;
        float strongest = 0f;
        for (int i = 0; i < mix.Length; i++)
        {
            if (mix[i] > strongest)
            {
                strongest = mix[i];
                maxIndex = i;
            }
        }

        if (maxIndex >= layers.Length || layers[maxIndex] == null) return null;
        return layers[maxIndex].name;
    }

    // Figures out which surface name (water, terrain layer or tag) the player is on
    private string GetSurfaceName()
    {
        Vector3 origin = transform.position + Vector3.up * rayStartHeight;
        if (!TryGetGround(origin, out RaycastHit hit))
        {
            if (debug && lastLoggedGround != "NO GROUND")
            {
                Debug.Log("Footsteps: ray hit nothing. Check groundMask, rayLength, and that the floor has a collider.");
                lastLoggedGround = "NO GROUND";
            }
            return null;
        }

        string surfaceName = FindTag(hit.collider.transform);

        // Standing on a Terrain: use its strongest texture layer instead of the tag
        Terrain terrain = hit.collider.GetComponent<Terrain>();
        if (terrain != null)
        {
            string layerName = GetTerrainLayerName(hit.point, terrain);
            if (!string.IsNullOrEmpty(layerName)) surfaceName = layerName;
        }

        if (debug)
        {
            string msg = "Footsteps: ground = '" + hit.collider.name + "', surface = '" + surfaceName + "'";
            if (msg != lastLoggedGround)
            {
                Debug.Log(msg);
                lastLoggedGround = msg;
            }
        }

        return surfaceName;
    }

    // Picks the clip and playback speed for the current surface
    private AudioClip GetClip(bool running, out float playbackSpeed)
    {
        AudioClip walk = walkSound;
        AudioClip run = runSound;
        float walkSpeed = walkPlaybackSpeed;
        float runSpeed = runPlaybackSpeed;

        string surfaceName = GetSurfaceName();

        if (surfaceName != null && surfaces != null)
        {
            foreach (Surface s in surfaces)
            {
                if (string.Equals(s.tag, surfaceName, System.StringComparison.OrdinalIgnoreCase))
                {
                    if (s.walkSound != null) walk = s.walkSound;
                    if (s.runSound != null) run = s.runSound;
                    if (s.walkPlaybackSpeed > 0f) walkSpeed = s.walkPlaybackSpeed;
                    if (s.runPlaybackSpeed > 0f) runSpeed = s.runPlaybackSpeed;
                    break;
                }
            }
        }

        // Running but no run clip on this surface: fall back to walk clip with the walk speed
        if (running && run != null)
        {
            playbackSpeed = runSpeed;
            return run;
        }

        playbackSpeed = walkSpeed;
        return walk;
    }
}
