using UnityEngine;

/// <summary>
/// CarPhysics - proper rigid-body car physics for the open world.
///
/// Design goals:
///   * zero external dependencies (no prefab names, no missing assets)
///   * creates its own WheelColliders at runtime from the car size
///   * never throws: every optional piece is null-guarded
///
/// Attach to a car root. It requires a Rigidbody (added automatically).
/// Input is read through the Input System, but only if a device exists,
/// so the script is safe on WebGL/mobile and in the editor without focus.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[DisallowMultipleComponent]
public class CarPhysics : MonoBehaviour
{
    // ------------------------------------------------------------------ setup
    [Header("Body")]
    [Tooltip("Keep the shell upright. Disable for a bouncy arcade feel.")]
    public bool upright = true;

    [Tooltip("Negative lowers the centre of mass so the car does not flip.")]
    public float centreOfMassOffset = -0.35f;

    // ------------------------------------------------------------------ wheels
    [Header("Wheels (metres, auto-detected when autoFit is on)")]
    public bool autoFit = true;
    public float frontAxle = 1.45f;
    public float rearAxle = -1.45f;
    public float trackWidth = 0.95f;
    public float wheelRadius = 0.36f;
    public float suspensionDistance = 0.28f;

    [Header("Wheel visuals")]
    [Tooltip("Optional: drag the 4 real wheel objects here to spin them. " +
             "FL, FR, RL, RR - front left, front right, rear left, rear right.")]
    public Transform[] wheelVisuals = new Transform[0];

    [Tooltip("Create simple cylinder wheels when no visuals are assigned.")]
    public bool createWheelVisuals = true;

    public Material wheelMaterial;

    // ------------------------------------------------------------------ engine
    [Header("Engine")]
    public float maxMotorTorque = 900f;
    public float maxBrakeTorque = 2600f;
    public float maxSteerAngle = 30f;
    public float steerSpeed = 5f;
    public float steerReturn = 8f;
    public float topSpeed = 55f;
    public float reverseTopSpeed = 14f;
    public bool allWheelDrive = true;
    public float handbrakeTorque = 4200f;

    [Header("Aero / stability")]
    public float downforce = 70f;
    public float antiRoll = 9000f;
    public float gripFront = 3.4f;
    public float gripRear = 3.8f;

    // ------------------------------------------------------------------ state
    [Header("Read only (runtime)")]
    public float speedKmh;
    public float motorInput;
    public float steerInput;
    public float brakeInput;
    public bool handbrake;

    // ------------------------------------------------------------------ private
    WheelCollider[] wheels;
    Rigidbody body;
    float steerCurrent;
    bool ready;

    // ------------------------------------------------------------------ setup
    void Awake()
    {
        body = GetComponent<Rigidbody>();
        if (body == null) { enabled = false; return; }

        if (autoFit) FitToBody();
        BuildWheelColliders();
        BuildWheelVisuals();

        // physics tuning on the rigid body
        body.mass = 1350f;
        body.linearDamping = 0.02f;
        body.angularDamping = 0.6f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.centerOfMass = new Vector3(0f, centreOfMassOffset, 0f);
        body.maxAngularVelocity = 12f;

        // a car never sleeps in the middle of a world stream
        body.sleepThreshold = 0f;

        ready = wheels != null && wheels.Length == 4;
    }

    /// <summary>Derive the wheel layout from the actual mesh size of the car.</summary>
    void FitToBody()
    {
        var rends = GetComponentsInChildren<MeshRenderer>(true);
        if (rends == null || rends.Length == 0) return;

        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

        float width = Mathf.Max(0.6f, b.size.x);
        float length = Mathf.Max(1.5f, b.size.z);
        float height = Mathf.Max(0.6f, b.size.y);

        frontAxle = length * 0.32f;
        rearAxle = -length * 0.32f;
        trackWidth = Mathf.Max(0.7f, width * 0.92f);
        wheelRadius = Mathf.Clamp(height * 0.26f, 0.18f, 0.7f);
        suspensionDistance = Mathf.Clamp(height * 0.18f, 0.12f, 0.5f);
        centreOfMassOffset = -height * 0.22f;
    }

    void BuildWheelColliders()
    {
        if (wheels != null) return;

        // drop any wheel colliders a previous run created
        var old = GetComponentsInChildren<WheelCollider>(true);
        for (int i = 0; i < old.Length; i++)
            if (old[i] != null) Destroy(old[i].gameObject);

        float y = wheelRadius + suspensionDistance * 0.5f;
        Vector3[] pos =
        {
            new Vector3(-trackWidth * 0.5f, y,  frontAxle),   // FL
            new Vector3( trackWidth * 0.5f, y,  frontAxle),   // FR
            new Vector3(-trackWidth * 0.5f, y,  rearAxle),    // RL
            new Vector3( trackWidth * 0.5f, y,  rearAxle),    // RR
        };

        wheels = new WheelCollider[4];
        for (int i = 0; i < 4; i++)
        {
            var go = new GameObject("WheelCollider_" + i);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos[i];
            go.layer = gameObject.layer;

            var wc = go.AddComponent<WheelCollider>();
            wc.radius = wheelRadius;
            wc.mass = 22f;
            wc.suspensionDistance = suspensionDistance;
            wc.forceAppPointDistance = 0.12f;
            wc.wheelDampingRate = 0.6f;
            wc.suspensionSpring = new JointSpring
            {
                spring = 32000f,
                damper = 2200f,
                targetPosition = 0.5f
            };
            wc.steerAngle = 0f;
            wc.motorTorque = 0f;
            wc.brakeTorque = 0f;
            wheels[i] = wc;
        }
    }

    void BuildWheelVisuals()
    {
        if (wheelVisuals != null && wheelVisuals.Length == 4) return;
        if (!createWheelVisuals) return;
        if (wheels == null) return;

        var mats = new[] { wheelMaterial };
        for (int i = 0; i < wheels.Length; i++)
        {
            var child = new GameObject("WheelVisual_" + i);
            child.transform.SetParent(wheels[i].transform, false);
            child.transform.localPosition = Vector3.zero;

            var mf = child.AddComponent<MeshFilter>();
            var mr = child.AddComponent<MeshRenderer>();

            if (WheelMesh == null) WheelMesh = BuildCylinderMesh();
            mf.sharedMesh = WheelMesh;

            Material m = wheelMaterial;
            if (m == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit");
                if (sh == null) sh = Shader.Find("Standard");
                if (sh != null)
                {
                    m = new Material(sh) { name = "WheelRubber" };
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.08f, 0.08f, 0.09f));
                    if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.25f);
                }
            }
            if (m != null) mr.sharedMaterial = m;
            else if (mats != null && mats.Length > 0 && mats[0] != null) mr.sharedMaterial = mats[0];
        }
    }

    static Mesh WheelMesh;

    static Mesh BuildCylinderMesh()
    {
        // a very small cylinder, axis on X (so it spins like a wheel)
        var mesh = new Mesh { name = "WheelCylinder" };
        const int seg = 16;
        var verts = new Vector3[seg * 2 + 2];
        var norms = new Vector3[seg * 2 + 2];
        var uvs = new Vector2[seg * 2 + 2];
        var tris = new int[seg * 6];

        const float hw = 0.11f;   // half width
        for (int i = 0; i < seg; i++)
        {
            float a = (Mathf.PI * 2f * i) / seg;
            float y = Mathf.Cos(a), z = Mathf.Sin(a);
            verts[i * 2] = new Vector3(-hw, y, z);
            verts[i * 2 + 1] = new Vector3(hw, y, z);
            norms[i * 2] = new Vector3(-hw < 0 ? 0 : 0, y, z).normalized;
            norms[i * 2 + 1] = new Vector3(0, y, z).normalized;
            uvs[i * 2] = new Vector2(i / (float)seg, 0f);
            uvs[i * 2 + 1] = new Vector2(i / (float)seg, 1f);
        }
        verts[seg * 2] = new Vector3(-hw, 0, 0);
        verts[seg * 2 + 1] = new Vector3(hw, 0, 0);
        norms[seg * 2] = new Vector3(-1, 0, 0);
        norms[seg * 2 + 1] = new Vector3(1, 0, 0);

        for (int i = 0; i < seg; i++)
        {
            int a0 = i * 2, a1 = i * 2 + 1;
            int b0 = ((i + 1) % seg) * 2, b1 = ((i + 1) % seg) * 2 + 1;
            int t = i * 6;
            tris[t] = a0; tris[t + 1] = b0; tris[t + 2] = b1;
            tris[t + 3] = a0; tris[t + 4] = b1; tris[t + 5] = a1;
        }

        mesh.vertices = verts;
        mesh.normals = norms;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        return mesh;
    }

    // ------------------------------------------------------------------ input
    void ReadInput()
    {
        motorInput = 0f;
        steerInput = 0f;
        brakeInput = 0f;
        handbrake = false;

        // legacy input first (works in every build configuration)
        float up = 0f, down = 0f, left = 0f, right = 0f, space = 0f;
        bool any = false;

        try
        {
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) { up = 1f; any = true; }
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) { down = 1f; any = true; }
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) { left = 1f; any = true; }
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) { right = 1f; any = true; }
            if (Input.GetKey(KeyCode.Space)) { space = 1f; any = true; }
        }
        catch (System.Exception) { /* input system may be set to the new backend only */ }

        if (!any)
        {
            try
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null)
                {
                    if (kb.wKey.isPressed || kb.upArrowKey.isPressed) up = 1f;
                    if (kb.sKey.isPressed || kb.downArrowKey.isPressed) down = 1f;
                    if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) left = 1f;
                    if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) right = 1f;
                    if (kb.spaceKey.isPressed) space = 1f;
                }
            }
            catch (System.Exception) { /* no input device available */ }
        }

        motorInput = up - down;
        steerInput = right - left;
        handbrake = space > 0.5f;

        // the new input system reports the brake on the S key when going forward
        brakeInput = (motorInput < -0.01f) ? 0f : 0f;
    }

    // ------------------------------------------------------------------ loop
    void Update()
    {
        if (!ready) return;
        ReadInput();

        // steering: smooth towards the input
        float target = steerInput * maxSteerAngle;
        float rate = (Mathf.Abs(target) > Mathf.Abs(steerCurrent) ? steerSpeed : steerReturn) * Time.deltaTime;
        steerCurrent = Mathf.MoveTowards(steerCurrent, target, Mathf.Max(0.1f, rate * maxSteerAngle));

        // speed read-out
        float fwd = Vector3.Dot(body.linearVelocity, transform.forward);
        speedKmh = Mathf.Abs(fwd) * 3.6f;
    }

    void FixedUpdate()
    {
        if (!ready) return;

        // ---- steering ----
        if (wheels[0] != null) wheels[0].steerAngle = steerCurrent;
        if (wheels[1] != null) wheels[1].steerAngle = steerCurrent;

        // ---- engine ----
        float fwdSpeed = Vector3.Dot(body.linearVelocity, transform.forward);
        float limit = motorInput >= 0f ? topSpeed : reverseTopSpeed;
        float t = 1f - Mathf.Clamp01(Mathf.Abs(fwdSpeed) / Mathf.Max(1f, limit));
        float torque = motorInput * maxMotorTorque * t;

        if (handbrake)
        {
            // rear handbrake: no motor, heavy brake on the rear
            torque = 0f;
            if (wheels[2] != null) { wheels[2].motorTorque = 0f; wheels[2].brakeTorque = handbrakeTorque; }
            if (wheels[3] != null) { wheels[3].motorTorque = 0f; wheels[3].brakeTorque = handbrakeTorque; }
        }
        else if (allWheelDrive)
        {
            for (int i = 0; i < 4; i++)
                if (wheels[i] != null) { wheels[i].motorTorque = torque * 0.25f; wheels[i].brakeTorque = 0f; }
        }
        else
        {
            if (wheels[2] != null) { wheels[2].motorTorque = torque; wheels[2].brakeTorque = 0f; }
            if (wheels[3] != null) { wheels[3].motorTorque = torque; wheels[3].brakeTorque = 0f; }
            if (wheels[0] != null) { wheels[0].motorTorque = 0f; wheels[0].brakeTorque = 0f; }
            if (wheels[1] != null) { wheels[1].motorTorque = 0f; wheels[1].brakeTorque = 0f; }
        }

        // ---- auto brake when coasting backwards ----
        if (motorInput > 0.01f && fwdSpeed < -0.6f)
        {
            float b = maxBrakeTorque;
            for (int i = 0; i < 4; i++) if (wheels[i] != null) wheels[i].brakeTorque = b;
        }

        // ---- aero downforce ----
        if (downforce > 0f)
        {
            float v = body.linearVelocity.magnitude;
            body.AddForce(-transform.up * (downforce * v * v * 0.01f), ForceMode.Force);
        }

        // ---- anti roll ----
        ApplyAntiRoll(0, 1, antiRoll);
        ApplyAntiRoll(2, 3, antiRoll);

        // ---- keep upright ----
        if (upright && body.angularVelocity.sqrMagnitude > 0.001f)
        {
            Vector3 right = transform.right;
            Vector3 up = transform.up;
            float f = Vector3.Dot(right, Physics.gravity);
            if (Mathf.Abs(f) < 0.05f) return;
            Vector3 correction = (up - right * f) * body.mass * 0.6f;
            body.AddForceAtPosition(correction, transform.position, ForceMode.Force);
        }

        AnimateWheels();
    }

    void ApplyAntiRoll(int a, int b, float force)
    {
        if (wheels[a] == null || wheels[b] == null) return;
        float travelA = GetTravel(a);
        float travelB = GetTravel(b);
        float anti = force * (travelA - travelB);
        if (Mathf.Abs(anti) < 0.01f) return;

        Vector3 at = wheels[a].transform.position;
        Vector3 bt = wheels[b].transform.position;
        body.AddForceAtPosition(transform.up * anti, at, ForceMode.Force);
        body.AddForceAtPosition(-transform.up * anti, bt, ForceMode.Force);
    }

    /// <summary>Standard anti-roll travel value: ~1 on flat ground, drops when compressed.</summary>
    float GetTravel(int index)
    {
        var wc = wheels[index];
        if (wc == null) return 0f;
        WheelHit hit;
        if (!wc.GetGroundHit(out hit)) return 0f;
        return -Vector3.Dot(wc.transform.up, hit.normal);
    }

    // ------------------------------------------------------------------ wheels visual
    void AnimateWheels()
    {
        if (wheels == null) return;
        for (int i = 0; i < wheels.Length; i++)
        {
            if (wheels[i] == null) continue;

            Vector3 pos;
            Quaternion rot;
            wheels[i].GetWorldPose(out pos, out rot);

            Transform visual = null;
            if (wheelVisuals != null && i < wheelVisuals.Length) visual = wheelVisuals[i];
            if (visual == null)
            {
                var child = wheels[i].transform.Find("WheelVisual_" + i);
                if (child != null) visual = child;
            }
            if (visual == null) continue;

            visual.position = pos;
            visual.rotation = rot;
        }
    }

    // ------------------------------------------------------------------ helpers
    public void SetInputs(float motor, float steer, float brake)
    {
        motorInput = Mathf.Clamp(motor, -1f, 1f);
        steerInput = Mathf.Clamp(steer, -1f, 1f);
        brakeInput = Mathf.Clamp(brake, 0f, 1f);
    }

    public bool IsReady { get { return ready; } }
}
