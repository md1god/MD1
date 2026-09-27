using UnityEngine;

/// <summary>
/// PlayerKart - arcade-style kart controller with engine force, steering, drift and boost.
///
/// This is the arcade alternative to CarPhysics (which is WheelCollider-based and
/// realistic). Use one or the other on the Kart, never both at the same time.
///
/// Input works on both backends: legacy Input Manager axes when available, and the
/// new Input System keyboard (WASD/arrows + Shift/Space) otherwise, so it stays alive
/// in projects where the active input handler is Input System only.
/// Uses ForceMode.Acceleration throughout so behaviour is mass-independent.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[DisallowMultipleComponent]
public class PlayerKart : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Forward acceleration in m/s^2 (Acceleration mode, mass-independent).")]
    public float engineForce = 22f;
    public float maxSpeed = 25f;           // Top forward speed (m/s)
    public float steeringAngle = 25f;      // How sharp the kart turns (deg/sec factor)
    [Tooltip("Braking deceleration in m/s^2 when pressing back while moving forward.")]
    public float brakeForce = 30f;
    public float reverseSpeed = 12f;       // Max reverse speed (m/s)

    [Header("Drift")]
    [Tooltip("Sideways acceleration in m/s^2 while handbraking.")]
    public float handbrakeForce = 9f;
    public float driftBoostChargeTime = 1.5f; // Time to fully charge drift boost
    [Tooltip("Extra boost speed (m/s) released at full charge.")]
    public float maxDriftBoost = 10f;
    public LayerMask trackSurface;         // Legacy / optional extra surface mask

    [Header("Surface Check")]
    [Range(0f, 1f)]
    [Tooltip("Engine multiplier when off-track. 1 = no penalty.")]
    public float offTrackSlowdown = 0.5f;
    [Tooltip("Layer(s) considered track. Leave = 0 (Nothing) to accept any ground hit.")]
    public LayerMask trackLayer;

    [Header("Debug")]
    public bool showDebugInfo = false;

    // Private fields
    private Rigidbody rb;
    private float currentEngineForce;
    private float currentSteering;
    private bool isDrifting = false;
    private float driftCharge = 0f;
    private bool isGrounded = true;
    private bool isOffTrack = false;
    private float speedFactor; // 0..1 normalised forward speed, recomputed every physics step

    // Properties for external access
    public float CurrentSpeed => rb != null ? rb.linearVelocity.magnitude : 0f;
    public float CurrentSpeedKmh => CurrentSpeed * 3.6f;
    public bool IsDrifting => isDrifting;
    public float DriftCharge => driftCharge;
    public float DriftBoostMultiplier => 1f + driftCharge * (maxDriftBoost / Mathf.Max(1f, maxSpeed));

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null) { enabled = false; return; }
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        if (rb.mass < 1f) rb.mass = 800f;
        if (rb.centerOfMass.y > -0.2f)
            rb.centerOfMass = new Vector3(0f, -0.5f, 0f);
    }

    void FixedUpdate()
    {
        HandleInput();
        ApplyPhysics();
        CheckSurface();
        CheckGround();
    }

    /// <summary>Reads throttle / steer / handbrake from legacy axes first, new Input System keyboard as fallback.</summary>
    static void ReadInput(out float accel, out float steer, out bool handbrake)
    {
        accel = 0f; steer = 0f; handbrake = false;

        // Legacy Input Manager (works when the project still has it enabled)
        try
        {
            float a = Input.GetAxis("Vertical");
            float s = Input.GetAxis("Horizontal");
            if (Mathf.Abs(a) > 0.001f || Mathf.Abs(s) > 0.001f || Input.GetButton("Fire3"))
            {
                accel = a; steer = s; handbrake = Input.GetButton("Fire3");
                return;
            }
        }
        catch (System.Exception) { /* legacy backend unavailable - fall through */ }

        // New Input System keyboard
        try
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                float up = (kb.wKey.isPressed || kb.upArrowKey.isPressed) ? 1f : 0f;
                float down = (kb.sKey.isPressed || kb.downArrowKey.isPressed) ? 1f : 0f;
                float left = (kb.aKey.isPressed || kb.leftArrowKey.isPressed) ? 1f : 0f;
                float right = (kb.dKey.isPressed || kb.rightArrowKey.isPressed) ? 1f : 0f;
                accel = up - down;
                steer = right - left;
                handbrake = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed || kb.spaceKey.isPressed;
            }
        }
        catch (System.Exception) { /* no input device available */ }
    }

    void HandleInput()
    {
        ReadInput(out float accel, out float steer, out bool handbrake);

        float effectiveEngine = engineForce * (isOffTrack ? offTrackSlowdown : 1f);

        // Engine / brake / reverse
        float forwardVel = Vector3.Dot(rb.linearVelocity, transform.forward);
        if (accel > 0.1f)
        {
            currentEngineForce = accel * effectiveEngine;
        }
        else if (accel < -0.1f)
        {
            if (forwardVel > 1f)
            {
                // Braking while still rolling forward
                currentEngineForce = accel * brakeForce;
            }
            else
            {
                // Reversing (capped later by reverseSpeed)
                currentEngineForce = accel * effectiveEngine * 0.6f;
            }
        }
        else
        {
            currentEngineForce = 0f;
        }

        // Handbrake / drift (only when grounded, moving forward, and holding throttle direction)
        if (handbrake && isGrounded && CanDrift() && accel > 0.1f)
        {
            isDrifting = true;
            driftCharge = Mathf.Clamp01(driftCharge + Time.fixedDeltaTime / Mathf.Max(0.01f, driftBoostChargeTime));
        }
        else if (!handbrake && isDrifting)
        {
            // Release drift boost on exit
            ApplyDriftBoost();
            isDrifting = false;
            driftCharge = 0f;
        }
        else if (!handbrake)
        {
            driftCharge = Mathf.Max(0f, driftCharge - Time.fixedDeltaTime * 0.5f);
        }

        // Steering (only effective when moving) — stored for ApplyPhysics
        speedFactor = Mathf.Clamp01(rb.linearVelocity.magnitude / Mathf.Max(0.01f, maxSpeed));
        currentSteering = steer * steeringAngle * (0.5f + 0.5f * speedFactor);
    }

    void ApplyPhysics()
    {
        Vector3 forward = transform.forward;

        float cappedMax = isOffTrack ? maxSpeed * offTrackSlowdown : maxSpeed;

        // Limit forward speed
        float forwardVel = Vector3.Dot(rb.linearVelocity, forward);
        if (forwardVel > 0 && forwardVel > cappedMax)
        {
            // Bleed off excess speed smoothly instead of snapping
            rb.linearVelocity = rb.linearVelocity - forward * (forwardVel - cappedMax) * 0.5f;
        }

        // Apply reverse speed limit
        if (forwardVel < 0 && -forwardVel > reverseSpeed)
        {
            rb.linearVelocity = rb.linearVelocity + forward * (-forwardVel - reverseSpeed) * 0.5f;
        }

        // Engine force (Acceleration = m/s^2, mass-independent)
        rb.AddForce(forward * currentEngineForce, ForceMode.Acceleration);

        // Steering
        if (isGrounded && Mathf.Abs(currentSteering) > 0.1f)
        {
            Quaternion steerRotation = Quaternion.Euler(0, currentSteering * Time.fixedDeltaTime * 3f, 0);
            rb.MoveRotation(rb.rotation * steerRotation);
        }

        // Handbrake drift
        if (isDrifting)
        {
            // Push kart sideways opposite to steering to break traction
            Vector3 right = transform.right;
            float sideDir = currentSteering > 0f ? -1f : 1f;
            rb.AddForce(right * sideDir * handbrakeForce, ForceMode.Acceleration);

            // Small forward push while drifting, scaled by charge
            float driftBoostAccel = engineForce * 0.3f * driftCharge;
            rb.AddForce(forward * driftBoostAccel, ForceMode.Acceleration);
        }

        // Natural drag
        rb.linearDamping = Mathf.Lerp(0.4f, 1.0f, speedFactor);
    }

    void CheckSurface()
    {
        // Raycast down to check if on track.
        // If trackLayer is 0 (Nothing) we accept ANY ground hit so a default
        // scene (Ground on Default layer) works out of the box.
        RaycastHit hit;
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        bool hasHit;
        if (trackLayer.value != 0)
        {
            hasHit = Physics.Raycast(origin, Vector3.down, out hit, 6f, trackLayer.value);
            if (!hasHit && trackSurface.value != 0)
                hasHit = Physics.Raycast(origin, Vector3.down, out hit, 6f, trackSurface.value);
        }
        else
        {
            hasHit = Physics.Raycast(origin, Vector3.down, out hit, 6f);
        }
        isOffTrack = !hasHit;

        if (showDebugInfo)
        {
            Debug.Log($"Speed: {CurrentSpeedKmh:F1} km/h | OffTrack: {isOffTrack} | ForwardVel: {Vector3.Dot(rb.linearVelocity, transform.forward):F2}");
        }
    }

    void CheckGround()
    {
        // Check if kart is grounded (simple raycast check)
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        isGrounded = Physics.Raycast(origin, Vector3.down, 2.5f);
    }

    void ApplyDriftBoost()
    {
        if (driftCharge > 0.3f && rb != null)
        {
            // Release the charged boost as an instant velocity kick (m/s)
            float boostSpeed = maxDriftBoost * driftCharge;
            rb.AddForce(transform.forward * boostSpeed, ForceMode.VelocityChange);
        }
    }

    // Public method for external boost (boostPower in m/s velocity kick)
    public void ApplyBoost(float boostPower)
    {
        if (rb != null)
            rb.AddForce(transform.forward * boostPower, ForceMode.VelocityChange);
    }

    // For drift charge recovery over time when not drifting
    void Update()
    {
        // Visual/Animation feedback could go here
    }

    // Public getter for surface type at current position
    public bool IsOnTrack()
    {
        return !isOffTrack;
    }

    // Get current drift charge as percentage (0-1)
    public float GetDriftChargePercent()
    {
        return driftCharge;
    }

    // Check if kart can initiate drift
    public bool CanDrift()
    {
        return rb != null && isGrounded && CurrentSpeed > 5f;
    }
}
