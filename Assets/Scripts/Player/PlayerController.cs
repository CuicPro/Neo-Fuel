using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(NetworkAnimator))]
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
    public UnityEngine.UI.Slider staminaBar;

    private Animator animator;
    private NetworkAnimator networkAnimator;
    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;

    // Variables réseau pour synchronisation des animations
    private NetworkVariable<float> networkMoveX = new NetworkVariable<float>(
        readPerm: NetworkVariableReadPermission.Everyone,
        writePerm: NetworkVariableWritePermission.Owner);

    private NetworkVariable<float> networkMoveZ = new NetworkVariable<float>(
        readPerm: NetworkVariableReadPermission.Everyone,
        writePerm: NetworkVariableWritePermission.Owner);

    private NetworkVariable<bool> networkIsGrounded = new NetworkVariable<bool>(
        readPerm: NetworkVariableReadPermission.Everyone,
        writePerm: NetworkVariableWritePermission.Owner);

    // Référence vers PlayerCameraController
    private PlayerCameraController cameraController;

    public override void OnNetworkSpawn()
    {
        cameraController = GetComponent<PlayerCameraController>();
        if (cameraController == null)
        {
            Debug.LogError("PlayerCameraController not found! Make sure it's on the same GameObject as PlayerController.");
        }

        if (!IsOwner)
        {
            enabled = false;
            return;
        }
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        networkAnimator = GetComponent<NetworkAnimator>();

        stamina = maxStamina;
        if (staminaBar != null)
            staminaBar.maxValue = maxStamina;

        if (!IsOwner) return;

        // Positionne le joueur sur le sol si possible
        if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, out RaycastHit hit, 10f))
        {
            transform.position = hit.point;
        }
    }

    void Update()
    {
        float smoothTime = 0.15f;

        if (!IsOwner)
        {
            // Si pas owner, applique les valeurs réseau pour l'animation
            animator.SetFloat("MoveX", networkMoveX.Value, smoothTime, Time.deltaTime);
            animator.SetFloat("MoveZ", networkMoveZ.Value, smoothTime, Time.deltaTime);
            animator.SetBool("IsGrounded", networkIsGrounded.Value);
            return;
        }

        // INPUT
        float inputX = Input.GetAxisRaw("Horizontal");
        float inputZ = Input.GetAxisRaw("Vertical");
        Vector3 inputDir = new Vector3(inputX, 0f, inputZ).normalized;
        bool isMoving = inputDir.magnitude > 0.1f;

        // Camera-relative movement
        Vector3 moveDirection = Vector3.zero;
        if (cameraController != null && cameraController.GetCameraTransform() != null)
        {
            Transform cameraTransform = cameraController.GetCameraTransform();

            Vector3 camForward = cameraTransform.forward;
            camForward.y = 0f;
            camForward.Normalize();

            Vector3 camRight = cameraTransform.right;
            camRight.y = 0f;
            camRight.Normalize();

            moveDirection = (camForward * inputZ + camRight * inputX).normalized;

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
        }

        // Stamina / Running logic
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

        // Vitesse cible et interpolation
        float targetSpeed = isMoving ? (isRunning ? runSpeed : walkSpeed) : 0f;
        float accel = (targetSpeed > currentSpeed) ? acceleration : deceleration;
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accel * Time.deltaTime);

        // Gravité et saut
        Ray groundRay = new Ray(transform.position + Vector3.up * 0.2f, Vector3.down);
        isGrounded = Physics.Raycast(groundRay, 0.4f);

        if (isGrounded && velocity.y < 0f)
            velocity.y = -2f;

        if (isGrounded && Input.GetButtonDown("Jump"))
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            animator.SetTrigger("Jump");
            networkAnimator.SetTrigger("Jump");
        }

        velocity.y += gravity * Time.deltaTime;

        // Mouvement
        Vector3 move = moveDirection * currentSpeed;
        controller.Move((move + velocity) * Time.deltaTime);

        // Calcul des paramètres animation
        float moveX = 0f;
        float moveZ = 0f;

        float speedPercent = currentSpeed / runSpeed;

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

        // Applique localement pour ce client
        animator.SetFloat("MoveX", moveX, smoothTime, Time.deltaTime);
        animator.SetFloat("MoveZ", moveZ, smoothTime, Time.deltaTime);
        animator.SetBool("IsGrounded", isGrounded);

        // Synchronise les valeurs réseau
        networkMoveX.Value = moveX;
        networkMoveZ.Value = moveZ;
        networkIsGrounded.Value = isGrounded;
    }
}
