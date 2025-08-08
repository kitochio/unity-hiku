using UnityEngine;
using UnityEngine.InputSystem;  // ★ 新Input System用

public class MouseFollowParticle : MonoBehaviour
{
    Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
    }

    void Update()
    {
        if (Mouse.current == null) return;

        Vector2 mousePosition = Mouse.current.position.ReadValue();
        Vector3 screenPosition = new Vector3(mousePosition.x, mousePosition.y, 10f); // zはカメラからの距離
        Vector3 worldPosition = mainCamera.ScreenToWorldPoint(screenPosition);

        transform.position = worldPosition;
    }
}
