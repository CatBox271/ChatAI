using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

public class TTSStreamProcessor : MonoBehaviour
{
    [Header("TTS管理器引用")]
    private TencentCloudTTSManager ttsManager;

    [Header("分段设置")]
    [SerializeField] private float sentencePause = 0.1f;

    private Dictionary<int, StringBuilder> pendingChunks = new();
    private Dictionary<int, List<string>> sessionQueues = new();
    private List<string> errorPass = new();

    private int currentSequenceId = -1;
    private bool isSilent = false;

    // 限流相关
    private Queue<TTSRequestQueueItem> ttsRequestQueue = new();
    private Coroutine ttsProcessorCoroutine;
    private float minRequestInterval = 0.5f; // 每秒最多2条 => 间隔0.5秒
    private DateTime lastRequestTime = DateTime.MinValue;

    private static readonly Regex ControlTagRegex = new Regex(@"\[t+s*_(\w+)(?::([^\]]*))?\]", RegexOptions.Compiled);
    private static readonly Regex IgnoreTagRegex = new Regex(@"<[^>]+>", RegexOptions.Compiled);

    public static TTSStreamProcessor Instance;

    private void Awake() => Instance = this;
    private Coroutine play_audio;

    private void Start()
    {
        ttsManager = TencentCloudTTSManager.Instance;
    }

    // 入队请求并启动处理协程
    private void EnqueueTTSRequest(string text, string sessionId, Action<bool, string> callback)
    {
        if (string.IsNullOrEmpty(text)) return;
        ttsRequestQueue.Enqueue(new TTSRequestQueueItem { text = text, sessionId = sessionId, callback = callback });
        if (ttsProcessorCoroutine == null)
        {
            ttsProcessorCoroutine = StartCoroutine(ProcessTTSRequestQueue());
        }
    }

    private IEnumerator ProcessTTSRequestQueue()
    {
        while (ttsRequestQueue.Count > 0)
        {
            // 计算距离上次请求的时间差
            TimeSpan elapsed = DateTime.Now - lastRequestTime;
            if (elapsed.TotalSeconds < minRequestInterval)
            {
                float waitTime = minRequestInterval - (float)elapsed.TotalSeconds;
                yield return new WaitForSeconds(waitTime);
            }

            TTSRequestQueueItem item = ttsRequestQueue.Dequeue();
            lastRequestTime = DateTime.Now;
            ttsManager.TextToVoice(item.text, item.sessionId, item.callback);
            yield return null; // 保证每帧至少一次，避免死循环
        }
        ttsProcessorCoroutine = null;
    }



    public void OnChunk(int sequenceId, string chunk, bool end = false)
    {
        if (!pendingChunks.ContainsKey(sequenceId))
        {
            OrderClear();
            errorPass.Clear();
            pendingChunks[sequenceId] = new StringBuilder();
            sessionQueues[sequenceId] = new List<string>();
        }

        pendingChunks[sequenceId].Append(chunk);
        ProcessAvailableSegments(sequenceId);

        if (end && pendingChunks[sequenceId].Length > 0)
        {
            string remaining = pendingChunks[sequenceId].ToString();
            string cleanText = ExtractAndExecute(remaining);
            if (!string.IsNullOrEmpty(cleanText) && !isSilent)
            {
                string sessionId = Guid.NewGuid().ToString();
                sessionQueues[sequenceId].Add(sessionId);
                EnqueueTTSRequest(cleanText, sessionId, (success, id) =>
                {
                    if (!success) Debug.LogError($"TTS生成失败: {id}");
                    else Debug.Log($"TTS生成成功:{cleanText}");
                });
            }
            pendingChunks[sequenceId].Clear();
        }

        if (currentSequenceId != sequenceId)
        {
            if (play_audio != null) StopCoroutine(play_audio);
            play_audio = StartCoroutine(PlaySequence(sequenceId));
        }
    }



    public void OnSequenceComplete(int sequenceId, string fullContext = "")
    {
        if (pendingChunks.ContainsKey(sequenceId))
            OnChunk(sequenceId, "", true);
        else
            OnChunk(sequenceId, fullContext, true);
    }

    private void ProcessAvailableSegments(int sequenceId)
    {
        StringBuilder text = pendingChunks[sequenceId];
        List<string> segments = SplitIntoSegments(text);

        foreach (string segment in segments)
        {
            if (ControlTagRegex.IsMatch(segment))
            {
                ExtractAndExecute(segment);
                continue;
            }

            string cleanText = ExtractAndExecute(segment);
            if (!string.IsNullOrEmpty(cleanText) && !isSilent)
            {
                string sessionId = Guid.NewGuid().ToString();
                sessionQueues[sequenceId].Add(sessionId);
                EnqueueTTSRequest(cleanText, sessionId, (success, id) =>
                {
                    if (!success) Debug.Log($"TTS生成失败: {id}");
                });
            }
        }
    }


    /// <summary>
    /// 传入完整的原始文本，自动分段、合成并播放。
    /// 如果 sessionQueues 中不存在该 sequenceId，则会初始化并重置全局设置。
    /// </summary>
    /// <param name="sequenceId">序列ID</param>
    /// <param name="rawText">包含可能控制标签的原始文本</param>
    public void ProcessFullText(int sequenceId, string rawText)
    {
        if (string.IsNullOrEmpty(rawText)) return;

        // 如果该 sequenceId 尚未初始化，则按原有逻辑初始化
        if (!sessionQueues.ContainsKey(sequenceId))
        {
            OrderClear();
            errorPass.Clear();
            pendingChunks[sequenceId] = new StringBuilder();
            sessionQueues[sequenceId] = new List<string>();
        }

        // 创建一个临时 StringBuilder 用于分段，避免影响流式 pendingChunks
        StringBuilder tempBuilder = new StringBuilder(rawText);
        List<string> segments = SplitIntoSegments(tempBuilder);

        //还需要把末尾项加入segment
        segments.Add(pendingChunks[sequenceId].ToString());

        foreach (string segment in segments)
        {
            // 处理控制标签（不生成语音）
            if (ControlTagRegex.IsMatch(segment))
            {
                ExtractAndExecute(segment);
                continue;
            }

            // 提取纯文本，同时执行标签指令
            string cleanText = ExtractAndExecute(segment);
            if (!string.IsNullOrEmpty(cleanText) && !isSilent)
            {
                string sessionId = Guid.NewGuid().ToString();
                sessionQueues[sequenceId].Add(sessionId);
                EnqueueTTSRequest(cleanText, sessionId, (success, id) =>
                {
                    if (!success) Debug.Log($"TTS生成失败: {id}");
                });
            }
        }

        // 如果当前没有在播放这个序列，则停止当前播放并开始播放新序列（抢占式，与 OnChunk 行为一致）
        if (currentSequenceId != sequenceId)
        {
            if (play_audio != null) StopCoroutine(play_audio);
            play_audio = StartCoroutine(PlaySequence(sequenceId));
        }
    }

    readonly HashSet<char> bracketBegin = new() { '(' , '（' };
    readonly HashSet<char> bracketStart = new() { '）' , ')' };
    private List<string> SplitIntoSegments(StringBuilder text)
    {
        List<string> segments = new List<string>();
        int bracketDepth = 0;
        int lastBracketEnd = -1;
        int lastSegmentEnd = 0;
        int commaCount = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (bracketBegin.Contains(c)) bracketDepth++;
            else if (bracketStart.Contains(c))
            {
                bracketDepth--;
                lastBracketEnd = i;
            }

            if (c == '[' && i + 4 < text.Length && text[i + 1] == 't' && text[i + 2] == 't' && text[i + 3] == 's' && text[i + 4] == '_')
            {
                int endBracket = FindClosingBracket(text, i);
                if (endBracket != -1)
                {
                    if (i > lastSegmentEnd)
                    {
                        string beforeTag = text.ToString(lastSegmentEnd, i - lastSegmentEnd);
                        SplitByPunctuation(beforeTag, segments);
                    }
                    string tag = text.ToString(i, endBracket - i + 1);
                    segments.Add(tag);
                    lastSegmentEnd = endBracket + 1;
                    i = endBracket;
                    continue;
                }
            }

            if (bracketDepth == 0)
            {
                if (lastBracketEnd != -1)
                {
                    //去掉括号内容
                    lastSegmentEnd = i + 1;
                    lastBracketEnd = -1;
                }
                if (c == ',' || c == '，')
                {
                    commaCount++;
                }
                if (c == '。' || c == '！' || c == '？' || c == '；' || c == '\n' || commaCount > 2)
                {
                    commaCount = 0;
                    if (i + 1 > lastSegmentEnd)
                    {
                        string segment = text.ToString(lastSegmentEnd, i + 1 - lastSegmentEnd);
                        if (!string.IsNullOrEmpty(segment) && ContainsValidChars(segment))
                        {
                            segments.Add(segment);
                        }
                        lastSegmentEnd = i + 1;
                    }
                }
            }
        }

        if (lastSegmentEnd > 0)
        {
            text.Remove(0, lastSegmentEnd);
        }

        return segments;
    }

    private int FindClosingBracket(StringBuilder sb, int start)
    {
        for (int i = start + 1; i < sb.Length; i++)
        {
            if (sb[i] == ']') return i;
        }
        return -1;
    }

    private void SplitByPunctuation(string textBlock, List<string> targetList)
    {
        if (string.IsNullOrEmpty(textBlock)) return;
        int last = 0;
        int depth = 0;
        for (int i = 0; i < textBlock.Length; i++)
        {
            char c = textBlock[i];
            if (c == '(' || c == '（') depth++;
            else if (c == ')' || c == '）') depth--;

            if (depth == 0)
            {
                if (c == '。' || c == '！' || c == '？' || c == '；' || c == '\n')
                {
                    string sentence = textBlock.Substring(last, i + 1 - last);
                    if (!string.IsNullOrEmpty(sentence) && ContainsValidChars(sentence))
                        targetList.Add(sentence);
                    last = i + 1;
                }
            }
        }
        if (last < textBlock.Length)
        {
            string remain = textBlock.Substring(last);
            if (!string.IsNullOrEmpty(remain) && ContainsValidChars(remain))
                targetList.Add(remain);
        }
    }

    private bool ContainsValidChars(string str)
    {
        return Regex.IsMatch(str, @"[\u4e00-\u9fffA-Za-z0-9]");
    }

    private string ExtractAndExecute(string text)
    {
        string result = text;

        result = ControlTagRegex.Replace(result, match =>
        {
            string tagName = match.Groups[1].Value.ToLower();
            string value = match.Groups[2].Value;

            switch (tagName)
            {
                case "speed":
                    if (float.TryParse(value, out float speed)) ttsManager.speed = Mathf.Clamp(speed, -2, 6);
                    break;
                case "emo":
                    ttsManager.emotionCategory = ParseEmotion(value);
                    break;
                case "volume":
                    if (float.TryParse(value, out float volume)) ttsManager.volume = Mathf.Clamp(volume, -10, 10);
                    break;
                case "voice":
                    if (int.TryParse(value, out int voiceType)) ttsManager.voiceType = voiceType;
                    break;
                case "intensity":
                    if (int.TryParse(value, out int intensity)) ttsManager.emotionIntensity = Mathf.Clamp(intensity, 50, 100);
                    break;
                case "silent":
                    isSilent = true;
                    break;
                case "clear":
                    OrderClear();
                    break;
            }
            return "";
        });

        result = IgnoreTagRegex.Replace(result, "");
        result = Regex.Replace(result, @"\s+", " ");
        return result.Trim();
    }

    private void OrderClear()
    {
        ttsManager.speed = 0;
        ttsManager.volume = 0;
        ttsManager.emotionCategory = TencentCloudTTSManager.EmotionType.neutral;
        ttsManager.emotionIntensity = 75;
        ttsManager.voiceType = 601009;
        isSilent = false;
    }

    private IEnumerator PlaySequence(int sequenceId)
    {
        currentSequenceId = sequenceId;
        List<string> queue = sessionQueues[sequenceId];
        int a = 0;
        while (true)
        {
            if (queue.Count > a)
            {
                if (errorPass.Contains(queue[a]))
                {
                    a++;
                    continue;
                }
                if (ttsManager.HasAudio(queue[a]))
                {
                    //print(queue[a]);
                    bool isComplete = false;
                    ttsManager.PlayCachedAudio(queue[a], () => isComplete = true);
                    yield return new WaitUntil(() => isComplete);
                    yield return new WaitForSeconds(sentencePause);
                    a++;
                }
            }
            yield return null;
        }
    }

    private TencentCloudTTSManager.EmotionType ParseEmotion(string value)
    {
        switch (value.ToLower())
        {
            case "neutral": return TencentCloudTTSManager.EmotionType.neutral;
            case "sad": return TencentCloudTTSManager.EmotionType.sad;
            case "happy": return TencentCloudTTSManager.EmotionType.happy;
            case "angry": return TencentCloudTTSManager.EmotionType.angry;
            case "fear": return TencentCloudTTSManager.EmotionType.fear;
            case "sajiao": return TencentCloudTTSManager.EmotionType.sajiao;
            case "disgusted": return TencentCloudTTSManager.EmotionType.disgusted;
            case "amaze": return TencentCloudTTSManager.EmotionType.amaze;
            case "peaceful": return TencentCloudTTSManager.EmotionType.peaceful;
            case "suprised": return TencentCloudTTSManager.EmotionType.amaze;
            default: return TencentCloudTTSManager.EmotionType.neutral;
        }
    }

    public void Stop()
    {
        if (ttsProcessorCoroutine != null)
            StopCoroutine(ttsProcessorCoroutine);
        ttsRequestQueue.Clear();
        StopAllCoroutines();
        pendingChunks.Clear();
        sessionQueues.Clear();
        currentSequenceId = -1;
        if (ttsManager != null) ttsManager.Stop();
    }

    // 内部请求项结构
    private struct TTSRequestQueueItem
    {
        public string text;
        public string sessionId;
        public Action<bool, string> callback;
    }
}