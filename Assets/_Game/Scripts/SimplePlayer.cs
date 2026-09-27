using UnityEngine;
using UnityEngine.InputSystem;

// SimplePlayer: WASD/Arrows movement + Space jump.
// WebGL-compatible. Attach to Capsule with CharacterController, Tag = "Player".
[RequireComponent(typeof(CharacterController))]
public class SimplePlayer : MonoBehaviour
{
    public float speed = 6f;
    public float sprintMultiplier = 1.8f;
    public float jump = 5f;
    public float gravity = -14f;
    public float rotationSpeed = 12f;

    float vy;
    CharacterController cc;
    InputAction moveAction, jumpAction, sprintAction;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        var map = new InputActionMap("Player");
        moveAction = map.AddAction("Move", binding: "<Gamepad>/leftStick");
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w").With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/s").With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/a").With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/d").With("Right", "<Keyboard>/rightArrow");
        jumpAction = map.AddAction("Jump", binding: "<Keyboard>/space");
        sprintAction = map.AddAction("Sprint", binding: "<Keyboard>/leftShift");
        map.Enable();
    }

    void OnDestroy()
    {
        moveAction?.Dispose();
        jumpAction?.Dispose();
        sprintAction?.Dispose();
    }

    void Update()
    {
        if (moveAction == null) return;

        Vector2 input = moveAction.ReadValue<Vector2>();
        bool sprint = sprintAction.IsPressed();
        bool jumpInput = jumpAction.WasPressedThisFrame();

        var cam = Camera.main;
        Vector3 fwd = cam ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : Vector3.forward;
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;

        Vector3 move = (fwd * input.y + right * input.x);
        if (move.magnitude > 1f) move.Normalize();

        float currentSpeed = speed * (sprint ? sprintMultiplier : 1f);
        move *= currentSpeed;

        if (cc.isGrounded)
        {
            vy = -1f;
            if (jumpInput) vy = jump;
        }
        else vy += gravity * Time.deltaTime;

        move.y = vy;
        cc.Move(move * Time.deltaTime);

        if (input.sqrMagnitude > 0.01f)
        {
            var targetRot = Quaternion.LookRotation(new Vector3(move.x, 0, move.z));
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }
    }
}
