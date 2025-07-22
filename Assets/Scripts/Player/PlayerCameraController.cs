using UnityEngine;
using Unity.Netcode;

public class PlayerCameraController : NetworkBehaviour
{
    [Header("Camera References")]
    public GameObject cameraRig;        // Pivot rotatif
    public Camera playerCamera;         // Cam�ra r�elle dans le rig

    [Header("Settings")]
    public float mouseSensitivity = 2f;
    public float verticalClamp = 80f;

    [HideInInspector]
    public bool isPlayerMoving = false;

    private float rotationX = 0f; // vertical
    private float mouseX, mouseY;

    private Transform cameraTransform;
    private Transform rigTransform;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            if (cameraRig != null) cameraRig.SetActive(false);
            if (playerCamera != null) playerCamera.gameObject.SetActive(false);
            enabled = false;
            return;
        }

        // Activer notre cam�ra
        if (cameraRig != null) cameraRig.SetActive(true);
        if (playerCamera != null)
        {
            playerCamera.gameObject.SetActive(true);
            playerCamera.enabled = true;
            cameraTransform = playerCamera.transform;
        }

        rigTransform = cameraRig.transform;

        // D�sactiver la Main Camera de la sc�ne s�il y en a une
        if (Camera.main != null && Camera.main != playerCamera)
            Camera.main.enabled = false;
    }

    void Start()
    {
        if (!IsOwner) return;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // S�curit� si pas de r�f�rence auto
        if (cameraTransform == null && playerCamera != null)
            cameraTransform = playerCamera.transform;
        if (rigTransform == null && cameraRig != null)
            rigTransform = cameraRig.transform;
    }

    void Update()
    {
        if (!IsOwner) return;

        HandleCursor();
        HandleMouseLook();
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

    void HandleMouseLook()
    {
        if (cameraTransform == null || rigTransform == null) return;
        if (Cursor.lockState != CursorLockMode.Locked) return;

        mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Vertical : cam�ra seulement
        rotationX -= mouseY;
        rotationX = Mathf.Clamp(rotationX, -verticalClamp, verticalClamp);
        cameraTransform.localRotation = Quaternion.Euler(rotationX, 0f, 0f);

        if (isPlayerMoving)
        {
            // Tourne le joueur si il bouge
            transform.Rotate(Vector3.up * mouseX);

            // Caler la cam�ra sur la rotation du joueur
            rigTransform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        }
        else
        {
            // Sinon, free look de la cam�ra autour du joueur
            rigTransform.Rotate(Vector3.up * mouseX);
        }
    }

    // Pour permettre � d'autres scripts d'acc�der � la direction cam�ra
    public Vector3 GetCameraForwardFlat()
    {
        if (cameraTransform == null) return transform.forward;
        Vector3 dir = cameraTransform.forward;
        dir.y = 0f;
        return dir.normalized;
    }

    public Vector3 GetCameraRightFlat()
    {
        if (cameraTransform == null) return transform.right;
        Vector3 dir = cameraTransform.right;
        dir.y = 0f;
        return dir.normalized;
    }

    public Transform GetCameraTransform()
    {
        return cameraTransform;
    }
}
