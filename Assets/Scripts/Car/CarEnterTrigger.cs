using UnityEngine;

public class CarEnterTrigger : MonoBehaviour
{
    public GameObject player;
    public Transform driverSeat;
    public Transform passengerSeat;

    public GameObject playerCameraRig;
    public GameObject carCameraRig;

    public bool isDriverTrigger = true; // coche dans l'inspector selon le rôle

    private VehicleController vehicleController;
    private bool canEnter = false;
    private bool isSetup = false;

    void Start()
    {
        vehicleController = GetComponentInParent<VehicleController>();

        if (!isSetup)
        {
            vehicleController.SetupVehicle(player, playerCameraRig, carCameraRig);
            isSetup = true;
        }
    }

    void Update()
    {
        if (canEnter && Input.GetKeyDown(KeyCode.E))
        {
            if (isDriverTrigger && !vehicleController.IsDriverInside())
            {
                vehicleController.EnterAsDriver(driverSeat.position);
            }
            else if (!isDriverTrigger && !vehicleController.IsPassengerInside())
            {
                vehicleController.EnterAsPassenger(passengerSeat.position);
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            canEnter = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            canEnter = false;
    } 
}
