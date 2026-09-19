using UnityEngine;
using UnityEngine.InputSystem;

// سيارة / حصان بسيط - نفس الكود يشتغل للاتنين
// التركيب: أي موديل + هذا السكربت + Empty اسمه Seat فوقه
// E للركوب والنزول قرب السيارة
public class SimpleCar : MonoBehaviour
{
    public float speed = 8f;
    public float turn = 90f;
    public Transform seat; // اسحب هنا الـ Seat
    public float useDist = 3f;

    Transform player;
    CharacterController playerCC;
    bool driving;

    void Start()
    {
        var p = GameObject.FindWithTag("Player");
        if (p) { player = p.transform; playerCC = p.GetComponent<CharacterController>(); }
        if (!seat) seat = transform;
    }

    void Update()
    {
        if (Keyboard.current == null || !player) return;
        var kb = Keyboard.current;

        if (kb.eKey.wasPressedThisFrame)
        {
            float d = Vector3.Distance(player.position, transform.position);
            if (!driving && d < useDist) SetDrive(true);
            else if (driving) SetDrive(false);
        }

        if (!driving) return;

        float v = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1 : 0)
                - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1 : 0);
        float h = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1 : 0)
                - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1 : 0);

        transform.Rotate(0, h * turn * Time.deltaTime, 0);
        transform.Translate(0, 0, v * speed * Time.deltaTime);

        player.position = seat.position;
    }

    void SetDrive(bool on)
    {
        driving = on;
        if (playerCC) playerCC.enabled = !on;
        if (on) player.SetParent(transform);
        else
        {
            player.SetParent(null);
            player.position = transform.position + transform.right * 2f + Vector3.up * 0.5f;
        }
    }
}
