using UnityEngine;
using UnityEngine.InputSystem;  // New Input System

// マウス位置に追従するシンプルな 2D 用フォロワー
public class MouseFollowParticle : MonoBehaviour
{
    [Header("Follow Settings")]
    [Tooltip("ScreenToWorldPoint に渡す Z（カメラからの距離）")]
    [SerializeField] private float depthFromCamera = 10f;

    private Camera _mainCamera;

    void Start()
    {
        _mainCamera = Camera.main;
    }

    void Update()
    {
        if (Mouse.current == null) return;

        // カメラ参照が切れていたら取り直す
        if (_mainCamera == null) _mainCamera = Camera.main;
        if (_mainCamera == null) return;

        // 画面座標→ワールド座標へ変換して追従
        Vector2 mouse = Mouse.current.position.ReadValue();
        Vector3 screen = new Vector3(mouse.x, mouse.y, depthFromCamera);
        Vector3 world = _mainCamera.ScreenToWorldPoint(screen);
        transform.position = world;
    }
}
