using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json; // 需要安装Newtonsoft.Json包using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;

[System.Serializable]
public class DeepSeekMessage
{
    [JsonProperty("role")]
    public string role;

    [JsonProperty("content")]
    public string content;

    [JsonProperty("reasoning_content", NullValueHandling = NullValueHandling.Ignore)]
    public string reasoning_content;

    [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
    public string name;

    [JsonProperty("tool_call_id", NullValueHandling = NullValueHandling.Ignore)]
    public string tool_call_id;

    // 直接使用公共字段，配合ShouldSerialize
    [JsonProperty("tool_calls", NullValueHandling = NullValueHandling.Ignore)]
    public List<ToolCall> tool_calls;

    [JsonConstructor]
    public DeepSeekMessage() { }

    public DeepSeekMessage(string role, string content)
    {
        this.role = role;
        this.content = content;
    }
    public DeepSeekMessage(string role, string reasoning_content, string content)
    {
        this.role = role;
        this.content = content;
        this.reasoning_content = reasoning_content;
    }
    public bool ShouldSerializename()
    {
        // 只有当name有值且角色不是"tool"时才序列化
        return !string.IsNullOrEmpty(name) && role != "tool";
    }

    // 关键：这个方法告诉Newtonsoft.Json何时序列化tool_calls
    public bool ShouldSerializetool_calls()
    {
        return tool_calls != null && tool_calls.Count > 0;
    }

    public bool ShouldSerializetool_call_id()
    {
        return !string.IsNullOrEmpty(tool_call_id);
    }
}

// 为了更好的类型安全，可以创建专门的派生类
[System.Serializable]
public class UserMessage : DeepSeekMessage
{
    public UserMessage(string content,string name = "") : base("user", content) { }
}

[System.Serializable]
public class AssistantMessage : DeepSeekMessage
{
    public AssistantMessage(string content = null, string reasioning_content = null, List<ToolCall> toolCalls = null)
        : base("assistant", reasioning_content, content)
    {
        this.tool_calls = toolCalls;
    }
}

[System.Serializable]
public class ToolMessage : DeepSeekMessage
{
    public ToolMessage(string toolCallId, string content)
        : base("tool", content)
    {
        this.tool_call_id = toolCallId;
    }
}

// 同时修正ToolCalls类（注意大小写和字段名）
[System.Serializable]
public class ToolCall
{
    [JsonProperty("index")]
    public int index;
    [JsonProperty("id")]
    public string id;

    [JsonProperty("type")]
    public string type = "function";

    [JsonProperty("function")]
    public FunctionCall function;
}

[System.Serializable]
public class FunctionCall
{
    [JsonProperty("name")]
    public string name;

    [JsonProperty("arguments")]
    public string arguments;
}
[System.Serializable]
public class DeepSeekRequest
{
    public string model = "deepseek-v4-flash";
    public double temperature = 0.7;
    public int max_tokens = 2048;
    public bool stream = false;
    public string reasoning_effort;
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ThinkingConfig thinking = new(false);

    public bool ShouldSerializethinking()
    {
        return thinking != null && thinking.type == "enabled";
    }

    public bool ShouldSerializereasoning_effort()
    {
        return !string.IsNullOrEmpty(reasoning_effort);
    }


    [HideInInspector]
    public List<DeepSeekMessage> messages;
    public List<Tool> tools = new();
    public string tool_choice = "auto";
    //新词度-2~2
    public float frequency_penalty = 0f;
    //新话题度-2~2
    public float presence_penalty = 0f;

    public DeepSeekRequest(DeepSeekRequest with_out_message, List<DeepSeekMessage> messages)
    {
        model = with_out_message.model;
        temperature = with_out_message.temperature;
        tools = with_out_message.tools;
        stream = with_out_message.stream;
        max_tokens = with_out_message.max_tokens;
        frequency_penalty = with_out_message.frequency_penalty;
        presence_penalty = with_out_message.presence_penalty;
        thinking = with_out_message.thinking;
        this.messages = messages;
    }
    public DeepSeekRequest()
    { }

    public DeepSeekRequest DeepCopy()
    {
        // 使用 Newtonsoft.Json 深拷贝（独立副本，修改不影响原对象）
        string json = JsonConvert.SerializeObject(this);
        return JsonConvert.DeserializeObject<DeepSeekRequest>(json);
    }
}

// 添加ThinkingConfig类
[System.Serializable]
public class ThinkingConfig
{
    public string type;
    public ThinkingConfig (bool t)
    {
        type = t ? "enabled" : "disabled";
    }
    public bool ToBool()
    {
        return type == "enabled";
    }
}
[System.Serializable]
public class DeepSeekResponse
{
    public Choice[] choices;

    [System.Serializable]
    public class Choice
    {
        public Message message;

        [System.Serializable]
        public class Message
        {
            public string role;
            public string reasoning_content;
            public string content;
            public List<ToolCall> tool_calls;
        }
    }
}

[System.Serializable]
public class DeepSeekChunk
{
    public string id;
    public string @object;  // 注意：object 是 C# 关键字，用 @ 转义
    public int created;
    public string model;
    public string system_fingerprint;
    public Choice[] choices;

    [System.Serializable]
    public class Choice
    {
        public int index;
        public Delta delta;  // 流式响应用的是 delta，不是 message
        public string finish_reason;
        public object logprobs;  // 通常为 null
    }

    [System.Serializable]
    public class Delta
    {
        public string role;  // 仅第一个 chunk 有
        public string content;
        public string reasoning_content;
        public List<ToolCall> tool_calls;  // 如果有工具调用
    }
}
// 新增：Tool类定义
[Serializable]
public class Tool
{
    public string type = "function";
    public Function function;
}

[Serializable]
public class Function
{
    public string name;
    public string description;
    public object parameters;
}

public struct Addition
{
    public string context;
    public AITask task;

    public Addition(string context = "", AITask task = null)
    {
        this.context = context;
        this.task = task;
    }

    public string GetRealAddition()
    {
        StringBuilder final_addition = new();
        if (!string.IsNullOrEmpty(context))
        {
            final_addition.Append(context);
            final_addition.Append("\n");
            context = "";
        }
        if (task != null)
        {
            final_addition.Append(task.GetAllTasks());
        }
        if (final_addition.Length != 0)
        {
            final_addition.Insert(0, "<only_ai>");
            final_addition.Append("</only_ai>");
        }
        return final_addition.ToString();
    }
}
public class RequestInfo
{
    public DeepSeekRequest request;
    public List<DeepSeekMessage> messages;

    public Action<string> onChunk;
    public Action<string> onResponse;
    public Action<string> onError;

    public Itool toolkit;
    public bool back_tool = true;
    public bool useAuxApi = false;

    public void AddMessages(List<DeepSeekMessage> message)
    {
        messages.AddRange(message);
        request.messages.AddRange(message);
    }
    public void AddMessages(DeepSeekMessage message)
    {
        messages.Add(message);
        request.messages.Add(message);
    }
    /// <summary>
    /// 注意request是实际发送的messages和messages不同
    /// </summary>
    /// <param name="request"></param>
    /// <param name="messages"></param>
    /// <param name="onChunk"></param>
    /// <param name="onResponse"></param>
    /// <param name="onError"></param>
    /// <param name="toolkit"></param>
    /// <param name="back_tool"></param>
    public RequestInfo(DeepSeekRequest request, List<DeepSeekMessage> messages, Action<string> onChunk,Action<string> onResponse,Action<string> onError ,Itool toolkit = null,bool back_tool = true)
    {
        this.request = request;
        this.messages = messages;
        this.onChunk = onChunk;
        this.onResponse = onResponse;
        this.onError = onError;
        this.toolkit = toolkit;
        this.back_tool = back_tool;
    }
}
public class DeepSeekAPIManager : MonoBehaviour
{
    public string apiUrl = "https://api.deepseek.com/v1/chat/completions";
    public string apiKey = "YOUR_API_KEY_HERE"; // 替换为你的API密钥

    [Header("辅助 API（记忆系统）")]
    public string auxApiUrl = "https://api.deepseek.com/v1/chat/completions";
    public string auxApiKey = "";

    public int MaxTokens;
    [NonReorderable]
    public DeepSeekRequest requestData;

    public string 系统消息;
    [NonReorderable]
    public string 角色设定;

    public MainAITool useTool;

    public Chat chat;

    private void Awake()
    {
        requestData.tools = useTool.all_tools;
        auxApiUrl = PlayerPrefs.GetString("AuxApiUrl", auxApiUrl);
        auxApiKey = SecureStorage.Unprotect(PlayerPrefs.GetString("AuxApiKeyEncrypted", ""));
    }

    List<DeepSeekMessage> reuseList = new();
    //代表从message的第几项开始引用
    public int reuseFrom = -1;
    private int last_MaxToken;

    #region 固定头
    public int pinnedMessageCount = 300;              // 期望固定的消息条数
    private List<DeepSeekMessage> cachedPinnedHead = new();   // 固定头缓存
    private int pinnedHeadOffset = -1;                // 占用了当前 messages 前多少条

    /// <summary>
    /// 构建固定头，计算 pinnedHeadOffset。
    /// 请在首次需要时调用一次，之后直接使用 cachedPinnedHead 和 pinnedHeadOffset。
    /// </summary>
    private void BuildPinnedHead(List<DeepSeekMessage> currentMessages)
    {
        pinnedHeadOffset = -1;
        List<DeepSeekMessage> fullHead = new List<DeepSeekMessage>();
        int remaining = pinnedMessageCount;
        string splitedPath = Path.Combine(Application.persistentDataPath, chat.CharacterPath, "SplitedMessages");
        int fileIndex = 0;

        // 1. 从分片存档加载（0_message 最老）
        while (remaining > 0)
        {
            string filePath = Path.Combine(splitedPath, $"{fileIndex}_message.json");
            if (!File.Exists(filePath)) break;
            var wrapper = SLManager.instance.ImportFromJson<ConversationHistoryWrapper>(filePath);
            if (wrapper?.messages == null) break;
            var part = wrapper.messages;
            if (part.Count <= remaining)
            {
                fullHead.InsertRange(0, part);     // 更老的放前面
                remaining -= part.Count;
            }
            else
            {
                fullHead.InsertRange(0, part.Take(remaining));
                remaining = 0;
            }
            fileIndex++;
        }

        // 2. 不足则从当前 messages 头部补
        int useFromCurrent = 0;
        if (remaining > 0 && currentMessages.Count > 0)
        {
            useFromCurrent = Mathf.Min(remaining, currentMessages.Count);
            fullHead.AddRange(currentMessages.Take(useFromCurrent));
        }

        // 3. 安全截断
        cachedPinnedHead = fullHead;//GetSafeTruncation(fullHead);

        // 4. 计算实际占用当前 messages 的数量
        if (useFromCurrent > 0 && cachedPinnedHead != null)
        {
            // 截断后，尾部最多可能全部来自 currentMessages，保守计算 min
            int countFromCurrent = Mathf.Min(useFromCurrent, cachedPinnedHead.Count);
            pinnedHeadOffset = countFromCurrent;
        }
        else
        {
            pinnedHeadOffset = -1;   // 表示固定头全部来自存档，未占用当前消息
        }
    }

    /// <summary>
    /// 安全截断：确保末尾没有未完成的工具调用
    /// </summary>
    private List<DeepSeekMessage> GetSafeTruncation(List<DeepSeekMessage> messages)
    {
        if (messages.Count == 0) return messages;
        Dictionary<string, int> toolStates = new Dictionary<string, int>();
        int lastSafeIndex = -1;

        for (int i = 0; i < messages.Count; i++)
        {
            var msg = messages[i];
            if (msg.role == "assistant" && msg.tool_calls != null)
            {
                foreach (var tc in msg.tool_calls)
                    if (!string.IsNullOrEmpty(tc.id))
                        toolStates[tc.id] = 0;
            }
            else if (msg.role == "tool" && !string.IsNullOrEmpty(msg.tool_call_id) && toolStates.ContainsKey(msg.tool_call_id))
            {
                toolStates[msg.tool_call_id] = 1;
            }

            // 当所有已发起的调用都已完成时，记录这个位置
            if (toolStates.Count > 0 && toolStates.All(kv => kv.Value == 1))
                lastSafeIndex = i;
        }

        // 如果末尾本就完全闭合，直接返回整个列表
        if (lastSafeIndex == messages.Count - 1)
            return messages;

        // 否则回退到最后一个安全点
        if (lastSafeIndex >= 0)
            return messages.Take(lastSafeIndex + 1).ToList();

        // 整个列表都未闭合，极端情况，返回空
        Debug.LogWarning("固定头无法找到工具调用闭环，已清空");
        return new List<DeepSeekMessage>();
    }

    #endregion

    public void OwnAddMessageSend(DeepSeekMessage message)
    {
        if (message.name != "" && message.role == "user")
        {
            message.content = $"<user_name:{message.name}>" + message.content;
        }
        chat.messages.Add(message);
        OwnChatSend();
    }
    readonly string roleplay_prompt = @"
【角色沉浸要求】在你的思考过程（<ai_think>标签内）中，请遵守以下规则：
1. 请以角色第一人称进行内心独白，用括号包裹内心活动，例如""（心想：……）""或""(内心OS：……)""
2. 用第一人称描写角色的内心感受，例如""我心想""""我觉得""""我暗自""等
3. 对复杂问题的内心推理，例如""让我好好思考一下。""""一步要怎么做?""
4. 思考内容应沉浸在角色中，通过内心独白分析剧情和规划回复";

    readonly int InsertCount = 3;
    public void OwnChatSend()
    {

        if (cachedPinnedHead.Count == 0)
        {
            //尝试生成头
            BuildPinnedHead(chat.messages);

            // 固定头 token 超限警告（每角色一次）
            int pinnedTokens = 0;
            foreach (var m in cachedPinnedHead)
            {
                pinnedTokens += TokenEstimator.EstimateTokensCached(m.content);
                if (!string.IsNullOrEmpty(m.reasoning_content))
                    pinnedTokens += TokenEstimator.EstimateTokensCached(m.reasoning_content);
            }
            Debug.Log($"[固定头] {cachedPinnedHead.Count}条 {pinnedTokens} tokens / MaxTokens={MaxTokens} ({pinnedTokens*100/Math.Max(1,MaxTokens)}%)");
            if (pinnedTokens > MaxTokens * 0.9f)
            {
                string warnKey = $"PinnedTokenWarn_{chat.Character}";
                if (PlayerPrefs.GetInt(warnKey, 0) == 0)
                {
                    PlayerPrefs.SetInt(warnKey, 1);
                    PopUp.instance.SetConfirm(null,
                        $"固定消息占用 {pinnedTokens} tokens，超过上限 {MaxTokens} 的90%！\n请减少固定消息条数或增加最大处理Token。");
                }
            }
        }
        var cutoff = chat.messages.Skip(Mathf.Max(0, pinnedHeadOffset)).ToList();
        //限制Token后的聊天
        int add = cutoff.Count - reuseFrom - reuseList.Count + InsertCount + cachedPinnedHead.Count;
        if (reuseFrom == -1 || add > 20 || add < 0 || last_MaxToken != MaxTokens || reuseList.Count == 0)
        {
            if (add < 0 || reuseFrom < 0 || last_MaxToken != MaxTokens)
            {
                reuseList = GetOptimizedContext(cutoff, true, -1);
                reuseFrom = cutoff.Count - reuseList.Count;
                if (cachedPinnedHead.Count != 0)
                {
                    reuseList.InsertRange(0, cachedPinnedHead);
                    reuseList.Insert(cachedPinnedHead.Count, new DeepSeekMessage("system", "以上是固定对话历史，以下是最近的对话"));
                }
                print("新缓存");
            }
            else
            {
                reuseList.AddRange(GetOptimizedContext(cutoff.GetRange(reuseFrom, cutoff.Count - reuseFrom), true, -2));
                print("延续缓存");
            }
            last_MaxToken = MaxTokens;
            var roleplay = chat.CSetting.now_setting.roleplayMode ? roleplay_prompt : "";
            InsertUserMessages(reuseList, new List<string>() { 角色设定 + roleplay }, Math.Max(0, reuseList.Count - 2));
            InsertUserMessages(reuseList, new List<string>() { 角色设定 + roleplay });
            InsertSystemMessages(reuseList, new List<string>() { 系统消息 });
        }
        add = cutoff.Count - reuseFrom - reuseList.Count + InsertCount + cachedPinnedHead.Count;
        print($"截断剩余{reuseList.Count - InsertCount - cachedPinnedHead.Count}");
        print($"增量{add}");
        if (add > 0)
        {
            reuseList.AddRange(GetOptimizedContext(cutoff.GetRange(cutoff.Count - add, add), true, -2));
        }
        
        SendRequest(new RequestInfo(new DeepSeekRequest(requestData, reuseList), chat.messages, chat.OnChunk, chat.OnResponse, chat.OnError, chat.useTool, true));
    }

    public void SendRequest(RequestInfo requestInfo)
    {
        var settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = null  // 保持原字段名
            }
        };
        // 使用Newtonsoft.Json序列化

        var url = requestInfo.useAuxApi ? auxApiUrl : apiUrl;
        var key = requestInfo.useAuxApi ? auxApiKey : apiKey;

        // 非 DeepSeek 模型去掉专属/不支持字段
        bool isDeepSeek = requestInfo.request.model.ToLower().Contains("deepseek");
        if (!isDeepSeek)
        {
            requestInfo.request.thinking = null;
            requestInfo.request.reasoning_effort = null;
            requestInfo.request.tools = null;
            requestInfo.request.tool_choice = null;
        }

        // 统一 reasoning_content：thinking 开启时补空，关闭时清掉
        bool thinkingOn = requestInfo.request.thinking != null && requestInfo.request.thinking.type == "enabled";
        foreach (var msg in requestInfo.request.messages)
        {
            if (msg.role == "assistant")
            {
                if (thinkingOn && string.IsNullOrEmpty(msg.reasoning_content))
                    msg.reasoning_content = "";
                else if (!thinkingOn)
                    msg.reasoning_content = null;
            }
        }

        string jsonData = JsonConvert.SerializeObject(requestInfo.request, settings);

        //SLManager.instance.ExportToJson(requestInfo.request, "", $"api_debug_{System.DateTime.Now:HH-mm-ss}");
        //Debug.Log($"[API] {(requestInfo.useAuxApi ? "辅助" : "主")} URL={url} Model={requestInfo.request.model}");

        //判断流式
        if (requestInfo.request.stream)
        {
            StartCoroutine(SendingStream(jsonData, requestInfo));
        }
        else
        {
            StartCoroutine(Sending(jsonData, requestInfo));
        }
    }

    IEnumerator Sending(string jsonData, RequestInfo requestInfo)
    {
        // 创建UnityWebRequest
        var url = requestInfo.useAuxApi ? auxApiUrl : apiUrl;
        var key = requestInfo.useAuxApi ? auxApiKey : apiKey;
        UnityWebRequest request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);

        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", $"Bearer {key}");

        // 发送请求
        yield return request.SendWebRequest();

        // 处理响应
        if (request.result == UnityWebRequest.Result.Success)
        {
            string responseJson = request.downloadHandler.text;
            var response = JsonUtility.FromJson<DeepSeekResponse>(responseJson);

            if (response.choices != null && response.choices.Length > 0)
            {
                var back_message = response.choices[0].message;
                string aiResponse = back_message.content;
                string after_thinking = back_message.reasoning_content;
                //if (!string.IsNullOrEmpty(after_thinking)) print("思考:" + after_thinking);

                if (back_message.tool_calls != null && back_message.tool_calls.Count > 0)
                {
                    // 把AI的回复加入对话记录
                    requestInfo.AddMessages(new AssistantMessage(reasioning_content: FormatCorrect(after_thinking), content: FormatCorrect(aiResponse), toolCalls: back_message.tool_calls));

                    // 关键修改：等待工具调用完成
                    yield return StartCoroutine(requestInfo.toolkit.DealToolCallsCoroutine(back_message.tool_calls, (newMessages) =>
                    {
                        if (newMessages != null && newMessages.Count > 0)
                        {
                            if (requestInfo.back_tool)
                            {
                                requestInfo.AddMessages(newMessages);
                                SendRequest(requestInfo);
                            }
                            else
                            {
                                requestInfo.onResponse?.Invoke(aiResponse);
                            }
                        }
                    }));
                }
                else
                {
                    // 把AI的回复加入对话记录
                    requestInfo.messages?.Add(new DeepSeekMessage("assistant", FormatCorrect(after_thinking), FormatCorrect(aiResponse)));
                    requestInfo.onResponse?.Invoke(aiResponse);
                }
            }
            else
            {
                requestInfo.onError?.Invoke("No response from AI");
            }
        }
        else
        {
            requestInfo.onError?.Invoke($"API Error: {request.error}");
        }
        request.Dispose();
    }

    public bool StreamIsGetting = false;

    public string FormatCorrect(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        string pattern = @"\[t+s*_([^\]]+)\]";
        
        return Regex.Replace(input, pattern, "[tts_$1]");
    }
    IEnumerator SendingStream(string jsonData, RequestInfo requestInfo)
    {
        //SLManager.instance.ExportToJson(jsonData, "", System.DateTime.Now.ToShortTimeString().Replace(":", "-"));
        var url = requestInfo.useAuxApi ? auxApiUrl : apiUrl;
        var key = requestInfo.useAuxApi ? auxApiKey : apiKey;
        UnityWebRequest request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);

        StringBuilder final_content = new StringBuilder();
        StringBuilder final_thinking = new StringBuilder();

        // 工具调用相关缓存
        Dictionary<int, StringBuilder> toolCallArguments = new Dictionary<int, StringBuilder>();
        Dictionary<int, ToolCall> toolCalls = new Dictionary<int, ToolCall>();
        bool hasToolCalls = false;

        StreamIsGetting = true;

        requestInfo.messages.Add(new DeepSeekMessage("assistant", final_thinking.ToString(), final_content.ToString()));

        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new StreamDownloadHandler(
            onDataReceived: (jsonData) => {
                // 处理每个 chunk
                try
                {
                    var chunk = JsonConvert.DeserializeObject<DeepSeekChunk>(jsonData);
                    if (chunk.choices != null && chunk.choices.Length > 0)
                    {
                        var delta = chunk.choices[0].delta;
                        var finish_reason = chunk.choices[0].finish_reason;

                        // 1. 处理普通文本内容
                        if (!string.IsNullOrEmpty(delta.content))
                        {
                            final_content.Append(delta.content);
                            requestInfo.messages[^1].content = final_content.ToString();
                            requestInfo.onChunk?.Invoke(delta.content); // 实时输出
                        }

                        // 2. 处理思考内容
                        if (!string.IsNullOrEmpty(delta.reasoning_content))
                        {
                            final_thinking.Append(delta.reasoning_content);
                            requestInfo.messages[^1].reasoning_content = final_thinking.ToString();
                            // 可选择是否实时输出思考过程
                            //Debug.Log("[思考] "+ delta.reasoning_content);
                            //requestInfo.onChunk?.Invoke("[思考] " + delta.reasoning_content);
                        }

                        // 3. 处理工具调用
                        if (delta.tool_calls != null && delta.tool_calls.Count > 0)
                        {
                            hasToolCalls = true;

                            foreach (var tc in delta.tool_calls)
                            {
                                int index = tc.index;

                                // 初始化或更新工具调用信息
                                if (!toolCalls.ContainsKey(index))
                                {
                                    toolCalls[index] = new ToolCall
                                    {
                                        id = tc.id,
                                        type = tc.type,
                                        function = new FunctionCall
                                        {
                                            name = tc.function?.name ?? "",
                                            arguments = ""
                                        }
                                    };
                                    toolCallArguments[index] = new StringBuilder();
                                }

                                // 累积 arguments
                                if (tc.function != null && !string.IsNullOrEmpty(tc.function.arguments))
                                {
                                    toolCallArguments[index].Append(tc.function.arguments);
                                    toolCalls[index].function.arguments = toolCallArguments[index].ToString();
                                }
                            }
                        }

                        // 4. 检查结束状态
                        if (finish_reason == "stop")
                        {
                            // 正常结束，没有工具调用
                            // 在 onComplete 中处理
                        }
                        else if (finish_reason == "tool_calls")
                        {
                            // 工具调用结束，需要执行工具
                            // 注意：这里不能在流式回调中直接启动协程，需要延迟处理
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"解析chunk失败: {e.Message}\n数据: {jsonData}");
                    requestInfo.onError?.Invoke($"解析chunk失败: {e.Message}");
                }
            },
            onError: (error) => {
                StreamIsGetting = false;
                requestInfo.onError?.Invoke(error);
            },
            onComplete: () => {
                StreamIsGetting = false;

                // 判断是否有工具调用
                if (hasToolCalls && toolCalls.Count > 0)
                {
                    // 将工具调用信息添加到消息中
                    var toolCallsList = new List<ToolCall>(toolCalls.Values);

                    // 创建带工具调用的 AssistantMessage
                    var assistantMessage = new AssistantMessage(
                            reasioning_content: FormatCorrect(final_thinking.ToString()),
                            content: FormatCorrect(final_content.ToString()),
                            toolCalls: toolCallsList
                        );

                    // 替换最后一条消息（之前添加的占位消息）
                    if (requestInfo.messages.Count > 0 && requestInfo.messages[^1].role == "assistant")
                    {
                        requestInfo.messages[^1] = assistantMessage;
                        requestInfo.request.messages.Add(assistantMessage);
                    }

                    // 执行工具调用
                    // 注意：需要启动协程，这里使用 Unity 的 MonoBehaviour 来启动
                    if (requestInfo.toolkit!= null)
                    {
                        // 假设 useTool 是 MonoBehaviour，通过回调执行
                        var toolHandler = requestInfo.toolkit; // 获取工具处理器
                        StartCoroutine(
                                toolHandler.DealToolCallsCoroutine(toolCallsList, (newMessages) =>
                                {
                                    print("工具结果"+newMessages[0].content);
                                    if (newMessages != null && newMessages.Count > 0)
                                    {
                                        if (requestInfo.back_tool)
                                        {
                                            requestInfo.AddMessages(newMessages);
                                            print(requestInfo.request.messages[^1]);
                                            SendRequest(requestInfo);
                                        }
                                        else
                                        {
                                            requestInfo.onResponse?.Invoke("");
                                        }
                                    }
                                })
                            );
                    }
                    else
                    {
                        Debug.LogError("useTool 未初始化");
                        requestInfo.onError?.Invoke("工具处理器未初始化");
                    }
                }
                else
                {
                    // 没有工具调用，正常完成
                    var assistantMessage = new DeepSeekMessage(
                            "assistant",
                            FormatCorrect(final_thinking.ToString()),
                            FormatCorrect(final_content.ToString())
                        );

                    // 替换最后一条消息（之前添加的占位消息）
                    if (requestInfo.messages.Count > 0 && requestInfo.messages[^1].role == "assistant")
                    {
                        requestInfo.messages[^1] = assistantMessage;
                    }
                    // 回调完整内容
                    requestInfo.onResponse?.Invoke(final_content.ToString());
                }

                // 清理缓存
                toolCallArguments.Clear();
                toolCalls.Clear();
            }
        );

        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", $"Bearer {key}");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            StreamIsGetting = false;
            requestInfo.onError?.Invoke($"API Error: {request.error}");
        }

        request.Dispose();
    }
    /// <summary>
    /// 限制上下文长度
    /// </summary>
    /// <param name="_message">完整消息</param>
    /// <param name="MaxTokens">值为-1时为默认MaxToken</param>
    /// <param name="systemMessage">不写默认原始系统消息</param>
    /// <param name="characterSetting">不写默认原始角色设定消息</param>
    /// <returns></returns>
    public List<DeepSeekMessage> GetOptimizedContext(List<DeepSeekMessage> _message, bool thinking, int MaxTokens = -1)
    {
        //MaxTokens == -2 代表全部取用
        if (MaxTokens == -1) MaxTokens = this.MaxTokens;

        List<DeepSeekMessage> optimized = new List<DeepSeekMessage>();
        int estimatedTokens = 0;

        // 用于跟踪需要寻找的assistant消息（可能有多个tool调用）
        HashSet<string> pendingToolCallIds = new HashSet<string>();
        bool needToFindAssistant = false;

        int system_messageCount = 0;

        // 从最新消息开始添加（倒序遍历）
        for (int i = _message.Count - 1; i >= 0; i--)
        {
            // 复制保证不影响原来的列表
            bool keepReasoning = (chat != null && chat.CSetting != null && chat.CSetting.now_setting != null)
                                 ? chat.CSetting.now_setting.KeepReasoningContent
                                 : false;
            var msg = new DeepSeekMessage()
            {
                role = _message[i].role,
                content = _message[i].content,
                reasoning_content = keepReasoning ? _message[i].reasoning_content : null,
                name = _message[i].name,
                tool_call_id = _message[i].tool_call_id,
                tool_calls = _message[i].tool_calls,
            };

            int messageTokens = TokenEstimator.EstimateTokensCached(msg.content);

            // 如果是tool消息，记录需要寻找对应的assistant
            if (msg.role == "tool" && !string.IsNullOrEmpty(msg.tool_call_id))
            {
                needToFindAssistant = true;
                pendingToolCallIds.Add(msg.tool_call_id);
                optimized.Insert(system_messageCount, msg);
                estimatedTokens += messageTokens;
                continue;
            }

            // 正在寻找对应的assistant时
            if (needToFindAssistant)
            {
                bool isTargetAssistant = msg.role == "assistant" && msg.tool_calls != null;

                if (isTargetAssistant)
                {
                    var calledToolIds = msg.tool_calls
                        .Where(tc => !string.IsNullOrEmpty(tc.id))
                        .Select(tc => tc.id)
                        .ToList();

                    bool hasPendingTool = calledToolIds.Any(id => pendingToolCallIds.Contains(id));

                    if (hasPendingTool)
                    {
                        optimized.Insert(system_messageCount, msg);
                        estimatedTokens += messageTokens;

                        foreach (var toolId in calledToolIds)
                        {
                            pendingToolCallIds.Remove(toolId);
                        }

                        if (pendingToolCallIds.Count == 0)
                        {
                            needToFindAssistant = false;
                        }
                    }
                    else
                    {
                        // 不是我们要找的assistant，但仍需保留（对话完整性）
                        optimized.Insert(system_messageCount, msg);
                        estimatedTokens += messageTokens;
                    }
                }
                else
                {
                    // 还没找到对应的assistant，保留中间消息
                    optimized.Insert(system_messageCount, msg);
                    estimatedTokens += messageTokens;
                }
                continue;
            }

            // 正常情况：检查token限制
            if (estimatedTokens + messageTokens <= MaxTokens || MaxTokens == -2)
            {
                optimized.Insert(system_messageCount, msg);
                estimatedTokens += messageTokens;
            }
            else
            {
                break;
            }
        }

        // ==================== 修复点：清理孤儿tool消息 ====================
        if (pendingToolCallIds.Count > 0)
        {
            optimized.RemoveAll(m =>
                m.role == "tool" &&
                !string.IsNullOrEmpty(m.tool_call_id) &&
                pendingToolCallIds.Contains(m.tool_call_id));

            estimatedTokens = optimized.Sum(m => TokenEstimator.EstimateTokensCached(m.content));
        }

        // ==================== thinking模式：无reasoning的assistant-tool对 → assistant-user ====================
        if (thinking)
        {
            // 1. 收集所有"无reasoning"的assistant所调用的tool_call_id
            HashSet<string> toolIdsFromNonReasoningAssistant = new HashSet<string>();
            // 同时直接标记需要清理tool_calls的assistant消息
            List<DeepSeekMessage> assistantsToClean = new List<DeepSeekMessage>();

            foreach (var msg in optimized)
            {
                if (msg.role == "assistant" &&
                    string.IsNullOrEmpty(msg.reasoning_content) &&
                    msg.tool_calls != null &&
                    msg.tool_calls.Count > 0)
                {
                    // 记录这个assistant
                    assistantsToClean.Add(msg);

                    // 收集其调用的所有tool id
                    foreach (var tc in msg.tool_calls)
                    {
                        if (!string.IsNullOrEmpty(tc.id))
                        {
                            toolIdsFromNonReasoningAssistant.Add(tc.id);
                        }
                    }
                }
            }

            // 2. 将匹配的 tool 消息转换为 user 消息
            foreach (var msg in optimized)
            {
                if (msg.role == "tool" &&
                    !string.IsNullOrEmpty(msg.tool_call_id) &&
                    toolIdsFromNonReasoningAssistant.Contains(msg.tool_call_id))
                {
                    msg.role = "user";
                    msg.tool_call_id = null;  // user角色不需要此字段
                                              // content 保持不变
                }
            }

            // 3. 清除对应assistant的tool_calls，使其成为普通文本消息
            foreach (var assistantMsg in assistantsToClean)
            {
                assistantMsg.tool_calls = null;   // 或 new List<ToolCall>()，根据你的序列化要求
            }
        }

        return optimized;
    }
    /// <summary>
    /// 插入系统消息
    /// </summary>
    /// <param name="optimized">输入的列表</param>
    /// <param name="all">要插入的系统消息</param>
    /// <param name="lastcount">导数第几个插入</param>
    public void InsertSystemMessages(List<DeepSeekMessage> optimized, List<string> all)
    {
        for (int i = all.Count - 1; i > -1; i--)
        {
            optimized.Insert(0, new DeepSeekMessage("system", all[i]));
        }
    }
    public void InsertUserMessages(List<DeepSeekMessage> optimized, List<string> all ,int index = 0)
    {
        for (int i = all.Count - 1; i > -1; i--)
        {
            optimized.Insert(index, new DeepSeekMessage("user", all[i]));
        }
    }
    public static class TokenEstimator
    {
        private static Dictionary<string, int> tokenCache = new Dictionary<string, int>();

        public static int EstimateTokensCached(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;

            // 检查缓存
            if (tokenCache.TryGetValue(text, out int cached))
                return cached;

            // 计算并缓存
            int tokens = QuickEstimate(text);
            tokenCache[text] = tokens;

            return tokens;
        }

        private static int QuickEstimate(string text)
        {
            // 简化版本，更快
            int chineseCount = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c >= 0x4E00 && c <= 0x9FFF)
                    chineseCount++;
            }

            return (chineseCount * 5 + (text.Length - chineseCount)) / 2;
        }
    }
}

public class StreamDownloadHandler : DownloadHandlerScript
{
    private Action<string> onDataReceived;
    private Action<string> onError;
    private Action onComplete;
    private System.Text.StringBuilder buffer = new System.Text.StringBuilder();

    public StreamDownloadHandler(Action<string> onDataReceived, Action<string> onError, Action onComplete)
        : base(new byte[4096]) // 4KB 缓冲区
    {
        this.onDataReceived = onDataReceived;
        this.onError = onError;
        this.onComplete = onComplete;
    }

    protected override bool ReceiveData(byte[] data, int dataLength)
    {
        if (data == null || dataLength == 0) return false;

        // 将接收到的字节转换为字符串
        string text = System.Text.Encoding.UTF8.GetString(data, 0, dataLength);
        buffer.Append(text);

        // 按行处理数据
        string currentBuffer = buffer.ToString();
        int lineEnd;
        while ((lineEnd = currentBuffer.IndexOf('\n')) >= 0)
        {
            string line = currentBuffer.Substring(0, lineEnd).Trim();
            currentBuffer = currentBuffer.Substring(lineEnd + 1);

            if (line.StartsWith("data: "))
            {
                string jsonData = line.Substring(6);
                if (jsonData == "[DONE]")
                {
                    //onComplete?.Invoke();
                }
                else
                {
                    onDataReceived?.Invoke(jsonData);
                }
            }
        }

        buffer.Clear();
        buffer.Append(currentBuffer);

        return true;
    }

    protected override void CompleteContent()
    {
        // 处理最后可能剩余的缓冲数据
        if (buffer.Length > 0)
        {
            string remaining = buffer.ToString();
            if (remaining.StartsWith("data: "))
            {
                string jsonData = remaining.Substring(6);
                if (jsonData != "[DONE]")
                {
                    onDataReceived?.Invoke(jsonData);
                }
            }
        }
        onComplete?.Invoke();
    }
}