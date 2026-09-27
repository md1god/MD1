using UnityEngine;

// كاميرا تتبع بسيطة - حطها على Main Camera واسحب اللاعب في خانة target
public class FollowCam : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0, 3f, -6f);
    public float speed = 5f;

    void LateUpdate()
    {
        if (!target) return;
        Vector3 want = target.position + target.rotation * offset;
        transform.position = Vector3.Lerp(transform.position, want, speed * Time.deltaTime);
        transform.LookAt(target.position + Vector3.up * 1.2f);
    }
}
