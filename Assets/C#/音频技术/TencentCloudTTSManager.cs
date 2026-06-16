using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
/// <summary>
/// 腾讯云TTS管理器（Unity版本）
/// </summary>
/// 
public class TencentCloudTTSManager : MonoBehaviour
{
    [Serializable]
    public class TTSRequest
    {
        public string Text;
        public string SessionId;
        public float Volume;
        public float Speed;
        public int VoiceType;
        public int PrimaryLanguage;
        public int SampleRate;
        public string Codec;
        public string EmotionCategory;
        public int EmotionIntensity;
    }

    public enum EmotionType
    {
        neutral,//中性
        sad,//悲伤
        happy,//高兴
        angry,//生气
        fear,//恐惧
        sajiao,//撒娇
        disgusted,//厌恶
        amaze,//震惊
        peaceful,//平静
    }

    [Header("腾讯云配置")]
    public string secretId = "";
    public string secretKey = "";
    [SerializeField] private string token = "";                       // 使用临时凭证时填写

    [Header("TTS请求配置")]
    [SerializeField] private string endpoint = "tts.tencentcloudapi.com";
    [SerializeField] private string region = "";                      // 可选，如 "ap-guangzhou"
    [SerializeField] private string action = "TextToVoice";
    [SerializeField] private string version = "2019-08-23";

    [Header("语音参数")]
    [SerializeField] private int primaryLanguage = 1;    // 主语言：1-中文
    [SerializeField] private string codec = "mp3";       // 音频格式：mp3/wav/pcm
    [SerializeField] private int sampleRate = 24000;     // 采样率 8k / 16k / 24k 
    //可变参数
    public int voiceType = 601009;    // 音色类型
    public float volume = 0f;          // 音量：-10~10
    public float speed = 0f;           // 语速：-2~6
    //-2代表0.6倍
    //-1代表0.8倍
    //0代表1.0倍（默认）
    //1代表1.2倍
    //2代表1.5倍
    //6代表2.5倍
    //如果需要更细化的语速，可以保留小数点后 2 位，例如0.5/1.25/2.81等。
    public EmotionType emotionCategory = EmotionType.neutral;
    public int emotionIntensity = 100;
    //播放列表
    private Dictionary<string, byte[]> audios = new();

    private AudioSource audioSource;

    public static TencentCloudTTSManager Instance;

    void Awake()
    {
        //单例模式
        Instance = this;
        // 添加AudioSource组件用于播放音频
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        LoadTTSConfig();
    }

    public void LoadTTSConfig()
    {
        string character = PlayerPrefs.GetString("Character", "");
        var cfg = GlobalApiConfig.Load(character);
        if (!string.IsNullOrEmpty(cfg.ttsSecretId)) secretId = cfg.ttsSecretId;
        if (!string.IsNullOrEmpty(cfg.ttsSecretKey)) secretKey = cfg.ttsSecretKey;
    }

    public void SaveTTSConfig()
    {
        string character = PlayerPrefs.GetString("Character", "");
        var cfg = GlobalApiConfig.Load(character);
        GlobalApiConfig.Save(character, cfg.auxApiUrl, cfg.auxApiKey, secretId, secretKey);
    }

    /// <summary>
    /// 文本转语音
    /// </summary>
    /// <param name="text">要转换的文本（最多150字符）</param>
    /// <param name="sessionId">会话ID（可选）</param>
    /// <param name="onComplete">完成回调（音频剪辑，错误信息）</param>
    public void TextToVoice(string text, string sessionId = "", Action<bool, string> onComplete = null)
    {
        StartCoroutine(TextToVoiceCoroutine(text, sessionId, onComplete));
    }

    private IEnumerator TextToVoiceCoroutine(string text, string sessionId, Action<bool,string> onComplete)
    {
        //保留sessionId
        sessionId = string.IsNullOrEmpty(sessionId) ? Guid.NewGuid().ToString() : sessionId;
        // 1. 构建请求体
        TTSRequest tts_request = new TTSRequest
        {
            Text = text,
            SessionId = sessionId,
            Volume = volume,
            Speed = speed,
            VoiceType = voiceType,
            PrimaryLanguage = primaryLanguage,
            Codec = codec,
            SampleRate = sampleRate,
            EmotionCategory = emotionCategory.ToString(),
            EmotionIntensity = emotionIntensity,
        };

        string requestBodyJson = JsonUtility.ToJson(tts_request);

        // 2. 生成签名
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string nonce = Guid.NewGuid().ToString("N");

        var headers = GenerateTC3Signature(
            secretId,
            secretKey,
            token,
            endpoint,
            action,
            version,
            region,
            requestBodyJson,
            timestamp
        );

        // 3. 发送请求
        string url = $"https://{endpoint}";

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(requestBodyJson);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            // 添加请求头
            foreach (var header in headers)
            {
                request.SetRequestHeader(header.Key, header.Value);
            }
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            // 4. 处理响应
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var response = JsonUtility.FromJson<TTSResponse>(request.downloadHandler.text);
                    if (response.Response != null && !string.IsNullOrEmpty(response.Response.Audio))
                    {
                        // 解码Base64音频数据
                        audios[sessionId] = Convert.FromBase64String(response.Response.Audio);
                        //成功时返回SessionID
                        onComplete?.Invoke(true, sessionId);
                    }
                    else if (response.Response != null && response.Response.Error != null)
                    {
                        onComplete?.Invoke(false ,$"[{response.Response.Error.Code}] {response.Response.Error.Message}");
                    }
                    else
                    {
                        onComplete?.Invoke(false,"未知响应格式");
                    }
                }
                catch (Exception e)
                {
                    onComplete?.Invoke(false,$"解析响应失败: {e.Message}");
                }
            }
            else
            {
                onComplete?.Invoke(false,$"请求失败: {request.error}");
            }
        }
    }
  
    public bool HasAudio(string sessionId)
    {
        return audios != null && audios.ContainsKey(sessionId);
    }
    public bool IsPlaying
    {
        get
        {
            return audioSource != null && audioSource.isPlaying;
        }
    }

    public void Stop()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
        StopAllCoroutines();
    }


    /// <summary>
    /// 生成TC3-HMAC-SHA256签名
    /// </summary>
    private Dictionary<string, string> GenerateTC3Signature(
        string secretId, string secretKey, string token, string endpoint,
        string action, string version, string region, string requestBody,
        long timestamp)
    {
        string algorithm = "TC3-HMAC-SHA256";
        string date = DateTimeOffset.FromUnixTimeSeconds(timestamp).ToString("yyyy-MM-dd");
        string service = "tts";

        // 1. 构建规范请求串
        string httpRequestMethod = "POST";
        string canonicalUri = "/";
        string canonicalQueryString = "";
        string canonicalHeaders = $"content-type:application/json\nhost:{endpoint}\n";
        string signedHeaders = "content-type;host";
        string hashedRequestPayload = Sha256Hex(requestBody);

        string canonicalRequest = $"{httpRequestMethod}\n{canonicalUri}\n{canonicalQueryString}\n{canonicalHeaders}\n{signedHeaders}\n{hashedRequestPayload}";

        // 2. 构建待签名字符串
        string credentialScope = $"{date}/{service}/tc3_request";
        string hashedCanonicalRequest = Sha256Hex(canonicalRequest);
        string stringToSign = $"{algorithm}\n{timestamp}\n{credentialScope}\n{hashedCanonicalRequest}";

        // 3. 计算签名
        byte[] secretDate = HmacSha256(($"TC3{secretKey}"), date);
        byte[] secretService = HmacSha256(secretDate, service);
        byte[] secretSigning = HmacSha256(secretService, "tc3_request");
        byte[] signatureBytes = HmacSha256(secretSigning, stringToSign);
        string signature = ByteArrayToHexString(signatureBytes).ToLower();

        // 4. 构建Authorization头
        string authorization = $"{algorithm} Credential={secretId}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";

        // 5. 返回请求头
        var headers = new Dictionary<string, string>
        {
            { "Authorization", authorization },
            { "Host", endpoint },
            { "X-TC-Action", action },
            { "X-TC-Timestamp", timestamp.ToString() },
            { "X-TC-Version", version },
            { "X-TC-Language", "zh-CN" }
        };

        if (!string.IsNullOrEmpty(region))
            headers.Add("X-TC-Region", region);

        if (!string.IsNullOrEmpty(token))
            headers.Add("X-TC-Token", token);

        return headers;
    }

    // 辅助方法
    private string Sha256Hex(string str)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(str));
            return ByteArrayToHexString(hashBytes).ToLower();
        }
    }

    private byte[] HmacSha256(byte[] key, string msg)
    {
        using (HMACSHA256 hmac = new HMACSHA256(key))
        {
            return hmac.ComputeHash(Encoding.UTF8.GetBytes(msg));
        }
    }

    private byte[] HmacSha256(string key, string msg)
    {
        return HmacSha256(Encoding.UTF8.GetBytes(key), msg);
    }

    private string ByteArrayToHexString(byte[] bytes)
    {
        StringBuilder sb = new StringBuilder();
        foreach (byte b in bytes)
        {
            sb.Append(b.ToString("x2"));
        }
        return sb.ToString();
    }

    private AudioType GetAudioType(string codec)
    {
        return codec.ToLower() == "mp3" ? AudioType.MPEG : AudioType.WAV;
    }

    /// <summary>
    /// 播放音频
    /// </summary>
    public void PlayAudioClip(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.Stop();
            audioSource.clip = clip;
            audioSource.Play();
        }
    }

    /// <summary>
    /// 播放缓存的音频（通过SessionId）
    /// </summary>
    public void PlayCachedAudio(string sessionId, Action onComplete = null)
    {
        if (!audios.ContainsKey(sessionId))
        {
            Debug.LogWarning($"未找到SessionId: {sessionId} 的缓存音频");
            onComplete?.Invoke();
            return;
        }

        StartCoroutine(LoadAndPlayAudio(audios[sessionId], sessionId, onComplete));
    }
    private IEnumerator LoadAndPlayAudio(byte[] audioData, string sessionId, Action onComplete)
    {
        string fileType = codec.ToLower() == "mp3" ? "mp3" : "wav";
        string filePath = Application.temporaryCachePath + $"/tts_audio_{sessionId}.{fileType}";

        // 保存临时文件
        System.IO.File.WriteAllBytes(filePath, audioData);

        // 使用UnityWebRequest加载音频
        using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip("file://" + filePath, GetAudioType(codec)))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                clip.name = "TTS_Audio";
                PlayAudioClip(clip);

                // 等待播放完成
                yield return new WaitUntil(() => !IsPlaying);
            }
            else
            {
                Debug.LogError($"加载音频失败: {request.error}");
            }
        }

        // 清理临时文件
        if (System.IO.File.Exists(filePath))
            System.IO.File.Delete(filePath);

        // 播放完成回调
        onComplete?.Invoke();
    }
    /// <summary>
    /// 清除缓存
    /// </summary>
    public void ClearCache(string sessionId = null)
    {
        if (string.IsNullOrEmpty(sessionId))
            audios.Clear();
        else
            audios.Remove(sessionId);
    }
    /// <summary>
    /// 简单的示例用法
    /// </summary>
    [ContextMenu("测试TTS")]
    public void TestTTS()
    {
        TextToVoice("你好，世界，这是一个Unity中的腾讯云语音合成测试", "", (succuss, id) =>
        {
            if (succuss)
            {
                Debug.Log("语音合成成功，开始播放");
                PlayCachedAudio(id);
            }
            else
            {
                Debug.LogError($"语音合成失败: {id}");
            }
        });
    }

    // 数据结构
    [Serializable]
    public class TTSResponse
    {
        public ResponseData Response;
    }

    [Serializable]
    public class ResponseData
    {
        public string Audio;
        public string SessionId;
        public string RequestId;
        public ErrorInfo Error;
    }

    [Serializable]
    public class ErrorInfo
    {
        public string Code;
        public string Message;
    }
}