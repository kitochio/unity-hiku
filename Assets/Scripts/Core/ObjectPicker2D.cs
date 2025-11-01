using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 2D 用のシンプルなオブジェクトピッカー（マウス位置から Raycast）。
/// </summary>
public static class ObjectPicker2D
{
    public static GameObject GetUnderMouse2D(Camera cam, Vector2 mouseScreen, LayerMask layerMask)
    {
        if (!cam) return null;
        var ray = cam.ScreenPointToRay(mouseScreen);
        var hit = Physics2D.GetRayIntersection(ray, Mathf.Infinity, layerMask);
        return hit.collider ? hit.collider.gameObject : null;
    }
}

