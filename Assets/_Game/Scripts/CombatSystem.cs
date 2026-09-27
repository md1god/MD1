using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// HealthComponent: Health, damage, death handling for any entity.
/// </summary>
public class HealthComponent : MonoBehaviour
{
    public float maxHealth = 100f;
    public float currentHealth;
    public bool isInvincible = false;
    public float invincibilityTime = 0.5f;

    float _invincibilityTimer;
    bool _isDead;

    public event System.Action<float, float> OnHealthChanged; // current, max
    public event System.Action OnDeath;

    void Awake()
    {
        currentHealth = maxHealth;
    }

    void Update()
    {
        if (_invincibilityTimer > 0f)
            _invincibilityTimer -= Time.deltaTime;
    }

    public void TakeDamage(float amount, GameObject source = null)
    {
        if (_isDead || isInvincible || _invincibilityTimer > 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        _invincibilityTimer = invincibilityTime;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0f)
            Die();
    }

    public void Heal(float amount)
    {
        if (_isDead) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    void Die()
    {
        _isDead = true;
        OnDeath?.Invoke();

        // Disable components
        var renderer = GetComponent<Renderer>();
        if (renderer) renderer.enabled = false;
        var collider = GetComponent<Collider>();
        if (collider) collider.enabled = false;
        var rb = GetComponent<Rigidbody>();
        if (rb) rb.isKinematic = true;

        // Respawn or destroy
        if (CompareTag("Player"))
        {
            Invoke(nameof(RespawnPlayer), 3f);
        }
        else
        {
            Destroy(gameObject, 5f);
        }
    }

    void RespawnPlayer()
    {
        currentHealth = maxHealth;
        _isDead = false;
        var renderer = GetComponent<Renderer>();
        if (renderer) renderer.enabled = true;
        var collider = GetComponent<Collider>();
        if (collider) collider.enabled = true;

        // Find safe spawn point
        var spawn = FindSafeSpawn();
        transform.position = spawn;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    Vector3 FindSafeSpawn()
    {
        // Try to find a chunk center at y=2
        var tracker = FindFirstObjectByType<PlayerChunkTracker>();
        if (tracker)
        {
            var active = tracker.GetActiveChunkCoords();
            if (active != null && active.Count > 0)
            {
                return new Vector3(active[0].x * 5000f, 2f, active[0].y * 5000f);
            }
        }
        return new Vector3(0, 2, 0);
    }
}

/// <summary>
/// SimpleCombat: Raycast-based shooting/melee for player/enemies.
/// </summary>
public class SimpleCombat : MonoBehaviour
{
    public float range = 50f;
    public float damage = 25f;
    public float fireRate = 0.5f;
    public LayerMask targetLayers;
    public Transform firePoint;
    public GameObject hitEffect;

    float _nextFireTime;
    Camera _cam;

    void Awake()
    {
        _cam = Camera.main;
        if (!firePoint) firePoint = transform;
    }

    void Update()
    {
        if (CompareTag("Player") && Keyboard.current != null)
        {
            if (Mouse.current.leftButton.isPressed && Time.time >= _nextFireTime)
            {
                Fire();
                _nextFireTime = Time.time + fireRate;
            }
        }
    }

    void Fire()
    {
        if (!_cam) return;

        Ray ray = _cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        if (Physics.Raycast(ray, out RaycastHit hit, range, targetLayers))
        {
            var health = hit.collider.GetComponentInParent<HealthComponent>();
            if (health)
            {
                health.TakeDamage(damage, gameObject);
                if (hitEffect)
                    Instantiate(hitEffect, hit.point, Quaternion.LookRotation(hit.normal));
            }
        }
    }
}