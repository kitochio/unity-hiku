using System;
using UnityEngine;
using UnityEngine.Events;
using TMPro;
using System.Collections;

/// <summary>
/// ゲーム全体の状態管理とUI更新を行うディレクター。
/// ・タイマー/ステータス表示の更新
/// ・開始/一時停止/終了の状態遷移
/// ・保存オブジェクトの強調表示や削除検出
/// </summary>
public class GameDirector : MonoBehaviour
{
    /// <summary>ゲームの進行状態</summary>
    public enum GameState { Ready, Playing, Paused, GameOver }

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private GameObject hudPanel;
    [SerializeField] private GameObject gameOverPanel;

    [Header("Gameplay")]
    [SerializeField] private bool useTimer = false;
    [SerializeField, Min(0f)] private float totalTime = 60f;

    [Header("Events (Inspector Hook)")]
    [SerializeField] private UnityEvent onGameStarted;
    [SerializeField] private UnityEvent onGameEnded;

    [Header("References")]
    [SerializeField] private HoverPickAndStore picker;

    private Coroutine _statusRoutine;

    public GameState State { get; private set; } = GameState.Paused;
    public int Score { get; private set; }
    public float ElapsedTime { get; private set; }
    public float RemainingTime => useTimer ? Mathf.Max(0f, totalTime - ElapsedTime) : 0f;

    private readonly System.Collections.Generic.List<int> _savedOrderIds = new System.Collections.Generic.List<int>();
    private const float EmissionIntensity = 1.7f;
    private static readonly Color ColorLast = Color.red * EmissionIntensity;
    private static readonly Color ColorOther = Color.green * EmissionIntensity;
    private readonly System.Collections.Generic.List<GameObject> _prevSavedObjects = new System.Collections.Generic.List<GameObject>();
    private readonly System.Collections.Generic.Dictionary<int, Color> _originalEmissionById = new System.Collections.Generic.Dictionary<int, Color>();

    /// <summary>
    /// 起動時にUI表示状態を反映し、初期表示を更新します。
    /// </summary>
    private void Start()
    {
        ApplyUIState();
        UpdateUI();
    }

    /// <summary>
    /// 毎フレーム、保存オブジェクトの見た目を監視/更新し、
    /// Playing 中であれば経過時間とUIを更新します（タイマー終了時はゲーム終了）。
    /// </summary>
    private void Update()
    {
        UpdateSavedObjectColorsIfChanged();

        if (State != GameState.Playing) return;

        ElapsedTime += Time.deltaTime;

        if (useTimer && RemainingTime <= 0f)
        {
            EndGame();
            return;
        }

        UpdateUI();
    }

    /// <summary>
    /// ゲームを GameOver 状態へ遷移させ、UIとステータス表示を更新します。
    /// </summary>
    public void EndGame()
    {
        if (State != GameState.Playing && State != GameState.Paused) return;

        State = GameState.GameOver;

        ApplyUIState();
        UpdateUI();
        SetStatusText("Game Over");
    }

    /// <summary>
    /// 一時停止に遷移します。経過時間を 0 にリセットし、UIを更新します。
    /// </summary>
    public void PauseGame()
    {
        if (State == GameState.Paused) return;

        ElapsedTime = 0f;
        State = GameState.Paused;
        SetStatusText(string.Empty);
        ApplyUIState();
        UpdateUI();
    }

    /// <summary>
    /// 一時停止から再開し、開始メッセージを表示します。
    /// </summary>
    public void ResumeGame()
    {
        if (State != GameState.Paused) return;

        State = GameState.Playing;
        ShowGameStartMessage();
        ApplyUIState();
    }

    /// <summary>
    /// タイマーなどのUI表示を更新します。
    /// </summary>
    private void UpdateUI()
    {
        if (timerText != null)
        {
            if (useTimer)
                timerText.text = $"Time: {Mathf.CeilToInt(RemainingTime)}";
            else
                timerText.text = $"Time: {ElapsedTime:0.0}s";
        }
    }

    /// <summary>
    /// 現在の <see cref="State"/> に応じて HUD とゲームオーバーパネルの表示を切り替えます。
    /// </summary>
    private void ApplyUIState()
    {
        if (hudPanel != null)
            hudPanel.SetActive(State == GameState.Playing || State == GameState.Paused);

        if (gameOverPanel != null)
            gameOverPanel.SetActive(State == GameState.GameOver);
    }

    /// <summary>
    /// ステータス用テキストにメッセージを即時表示します（フェードなし）。
    /// </summary>
    /// <param name="msg">表示するメッセージ</param>
    private void SetStatusText(string msg)
    {
        if (statusText == null) return;

        if (_statusRoutine != null)
        {
            StopCoroutine(_statusRoutine);
            _statusRoutine = null;
        }

        var c = statusText.color;
        c.a = 1f;
        statusText.color = c;
        statusText.text = msg;
    }

    /// <summary>
    /// 「GameStart!」の短いメッセージを表示するコルーチンを開始します。
    /// </summary>
    private void ShowGameStartMessage()
    {
        if (statusText == null) return;

        if (_statusRoutine != null)
        {
            StopCoroutine(_statusRoutine);
            _statusRoutine = null;
        }

        _statusRoutine = StartCoroutine(ShowStatusMessageCoroutine("GameStart!", 0.3f, 0.5f));
    }

    /// <summary>
    /// 任意メッセージを一定時間保持してからフェードアウトします。
    /// </summary>
    /// <param name="message">表示する文言</param>
    /// <param name="holdSeconds">保持秒数（0なら即フェード）</param>
    /// <param name="fadeSeconds">フェード時間（0なら即消去）</param>
    private IEnumerator ShowStatusMessageCoroutine(string message, float holdSeconds, float fadeSeconds)
    {
        // Initialize text and full alpha
        statusText.text = message;
        var c = statusText.color;
        c.a = 1f;
        statusText.color = c;

        // Hold
        if (holdSeconds > 0f)
            yield return new WaitForSeconds(holdSeconds);

        // Fade out
        float t = 0f;
        while (t < fadeSeconds)
        {
            t += Time.deltaTime;
            float a = fadeSeconds > 0f ? Mathf.Lerp(1f, 0f, t / fadeSeconds) : 0f;
            var cc = statusText.color;
            cc.a = a;
            statusText.color = cc;
            yield return null;
        }

        // Clear and restore alpha
        statusText.text = string.Empty;
        c = statusText.color;
        c.a = 1f;
        statusText.color = c;
        _statusRoutine = null;
    }

    // SavedObjects の並び変化に応じて UI/色/エフェクトを更新
    /// <summary>
    /// 保存オブジェクト列に変化があれば、
    /// ・最後のオブジェクトを赤、その他を緑の Emission に変更
    /// ・最後のオブジェクトに限り破壊時パーティクルを ON
    /// また、Playing 中に最後の要素が外れた場合はゲーム終了、
    /// Paused 中に新規追加された場合は再開します。
    /// </summary>
    private void UpdateSavedObjectColorsIfChanged()
    {
        if (picker == null)
            picker = FindFirstObjectByType<HoverPickAndStore>();
        if (picker == null) return;

        var src = picker.SavedObjects;
        if (src == null) return;

        var objs = new System.Collections.Generic.List<GameObject>();
        var ids = new System.Collections.Generic.List<int>();
        foreach (var go in src)
        {
            if (go == null) continue;
            objs.Add(go);
            ids.Add(go.GetInstanceID());
        }

        // 並びが前回と同じなら何もしない（軽量化）
        if (ids.Count == _savedOrderIds.Count)
        {
            bool same = true;
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] != _savedOrderIds[i]) { same = false; break; }
            }
            if (same) return;
        }

        // Playing 中に前回の最後の要素が消えたらゲーム終了
        if (State == GameState.Playing && _prevSavedObjects.Count > 0)
        {
            var prevLast = _prevSavedObjects[_prevSavedObjects.Count - 1];
            bool lastRemoved = false;
            if (prevLast == null)
            {
                // 最後の要素が Destroy された
                lastRemoved = true;
            }
            else
            {
                int prevLastId = prevLast.GetInstanceID();
                if (!ids.Contains(prevLastId))
                    lastRemoved = true;
            }

            if (lastRemoved)
            {
                EndGame();
            }
        }

        // Paused 中に新規追加があれば再開
        bool addedNewObject = false;
        for (int i = 0; i < ids.Count; i++)
        {
            if (!_savedOrderIds.Contains(ids[i])) { addedNewObject = true; break; }
        }
        if (addedNewObject && State == GameState.Paused)
        {
            ResumeGame();
        }

        _savedOrderIds.Clear();
        _savedOrderIds.AddRange(ids);

        // 前回リストから外れたオブジェクトは元の Emission 色へ戻す
        if (_prevSavedObjects.Count > 0)
        {
            for (int i = 0; i < _prevSavedObjects.Count; i++)
            {
                var prev = _prevSavedObjects[i];
                if (prev == null) continue;
                int pid = prev.GetInstanceID();
                if (!ids.Contains(pid))
                {
                    if (_originalEmissionById.TryGetValue(pid, out var orig))
                    {
                        var mop = prev.GetComponent<MaterialOperations>();
                        if (mop != null)
                        {
                            mop.SetEmissionColor(orig);
                        }
                        _originalEmissionById.Remove(pid);
                    }
                    // Removed from list: stop child ParticleSystems
                    var prevParticles = prev.GetComponentsInChildren<ParticleSystem>(true);
                    for (int p = 0; p < prevParticles.Length; p++)
                    {
                        var ps = prevParticles[p];
                        if (ps == null) continue;
                        if (ps.isPlaying)
                        {
                            ps.Stop(true);
                            ps.Clear(true);
                        }
                    }
                }
            }
        }

        // Build previous id set to detect newly added objects
        var prevIds = new System.Collections.Generic.HashSet<int>();
        for (int i = 0; i < _prevSavedObjects.Count; i++)
        {
            var pgo = _prevSavedObjects[i];
            if (pgo == null) continue;
            prevIds.Add(pgo.GetInstanceID());
        }

        for (int i = 0; i < objs.Count; i++)
        {
            var go = objs[i];
            var isLast = i == objs.Count - 1;

            var m = go != null ? go.GetComponent<MaterialOperations>() : null;

            // 初回だけ現在の Emission をキャッシュ
            int id = go.GetInstanceID();
            if (!_originalEmissionById.ContainsKey(id))
            {
                if (TryGetCurrentEmissionColor(go, out var current))
                {
                    _originalEmissionById[id] = current;
                }
            }

            if (m != null)
            {
                m.SetEmissionColor(isLast ? ColorLast : ColorOther);
            }

            // Play particles only when newly added to the list
            bool isNewlyAdded = !prevIds.Contains(id);
            if (isNewlyAdded)
            {
                var particles = go != null ? go.GetComponentsInChildren<ParticleSystem>(true) : null;
                if (particles != null)
                {
                    for (int p = 0; p < particles.Length; p++)
                    {
                        var ps = particles[p];
                        if (ps == null) continue;
                        if (!ps.isPlaying) ps.Play();
                    }
                }
            }

            // 破棄時パーティクル: 最後の要素のみ ON、それ以外は OFF
            var pod = go != null ? go.GetComponent<PlayParticleOnDestroy>() : null;
            if (pod != null)
            {
                pod.SetPlayOnDestroy(isLast);
            }
        }

        // 次回比較用に保存
        _prevSavedObjects.Clear();
        _prevSavedObjects.AddRange(objs);
    }

    /// <summary>
    /// 対象の SpriteRenderer が Particles/Standard Unlit を使用し、
    /// _EmissionColor を持っている場合に現在の Emission 色を取得します。
    /// </summary>
    /// <param name="go">対象の GameObject</param>
    /// <param name="color">取得した色（成功時）</param>
    /// <returns>取得できた場合 true</returns>
    private static bool TryGetCurrentEmissionColor(GameObject go, out Color color)
        => EmissionColorUtil.TryGetCurrent(go, out color);
}
