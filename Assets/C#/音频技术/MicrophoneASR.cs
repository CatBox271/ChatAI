using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Adrenak.UniMic;
using System.Linq;

public class PromptSave
{
    public string brief;
    public string atmosphere;
    public List<string> hotwords = new();

    public PromptSave(string brief, string atmosphere, List<string> hotwords)
    {
        this.brief = brief;
        this.atmosphere = atmosphere;
        this.hotwords = hotwords;
    }
}

public class AllPrompt
{
    public int index = 0;
    public List<PromptSave> prompts = new();
}

public class MicrophoneASR : MonoBehaviour
{
    [Header("VAD 设置")]
    [SerializeField] private float segmentSilenceDuration = 1.0f;
    [SerializeField] private float minSpeechDuration = 0.4f;

    [Header("VAD 谱通量")]
    [SerializeField] public float spectralFluxThreshold = 5.0f;
    [SerializeField] private int fftFrameSize = 1024;
    [SerializeField] private int smoothingFrames = 10;

    [Header("检测到前保留的音频帧")]
    public int keepHeadFrames;
    private int PRE_RECORD_FRAMES => smoothingFrames * keepHeadFrames;

    [Header("网络设置")]
    public string serverIp = "";
    public int serverPort;

    public Text MicrophoneState;
    public Button send;
    public TencentCloudTTSManager tts;

    public event Action<string> OnTranscriptionText;
    public event Action OnAutoSend;

    public static MicrophoneASR instance;

    public int prompt_index;
    public List<PromptSave> AllPrompts;

    private Mic.Device _micDevice;
    private int _actualSampleRate;

    // 音频处理缓冲区
    private List<float> _sampleAccumulator = new List<float>();
    private Queue<float[]> _preRecordBuffer = new Queue<float[]>();
    private List<float> recordingBuffer = new();
    private bool isRecording = false;
    private float lowVolumeTimer;
    private float lastLoudTime;

    private enum VADState { Idle, Recording }
    private VADState vadState = VADState.Idle;

    private float[] _prevSpectrum;
    private float[] _spectrumBuffer;
    private Queue<float> _recentFluxValues = new Queue<float>();

    // 长连接
    private TcpClient _client;
    private NetworkStream _stream;
    private bool _connected = false;
    private bool _stopLoop = false;

    // 自动发送相关
    public bool auto_sending { get { return SendDelay > 0; } }
    public string sending_keyword = "对话完毕";

    private bool recognize;
    private float delayTime;

    [Header("自动上传时间")]
    public float SendDelay = 3f;

    private bool isListening = false;

    [Header("UI")]
    public InputField AutoSendInput;
    public InputField KeywordInput;
    public InputField ServerIP;
    public InputField ServerPort;

    void TextInput(string _)
    {
        PlayerPrefs.SetFloat("AutoSendDelay", SendDelay = float.Parse(AutoSendInput.text));
        PlayerPrefs.SetString("SendKeyword", sending_keyword = KeywordInput.text);
        PlayerPrefs.SetString("ASRIP", serverIp = ServerIP.text);
        PlayerPrefs.SetInt("ASRPort", serverPort = int.Parse(ServerPort.text));
    }

    void TextLoad()
    {
        AutoSendInput.text = (SendDelay = PlayerPrefs.GetFloat("AutoSendDelay", 4f)).ToString();
        KeywordInput.text = sending_keyword = PlayerPrefs.GetString("SendKeyword", "对话完毕");
        ServerIP.text = serverIp = PlayerPrefs.GetString("ASRIP", "frp-tip.com");
        ServerPort.text = (serverPort = PlayerPrefs.GetInt("ASRPort", 18267)).ToString();

        AutoSendInput.onEndEdit.AddListener(TextInput);
        KeywordInput.onEndEdit.AddListener(TextInput);
        ServerIP.onEndEdit.AddListener(TextInput);
        ServerPort.onEndEdit.AddListener(TextInput);
    }


    public PromptSave now_prompt
    {
        get
        {
            if (-1 < prompt_index && prompt_index < AllPrompts.Count) return AllPrompts[prompt_index];
            return null;
        }
    }

    public string promptWords
    {
        get
        {
            StringBuilder final = new();
            PromptSave prompt = now_prompt;
            if (prompt != null)
            {
                if (!string.IsNullOrEmpty(prompt.atmosphere))
                {
                    final.Append("对话环境:");
                    final.Append(prompt.atmosphere.Trim().Replace("\n", " "));
                    final.Append("\n");
                }
                if (prompt.hotwords != null && prompt.hotwords.Count > 0)
                {
                    final.Append("注意专有名词:");
                    final.Append(promptHotWords);
                }
            }
            return final.ToString();
        }
    }

    public string promptHotWords
    {
        get
        {
            StringBuilder final = new();
            PromptSave prompt = now_prompt;
            if (prompt != null && prompt.hotwords != null && prompt.hotwords.Count > 0)
            {
                for (int i = 0; i < prompt.hotwords.Count; i++)
                {
                    final.Append(prompt.hotwords[i]);
                    final.Append(",");
                }
                final.Append(sending_keyword);
                final.Append(",");
                final.Append(Chat.chat.CSetting.now_setting.UserName);
                final.Append(",");
                final.Append(Chat.chat.Character);
            }
            return final.ToString();
        }
    }

    public string RemovePunctuation(string input)
    {
        var punctuations = new HashSet<char>
        {
            '.', ',', '!', '?', ';', ':', '\'', '"', '(', ')', '[', ']', '{', '}', '<', '>', '/', '\\', '|', '`', '~', '@', '#', '$', '%', '^', '&', '*', '-', '_', '+', '=',
            '。', '，', '！', '？', '；', '：', '“', '”', '‘', '’', '（', '）', '【', '】', '《', '》', '、', '·', '～', '…', '—', '－'
        };
        var sb = new StringBuilder(input.Length);
        foreach (char c in input)
            if (!punctuations.Contains(c))
                sb.Append(c);
        return sb.ToString();
    }

    private void Awake()
    {
        instance = this;
        Load();
        TextLoad();

        Chat.chat.WhileCharacterChange += Load;
    }

    void Load()
    {
        var get = SLManager.instance.ImportFromJson<AllPrompt>(Chat.chat.CharacterPath, "ASR_Prompts.json");
        if (get == null) get = new();
        prompt_index = get.index;
        AllPrompts = get.prompts;
    }

    public void Save()
    {
        SLManager.instance.ExportToJson(new AllPrompt() { index = prompt_index, prompts = AllPrompts }, Chat.chat.CharacterPath, "ASR_Prompts.json");
    }

    private void InitUniMic()
    {
        if (Mic.AvailableDevices.Count == 0)
        {
            Debug.LogError("未检测到麦克风设备");
            return;
        }
        _micDevice = Mic.AvailableDevices[0];
        Debug.Log($"使用 UniMic 设备: {_micDevice.Name}, 采样率范围 [{_micDevice.MinFrequency}, {_micDevice.MaxFrequency}]");
        _micDevice.OnFrameCollected += OnAudioFrame;
    }

    private void OnAudioFrame(int frequency, int channelCount, float[] samples)
    {
        if (!isListening) return;
        if (!canRecord) return;

        if (_actualSampleRate == 0)
        {
            _actualSampleRate = frequency;
            Debug.Log($"实际录音采样率: {_actualSampleRate} Hz");
        }

        if (channelCount > 1)
        {
            float[] mono = new float[samples.Length / channelCount];
            for (int i = 0; i < mono.Length; i++)
                mono[i] = samples[i * channelCount];
            samples = mono;
        }

        _sampleAccumulator.AddRange(samples);

        int halfSpectrumSize = fftFrameSize / 2;
        if (_spectrumBuffer == null || _spectrumBuffer.Length != halfSpectrumSize)
            _spectrumBuffer = new float[halfSpectrumSize];

        while (_sampleAccumulator.Count >= fftFrameSize)
        {
            bool isLoud = false;

            float[] frame = _sampleAccumulator.Take(fftFrameSize).ToArray();
            _sampleAccumulator.RemoveRange(0, fftFrameSize);

            SimpleFFT.ComputeMagnitudeSpectrum(frame, fftFrameSize, _spectrumBuffer);
            if (_prevSpectrum != null)
            {
                float flux = 0f;
                for (int i = 0; i < halfSpectrumSize; i++)
                {
                    float diff = _spectrumBuffer[i] - _prevSpectrum[i];
                    flux += diff * diff;
                }
                float normalizedFlux = flux / halfSpectrumSize;
                _recentFluxValues.Enqueue(normalizedFlux);
                if (_recentFluxValues.Count > smoothingFrames)
                    _recentFluxValues.Dequeue();

                if (_recentFluxValues.Count == smoothingFrames)
                {
                    float mean = _recentFluxValues.Average();
                    isLoud = mean >= spectralFluxThreshold;
                }
            }
            _prevSpectrum = new float[halfSpectrumSize];
            Array.Copy(_spectrumBuffer, _prevSpectrum, halfSpectrumSize);

            switch (vadState)
            {
                case VADState.Idle:
                    if (isLoud)
                    {
                        recordingBuffer.Clear();

                        while(_preRecordBuffer.Count > 0) recordingBuffer.AddRange(_preRecordBuffer.Dequeue());
                        recordingBuffer.AddRange(frame);

                        lowVolumeTimer = 0f;
                        lastLoudTime = Time.time;
                        vadState = VADState.Recording;
                        MicrophoneState.text = "录音中...";
                        //在这里调用直接发送头
                        SendHeader();
                    }
                    else
                    {
                        _preRecordBuffer.Enqueue(frame);
                        if (_preRecordBuffer.Count > PRE_RECORD_FRAMES)
                            _preRecordBuffer.Dequeue();
                    }
                    break;

                case VADState.Recording:
                    recordingBuffer.AddRange(frame);
                    //在这里直接流式发送
                    SendAudioFrame(recordingBuffer.ToArray());
                    recordingBuffer.Clear();

                    if (isLoud)
                    {
                        lowVolumeTimer = 0f;
                        lastLoudTime = Time.time;
                    }
                    else
                    {
                        lowVolumeTimer += (float)fftFrameSize / _actualSampleRate;
                        if (lowVolumeTimer >= segmentSilenceDuration)
                        {
                            float duration = (float)recordingBuffer.Count / _actualSampleRate;
                            //Debug.Log($"结束语音片段，时长: {duration:F2}s");
                            //在这里发送结束块。
                            SendEndChunk();

                            MicrophoneState.text = "识别中....";
                            vadState = VADState.Idle;
                            isRecording = false;
                            lowVolumeTimer = 0f;
                        }
                    }
                    break;
            }
        }
    }

    private void SendHeader()
    {
        if (!_connected)
        {
            StopConnection();
            StartConnection();
        }
        byte[] promptBytes = Encoding.UTF8.GetBytes(promptWords);
        byte[] promptLenBytes = BitConverter.GetBytes(promptBytes.Length);
        send_list.Add(promptLenBytes);
        send_list.Add(promptBytes);
        byte[] sampleRateBytes = BitConverter.GetBytes(_actualSampleRate);
        byte[] channelsBytes = BitConverter.GetBytes(1);
        send_list.Add(sampleRateBytes);
        send_list.Add(channelsBytes);
    }

    private void SendAudioFrame(float[] frame)
    {
        int byteCount = frame.Length * 2;
        byte[] pcmBytes = new byte[byteCount];
        for (int i = 0; i < frame.Length; i++)
        {
            short val = (short)(Mathf.Clamp(frame[i], -1f, 1f) * 32767f);
            pcmBytes[i * 2] = (byte)(val & 0xFF);
            pcmBytes[i * 2 + 1] = (byte)((val >> 8) & 0xFF);
        }
        byte[] lenBytes = BitConverter.GetBytes(byteCount);
        send_list.Add(lenBytes);
        send_list.Add(pcmBytes);
    }

    private void SendEndChunk()
    {
        byte[] endBytes = BitConverter.GetBytes(0);
        send_list.Add(endBytes);
    }
    private IEnumerator SafeWrite(NetworkStream stream, byte[] data)
    {
        var task = stream.WriteAsync(data, 0, data.Length);
        yield return new WaitUntil(() => task.IsCompleted);
        if (task.IsFaulted)
            throw task.Exception;
    }

    private readonly List<byte[]> send_list = new();

    //常驻发送协程
    private IEnumerator SendLoop()
    {
        send_list.Clear();
        int bundleSize = 0;
        byte[] merged;
        while (!_stopLoop && _connected && _stream != null)
        {

            if (send_list.Count > 0)
            {
                lock (send_list)
                {
                    for (int i = 0; i < send_list.Count; i++)
                    {
                        bundleSize += send_list[i].Length;
                    }
                    // 计算总大小，一次性分配数组并拷贝（比 List<byte> 更快）
                    merged = new byte[bundleSize];
                    int offset = 0;
                    foreach (byte[] chunk in send_list)
                    {
                        Buffer.BlockCopy(chunk, 0, merged, offset, chunk.Length);
                        offset += chunk.Length;
                    }
                    bundleSize = 0;
                    send_list.Clear();
                }
                yield return SafeWrite(_stream, merged);
            }
            for(int i = 0; i < 5 ;i++) yield return null;
        }
    }
    // 常驻接收协程
    private IEnumerator ReceiveLoop()
    {
        byte[] lenBuf = new byte[4];
        while (!_stopLoop && _connected && _stream != null)
        {
            // 读取响应长度（阻塞但可中断）
            int bytesRead = 0;
            while (bytesRead < 4)
            {
                if (_stopLoop || !_connected) yield break;
                var readTask = _stream.ReadAsync(lenBuf, bytesRead, 4 - bytesRead);
                yield return new WaitUntil(() => readTask.IsCompleted);
                if (readTask.IsFaulted) throw readTask.Exception;
                int read = readTask.Result;
                if (read == 0)
                {
                    _connected = false;
                    throw new Exception("连接已关闭");
                }
                bytesRead += read;
            }
            int respLen = BitConverter.ToInt32(lenBuf, 0);
            if (respLen <= 0 || respLen > 1024 * 1024)
            {
                Debug.LogError("无效响应长度");
                continue;
            }
            byte[] respBuf = new byte[respLen];
            bytesRead = 0;
            while (bytesRead < respLen)
            {
                if (_stopLoop || !_connected) yield break;
                var readTask = _stream.ReadAsync(respBuf, bytesRead, respLen - bytesRead);
                yield return new WaitUntil(() => readTask.IsCompleted);
                if (readTask.IsFaulted) throw readTask.Exception;
                int read = readTask.Result;
                if (read == 0)
                {
                    _connected = false;
                    throw new Exception("连接已关闭");
                }
                bytesRead += read;
            }
            string json = Encoding.UTF8.GetString(respBuf);
            ProcessResponse(json);
        }
    }

    private void ProcessResponse(string json)
    {
        try
        {
            TranscriptionResponse resp = JsonUtility.FromJson<TranscriptionResponse>(json);
            if (resp != null && !string.IsNullOrEmpty(resp.text))
            {
                var ab = RemovePunctuation(resp.text);
                if ((ContainmentRate_String(promptWords, resp.text) > 0.5f ||
                     ContainmentRate_String(promptHotWords, resp.text) > 0.6f ||
                     promptHotWords.Contains(resp.text.Remove(resp.text.Length - 1)))
                     && ab != sending_keyword)
                {
                    Debug.LogWarning("识别结果与提示词相同，忽略");
                    MicrophoneState.text = "识别无效";
                }
                else
                {
                    OnTranscriptionText?.Invoke(CombineSentences(resp.sentences));
                    MicrophoneState.text = "识别成功";
                    recognize = true;
                    delayTime = Time.time;
                    if (resp.text.Contains(sending_keyword))
                        TruelySend(false);
                }
            }
            else
            {
                MicrophoneState.text = "识别结果为空";
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"解析响应失败: {e.Message}");
        }
    }

    private string CombineSentences(List<Sentence> sentences)
    {
        StringBuilder final = new();
        string last_emo = "";
        foreach (var s in sentences)
        {
            s.text = s.text.Replace(sending_keyword, "");
            string noPunct = RemovePunctuation(s.text);
            if (!string.IsNullOrEmpty(noPunct))
            {
                if (last_emo != s.emotion)
                {
                    final.Append("[ser:");
                    final.Append(s.emotion);
                    final.Append("]");
                    last_emo = s.emotion;
                }
                final.Append(s.text);
            }
        }
        return final.ToString();
    }

    private void CheckAutoSend()
    {
        if (recognize && !isRecording)
        {
            float timeSinceLastLoud = Time.time - lastLoudTime;
            float timeSinceLastResult = Time.time - delayTime;
            if (timeSinceLastLoud >= segmentSilenceDuration + SendDelay && timeSinceLastResult >= SendDelay)
                TruelySend(true);
        }
    }

    private void TruelySend(bool auto)
    {
        OnAutoSend?.Invoke();
        lastLoudTime = float.MaxValue;
        delayTime = float.MaxValue;
        MicrophoneState.text = auto ? "自动发送触发" : "关键词发送触发";
        recognize = false;
    }

    // --- 连接管理 ---
    private void StartConnection()
    {
        if (_client != null && _client.Connected) return;
        try
        {
            _client = new TcpClient();
            string realIP = serverIp;
            if (string.IsNullOrEmpty(realIP)) realIP = "127.0.0.1";
            _client.ConnectAsync(realIP, serverPort).Wait(5000);
            if (!_client.Connected)
            {
                Debug.LogError("连接服务器失败");
                MicrophoneState.text = "连接失败";
                return;
            }
            _stream = _client.GetStream();
            _connected = true;
            _stopLoop = false;
            StartCoroutine(SendLoop());
            StartCoroutine(ReceiveLoop());
            Debug.Log("TCP 长连接已建立");
        }
        catch (Exception e)
        {
            Debug.LogError($"连接异常: {e.Message}");
            MicrophoneState.text = "连接错误";
        }
    }

    private void StopConnection()
    {
        _stopLoop = true;
        _connected = false;
        _stream?.Close();
        _client?.Close();
        _stream = null;
        _client = null;
    }

    public void SetListening(bool enable)
    {
        if (enable == isListening) return;
        isListening = enable;
        if (isListening)
        {
            if (_micDevice == null) InitUniMic();
            if (_micDevice != null && !_micDevice.IsRecording)
            {
                int sampleRateToUse = _micDevice.SupportsAnyFrequency ? 16000 : _micDevice.MaxFrequency;
                _micDevice.StartRecording(sampleRateToUse, 100);
                _actualSampleRate = 0;
                MicrophoneState.text = "语音识别启动";
                StartConnection(); // 建立长连接
            }
            else
                Debug.LogError("麦克风设备不可用");
        }
        else
        {
            if (_micDevice != null && _micDevice.IsRecording)
                _micDevice.StopRecording();
            StopConnection();
            MicrophoneState.text = "语音识别已关闭";
        }
        vadState = VADState.Idle;
        _sampleAccumulator.Clear();
        _preRecordBuffer.Clear();
        _recentFluxValues.Clear();
        _prevSpectrum = null;
        isRecording = false;
        recognize = false;
    }

    private void Update()
    {
        if (isListening && auto_sending) CheckAutoSend();
    }

    private bool canRecord => send.interactable && !tts.IsPlaying;

    private void OnDestroy()
    {
        SetListening(false);
        if (_micDevice != null)
            _micDevice.OnFrameCollected -= OnAudioFrame;
    }

    public static double ContainmentRate_String(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s2)) return 0;
        var set1 = new HashSet<char>(s1);
        int count = 0;
        foreach (char c in s2)
            if (set1.Contains(c)) count++;
        return (double)count / s2.Length;
    }

    [Serializable]
    private class TranscriptionResponse
    {
        public string text;
        public string error;
        public List<Sentence> sentences;
    }
    [Serializable]
    private class Sentence
    {
        public string text;
        public string emotion;
    }

    private static class SimpleFFT
    {
        public static void ComputeMagnitudeSpectrum(float[] samples, int length, float[] magnitude)
        {
            int n = length;
            float[] real = new float[n];
            float[] imag = new float[n];
            Array.Copy(samples, real, n);
            int j = 0;
            for (int i = 0; i < n; i++)
            {
                if (i < j)
                {
                    (real[i], real[j]) = (real[j], real[i]);
                }
                int m = n >> 1;
                while (m >= 1 && j >= m)
                {
                    j -= m;
                    m >>= 1;
                }
                j += m;
            }
            for (int step = 2; step <= n; step <<= 1)
            {
                int half = step >> 1;
                float theta = -2.0f * Mathf.PI / step;
                float wReal = Mathf.Cos(theta);
                float wImag = Mathf.Sin(theta);
                for (int i = 0; i < n; i += step)
                {
                    float wr = 1f, wi = 0f;
                    for (int k = 0; k < half; k++)
                    {
                        int idxA = i + k;
                        int idxB = idxA + half;
                        float tr = wr * real[idxB] - wi * imag[idxB];
                        float ti = wr * imag[idxB] + wi * real[idxB];
                        real[idxB] = real[idxA] - tr;
                        imag[idxB] = imag[idxA] - ti;
                        real[idxA] += tr;
                        imag[idxA] += ti;
                        float wTemp = wr;
                        wr = wr * wReal - wi * wImag;
                        wi = wTemp * wImag + wi * wReal;
                    }
                }
            }
            for (int i = 0; i < n / 2; i++)
                magnitude[i] = Mathf.Sqrt(real[i] * real[i] + imag[i] * imag[i]);
        }
    }
}