using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SQLite;
using UnityEngine;
using Newtonsoft.Json;
using System.Collections;
using System.Text.RegularExpressions;

public class MemorySystem : MonoBehaviour,Itool
{
    public Chat selfChat;
    private MemNodes Nodes;
    string _nowPath;

    [Header("Rename Node")]
    public bool doRename = false;
    public string renameOldName;
    public string renameNewName;
    [Header("Merge Nodes")]
    public bool doMerge = false;
    public string mergeTarget;
    public string[] mergeSources;
    [Header("Delete Node")]
    public bool doDelete = false;
    public string deleteNodeName;

    private void Start()
    {
        LoadMemory();
        selfChat.WhileCharacterChange += LoadMemory;
    }

    void Update()
    {
        if (doRename)
        {
            doRename = false;  // 只执行一次
            if (!string.IsNullOrEmpty(renameOldName) && !string.IsNullOrEmpty(renameNewName))
            {
                bool ok = Nodes.RenameNode(renameOldName, renameNewName);
                if (ok)
                {
                    SaveMemNode();  // 持久化节点数据
                    Debug.Log($"重命名成功：{renameOldName} → {renameNewName}");
                }
            }
        }

        if (doMerge)
        {
            doMerge = false;
            if (!string.IsNullOrEmpty(mergeTarget) && mergeSources != null && mergeSources.Length > 0)
            {
                bool ok = Nodes.MergeNodes(mergeTarget, mergeSources);
                if (ok)
                {
                    SaveMemNode();
                    Debug.Log($"合并成功：目标 {mergeTarget}，源 {string.Join(",", mergeSources)}");
                }
            }
        }

        if (doDelete)
        {
            doDelete = false;
            if (!string.IsNullOrEmpty(deleteNodeName))
            {
                bool ok = Nodes.DeleteNode(deleteNodeName);
                if (ok)
                {
                    SaveMemNode();
                    Debug.Log($"节点已删除：{deleteNodeName}");
                }
            }
        }
    }

    RequestInfo AuxReq(DeepSeekRequest request, List<DeepSeekMessage> messages, Action<string> onChunk, Action<string> onResponse, Action<string> onError, Itool toolkit = null, bool backTool = true)
    {
        var ri = new RequestInfo(request, messages, onChunk, onResponse, onError, toolkit, backTool);
        ri.useAuxApi = true;
        return ri;
    }

    void LoadMemory()
    {
        _nowPath = Path.Combine(Application.persistentDataPath, selfChat.CharacterPath);
        Nodes = SLManager.instance.ImportFromJson<MemNodes>(_nowPath, "SQNodes.json") ?? new();
        Nodes.Startdb(_nowPath);

        //CompatibleWithOld();
        //TestFixed();重新加入记忆
    }

    void SaveMemNode()
    {
        SLManager.instance.ExportToJson(Nodes, Path.Combine(Application.persistentDataPath, selfChat.CharacterPath), "SQNodes.json");
    }

    #region 兼容性处理

    private List<(int index, string datetimeStr, List<string[]> keywordGroups)> pendingMemories = new();

    private List<string> Origin = new();
    private Dictionary<string,string> pending = new();


    void CompatibleWithOld()
    {
        //先检测是否有旧记忆文件
        string oldFolderPath = Path.Combine(Application.persistentDataPath, selfChat.CharacterPath);
        string oldFilePath = Path.Combine(oldFolderPath, "LongMemory.json");
        if (File.Exists(oldFilePath))
        {
            //！！删除前先备份到另外的位置
            //直接复制
            try
            {
                var allbytes = File.ReadAllBytes(oldFilePath);
                File.WriteAllBytes(Path.Combine(oldFolderPath, $"MemoryBackup{DateTime.Now.ToString("YYYY-MM-DD hh-mm-ss")}"), allbytes);
            }
            catch (Exception e)
            {
                //失败旧终止
                Debug.LogError($"旧版记忆压缩备份失败！Error:{e}");
                return;
            }
            //利用AI执行读取
            StartCoroutine(TransformOldMemory(oldFolderPath));
        }
    }

    /// <summary>
    /// 用于存第一个循环结果的list
    /// </summary>
    // 在 MemorySystem 类中声明队列
    private Queue<(int memoryIndex, string[] keywords)> pendingKeywords = new();

    IEnumerator TransformOldMemory(string oldFolderPath)
    {
        print("开始处理旧记忆转换");
        var old_memory = SLManager.instance.ImportFromJson<ConversationMemory>(oldFolderPath, "LongMemory.json");
        if (old_memory == null) yield break;
        int total = old_memory.memory.Count;
        print($"共 {total} 条记忆");
        //total = 2;//测试数量
        int finish = 0;
        List<int> errorIndex = new();//保存错误的记忆项
        List<Memory> OldMemList = old_memory.memory;

        for (int i = 0; i < total; i++)
        {
            int index = i;   // 避免闭包陷阱
            var currentMem = OldMemList[index];

            var gainKeywords = 关键词提取.DeepCopy();
            gainKeywords.messages[2].content = $"当前我指的AI名称:{selfChat.Character}\n文本内容:{currentMem.clip}";

            selfChat.api.SendRequest(AuxReq(gainKeywords, null, null,
                (response) =>
                {
                    //Debug.Log(response);
                // 尝试提取第一个花括号内容
                Match braceMatch = Regex.Match(response, @"\{([^{}]*)\}");
                    if (!braceMatch.Success)
                    {
                        Debug.LogWarning($"记忆 {index} 未找到花括号包裹的内容，原始回复：{response}");
                        return;
                    }

                    string braceContent = braceMatch.Groups[1].Value; // 花括号内部字符串
                                                                      // 提取所有方括号内容
                var bracketMatches = Regex.Matches(braceContent, @"\[([^\]]*)\]");
                    //对于同一条记忆的关键词合并是没有问题的。
                    HashSet<string> allKeywords = new HashSet<string>();

                    foreach (Match bm in bracketMatches)
                    {
                        string inside = bm.Groups[1].Value;   // 例如 "人物1,人物2,焦点1"
                    string[] parts = inside
                            .Split(',')
                            .Select(k => k.Trim())
                            .Where(k => !string.IsNullOrWhiteSpace(k))
                            .ToArray();

                        foreach (string kw in parts)
                            allKeywords.Add(kw);
                    }

                    if (allKeywords.Count > 0)
                    {
                        string[] finalKeywords = allKeywords.ToArray();
                        pendingKeywords.Enqueue((index, finalKeywords));
                        Debug.Log($"记忆 {index} 提取关键词去重后共 {finalKeywords.Length} 个：{string.Join(", ", finalKeywords)}");
                        finish++;
                    }
                    else
                    {
                        Debug.LogWarning($"记忆 {index} 未提取到有效关键词，原始回复：{response}");
                        errorIndex.Add(index);
                    }
                },
                (error) =>
                {
                    Debug.LogError($"关键词提取错误 (索引 {index}): {error}");
                }
            ));

            yield return new WaitForSeconds(0.25f);   // 控制请求频率
        }

        int GroupCount = 20;//20个一组
        while ((total - finish - errorIndex.Count != 0) || pendingKeywords.Count != 0)
        {
            int currentGroupCount = 0;
            if (pendingKeywords.Count >= GroupCount)
            {
                currentGroupCount = GroupCount;
            }
            else if (total - finish - errorIndex.Count == 0)
            {
                currentGroupCount = pendingKeywords.Count;
            }
            if (currentGroupCount > 0)
            {
                List<(int memIndex, string[] keywords)> group = new();
                for (int i = 0; i < currentGroupCount; i++)
                {
                    group.Add(pendingKeywords.Dequeue());
                }
                //之后将这组执行组内去重
                Debug.Log("执行组内去重");
                yield return ProcessGroupAsync(group,OldMemList);
            }
            else yield return null;
        }
        Debug.Log("完成");
    }
    private List<(int memoryIndex, string[] keywords)> processedMemories = new();

    IEnumerator ProcessGroupAsync(List<(int memIndex, string[] keywords)> group, List<Memory> oldMemList)
    {
        // 1. 收集所有关键词
        var allKeywords = group.SelectMany(it => it.keywords).Distinct().ToList();
        if (allKeywords.Count == 0)
        {
            foreach (var item in group)
                SaveMemory(oldMemList[item.memIndex].clip, oldMemList[item.memIndex].dateTime,
                           new List<string[]> { item.keywords });
            Debug.Log("没有结果");
            yield break;
        }
        // 2. 组内去重
        var mergeReq = 关键词组内去重.DeepCopy();
        string kwJson = $"[{string.Join(",", allKeywords.Select(k => $"\"{k}\""))}]";
        mergeReq.messages.Add(new DeepSeekMessage("user", kwJson));
        bool mergeDone = false;
        string mergeResult = null;
        selfChat.api.SendRequest(AuxReq(mergeReq, null, null,
            r => { mergeResult = r; mergeDone = true; },
            e => { Debug.LogError($"去重失败: {e}"); mergeDone = true; }
        ));
        yield return new WaitUntil(() => mergeDone);
        Dictionary<string, string> map = new();
        if (!string.IsNullOrEmpty(mergeResult))
        {
            foreach (Match m in Regex.Matches(mergeResult, @"\[([^\]]+)\]\s*→\s*(\S+)"))
            {
                string groupStr = m.Groups[1].Value;
                string target = m.Groups[2].Value;
                foreach (var syn in groupStr.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)))
                    if (!map.ContainsKey(syn))
                        map[syn] = target;
            }
        }
        // 应用去重映射
        foreach (var item in group)
            for (int i = 0; i < item.keywords.Length; i++)
                if (map.TryGetValue(item.keywords[i], out string nw))
                    item.keywords[i] = nw;
        // 3. 唯一化并行处理
        var A = group.SelectMany(it => it.keywords).Distinct().ToList();
        int wordCount = A.Count;
        print(A.Count);
        string[] mappedWords = new string[wordCount];
        int completedCount = 0;
        string nodeList = Nodes.allNode ?? "";
        for (int j = 0; j < wordCount; j++)
        {
            int idx = j;
            string candidate = A[idx];
            var uniqueReq = 关键词唯一化.DeepCopy();
            uniqueReq.messages[2].content = $"节点列表：{nodeList}";
            uniqueReq.messages[3].content = $"候选词：{candidate}";
            print("发送请求");
            selfChat.api.SendRequest(AuxReq(uniqueReq, null, null,
                (resp) =>
                {
                    var mc = Regex.Matches(resp, @"\[([^\]]*)\]");
                    if (mc.Count > 0)
                    {
                        string finalWord = mc[mc.Count - 1].Groups[1].Value.Trim().Split(',')[0].Trim();
                        mappedWords[idx] = finalWord;
                        print("A");
                    }
                    else
                        mappedWords[idx] = candidate;
                    System.Threading.Interlocked.Increment(ref completedCount);
                },
                (err) =>
                {
                    Debug.LogError($"唯一化错误({candidate}): {err}");
                    mappedWords[idx] = candidate;
                    System.Threading.Interlocked.Increment(ref completedCount);
                }
            ));
        }
        // 等待所有唯一化请求完成
        yield return new WaitUntil(() => completedCount == wordCount);
        // 构建最终替换映射（只保留不一致的）
        Dictionary<string, string> finalMap = new();
        for (int j = 0; j < wordCount; j++)
        {
            if (A[j] != mappedWords[j])
                finalMap[A[j]] = mappedWords[j];
        }
        // 应用唯一化映射
        foreach (var item in group)
            for (int i = 0; i < item.keywords.Length; i++)
                if (finalMap.TryGetValue(item.keywords[i], out string finalW))
                    item.keywords[i] = finalW;
        // 4. 保存记忆（直接调用 SaveMemory）
        foreach (var item in group)
        {
            var mem = oldMemList[item.memIndex];
            SaveMemory(mem.clip, mem.dateTime, new List<string[]> { item.keywords });
        }
        Debug.Log($"完成一组 {group.Count} 条记忆的转换与保存。");
    }
    private long? ParseDateTimeToHourTimestamp(string datetimeStr)
    {
        if (string.IsNullOrEmpty(datetimeStr)) return null;
        if (DateTime.TryParse(datetimeStr, out DateTime localTime))
        {
            DateTime utc = localTime.ToUniversalTime();
            return (long)(utc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalHours;
        }
        Debug.LogError($"解析时间失败: {datetimeStr}");
        return null;
    }
    void TestFixed()
    {
        var old_memory = SLManager.instance.ImportFromJson<ConversationMemory>(selfChat.CharacterPath, "LongMemory.json");
        var list = old_memory.memory;
        for (int i = 0; i < list.Count; i++)
        {
            Nodes.memorySystem.AddMemory(list[i].clip, ParseDateTimeToHourTimestamp(list[i].dateTime));
        }
        Nodes.memorySystem.Close();
    }

   

    #endregion

    #region API接口
    /// <summary>
    /// 从一串文本中提出关键词,文本前需要附加ai_name。
    /// </summary>
    DeepSeekRequest 关键词提取 {
        get
        {
            return new()
            {
                model = "deepseek-v4-flash",
                temperature = 0.5f,
                max_tokens = 20000,
                stream = false,
                thinking = new(true),
                messages = new()
                {
                    new DeepSeekMessage()
                    {
                        role = "user",
                        content = Prompt.GetPrompt("关键词提取"),
                    },
                    new DeepSeekMessage()
                    { 
                        role = "assistant",
                        content = "您尚未提供需要分析的文本，请将文本内容发送给我，我将按照您的要求的步骤提取关键词。",
                        reasoning_content = "我们按照步骤来处理。用户会提供文本，但当前消息中没有提供文本。用户说“我将会给你一个串文本”，但实际没有给出。需要等待用户提供文本。所以应该回复请求用户提供文本。",
                    },
                    new DeepSeekMessage()
                    {
                        role = "user",
                        content = "",
                    },
                },
                frequency_penalty = 0,
                presence_penalty = 0,
            };
        }
    }
    /// <summary>
    ///同组去重,为并发做准备,Add
    /// </summary>
    DeepSeekRequest 关键词组内去重 = new()
    {
        model = "deepseek-v4-flash",
        temperature = 0.2f,
        max_tokens = 10000,
        stream = false,
        thinking = new(false),
        messages = new()
        {
            new DeepSeekMessage()
            {
                role = "user",
                content = "你^^只要^^找出列表中语义相同的词并映射,不区分褒贬。\n输出:[词A, 词B, 词C] → 词A\n^^不要意淫^^",
            },
            new DeepSeekMessage()
            {
                role = "assistant",
                content = "抱歉，我无法执行这个任务，因为你没有提供需要分析的词列表。请补充列表内容，我会严格按照要求不意淫并找出语义相同的词并做映射。",
            },
            new DeepSeekMessage()
            {
                role = "user",
                content = @"[""愤怒"",""生气"",""沮丧"",""悲伤"",""开心"",""兴奋""]",
            },
            new DeepSeekMessage()
            {
                role = "assistant",
                content = @"根据语义相同（近义）的标准，列表中可映射为：

[""愤怒"", ""生气""] → 愤怒

其余词（沮丧、悲伤、开心、兴奋）无相同语义的配对。",
            },
            new DeepSeekMessage()
            {
                role = "user",
                content = @"[""猫"",""cat"",""狗"",""dog"",""宠物"",""animal""]",
            },
            new DeepSeekMessage()
            {
                role = "assistant",
                content = @"根据语义相同（近义）的标准：

[""猫"", ""cat""] → 猫
[""狗"", ""dog""] → 狗

“宠物”和“animal”不与其他词构成同义映射。",
            },
            new DeepSeekMessage()
            {
                role = "user",
                content = @"[""人工智能"",""AI"",""机器学习"",""深度学习"",""神经网络"",""模型"",""算法"",""数据""]",
            },
            new DeepSeekMessage()
            {
                role = "assistant",
                content = @"根据语义相同（近义）的标准：

[""人工智能"", ""AI""] → 人工智能

其余词（机器学习、深度学习、神经网络、模型、算法、数据）与该对词不构成同义，彼此之间也不构成严格的同义（它们属于不同但相关的概念）。",
            },
            new DeepSeekMessage()
            {
                role = "user",
                content = @"[""姐姐"",""妹妹"",""妈妈"",""女儿"",""阿姨""]",
            },
            new DeepSeekMessage()
            {
                role = "assistant",
                content = @"根据语义相同（近义）的标准：

该列表中所有词均为不同女性亲属称谓，语义不相同且不意淫，无映射输出。",
            },
            new DeepSeekMessage()
            {
                role = "user",
                content = @"[""记忆树""，""记忆系统""，""记忆""，""大海""，""海洋""，""大洋""]",
            },
            new DeepSeekMessage()
            {
                role = "assistant",
                content = @"根据语义相同（近义）的标准：

[""大海"", ""海洋"", ""大洋""] → 大海

其余词（记忆树、记忆系统、记忆）语义不相同（彼此也不构成严格同义）。",
            },

        },
        frequency_penalty = 0,
        presence_penalty = 0,
    };
    /// <summary>
    ///节点仓管[1]节点列表[2]候选词
    /// </summary>
    DeepSeekRequest 关键词唯一化
    {
        get
        {
            return new()
            {
                model = "deepseek-v4-flash",
                temperature = 0.2f,
                max_tokens = 10000,
                stream = false,
                thinking = new(false),
                messages = new()
                {
                    new DeepSeekMessage()
                    {
                        role = "user",
                        content = Prompt.GetPrompt("关键词唯一化"),
                    },
                    new DeepSeekMessage()
                    {
                        role = "assistant",
                        content = "明白了。请提供已有列表和候选词。",
                    },
                    new DeepSeekMessage()
                    {
                        role = "user",
                        content = "节点列表",
                    },
                    new DeepSeekMessage()
                    {
                        role = "user",
                        content = "候选词",
                    },
                },
                frequency_penalty = 0,
                presence_penalty = 0,
            };
        }
    }
    #endregion
    DeepSeekRequest 对话关键词检索
    {
        get
        {
            return new()
            {
                model = "deepseek-v4-flash",
                temperature = 0.5f,
                max_tokens = 20000,
                stream = false,
                thinking = new(false),
                messages = new()
                {
                    new DeepSeekMessage()
                    {
                        role = "system",
                        content = Prompt.GetPrompt("对话关键词检索"),
                    },
                    new DeepSeekMessage()
                    {
                        role = "user",
                        content = "",
                    },
                },
                frequency_penalty = 0,
                presence_penalty = 0,
            };
        }
    }
    void AddMemory() 
    {
    
    }
    /// <summary>
    /// 根据用户输入检索相关记忆
    /// </summary>
    /// <param name="user_content">用户说的话</param>
    /// <param name="OnResponse">成功回调：返回记忆文本（多条用换行分隔），无结果时返回空字符串</param>
    /// <param name="OnError">错误回调：返回错误描述字符串</param>
    /// <summary>
    /// 异步检索记忆，不阻塞，结果通过 GetSystemAIResponse 注入下一条消息
    /// </summary>
    public void GetMemoryAsync(string user_content)
    {
        if (Nodes == null)
        {
            Debug.LogError("记忆节点未初始化");
            return;
        }
        // 缓存：同一句话跳过
        if (_lastMemoryInput == user_content) return;
        _lastMemoryInput = user_content;
        StartCoroutine(GetMemoryCoroutine(user_content, null, null));
    }

    string _lastMemoryInput;

    IEnumerator GetMemoryCoroutine(string user_content, Action<string> OnResponse, Action<string> OnError)
    {
        var swTotal = System.Diagnostics.Stopwatch.StartNew();

        // ========== 0. 合并：判断重要性 + 关键词提取（一次请求） ==========
        int importance = 3; // 默认普通
        var recentMsgs = selfChat.messages != null ? selfChat.messages.Skip(Math.Max(0, selfChat.messages.Count - 5)).ToList() : new();
        string recentText = string.Join("\n", recentMsgs.Select(m => $"[{m.role}]: {m.content?.Substring(0, Math.Min(m.content?.Length ?? 0, 200))}"));

        var sw1 = System.Diagnostics.Stopwatch.StartNew();
        var gainKeywords = 对话关键词检索.DeepCopy();
        gainKeywords.messages[1].content = $"当前AI名称:{selfChat.Character},当前用户名称:{selfChat.CSetting.now_setting.UserName}\n\n[最近对话]\n{recentText}\n\n[用户发言]\n{user_content}";
        Debug.Log($"[记忆检索] Model={gainKeywords.model} Thinking={gainKeywords.thinking?.type} Stream={gainKeywords.stream}");

        bool done = false;
        string extractResponse = null;
        string error = null;
        selfChat.api.SendRequest(AuxReq(gainKeywords, null, null,
            (resp) => { extractResponse = resp; done = true; },
            (err) => { error = err; done = true; }
        ));
        yield return new WaitUntil(() => done);
        sw1.Stop();

        if (!string.IsNullOrEmpty(error))
        {
            Debug.LogError($"关键词提取错误: {error}");
            OnError?.Invoke(error);
            yield break;
        }

        // 解析第一行作为重要性数字
        string responseBody = extractResponse.Trim();
        int firstNewline = responseBody.IndexOf('\n');
        string firstLine = firstNewline >= 0 ? responseBody.Substring(0, firstNewline).Trim() : responseBody;
        string keywordPart = firstNewline >= 0 ? responseBody.Substring(firstNewline + 1) : "";
        if (int.TryParse(firstLine, out int parsedImportance))
            importance = parsedImportance;
        else
            keywordPart = responseBody; // 没解析出数字，整段当关键词处理

        Debug.Log($"[记忆系统] 判断重要性={importance}");

        if (importance == 0)
        {
            Debug.Log($"[记忆系统] 判断跳过，总{swTotal.Elapsed.TotalSeconds:F1}s");
            OnResponse?.Invoke("");
            yield break;
        }
        int maxPerGroup = importance == -1 ? -1 : importance;

        if (string.IsNullOrWhiteSpace(keywordPart))
        {
            OnResponse?.Invoke("");
            yield break;
        }

        // 解析关键词（不去重）
        List<string> keywords = new List<string>();
        Match braceMatch = Regex.Match(keywordPart, @"\{([^{}]*)\}");
        string braceContent = braceMatch.Success ? braceMatch.Groups[1].Value : keywordPart;
        var bracketMatches = Regex.Matches(braceContent, @"\[([^\]]*)\]");
        foreach (Match bm in bracketMatches)
        {
            string inside = bm.Groups[1].Value;
            string[] parts = inside.Split(',')
                .Select(k => k.Trim())
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .ToArray();
            keywords.AddRange(parts);
        }

        if (keywords.Count == 0)
        {
            OnResponse?.Invoke("");
            yield break;
        }

        // 这里不再去重，保留原始顺序（或为归一化准备列表）
        // ========== 2. 批量归一化（并发） ==========
        // ========== 2. 批量归一化（单次请求） ==========
        var sw2 = System.Diagnostics.Stopwatch.StartNew();
        string nodeList = Nodes.allNode ?? "";
        int total = keywords.Count;
        string[] normalized = new string[total];

        if (total == 1)
        {
            bool singleDone = false;
            var uniqueReq = 关键词唯一化.DeepCopy();
            uniqueReq.messages[2].content = $"节点列表：{nodeList}";
            uniqueReq.messages[3].content = $"候选词：{keywords[0]}";
            Debug.Log($"[归一化] Model={uniqueReq.model} Thinking={uniqueReq.thinking?.type} Stream={uniqueReq.stream}");
            selfChat.api.SendRequest(AuxReq(uniqueReq, null, null,
                (resp) =>
                {
                    var mc = Regex.Matches(resp, @"\[([^\]]*)\]");
                    normalized[0] = mc.Count > 0 ? mc[mc.Count - 1].Groups[1].Value.Trim().Split(',')[0].Trim() : keywords[0];
                    singleDone = true;
                },
                (err) => { normalized[0] = keywords[0]; singleDone = true; }
            ));
            yield return new WaitUntil(() => singleDone);
        }
        else
        {
            // 批量归一化：一次请求处理所有候选词
            var batchReq = new DeepSeekRequest()
            {
                model = "deepseek-v4-flash",
                temperature = 0.2f,
                max_tokens = 10000,
                stream = false,
                thinking = new(false),
                messages = new()
                {
                    new DeepSeekMessage("user", Prompt.GetPrompt("关键词批量归一化")),
                    new DeepSeekMessage("assistant", "明白了。请提供已有列表和候选词列表。"),
                    new DeepSeekMessage("user", "节点列表：" + nodeList + "\n\n候选词列表：\n" + string.Join("\n", keywords.Select((k, i) => $"{i + 1}. {k}"))),
                },
                frequency_penalty = 0,
                presence_penalty = 0,
            };

            Debug.Log($"[归一化-批量] {total}个关键词, Model={batchReq.model}");
            bool batchDone = false;
            string batchResp = null;
            selfChat.api.SendRequest(AuxReq(batchReq, null, null,
                (resp) => { batchResp = resp; batchDone = true; },
                (err) => { Debug.LogError($"[归一化-批量] 失败: {err}"); batchDone = true; }
            ));
            yield return new WaitUntil(() => batchDone);

            if (!string.IsNullOrEmpty(batchResp))
            {
                var lineMatches = Regex.Matches(batchResp, @"\[([^\]]*)\]");
                for (int i = 0; i < total; i++)
                    normalized[i] = i < lineMatches.Count
                        ? lineMatches[i].Groups[1].Value.Trim().Split(',')[0].Trim()
                        : keywords[i];
            }
            else
            {
                for (int i = 0; i < total; i++) normalized[i] = keywords[i];
            }
        }
        sw2.Stop();
        Debug.Log($"[归一化] {total}个关键词, 耗时{sw2.Elapsed.TotalSeconds:F1}s");

        //yield break;

        // ========== 3. 检索记忆 ==========
        var swDb = System.Diagnostics.Stopwatch.StartNew();
        if (Nodes.GetMemory(normalized, maxPerGroup, out string[] memories, out string[] associate))
        {
            if ((memories != null && memories.Length > 0) || (associate != null && associate.Length > 0))
            {
                // ========== 3. Flash 智能简略 ==========
                var sw3 = System.Diagnostics.Stopwatch.StartNew();
                string rawMem = memories != null ? string.Join("\n", memories) : "";
                string rawAsc = associate != null ? string.Join("\n", associate) : "";

                var summarizeReq = new DeepSeekRequest()
                {
                    model = "deepseek-v4-flash",
                    temperature = 0.3f,
                    max_tokens = 1024,
                    stream = false,
                    thinking = new(false),
                    messages = new()
                    {
                        new DeepSeekMessage("system", Prompt.GetPrompt("记忆摘要")),
                        new DeepSeekMessage("user", $"最近对话：\n{recentText}\n\n用户当前发言：{user_content}\n\n检索到的记忆：\n{rawMem}\n\n联想到的内容：\n{rawAsc}")
                    }
                };

                bool sumDone = false;
                string sumResult = "";
                selfChat.api.SendRequest(AuxReq(summarizeReq, null, null,
                    (resp) => { sumResult = resp; sumDone = true; },
                    (err) => { Debug.LogError($"记忆摘要错误: {err}"); sumDone = true; }
                ));
                yield return new WaitUntil(() => sumDone);
                sw3.Stop();
                swDb.Stop();
                swTotal.Stop();

                string result = string.IsNullOrEmpty(sumResult) ? rawMem : sumResult;
                Debug.Log($"[记忆系统] 总{swTotal.Elapsed.TotalSeconds:F1}s 提取{sw1.Elapsed.TotalSeconds:F1}s 归一化{sw2.Elapsed.TotalSeconds:F1}s 检索{swDb.Elapsed.TotalSeconds:F1}s 摘要{sw3.Elapsed.TotalSeconds:F1}s 关键词{keywords.Count}个");
                if (OnResponse != null) OnResponse(result);
                else if (!string.IsNullOrEmpty(result)) Chat.chat.GetSystemAIResponse(result);

                SaveMemNode();
            }
            else
            {
                swDb.Stop();
                swTotal.Stop();
                Debug.Log($"[记忆系统] 总{swTotal.Elapsed.TotalSeconds:F1}s 提取{sw1.Elapsed.TotalSeconds:F1}s 归一化{sw2.Elapsed.TotalSeconds:F1}s 检索{swDb.Elapsed.TotalSeconds:F1}s 无结果");
                OnResponse?.Invoke("");
            }
        }
        else
        {
            OnError?.Invoke("记忆检索内部失败");
        }
    }
    #region 工具实现

    public IEnumerator DealToolCallsCoroutine(List<ToolCall> toolCalls, Action<List<DeepSeekMessage>> onComplete)
    {
        List<DeepSeekMessage> newMessages = new();
        foreach (var toolCall in toolCalls)
        {
            var function = toolCall.function;
            string result = "";
            switch (function.name)
            {
                #region transform_memory
                case "transform_memory":
                    var tArgs = JsonConvert.DeserializeObject<Dictionary<string, object>>(function.arguments);

                    // 直接取值，无校验
                    int memIndex = Convert.ToInt32(tArgs["content"]);
                    string tDatetimeStr = tArgs["datetime"]?.ToString();
                    var tGroupsArray = (Newtonsoft.Json.Linq.JArray)tArgs["keyword_groups"];

                    // 提取关键词组（保留过滤畸形关键词的逻辑）
                    List<string[]> tKeywordGroups = new();
                    foreach (var groupToken in tGroupsArray)
                    {
                        var arr = (Newtonsoft.Json.Linq.JArray)groupToken;
                        List<string> validKeywords = new();
                        foreach (var item in arr)
                        {
                            string kw = item?.ToString()?.Trim();
                            if (string.IsNullOrEmpty(kw))
                                continue;

                            validKeywords.Add(kw);
                        }
                        if (validKeywords.Count > 0)
                            tKeywordGroups.Add(validKeywords.ToArray());
                    }

                    pendingMemories.Add((memIndex, tDatetimeStr, tKeywordGroups));

                    foreach (var group in tKeywordGroups)
                        foreach (var node in group)
                            if (!Origin.Contains(node))
                                Origin.Add(node);

                    result = $"关键词收集成功（索引 {memIndex}）";
                    break;
                #endregion

                #region save_memory
                case "save_memory":
                    var args = JsonConvert.DeserializeObject<Dictionary<string, object>>(function.arguments);

                    // 直接取值
                    string content = args["content"].ToString();
                    string datetimeStr = args["datetime"]?.ToString();
                    var groupsArray = (Newtonsoft.Json.Linq.JArray)args["keyword_groups"];

                    List<string[]> keywordGroups = new();
                    foreach (var groupToken in groupsArray)
                    {
                        var arr = (Newtonsoft.Json.Linq.JArray)groupToken;
                        string[] keywords = arr.Select(x => x.ToString()).ToArray();
                        if (keywords.Length > 0)
                            keywordGroups.Add(keywords);
                    }

                    result = SaveMemory(content, datetimeStr, keywordGroups);
                    break;
                #endregion
                #region merge_keywords
                case "merge_keywords":
                    var mergeArgs = JsonConvert.DeserializeObject<Dictionary<string, object>>(function.arguments);
                    var outputArr = (mergeArgs["output"] as Newtonsoft.Json.Linq.JArray)?.Select(t => t.ToString()).ToArray();
                    for (int i = 0; i < outputArr.Length; i++)
                        pending[Origin[i]] = outputArr[i];
                    result = $"映射已保存，共 {pending.Count} 条";
                    break;
                #endregion
                default:
                    result = $"不支持的工具: {function.name}";
                    break;
            }
            newMessages.Add(new ToolMessage(toolCall.id, result));
        }
        onComplete?.Invoke(newMessages);
        yield return null;
    }
    private string SaveMemory(string content, string datetimeStr, List<string[]> keywordGroups)
    {
        // 解析时间戳（可选）
        long? timestampHour = null;
        if (!string.IsNullOrEmpty(datetimeStr))
        {
            timestampHour = ParseDateTimeToHourTimestamp(datetimeStr);
            if (timestampHour == null)
            {
                return $"时间格式错误: {datetimeStr}，请使用 'yyyy-MM-dd HH:mm:ss' 格式";
            }
        }

        // 执行实际保存
        bool success = Nodes.AddMemoryForGroups(keywordGroups, content, timestampHour);
        if (success)
        {
            SaveMemNode(); // 持久化节点数据
            string debugString = string.Join(" | ",
    keywordGroups.Select(g => $"[{string.Join(", ", g)}]"));
            Debug.Log($"保存记忆({content[..Math.Min(content.Length, 5)]})，关键词：{debugString}");

            return "记忆保存成功";
        }
        else
        {
            return "记忆保存失败：无法添加到节点系统";
        }
    }

    private void OnApplicationQuit()
    {
        print("quit");
        Nodes.memorySystem.Close();
        print(Nodes == null);
    }

    #endregion


}

