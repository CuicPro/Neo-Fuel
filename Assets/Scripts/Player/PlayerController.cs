using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : NetworkBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 3f;
    public float runSpeed = 6f;
    public float gravity = -9.81f;
    public float jumpHeight = 1.5f;

    [Header("Acceleration")]
    public float acceleration = 10f;
    public float deceleration = 10f;
    private float currentSpeed = 0f;

    [Header("Stamina")]
    public float maxStamina = 5f;
    public float staminaRegenRate = 1f;
    public float staminaDrainRate = 1f;
    public float staminaRecoverThreshold = 2f;

    private float stamina;
    private bool isTired = false;
    private bool hasReleasedShiftAfterTired = true;

    [Header("Rotation")]
    public float rotationSpeed = 10f;

    [Header("References")]
    public Transform cameraTransform;
    public UnityEngine.UI.Slider staminaBar;

    private Animator animator;
    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;

    private Camera playerCamera;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        // Active la caméra enfant uniquement pour le joueur local
        playerCamera = GetComponentInChildren<Camera>();
        if (playerCamera != null)
            playerCamera.enabled = true;

        // Si tu veux désactiver la caméra principale de la scène pour ce joueur
        if (Camera.main != null)
            Camera.main.enabled = false;
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        stamina = maxStamina;

        if (staminaBar != null)
            staminaBar.maxValue = maxStamina;

        if (!IsOwner)
        {
            // Désactive la caméra enfant des autres joueurs
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null)
                cam.enabled = false;
            return;
        }

        // Ta logique existante, avec caméra assignée
        if (playerCamera != null)
            cameraTransform = playerCamera.transform;
        else if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        // ✅ Positionne le joueur sur le sol si possible
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, out hit, 10f))
        {
            transform.position = hit.point;
        }
    }

    void HandleCursor()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (Input.GetMouseButtonDown(0))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void Update()
    {
        if (!IsOwner) return;

        float inputX = Input.GetAxisRaw("Horizontal");
        float inputZ = Input.GetAxisRaw("Vertical");
        Vector3 inputDir = new Vector3(inputX, 0f, inputZ).normalized;
        bool isMoving = inputDir.magnitude > 0.1f;

        // Camera-relative movement
        Vector3 camForward = cameraTransform.forward;
        camForward.y = 0f;
        camForward.Normalize();

        Vector3 camRight = cameraTransform.right;
        camRight.y = 0f;
        camRight.Normalize();

        Vector3 moveDirection = (camForward * inputZ + camRight * inputX).normalized;

        // Rotation vers direction du mouvement
        if (isMoving)
        {
            float lookInputX = inputX;
            float lookInputZ = Mathf.Abs(inputZ);
            if (inputZ < 0) lookInputX = -inputX;

            Vector3 lookInputDir = new Vector3(lookInputX, 0f, lookInputZ).normalized;
            Vector3 lookDirection = (camForward * lookInputDir.z + camRight * lookInputDir.x).normalized;

            Quaternion targetRot = Quaternion.LookRotation(lookDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }

        // ----- Stamina / Running -----
        bool shiftHeld = Input.GetKey(KeyCode.LeftShift);
        bool shiftReleased = Input.GetKeyUp(KeyCode.LeftShift);

        if (shiftReleased)
            hasReleasedShiftAfterTired = true;

        bool isTryingToRun = shiftHeld && isMoving;
        bool isRunning = false;

        if (isTryingToRun && !isTired && hasReleasedShiftAfterTired && stamina > 0f)
        {
            isRunning = true;
            stamina -= staminaDrainRate * Time.deltaTime;

            if (stamina <= 0f)
            {
                stamina = 0f;
                isTired = true;
                isRunning = false;
                hasReleasedShiftAfterTired = false;
            }
        }
        else
        {
            isRunning = false;

            if (stamina < maxStamina)
            {
                stamina += staminaRegenRate * Time.deltaTime;

                if (isTired && stamina >= staminaRecoverThreshold)
                    isTired = false;
            }
        }

        stamina = Mathf.Clamp(stamina, 0f, maxStamina);
        if (staminaBar != null)
            staminaBar.value = stamina;

        float targetSpeed = isMoving ? (isRunning ? runSpeed : walkSpeed) : 0f;
        float accel = (targetSpeed > currentSpeed) ? acceleration : deceleration;
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accel * Time.deltaTime);

        // ----- Gravity & Jump -----
        Ray groundRay = new Ray(transform.position + Vector3.up * 0.2f, Vector3.down);
        isGrounded = Physics.Raycast(groundRay, 0.4f);

        if (isGrounded && velocity.y < 0f)
            velocity.y = -2f;

        if (isGrounded && Input.GetButtonDown("Jump"))
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            animator.SetTrigger("Jump");
        }

        velocity.y += gravity * Time.deltaTime;

        // ----- Movement -----
        Vector3 move = moveDirection * currentSpeed;
        controller.Move((move + velocity) * Time.deltaTime);

        HandleCursor();

        // ----- Animator -----
        float moveX = 0f;
        float moveZ = 0f;

        // Calcule MoveZ en fonction de la vitesse réelle
        float speedPercent = currentSpeed / runSpeed; // Entre 0 et 1

        if (inputZ > 0.1f)
            moveZ = Mathf.Lerp(0f, 1f, speedPercent);
        else if (inputZ < -0.1f)
            moveZ = -Mathf.Lerp(0f, 0.5f, currentSpeed / walkSpeed);
        else
            moveZ = 0f;

        if (inputX > 0.1f)
            moveX = Mathf.Lerp(0f, 0.5f, currentSpeed / walkSpeed);
        else if (inputX < -0.1f)
            moveX = -Mathf.Lerp(0f, 0.5f, currentSpeed / walkSpeed);
        else
            moveX = 0f;

        float smoothTime = 0.15f; // temps de lissage (tu peux ajuster)
        animator.SetFloat("MoveX", moveX, smoothTime, Time.deltaTime);
        animator.SetFloat("MoveZ", moveZ, smoothTime, Time.deltaTime);
        animator.SetBool("IsGrounded", isGrounded);


    }
}
