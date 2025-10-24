using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; // New Input System

public class HoverPickAndStore : MonoBehaviour
{
    [Header("Pick settings (2D only)")]
    [Tooltip("Required component type name. Empty = no filter.")]
    public string requiredTypeName = "";

    [Tooltip("LayerMask used for 2D ray intersection")]
    public LayerMask layerMask = ~0;

    [Header("Store settings")]
    [Range(1, 20)]
    public int capacity = 3;
    public IReadOnlyCollection<GameObject> SavedObjects => _queue;

    private readonly Queue<GameObject> _queue = new Queue<GameObject>();
    private readonly HashSet<int> _ids = new HashSet<int>();

    private const int CapacityStart = 3;
    private const int CapacityMax = 10;
    private const float SecondsPerIncrease = 10f;
    [SerializeField] private GameDirector gameDirector;

    const float EPS = 1e-7f;

    private readonly List<Vector2> _pts = new();

    void Update()
    {
        UpdateDynamicCapacity();
        if (Mouse.current == null || Camera.main == null) return;

        CleanupDead();

        var screen = Mouse.current.position.ReadValue();
        var ray = Camera.main.ScreenPointToRay(screen);

        GameObject hitObj = null;

        // 2D-only: Ray intersection against 2D colliders
        var hit = Physics2D.GetRayIntersection(ray, Mathf.Infinity, layerMask);
        if (hit.collider != null) hitObj = hit.collider.gameObject;

        if (hitObj == null) return;

        if (!string.IsNullOrEmpty(requiredTypeName) && !HasType(hitObj, requiredTypeName))
            return;

        int id = hitObj.GetInstanceID();
        if (_ids.Contains(id)) return;

        Vector3 pos = hitObj.transform.position;
        if (!CanPlaceNextPoint(new Vector2(pos.x, pos.y))) return;

        if (_queue.Count >= capacity)
        {
            var old = _queue.Dequeue();
            if (old != null) _ids.Remove(old.GetInstanceID());
        }

        _queue.Enqueue(hitObj);
        _ids.Add(id);
        LogSavedDetails();
    }

    private void UpdateDynamicCapacity()
    {
        if (gameDirector == null)
            gameDirector = FindFirstObjectByType<GameDirector>();

        int target = CapacityStart;
        if (gameDirector != null)
        {
            float elapsed = gameDirector.ElapsedTime;
            int inc = Mathf.FloorToInt(elapsed / SecondsPerIncrease);
            target = Mathf.Clamp(CapacityStart + inc, CapacityStart, CapacityMax);
        }

        capacity = target;
    }

    private void CleanupDead()
    {
        if (_queue.Count == 0) return;

        int n = _queue.Count;
        bool removed = false;

        for (int i = 0; i < n; i++)
        {
            var go = _queue.Dequeue();
            if (go == null)
            {
                removed = true;
                continue;
            }
            _queue.Enqueue(go);
        }

        if (removed)
        {
            _ids.Clear();
            foreach (var go in _queue)
                if (go != null) _ids.Add(go.GetInstanceID());
            Debug.Log("Removed destroyed objects from saved list.");
            LogSavedDetails();
        }
    }

    private bool HasType(GameObject go, string typeName)
    {
        var c = go.GetComponent(typeName);
        if (c != null) return true;
        return false;
    }

    [ContextMenu("Log Saved Details")]
    public void LogSavedDetails()
    {
        var arr = _queue.ToArray();
        for (int i = 0; i < arr.Length; i++)
        {
            var go = arr[i];
            if (go == null)
            {
                Debug.Log($"[{i}] (null) destroyed");
                continue;
            }

            string info = $"[{i}] Name: {go.name}, " +
                        $"Tag: {go.tag}, " +
                        $"Layer: {LayerMask.LayerToName(go.layer)}, " +
                        $"Position: {go.transform.position}";
            Debug.Log($"{info}");
        }
    }

    private bool CanPlaceNextPoint(Vector2 q)
    {
        FillPointsFromQueue();
        if (_pts.Count < 2) return true;

        Vector2 pn = _pts[_pts.Count - 1];

        for (int i = 0; i < _pts.Count - 2; i++)
        {
            Vector2 a = _pts[i];
            Vector2 b = _pts[i + 1];
            if (SegmentsIntersect(a, b, pn, q))
                return false;
        }
        return true;
    }

    private void FillPointsFromQueue()
    {
        _pts.Clear();

        foreach (var go in _queue)
        {
            if (_pts.Count >= capacity) break;

            if (go != null)
            {
                Vector3 pos = go.transform.position;
                _pts.Add(new Vector2(pos.x, pos.y));
            }
        }
    }

    private static bool SegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
    {
        float d1 = Cross(p4 - p3, p1 - p3);
        float d2 = Cross(p4 - p3, p2 - p3);
        float d3 = Cross(p2 - p1, p3 - p1);
        float d4 = Cross(p2 - p1, p4 - p1);

        if (((d1 > EPS && d2 < -EPS) || (d1 < -EPS && d2 > EPS)) &&
            ((d3 > EPS && d4 < -EPS) || (d3 < -EPS && d4 > EPS)))
            return true;

        if (Mathf.Abs(d1) <= EPS && OnSegment(p3, p4, p1)) return true;
        if (Mathf.Abs(d2) <= EPS && OnSegment(p3, p4, p2)) return true;
        if (Mathf.Abs(d3) <= EPS && OnSegment(p1, p2, p3)) return true;
        if (Mathf.Abs(d4) <= EPS && OnSegment(p1, p2, p4)) return true;

        return false;
    }

    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    private static bool OnSegment(Vector2 a, Vector2 b, Vector2 p)
    {
        return p.x >= Mathf.Min(a.x, b.x) - EPS && p.x <= Mathf.Max(a.x, b.x) + EPS &&
               p.y >= Mathf.Min(a.y, b.y) - EPS && p.y <= Mathf.Max(a.y, b.y) + EPS;
    }
}
