using UnityEngine;

public class MouseHandMover : MonoBehaviour
{
    public float depth = 0.3f;  // how far from camera in meters

    void Update()
    {
        Vector3 mouse = Input.mousePosition;
        mouse.z = depth;
        Vector3 world = Camera.main.ScreenToWorldPoint(mouse);
        transform.position = world;
    }
}
