using UnityEngine;
using UnityEngine.InputSystem;  // New Input System

// マウス位置に追従するシンプルな 2D 用フォロワー
/// <summary>
/// マウス位置に追従して移動するシンプルな 2D パーティクル。
/// </summary>
public class MouseFollowParticle : MonoBehaviour
{
    [Header("Follow Settings")]
    [Tooltip("ScreenToWorldPoint に渡す Z（カメラからの距離）")]
    [SerializeField] private float depthFromCamera = 10f;

    private Camera _mainCamera;

    /// <summary>メインカメラをキャッシュします。</summary>
    void Start()
    {
        _mainCamera = Camera.main;
    }

    /// <summary>
    /// マウス座標をワールド座標へ変換し、Transform を追従させます。
    /// </summary>
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
