using UnityEngine;

public class Weapon : MonoBehaviour
{
    [Header("Tir")]
    public Transform firePoint;
    public GameObject bulletVisualPrefab;
    public float fireRate = 0.2f;
    public float damage = 10f;
    public float bulletForce = 50f;

    [Header("État de l'arme")]
    public bool isEquipped = false;
    public GameObject owner;

    [Header("Animator")]
    public Animator playerAnimator;

    private float lastShotTime;

    void Start()
    {
        if (playerAnimator == null)
        {
            Debug.LogWarning($"Animator non assigné sur l'arme {name}. Vérifie que PlayerInventory le fournit.");
        }
    }

    void Update()
    {
        if (!isEquipped) return;

        bool isAiming = Input.GetButton("Fire2"); // clic droit
        bool isShooting = Input.GetButton("Fire1"); // clic gauche

        if (playerAnimator != null)
        {
            playerAnimator.SetBool("isAiming", isAiming);
        }

        if (isShooting && Time.time - lastShotTime > fireRate)
        {
            lastShotTime = Time.time;
            Fire();
        }
    }

    void Fire()
    {
        // Raycast pour les dégâts directs
        if (Physics.Raycast(firePoint.position, firePoint.forward, out RaycastHit hit, 100f))
        {
            if (hit.collider.gameObject != owner)
            {
                Debug.Log("Touché : " + hit.collider.name);

                if (hit.collider.TryGetComponent<PlayerHealth>(out var health))
                {
                    health.ApplyDamage(damage);
                }
            }
        }

        // Création de la balle visuelle avec physique
        if (bulletVisualPrefab != null)
        {
            GameObject bullet = Instantiate(bulletVisualPrefab, firePoint.position, firePoint.rotation);
            bullet.SetActive(true);

            Rigidbody rb = bullet.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = bullet.AddComponent<Rigidbody>();
            }

            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.linearVelocity = firePoint.forward * bulletForce; // bonne syntaxe

            // Propriété owner
            Bullet bulletScript = bullet.GetComponent<Bullet>();
            if (bulletScript != null)
            {
                bulletScript.owner = owner;
            }
        }
    }
}
