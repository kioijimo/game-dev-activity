using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerLandingSound : MonoBehaviour
{
    [System.Serializable]
    public class SurfaceLandingSound
    {
        [Tooltip("Ground tag, e.g. Dirt or Stone.")]
        public string tag;

        [Tooltip("Landing sound for this surface.")]
        public AudioClip landingSound;

        [Range(0f, 1f)]
        public float volume = 1f;
    }

    [Header("Default Landing Sound")]
    [SerializeField] private AudioClip defaultLandingSound;

    [Range(0f, 1f)]
    [SerializeField] private float defaultVolume = 1f;

    [Header("Surface Landing Sounds")]
    [SerializeField] private SurfaceLandingSound[] surfaces;

    [Header("Sound Duration")]
    [SerializeField] private float landingSoundDuration = 0.5f;

    [Header("Ground Detection")]
    [SerializeField] private float rayStartHeight = 0.5f;
    [SerializeField] private float rayLength = 2f;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Landing Settings")]
    [SerializeField] private float minimumFallSpeed = 1f;
    [SerializeField] private bool debug = true;

    private CharacterController controller;
    private AudioSource landingAudioSource;

    private bool wasGrounded;
    private bool hasBeenAirborne;
    private float previousVerticalVelocity;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        // Create a separate AudioSource for landing sounds.
        landingAudioSource = gameObject.AddComponent<AudioSource>();

        landingAudioSource.playOnAwake = false;
        landingAudioSource.loop = false;
        landingAudioSource.spatialBlend = 0f;
        landingAudioSource.volume = 1f;

        wasGrounded = controller.isGrounded;
    }

    private void Update()
    {
        bool isGrounded = controller.isGrounded;

        // Detect when the player leaves the ground.
        if (!isGrounded)
        {
            hasBeenAirborne = true;

            previousVerticalVelocity = controller.velocity.y;
        }

        // Detect when the player lands.
        if (isGrounded && !wasGrounded && hasBeenAirborne)
        {
            float fallSpeed = Mathf.Abs(previousVerticalVelocity);

            if (fallSpeed >= minimumFallSpeed)
            {
                PlayLandingSound();
            }

            hasBeenAirborne = false;
        }

        wasGrounded = isGrounded;
    }

    private void PlayLandingSound()
    {
        AudioClip selectedClip = defaultLandingSound;
        float selectedVolume = defaultVolume;

        Vector3 origin =
            transform.position + Vector3.up * rayStartHeight;

        // Detect the ground beneath the player.
        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            rayLength,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        float closestDistance = float.MaxValue;
        RaycastHit groundHit = default;
        bool foundGround = false;

        foreach (RaycastHit hit in hits)
        {
            // Ignore the player's own colliders.
            if (hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                groundHit = hit;
                foundGround = true;
            }
        }

        if (foundGround)
        {
            string groundTag = FindGroundTag(
                groundHit.collider.transform
            );

            if (debug)
            {
                Debug.Log(
                    "[Landing Sound] Ground: " +
                    groundHit.collider.name +
                    ", Tag: " + groundTag,
                    this
                );
            }

            // Find the matching surface sound.
            if (surfaces != null)
            {
                foreach (SurfaceLandingSound surface in surfaces)
                {
                    if (surface == null)
                    {
                        continue;
                    }

                    if (string.Equals(
                        surface.tag,
                        groundTag,
                        System.StringComparison.OrdinalIgnoreCase
                    ))
                    {
                        if (surface.landingSound != null)
                        {
                            selectedClip = surface.landingSound;
                            selectedVolume = surface.volume;
                        }

                        break;
                    }
                }
            }
        }
        else if (debug)
        {
            Debug.LogWarning(
                "[Landing Sound] No ground detected. " +
                "Check ground colliders, ray length, and ground mask.",
                this
            );
        }

        // Play the selected landing sound for only 1 second.
        if (selectedClip != null)
        {
            // Stop any previous landing sound.
            landingAudioSource.Stop();
            CancelInvoke(nameof(StopLandingSound));

            landingAudioSource.clip = selectedClip;
            landingAudioSource.volume = selectedVolume;
            landingAudioSource.loop = false;
            landingAudioSource.time = 0f;

            landingAudioSource.Play();

            // Stop playback after the configured duration.
            Invoke(
                nameof(StopLandingSound),
                Mathf.Max(0f, landingSoundDuration)
            );

            if (debug)
            {
                Debug.Log(
                    "[Landing Sound] Playing: " +
                    selectedClip.name +
                    " for " + landingSoundDuration + " second(s).",
                    this
                );
            }
        }
        else if (debug)
        {
            Debug.LogWarning(
                "[Landing Sound] No sound assigned for this surface.",
                this
            );
        }
    }

    private void StopLandingSound()
    {
        landingAudioSource.Stop();
        landingAudioSource.clip = null;
    }

    private string FindGroundTag(Transform target)
    {
        // Check the ground object and its parents.
        while (target != null)
        {
            if (!target.CompareTag("Untagged"))
            {
                return target.tag;
            }

            target = target.parent;
        }

        return "Untagged";
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(StopLandingSound));

        if (landingAudioSource != null)
        {
            landingAudioSource.Stop();
        }
    }
}