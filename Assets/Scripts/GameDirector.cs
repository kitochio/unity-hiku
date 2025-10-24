using System;
using UnityEngine;
using UnityEngine.Events;
using TMPro;
using System.Collections;

public class GameDirector : MonoBehaviour
{
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

    private void Start()
    {
        ApplyUIState();
        UpdateUI();
    }

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

    public void EndGame()
    {
        if (State != GameState.Playing && State != GameState.Paused) return;

        State = GameState.GameOver;

        ApplyUIState();
        UpdateUI();
        SetStatusText("Game Over");
    }

    public void PauseGame()
    {
        if (State == GameState.Paused) return;

        ElapsedTime = 0f;
        State = GameState.Paused;
        SetStatusText(string.Empty);
        ApplyUIState();
        UpdateUI();
    }

    public void ResumeGame()
    {
        if (State != GameState.Paused) return;

        State = GameState.Playing;
        ShowGameStartMessage();
        ApplyUIState();
    }

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

    private void ApplyUIState()
    {
        if (hudPanel != null)
            hudPanel.SetActive(State == GameState.Playing || State == GameState.Paused);

        if (gameOverPanel != null)
            gameOverPanel.SetActive(State == GameState.GameOver);
    }

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
                }
            }
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

    private static bool TryGetCurrentEmissionColor(GameObject go, out Color color)
    {
        color = default;
        var sr = go != null ? go.GetComponent<SpriteRenderer>() : null;
        var mat = sr ? sr.sharedMaterial : null;
        if (!(mat != null && mat.shader != null && mat.shader.name == "Particles/Standard Unlit" && mat.HasProperty("_EmissionColor")))
            return false;
        color = mat.GetColor("_EmissionColor");
        return true;
    }
}
