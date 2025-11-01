using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GameObject を順序付きで保存するシンプルなストア。
/// ・重複チェック（InstanceID）
/// ・Destroy 済みの掃除
/// ・容量オーバー時の先頭エビクト
/// </summary>
public sealed class SavedObjectStore
{
    private readonly Queue<GameObject> _queue = new();
    private readonly HashSet<int> _ids = new();

    public IReadOnlyCollection<GameObject> Items => _queue;

    public bool Contains(GameObject go)
    {
        if (!go) return false;
        return _ids.Contains(go.GetInstanceID());
    }

    /// <summary>
    /// Destroy 済み要素を除去し、ID セットを再構築。
    /// </summary>
    public void CleanupDestroyed(Action onRemoved = null)
    {
        if (_queue.Count == 0) return;

        int n = _queue.Count;
        bool removed = false;
        for (int i = 0; i < n; i++)
        {
            var go = _queue.Dequeue();
            if (go) _queue.Enqueue(go); else removed = true;
        }

        if (removed)
        {
            _ids.Clear();
            foreach (var go in _queue)
                if (go) _ids.Add(go.GetInstanceID());
            onRemoved?.Invoke();
        }
    }

    /// <summary>
    /// 容量制限に従って古いものを退けてから末尾に追加。
    /// 既に含む場合は何もしない。
    /// </summary>
    public bool EnqueueWithEviction(GameObject go, int capacity)
    {
        if (!go) return false;
        int id = go.GetInstanceID();
        if (_ids.Contains(id)) return false;

        if (_queue.Count >= capacity)
        {
            var old = _queue.Dequeue();
            if (old) _ids.Remove(old.GetInstanceID());
        }

        _queue.Enqueue(go);
        _ids.Add(id);
        return true;
    }

    /// <summary>
    /// 現在の保存順から、最大 <paramref name="maxCount"/> まで 2D 点列を構築。
    /// </summary>
    public void RebuildRecentPoints(List<Vector2> dst, int maxCount)
    {
        if (dst == null) return;
        dst.Clear();
        foreach (var go in _queue)
        {
            if (dst.Count >= maxCount) break;
            if (!go) continue;
            Vector3 pos = go.transform.position;
            dst.Add(new Vector2(pos.x, pos.y));
        }
    }

    public void Clear()
    {
        _queue.Clear();
        _ids.Clear();
    }
}

