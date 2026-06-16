using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 音频重采样器：将高于 16 kHz 的采样数据实时压缩到 16 kHz
/// </summary>
public class AudioResamplerTo16K : MonoBehaviour
{
    [Tooltip("目标采样率 (Hz)")]
    public int targetSampleRate = 16000;

    [Tooltip("输出重采样后的数据事件，参数为交错格式的 float 数组")]
    public Action<float[]> OnResampledData;

    // 跨帧缓冲：存储尚未完全处理的输入样本（交错格式）
    private List<float> inputBuffer = new List<float>();

    // 相位累加器（用于线性插值）
    private double phase;

    // 上次输入的采样率（用于检测采样率变化）
    private int lastInputFrequency = -1;

    // 声道数（从最新一帧获取）
    private int currentChannelCount;

    // 步长：输入样本相对于输出样本的增量 = inputRate / targetRate
    private double phaseDelta;

    /// <summary>
    /// 音频帧采集回调（由外部音频系统调用）
    /// </summary>
    /// <param name="frequency">当前输入帧的采样率 (Hz)</param>
    /// <param name="channelCount">声道数（1=单声道，2=立体声等）</param>
    /// <param name="samples">交错格式的浮点音频数据，范围 [-1, 1]</param>
    public void OnFrameCollected(int frequency, int channelCount, float[] samples)
    {
        // 如果输入采样率 <= 目标采样率，无需压缩，直接输出原始数据（可选）
        if (frequency <= targetSampleRate)
        {
            OnResampledData?.Invoke(samples);
            return;
        }

        // 检测采样率或声道数变化，重置重采样状态
        if (frequency != lastInputFrequency || channelCount != currentChannelCount)
        {
            ResetResampler(frequency, channelCount);
        }

        // 将新数据加入缓冲
        inputBuffer.AddRange(samples);

        // 准备输出列表（交错格式）
        List<float> outputBuffer = new List<float>();

        // 只要缓冲中有足够样本产生至少一个输出样本，就持续重采样
        // 线性插值需要两个输入样本（位置 i 和 i+1），因此缓冲中至少需要 floor(phase)+2 个样本
        while (phase + 1 < inputBuffer.Count / (double)currentChannelCount)
        {
            // 计算当前输出样本对应的输入位置（样本索引，非交错）
            double inputPos = phase;
            int idx0 = (int)inputPos;                 // 前一个样本的索引（每声道）
            float frac = (float)(inputPos - idx0);    // 插值系数

            // 对每个声道独立进行线性插值
            for (int ch = 0; ch < currentChannelCount; ch++)
            {
                // 获取前后两个输入样本（交错格式）
                int baseIdx0 = idx0 * currentChannelCount + ch;
                int baseIdx1 = (idx0 + 1) * currentChannelCount + ch;

                float sample0 = inputBuffer[baseIdx0];
                float sample1 = inputBuffer[baseIdx1];

                // 线性插值
                float interpolated = sample0 + (sample1 - sample0) * frac;
                outputBuffer.Add(interpolated);
            }

            // 更新相位，步进一个输出样本
            phase += phaseDelta;
        }

        // 输出重采样后的数据
        if (outputBuffer.Count > 0)
        {
            OnResampledData?.Invoke(outputBuffer.ToArray());
        }

        // 从输入缓冲中移除已经处理过的样本
        // 保留尚未使用的样本（phase 的小数部分及未达到完整输出条件的尾部）
        int consumedSamples = (int)phase * currentChannelCount;
        if (consumedSamples > 0)
        {
            inputBuffer.RemoveRange(0, consumedSamples);
            phase -= (int)phase;   // 只保留小数部分
        }

        // 可选：限制缓冲无限增长（通常不会，因为每帧都会消耗）
        if (inputBuffer.Count > 10000 * currentChannelCount)
        {
            Debug.LogWarning("AudioResamplerTo16K: Input buffer too large, clearing. Possibly missing output consumption.");
            inputBuffer.Clear();
            phase = 0;
        }
    }

    /// <summary>
    /// 重置重采样器状态（采样率或声道变化时调用）
    /// </summary>
    private void ResetResampler(int frequency, int channelCount)
    {
        inputBuffer.Clear();
        phase = 0;
        lastInputFrequency = frequency;
        currentChannelCount = channelCount;
        phaseDelta = frequency / (double)targetSampleRate;  // 步长 = 输入采样率 / 目标采样率
    }

    /// <summary>
    /// 手动重置重采样器（例如在重新开始录音时调用）
    /// </summary>
    public void ManualReset()
    {
        if (lastInputFrequency != -1)
        {
            ResetResampler(lastInputFrequency, currentChannelCount);
        }
        else
        {
            inputBuffer.Clear();
            phase = 0;
        }
    }
}