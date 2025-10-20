using System;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

public class GameDirector : MonoBehaviour
{
    public enum GameState { Ready, Playing, Paused, GameOver }

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private GameObject startPanel;
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

    public event Action GameStarted;
    public event Action GameEnded;
    public event Action<int> ScoreChanged;

    public GameState State { get; private set; } = GameState.Ready;
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
        if (State != GameState.Playing) return;

        ElapsedTime += Time.deltaTime;

        if (useTimer && RemainingTime <= 0f)
        {
            EndGame();
            return;
        }

        UpdateUI();

        UpdateSavedObjectColorsIfChanged();
    }

    public void StartGame()
    {
        if (State == GameState.Playing) return;

        Time.timeScale = 1f;
        Score = 0;
        ElapsedTime = 0f;
        State = GameState.Playing;

        ApplyUIState();
        UpdateUI();

        onGameStarted?.Invoke();
        GameStarted?.Invoke();
        SetStatusText(string.Empty);
    }

    public void EndGame()
    {
        if (State != GameState.Playing && State != GameState.Paused) return;

        State = GameState.GameOver;

        ApplyUIState();
        UpdateUI();

        onGameEnded?.Invoke();
        GameEnded?.Invoke();
        SetStatusText("Game Over");
    }

    public void PauseGame()
    {
        if (State != GameState.Playing) return;

        State = GameState.Paused;
        Time.timeScale = 0f;
        SetStatusText("Paused");
        ApplyUIState();
    }

    public void ResumeGame()
    {
        if (State != GameState.Paused) return;

        State = GameState.Playing;
        Time.timeScale = 1f;
        SetStatusText(string.Empty);
        ApplyUIState();
    }

    public void ResetGame()
    {
        Time.timeScale = 1f;
        Score = 0;
        ElapsedTime = 0f;
        State = GameState.Ready;

        ApplyUIState();
        UpdateUI();
        SetStatusText("Ready");
    }

    public void AddScore(int amount)
    {
        if (State != GameState.Playing) return;

        Score = Mathf.Max(0, Score + amount);
        ScoreChanged?.Invoke(Score);
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (scoreText != null)
            scoreText.text = $"Score: {Score}";

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
        if (startPanel != null)
            startPanel.SetActive(State == GameState.Ready);

        if (hudPanel != null)
            hudPanel.SetActive(State == GameState.Playing || State == GameState.Paused);

        if (gameOverPanel != null)
            gameOverPanel.SetActive(State == GameState.GameOver);
    }

    private void SetStatusText(string msg)
    {
        if (statusText != null)
            statusText.text = msg;
    }

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

        if (ids.Count == _savedOrderIds.Count)
        {
            bool same = true;
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] != _savedOrderIds[i]) { same = false; break; }
            }
            if (same) return;
        }

        _savedOrderIds.Clear();
        _savedOrderIds.AddRange(ids);

        // Restore original color for objects that dropped out from previous list
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
            var m = go != null ? go.GetComponent<MaterialOperations>() : null;
            if (m == null) continue;

            // Cache original emission color once per object
            int id = go.GetInstanceID();
            if (!_originalEmissionById.ContainsKey(id))
            {
                if (TryGetCurrentEmissionColor(go, out var current))
                {
                    _originalEmissionById[id] = current;
                }
            }

            m.SetEmissionColor(i == objs.Count - 1 ? ColorLast : ColorOther);
        }

        // Keep current list for next diff
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
