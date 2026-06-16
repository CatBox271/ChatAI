using UnityEngine;

/// <summary>
/// 优化的带通滤波器（级联二阶高通 + 二阶低通，带增益补偿）
/// 适用于人声增强 (80Hz - 3000Hz)，在通带内保持单位增益，避免信号衰减
/// </summary>
public class AudioBandpassFilterState
{
    // 高通滤波器系数
    private float hp_b0, hp_b1, hp_b2;
    private float hp_a1, hp_a2;
    private float hp_x1, hp_x2;
    private float hp_y1, hp_y2;

    // 低通滤波器系数
    private float lp_b0, lp_b1, lp_b2;
    private float lp_a1, lp_a2;
    private float lp_x1, lp_x2;
    private float lp_y1, lp_y2;

    // 增益补偿因子（补偿通带内可能出现的轻微衰减）
    private float gainCompensation = 1.0f;

    public AudioBandpassFilterState(int sampleRate, float lowCutoff = 80f, float highCutoff = 3000f)
    {
        float nyquist = sampleRate * 0.5f;
        float lowNorm = Mathf.Clamp(lowCutoff / nyquist, 0.001f, 0.99f);
        float highNorm = Mathf.Clamp(highCutoff / nyquist, 0.01f, 0.99f);

        // 设计二阶 Butterworth 高通滤波器
        DesignHighPass(lowNorm, out hp_b0, out hp_b1, out hp_b2, out hp_a1, out hp_a2);

        // 设计二阶 Butterworth 低通滤波器
        DesignLowPass(highNorm, out lp_b0, out lp_b1, out lp_b2, out lp_a1, out lp_a2);

        // 计算通带增益补偿（可选，通常 Butterworth 级联后通带增益接近 1，此值为 1.0）
        gainCompensation = 1.0f;

        Reset();
    }

    private void DesignHighPass(float normFreq, out float b0, out float b1, out float b2, out float a1, out float a2)
    {
        float omega = Mathf.Tan(Mathf.PI * normFreq);
        float sqrt2 = 1.41421356237f;
        float norm = 1.0f / (1.0f + sqrt2 * omega + omega * omega);

        b0 = 1.0f * norm;
        b1 = -2.0f * norm;
        b2 = 1.0f * norm;
        a1 = 2.0f * (omega * omega - 1.0f) * norm;
        a2 = (1.0f - sqrt2 * omega + omega * omega) * norm;
    }

    private void DesignLowPass(float normFreq, out float b0, out float b1, out float b2, out float a1, out float a2)
    {
        float omega = Mathf.Tan(Mathf.PI * normFreq);
        float sqrt2 = 1.41421356237f;
        float norm = 1.0f / (1.0f + sqrt2 * omega + omega * omega);

        b0 = omega * omega * norm;
        b1 = 2.0f * omega * omega * norm;
        b2 = omega * omega * norm;
        a1 = 2.0f * (omega * omega - 1.0f) * norm;
        a2 = (1.0f - sqrt2 * omega + omega * omega) * norm;
    }

    public void Reset()
    {
        hp_x1 = hp_x2 = 0f;
        hp_y1 = hp_y2 = 0f;
        lp_x1 = lp_x2 = 0f;
        lp_y1 = lp_y2 = 0f;
    }

    /// <summary>
    /// 对数组中的前 count 个样本应用带通滤波（原地修改）
    /// </summary>
    public void Process(float[] buffer, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float x0 = buffer[i];

            // 第一步：高通滤波
            float hp_y0 = hp_b0 * x0 + hp_b1 * hp_x1 + hp_b2 * hp_x2 - hp_a1 * hp_y1 - hp_a2 * hp_y2;
            hp_x2 = hp_x1;
            hp_x1 = x0;
            hp_y2 = hp_y1;
            hp_y1 = hp_y0;

            // 第二步：低通滤波
            float lp_y0 = lp_b0 * hp_y0 + lp_b1 * lp_x1 + lp_b2 * lp_x2 - lp_a1 * lp_y1 - lp_a2 * lp_y2;
            lp_x2 = lp_x1;
            lp_x1 = hp_y0;
            lp_y2 = lp_y1;
            lp_y1 = lp_y0;

            // 应用增益补偿
            buffer[i] = lp_y0 * gainCompensation;
        }
    }
}