using UnityEngine;
using UnityEngine.InputSystem;

// حركة شخصية بسيطة - WASD + مسافة للقفز
// التركيب: Capsule + CharacterController + هذا السكربت + Tag = Player
[RequireComponent(typeof(CharacterController))]
public class SimplePlayer : MonoBehaviour
{
    public float speed = 5f;
    public float jump = 4f;
    float vy;
    CharacterController cc;

    void Awake() { cc = GetComponent<CharacterController>(); }

    void Update()
    {
        if (Keyboard.current == null) return;
        var kb = Keyboard.current;

        float h = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1 : 0)
                - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1 : 0);
        float v = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1 : 0)
                - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1 : 0);

        // حركة نسبية للكاميرا
        var cam = Camera.main;
        Vector3 fwd = cam ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : Vector3.forward;
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;

        Vector3 move = (fwd * v + right * h);
        if (move.magnitude > 1f) move.Normalize();
        move *= speed;

        if (cc.isGrounded)
        {
            vy = -1f;
            if (kb.spaceKey.wasPressedThisFrame) vy = jump;
        }
        else vy += -12f * Time.deltaTime;

        move.y = vy;
        cc.Move(move * Time.deltaTime);

        if (h != 0 || v != 0)
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(new Vector3(move.x, 0, move.z)), 10f * Time.deltaTime);
    }
}
