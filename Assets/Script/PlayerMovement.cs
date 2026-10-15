using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(AudioSource))]
public class PlayerMovement : MonoBehaviour
{
    private CharacterController controller;
    private Animator animator;
    private AudioSource audioSource;

    [Header("Player Movement")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;

    [Header("Jumping")]
    public float jumpHeight = 2f;
    public float gravity = -9.81f;

    [Header("Jump Sound")]
    public AudioClip jumpSound;

    [Range(0f, 1f)]
    public float jumpVolume = 1f;

    [SerializeField] private Transform cameraTransform;
    private Vector3 velocity;

    [Header("Sprint")]
    public float sprintSpeedMultiplier = 10f;

    [Header("Crouching")]
    public float crouchHeight = 0.5f;
    public float crouchSpeedMultiplier = 0.5f;
    public float crouchTransitionSpeed = 10f;

    private float standingHeight;
    private Vector3 standingCenter;

    private Vector3 crouchCameraOffset = new Vector3(0, -0.5f, 0);
    private Vector3 originalCameraLocalPos;

    private bool isCrouching;
    private bool isSprinting;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();

        // Configure AudioSource.
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.volume = 1f;
        audioSource.mute = false;
        audioSource.spatialBlend = 0f;

        // Store original character dimensions.
        standingHeight = controller.height;
        standingCenter = controller.center;

        // Find the main camera if none is assigned.
        if (cameraTransform == null)
        {
            Camera mainCam = Camera.main;

            if (mainCam != null)
            {
                cameraTransform = mainCam.transform;
            }
        }

        // Store the camera's original local position.
        if (cameraTransform != null)
        {
            originalCameraLocalPos = cameraTransform.localPosition;
        }

        // Check if the jump sound is assigned.
        if (jumpSound == null)
        {
            Debug.LogWarning(
                "Jump sound is missing! Assign an AudioClip " +
                "to the Jump Sound field in the Inspector."
            );
        }
        else
        {
            Debug.Log("Jump sound assigned: " + jumpSound.name);
        }
    }

    void Update()
    {
        MovePlayer();
    }

    void MovePlayer()
    {
        bool grounded = controller.isGrounded;

        // Keep the player grounded.
        if (grounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        // Get movement input.
        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");

        Vector3 forward = Vector3.forward;
        Vector3 right = Vector3.right;

        // Use camera direction for movement.
        if (cameraTransform != null)
        {
            forward = cameraTransform.forward;
            right = cameraTransform.right;
        }

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 move = forward * y + right * x;

        // Rotate player toward movement direction.
        if (move.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(move);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }

        // Sprint.
        isSprinting = Input.GetKey(KeyCode.LeftShift);

        float currentSpeed = moveSpeed;

        if (isCrouching)
        {
            currentSpeed *= crouchSpeedMultiplier;
        }

        if (isSprinting)
        {
            currentSpeed *= sprintSpeedMultiplier;
        }

        // Update animator parameters.
        if (animator != null)
        {
            float animSpeed = move.magnitude * currentSpeed;

            animator.SetFloat("speed", animSpeed);
            animator.SetBool("isCrouching", isCrouching);
        }

        // Jump and play jump sound.
        if (Input.GetButtonDown("Jump") && grounded)
        {
            // Calculate jump velocity.
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

            Debug.Log("Player jumped!");

            // Play jump sound.
            if (jumpSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(jumpSound, jumpVolume);

                Debug.Log("Playing jump sound: " + jumpSound.name);
            }
            else
            {
                Debug.LogError(
                    "Cannot play jump sound. Check the Jump Sound " +
                    "AudioClip assignment in the Inspector."
                );
            }

            // Play jump animation.
            if (animator != null)
            {
                animator.SetTrigger("jump");
            }
        }

        // Handle crouching.
        HandleCrouch();

        // Apply gravity.
        velocity.y += gravity * Time.deltaTime;

        // Calculate horizontal movement.
        Vector3 horizontalMove = move.normalized * currentSpeed;

        // Combine horizontal and vertical movement.
        Vector3 finalMove = new Vector3(
            horizontalMove.x,
            velocity.y,
            horizontalMove.z
        );

        // Move the character.
        controller.Move(finalMove * Time.deltaTime);
    }

    void HandleCrouch()
    {
        // Press Left Control to crouch.
        if (Input.GetKeyDown(KeyCode.LeftControl))
        {
            isCrouching = true;
        }
        // Release Left Control to stand.
        else if (Input.GetKeyUp(KeyCode.LeftControl))
        {
            // Check whether there is enough room to stand.
            if (!Physics.Raycast(
                transform.position,
                Vector3.up,
                standingHeight
            ))
            {
                isCrouching = false;
            }
        }

        // Adjust character height and center.
        if (isCrouching)
        {
            controller.height = Mathf.Lerp(
                controller.height,
                crouchHeight,
                crouchTransitionSpeed * Time.deltaTime
            );

            controller.center = Vector3.Lerp(
                controller.center,
                new Vector3(0f, crouchHeight / 2f, 0f),
                crouchTransitionSpeed * Time.deltaTime
            );
        }
        else
        {
            controller.height = Mathf.Lerp(
                controller.height,
                standingHeight,
                crouchTransitionSpeed * Time.deltaTime
            );

            controller.center = Vector3.Lerp(
                controller.center,
                standingCenter,
                crouchTransitionSpeed * Time.deltaTime
            );
        }

        // Move the camera when crouching.
        if (cameraTransform != null)
        {
            Vector3 targetCamPos = originalCameraLocalPos +
                (isCrouching ? crouchCameraOffset : Vector3.zero);

            cameraTransform.localPosition = Vector3.Lerp(
                cameraTransform.localPosition,
                targetCamPos,
                crouchTransitionSpeed * Time.deltaTime
            );
        }
    }
}