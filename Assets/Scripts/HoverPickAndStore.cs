using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; // New Input System

// 2D 専用：マウス下のオブジェクトを順番に保存し、
// 保存順の自己交差（線が交わる）を避ける簡単な制御を行う。
/// <summary>
/// 2D 専用：マウス下のオブジェクトを順序付きで保存するキュー。
/// ・型名フィルタによる保存可否判定
/// ・同一点列の自己交差を避ける簡易チェック
/// ・経過時間に応じた動的 capacity 調整 と 追加時のSE 再生
/// </summary>
public class HoverPickAndStore : MonoBehaviour
{
    [Header("Pick settings (2D only)")]
    [Tooltip("必要なコンポーネントの型名（空ならフィルタなし）")]
    public string requiredTypeName = "";

    [Tooltip("2D レイ判定に使用する LayerMask")]
    public LayerMask layerMask = ~0;

    [Header("Store settings")]
    [Range(1, 20)] public int capacity = 3;
    public IReadOnlyCollection<GameObject> SavedObjects => _store.Items;

    [Header("Audio")]
    [Tooltip("キューに追加成功時に鳴らす音")]
    [SerializeField] private AudioClip enqueueSfx;
    [Range(0f, 1f)] [SerializeField] private float enqueueSfxVolume = 1f;
    [Tooltip("再生に使う AudioSource（未指定なら自身から取得）")]
    [SerializeField] private AudioSource sfxSource;

    // 内部状態
    private readonly SavedObjectStore _store = new();
    private PlacementValidator2D _validator;

    // GameDirector の経過時間に応じて capacity を増やす
    private const int CapacityStart = 3;
    private const int CapacityMax = 10;
    private const float SecondsPerIncrease = 10f;
    [SerializeField] private GameDirector gameDirector;

    // 幾何用の定数
    

    // 交差判定用バッファ（最新順で capacity 件まで）
    
    void Awake()
    {
        _validator = new PlacementValidator2D(_store);
    }

    /// <summary>
    /// 毎フレーム、破棄済み要素のクリーンアップ、マウス下オブジェクトの取得、
    /// 型フィルタ判定、保存キューへの追加を行います。
    /// </summary>
    void Update()
    {
        UpdateDynamicCapacity();
        if (Mouse.current == null || Camera.main == null) return;

        // 破棄済み要素の掃除
        CleanupDead();

        // マウス下の 2D オブジェクトを取得
        var hitObj = GetObjectUnderMouse2D();
        if (!hitObj) return;

        // 型フィルタに適合するか
        if (!PassesTypeFilter(hitObj)) return;

        // 保存キューに追加を試みる
        TryEnqueue(hitObj);
    }

    // マウス位置から 2D コライダーを取得
    /// <summary>
    /// マウス位置から Ray を飛ばして 2D の衝突対象を取得します。
    /// </summary>
    GameObject GetObjectUnderMouse2D()
    {
        var screen = Mouse.current.position.ReadValue();
        return ObjectPicker2D.GetUnderMouse2D(Camera.main, screen, layerMask);
    }

    // 型フィルタに一致するか
    /// <summary>
    /// 型名フィルタに通過するかを判定します。
    /// </summary>
    bool PassesTypeFilter(GameObject go)
    {
        if (string.IsNullOrEmpty(requiredTypeName)) return true;
        return HasType(go, requiredTypeName);
    }

    // キューへの追加を試みる（重複・交差・満杯処理を含む）
    /// <summary>
    /// 保存キューへ追加を試みます（重複・自己交差を回避、満杯時は先頭を退避）。
    /// 追加に成功した場合はSEを再生します。
    /// </summary>
    /// <returns>追加できた場合 true</returns>
    bool TryEnqueue(GameObject go)
    {
        if (!go) return false;
        if (_store.Contains(go)) return false;

        var pos = go.transform.position;
        if (!CanPlaceNextPoint(new Vector2(pos.x, pos.y))) return false;

        _store.EnqueueWithEviction(go, capacity);
        LogSavedDetails();
        PlayEnqueueSfx();
        return true;
    }

    // 満杯なら最古を追い出す
    /// <summary>
    /// capacity を超える場合に最古の要素を取り除きます。
    /// </summary>
    void EvictIfFull() { /* handled by SavedObjectStore */ }

    // 動的に capacity を更新（GameDirector の経過時間に応じて）
    /// <summary>
    /// GameDirector の経過時間に応じて capacity を段階的に増やします。
    /// </summary>
    void UpdateDynamicCapacity()
    {
        if (gameDirector == null)
            gameDirector = FindFirstObjectByType<GameDirector>();
        capacity = CapacityPolicy.Compute(gameDirector, CapacityStart, CapacityMax, SecondsPerIncrease);
    }

    // 破棄済み（Destroy 済み）オブジェクトをキューから取り除く
    /// <summary>
    /// 破棄済み（Destroy済み）の参照をキューから取り除き、IDセットを再構築します。
    /// </summary>
    void CleanupDead()
    {
        _store.CleanupDestroyed(() =>
        {
            Debug.Log("Removed destroyed objects from saved list.");
            LogSavedDetails();
        });
    }

    // GameObject が指定された型名のコンポーネントを持つか
    /// <summary>
    /// 指定の型名のコンポーネントを保持しているかを判定します。
    /// </summary>
    bool HasType(GameObject go, string typeName)
    {
        var c = go.GetComponent(typeName);
        return c != null;
    }

    [ContextMenu("Log Saved Details")]
    /// <summary>
    /// 現在の保存キューの内容をデバッグログに出力します。
    /// </summary>
    public void LogSavedDetails()
    {
        var list = new List<GameObject>(_store.Items);
        for (int i = 0; i < list.Count; i++)
        {
            var go = list[i];
            if (!go)
            {
                Debug.Log($"[{i}] (null) destroyed");
                continue;
            }

            string info = $"[{i}] Name: {go.name}, " +
                          $"Tag: {go.tag}, " +
                          $"Layer: {LayerMask.LayerToName(go.layer)}, " +
                          $"Position: {go.transform.position}";
            Debug.Log(info);
        }
    }

    // 次の点 q を追加しても、既存の折れ線と自己交差しないかを判定
    /// <summary>
    /// 新しい点 q を追加したときに、直近の点列と自己交差しないかを判定します。
    /// </summary>
    bool CanPlaceNextPoint(Vector2 q)
    {
        return _validator != null ? _validator.CanPlaceNext(q, capacity) : true;
    }

    // 現在のキューから最大 capacity 件までの 2D 点列を作成（先頭が古い順）
    /// <summary>
    /// 現在のキューから最大 capacity までの2D点列を作成します（先頭が最古）。
    /// </summary>
    void RebuildRecentPoints() { /* moved to SavedObjectStore/PlacementValidator2D */ }

    // 2D 線分同士の交差判定（厳密めの境界含む） -------------------------
    /// <summary>
    /// 2D 線分 p1-p2 と p3-p4 が交差するかを判定します（端点含む）。
    /// </summary>
    static bool SegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
        => Geometry2D.SegmentsIntersect(p1, p2, p3, p4);

    /// <summary>2D 外積の z 成分（スカラー）</summary>
    static float Cross(Vector2 a, Vector2 b) => Geometry2D.Cross(a, b);

    /// <summary>
    /// 点 p が線分 a-b 上（端点を含む）にあるかを判定します。
    /// </summary>
    static bool OnSegment(Vector2 a, Vector2 b, Vector2 p) => Geometry2D.OnSegment(a, b, p);

    // Public wrapper for external preview checks
    /// <summary>
    /// プレビュー用の公開ラッパー。<see cref="CanPlaceNextPoint(UnityEngine.Vector2)"/> を呼び出します。
    /// </summary>
    public bool CanPlaceNextPointForPreview(Vector2 q) => CanPlaceNextPoint(q);

    /// <summary>
    /// Playing 中に保存が成功した際のSEを再生します。
    /// </summary>
    void PlayEnqueueSfx()
    {
        var gd = gameDirector != null ? gameDirector : FindFirstObjectByType<GameDirector>();
        var src = sfxSource != null ? sfxSource : GetComponent<AudioSource>();
        SfxOneShotGate.PlayIfAllowed(gd, src, enqueueSfx, enqueueSfxVolume);
    }
}
