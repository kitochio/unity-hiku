using UnityEngine;

/// <summary>
/// 経過時間に応じて容量を段階的に増やすシンプルなポリシー。
/// </summary>
public static class CapacityPolicy
{
    public static int Compute(GameDirector gd, int start, int max, float secondsPerIncrease)
    {
        int target = start;
        if (gd != null)
        {
            float elapsed = gd.ElapsedTime;
            float per = Mathf.Max(1e-6f, secondsPerIncrease);
            int inc = Mathf.FloorToInt(elapsed / per);
            target = Mathf.Clamp(start + inc, start, max);
        }
        return target;
    }
}

