using UnityEngine;

/// <summary>
/// Plays a looping walking sound while the player walks and a running sound
/// while they sprint (Left Shift). Stops when standing still or in the air.
/// Checks the ground tag under the player (e.g. Dirt, Stone) and uses that
/// surface's sounds and playback speeds. Untagged ground uses the defaults below.
/// Attach to the Player (the object with the CharacterController).
/// Doesn't touch PlayerMovement.cs - it just watches the controller's speed.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerFootsteps : MonoBehaviour
{
    [System.Serializable]
    public class Surface
    {
        public string tag;              // ground object tag, e.g. "Dirt" or "Stone"
        public AudioClip walkSound;     // loopable footsteps on this surface
        public AudioClip runSound;      // faster loopable footsteps on this surface

        [Tooltip("Playback speed for walking on this surface. 0 = use the default walk speed.")]
        [Range(0f, 3f)] public float walkPlaybackSpeed = 1f;

        [Tooltip("Playback speed for running on this surface. 0 = use the default run speed.")]
        [Range(0f, 3f)] public float runPlaybackSpeed = 1.3f;
    }

    [Header("Default Sounds (used when the ground has no matching tag)")]
    [SerializeField] private AudioClip walkSound;   // loopable footsteps
    [SerializeField] private AudioClip runSound;    // faster loopable footsteps
    [SerializeField, Range(0f, 1f)] private float volume = 0.7f;

    [Header("Surfaces")]
    [SerializeField] private Surface[] surfaces;

    [Header("Default Playback Speed (used when no surface matches, or a surface speed is 0)")]
    [SerializeField, Range(0.5f, 3f)] private float walkPlaybackSpeed = 1f;
    [SerializeField, Range(0.5f, 3f)] private float runPlaybackSpeed = 1.3f;
    [SerializeField, Range(0.5f, 3f)] private float crouchPitch = 0.7f;       // slower/softer when crouching

    [Header("Settings")]
    [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode crouchKey = KeyCode.LeftControl;
    [SerializeField] private float minMoveSpeed = 0.1f;      // above this = moving
    [SerializeField] private float stopDelay = 0.15f;        // keeps sound going through brief speed dips
    [SerializeField] private float airGrace = 0.1f;          // avoids stutter on tiny bumps

    [Header("Ground Check")]
    [SerializeField] private bool debug = true;   // prints status + ground info in the Console
    [SerializeField] private float rayStartHeight = 0.5f;
    [SerializeField] private float rayLength = 1.5f;
    [SerializeField] private LayerMask groundMask = ~0;

    private CharacterController cc;
    private AudioSource source;
    private float lastGroundedTime;
    private float lastMovingTime = -999f;
    private string lastLoggedGround = "";
    private bool warnedNoClip;

    private void Awake()
    {
        cc = GetComponent<CharacterController>();

        source = gameObject.AddComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;   // 2D so it doesn't fade with camera distance
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
        // Silence footsteps when this script is switched off (e.g. during ragdoll)
        if (source != null) source.Stop();
    }

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

        // Counts as moving for a short moment after the last movement, so speed flicker doesn't cut the sound
        bool moving = Time.time - lastMovingTime <= stopDelay;

        // Status line, twice a second
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
            // Warn once instead of failing silently
            if (!warnedNoClip)
            {
                Debug.LogWarning("Footsteps: no clip found. Assign a default Walk Sound on the Player, or check the ground tag / Surfaces list.", this);
                warnedNoClip = true;
            }
            return;
        }
        warnedNoClip = false;

        // Playback speed: crouch overrides, otherwise use this surface's walk/run speed
        float targetPitch = crouching ? crouchPitch : surfacePlaybackSpeed;

        source.mute = false;
        source.pitch = targetPitch;
        source.volume = volume;

        // Switch clips only when needed so it doesn't restart every frame
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

    // Casts down and returns the closest hit that is NOT part of the player itself
    private bool TryGetGround(Vector3 origin, out RaycastHit result)
    {
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, rayLength, groundMask, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        result = default;
        bool found = false;

        foreach (RaycastHit h in hits)
        {
            if (h.collider.transform.IsChildOf(transform)) continue;   // skip the player's own colliders
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

    // Looks at the ground under the player and picks the clip and playback speed for that surface
    private AudioClip GetClip(bool running, out float playbackSpeed)
    {
        AudioClip walk = walkSound;
        AudioClip run = runSound;
        float walkSpeed = walkPlaybackSpeed;
        float runSpeed = runPlaybackSpeed;

        Vector3 origin = transform.position + Vector3.up * rayStartHeight;
        if (TryGetGround(origin, out RaycastHit hit))
        {
            string groundTag = FindTag(hit.collider.transform);

            if (debug)
            {
                string msg = "Footsteps: ground = '" + hit.collider.name + "', tag = '" + groundTag + "'";
                if (msg != lastLoggedGround)
                {
                    Debug.Log(msg);
                    lastLoggedGround = msg;
                }
            }

            if (surfaces != null)
            {
                foreach (Surface s in surfaces)
                {
                    if (string.Equals(s.tag, groundTag, System.StringComparison.OrdinalIgnoreCase))
                    {
                        if (s.walkSound != null) walk = s.walkSound;
                        if (s.runSound != null) run = s.runSound;
                        if (s.walkPlaybackSpeed > 0f) walkSpeed = s.walkPlaybackSpeed;
                        if (s.runPlaybackSpeed > 0f) runSpeed = s.runPlaybackSpeed;
                        break;
                    }
                }
            }
        }
        else if (debug && lastLoggedGround != "NO GROUND")
        {
            Debug.Log("Footsteps: ray hit nothing. Check groundMask, rayLength, and that the floor has a collider.");
            lastLoggedGround = "NO GROUND";
        }

        // If running but this surface has no run clip, we fall back to the walk clip, so use the walk speed
        if (running && run != null)
        {
            playbackSpeed = runSpeed;
            return run;
        }

        playbackSpeed = walkSpeed;
        return walk;
    }
}