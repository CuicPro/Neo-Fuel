using UnityEngine;

public class VehicleController : MonoBehaviour
{
    [Header("Physique")]
    public float acceleration = 3000f;
    public float maxSpeed = 20f;
    public float turnSpeed = 100f;
    public float drag = 0.95f;

    [Header("Références")]
    public Transform exitPoint;

    private GameObject player;
    private GameObject playerCameraRig;
    private GameObject carCameraRig;

    private Rigidbody rb;
    private bool isInitialized = false;
    private bool isDriving = false;
    private bool isPassenger = false;

    public void SetupVehicle(GameObject playerObj, GameObject playerCam, GameObject carCam)
    {
        player = playerObj;
        playerCameraRig = playerCam;
        carCameraRig = carCam;
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = false;
        isInitialized = true;
    }

    public bool IsDriverInside() => isDriving;
    public bool IsPassengerInside() => isPassenger;

    public void EnterAsDriver(Vector3 seatPosition)
    {
        if (!isInitialized) return;

        player.transform.position = seatPosition;
        player.SetActive(false);
        playerCameraRig.SetActive(false);
        carCameraRig.SetActive(true);

        isDriving = true;
        isPassenger = false;
        this.enabled = true;
    }

    public void EnterAsPassenger(Vector3 seatPosition)
    {
        if (!isInitialized) return;

        player.transform.position = seatPosition;
        player.SetActive(false);
        playerCameraRig.SetActive(false);
        carCameraRig.SetActive(true);

        isPassenger = true;
        isDriving = false;
        this.enabled = false; // Ne pas conduire si passager
    }

    void Update()
    {
        if ((isDriving || isPassenger) && Input.GetKeyDown(KeyCode.E))
        {
            ExitCar();
        }
    }

    void ExitCar()
    {
        player.transform.position = exitPoint.position;
        player.SetActive(true);

        carCameraRig.SetActive(false);
        playerCameraRig.SetActive(true);

        isDriving = false;
        isPassenger = false;
        this.enabled = false;
    }

    void FixedUpdate()
    {
        if (!isDriving) return;

        float vertical = Input.GetAxis("Vertical");
        float horizontal = Input.GetAxis("Horizontal");

        float currentSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);

        if (Mathf.Abs(currentSpeed) < maxSpeed)
        {
            rb.AddForce(transform.forward * vertical * acceleration * Time.fixedDeltaTime);
        }

        if (Mathf.Abs(currentSpeed) > 1f && Mathf.Abs(vertical) > 0.1f)
        {
            float turn = horizontal * turnSpeed * Time.fixedDeltaTime * Mathf.Sign(currentSpeed);
            Quaternion rotation = Quaternion.Euler(0f, turn, 0f);
            rb.MoveRotation(rb.rotation * rotation);
        }

        rb.linearVelocity *= drag;
    }
}
