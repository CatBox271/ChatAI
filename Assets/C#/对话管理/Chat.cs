using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Threading;
using System.Text.RegularExpressions;
using System.Linq;
using System.Text;
using TMPro;

public class Chat : MonoBehaviour
{
    public DeepSeekAPIManager api;
    private SLManager SL;
    public CharacterSetting CSetting;
    public SpecialDayManager specialDayManager;
    public Button Send;

    public TMP_InputField UserContext;
    /// <summary>
    /// 不带persistentDataPath
    /// </summary>
    public string CharacterPath;
    public string Character;
    [Header("长期记忆")]
    [NonSerialized]
    public List<Memory> Memory = new();//LongMemory.json

    public MemorySystem memorySystem;

    public MainAITool useTool;

    public static Chat chat;
    public bool Host = true;
    public AITask task;

    public  float MinMindHour = 1;
    private DateTime lastTalkTime;

    public Action WhileCharacterChange;
    [NonSerialized]
    public List<DeepSeekMessage> messages;

    public void ChangeCharacter(string new_character)
    {
        Character = new_character;
        CharacterPath = "Character/" + Character;
        WhileCharacterChange?.Invoke();
        if (Host)
        {
            PlayerPrefs.SetString("Character", Character);
        }
    }
    private void Awake()
    {
        SL = SLManager.instance;
        if (CSetting != null) WhileCharacterChange += CSetting.Load;
        WhileCharacterChange += LoadCharacterMessage;
        if(task != null) WhileCharacterChange += task.LoadTask;

        messages = api.requestData.messages;
        
        if (Host)
        {
            chat = this;
            ChangeCharacter(PlayerPrefs.GetString("Character", "小冰"));
            Application.targetFrameRate = 60;
            simple_request.tools = useTool.all_tools;
        }
    }
    
    private TTSStreamProcessor tts;

    private void Start()
    {
        Send?.onClick.AddListener(SendMessage);

        tts = TTSStreamProcessor.Instance;
        if (Host)
        {
            MicrophoneASR.instance.OnTranscriptionText += OnASRText;
            MicrophoneASR.instance.OnAutoSend += OnASRAutoSend;
        }
    }

    private void OnASRText(string text)
    {
        // 将识别文本累积到输入框（假设每次追加一个空格或直接拼接）
        if (string.IsNullOrEmpty(UserContext.text))
            UserContext.text = text;
        else
            UserContext.text += " " + text;
    }

    private void OnASRAutoSend()
    {
        // 自动发送当前输入框内容
        if (!string.IsNullOrEmpty(UserContext.text))
        {
            SendMessage();
        }
    }

    public void SetStart(string name)
    {
        ChangeCharacter(name);
        simple_request.tools = useTool.all_tools;
    }

    public void LoadCharacterMessage()
    {
        print(CharacterPath);
        Memory = SL.ImportFromJson<ConversationMemory>(CharacterPath, "LongMemory.json")?.memory ?? new();
        if (SL.QuickLoad(this, SetLastDateTime) && CSetting != null && CSetting.now_setting != null)
        {
            api.reuseFrom = CSetting.now_setting.reuse_from;
        }
        else
        {
            print(CSetting != null && CSetting.now_setting != null);
                }
        BubbleManager.instance?.DeleteAll();
    }

    void SaveLongMemory()
    {
        SL.ExportToJson(new ConversationMemory { memory = Memory }, CharacterPath, "LongMemory.json");
    }

    void SetLastDateTime(DateTime dateTime)
    {
        lastTalkTime = dateTime;
        Debug.Log(lastTalkTime);
    }

    public void OnChunk(string back)
    {
        //刷新TextBubble
        BubbleManager.instance.LastestTextRefresh(false);

        if(CSetting.now_setting.TTS) tts?.OnChunk(api.requestData.messages.Count - 1, back);
    }
    public void OnResponse(string back)
    {
        BubbleManager.instance.LastestTextRefresh(true);
        if (CSetting.now_setting.TTS) tts?.OnSequenceComplete(api.requestData.messages.Count - 1, back);

        if (MultipleChatManager.IsMany())
        {
            print("a");
            MultipleChatManager.multipleChatManager.AddMessageToAll(Character, back);
        }
        else
        {
            Send.interactable = true;
        }
        //自动保存
        SaveTalk();
    }

    public void SaveTalk()
    {
        //多线程防止主线程卡顿
        Thread thread = new Thread(() =>
        {
            // 在另一个线程执行
            //聊天记录
            SL.QuickSave(this);
            //更新对话时间
            lastTalkTime = DateTime.Now;
            //长期记忆
            SaveLongMemory();
            //
            CSetting.CounterSave(noTimesCounter);
        });
        thread.Start();
    }

    public void OnError(string back)
    {
        Send.interactable = true;
        Debug.Log($"错误发生{gameObject.name},{ErrorType(back)}" );

        PopUp.instance.SetConfirm(null, ErrorType(back));
    }

    public string ErrorType(string error)
    {
        return error switch
        {
            "400"=> "请求体格式错误\n请根据错误信息提示修改请求体",
            "401"=> "API key 错误，认证失败\n请检查您的 API key 是否正确，如没有 API key，请先 创建 API key",
            "402"=> "账号余额不足\n请确认账户余额，并前往 充值 页面进行充值",
            "422" => "请求体参数错误\n请根据错误信息提示修改相关参数",
            "429"=> "请求速率（TPM 或 RPM）达到上限\n请合理规划您的请求速率",
            "500"=> "服务器内部故障\n请等待后重试。若问题一直存在，请联系我们解决",
            "503"=> "服务器繁忙\n请稍后重试您的请求",
            _ => error,
        };
    }

    string last_message;

    void AddSendMessage(string text = "")
    {
        if (!string.IsNullOrEmpty(text))
        {
            UserContext.text = text;
        }
        SendMessage();
    }
    void SendMessage()
    {
        if (string.IsNullOrEmpty(UserContext.text)) return;
        // 多人对话保留原有逻辑，不做记忆检索
        if (MultipleChatManager.IsMany())
        {
            Send.interactable = true;
            MultipleChatManager.multipleChatManager.AddMessageToAll(CSetting.now_setting.UserName, UserContext.text, start_from_user: true);
            last_message = UserContext.text;
            UserContext.text = "";
            RemindNonRepeat();
            return;
        }
        // 单人对话：先禁用按钮，启动记忆检索协程
        Send.interactable = false;
        StartCoroutine(SendMessageWithMemory());
    }
    IEnumerator SendMessageWithMemory()
    {
        string userInput = UserContext.text;          // 备份原始输入
        last_message = userInput;                // 保存最后一次消息
        UserContext.text = "";                       // 清空输入框

        StringBuilder final = new StringBuilder();

        // 3.1 离线时间提醒
        var timeSpan = System.DateTime.Now - lastTalkTime;
        if (timeSpan.TotalHours > MinMindHour)
        {
            float hoursPassed = Mathf.Round((float)timeSpan.TotalHours * 10f) / 10f;
            final.Append($"<only_ai>TimeRemind:距离上次对话已经过去了{hoursPassed}小时，现在是{DateTime.Now:yyyy-MM-dd HH:mm:ss}</only_ai>");
        }

        // 3.2 添加其他系统消息（如好奇脑等）
        final.Append(Add_AI_SYSTEM_MESSAGE());

        // 3.3 添加用户原始消息
        final.Append(userInput);

        // 3.4 异步检索相关记忆（不阻塞，结果注入下一条消息）
        memorySystem?.GetMemoryAsync(userInput);

        // 3.5 发送消息（复用原有发送方法）
        api.OwnAddMessageSend(new DeepSeekMessage
        {
            content = final.ToString(),
            role = "user",
            name = CSetting.now_setting.UserName
        });

        // 按钮恢复由 OnResponse/OnError 回调处理，无需额外操作
        RemindNonRepeat();
        yield break;
    }

    public void RemindNonRepeat()
    {
        CSetting.now_setting.noTimesCounter.RemindText();
        //同时调用不同脑区异步思考
        DifferentBrains();
    }

    public string OnceInfo()
    {
        StringBuilder final = new();
        final.Append(CSetting.now_setting.TTS ? "TTS开启,现在给每段话添加语音前缀[tts_...]" : "TTS关闭,你现在不用写语音前缀[tts_...]");
        return final.ToString();
    }

    public string Add_AI_SYSTEM_MESSAGE()
    {
        string final = "";
        //插入系统消息
        if (noTimesCounter.AI_SYSTEM_MESSAGE.Count != 0)
        {
            final = "<only_ai>System:";
            for (int i = 0; i < noTimesCounter.AI_SYSTEM_MESSAGE.Count; i++)
            {
                //就是API Request 中的message
                final += noTimesCounter.AI_SYSTEM_MESSAGE[i].ToString();
            }
            noTimesCounter.AI_SYSTEM_MESSAGE.Clear();
            final += "</only_ai>";
        }
        return final;
    }

    public void GetSystemAIResponse(string value)
    {
        noTimesCounter.AI_SYSTEM_MESSAGE.Add(value);
    }

    #region 不同脑区工作
    void DifferentBrains()
    {
        try
        {
            List<DeepSeekMessage> messages = new();
            messages.AddRange(api.requestData.messages);
            //好奇
            //Curious(messages);
            //记忆

        }
        catch(Exception e)
        {
            Debug.LogError($"多脑区工作时出现异常{e}");
        }
    }

    void BrainError(string result)
    {
        print($"脑区报错：{result}");
    }
    void Curious(List<DeepSeekMessage> messages)
    {
        var LessMessage = api.GetOptimizedContext(messages, false, 4096);
        api.InsertSystemMessages(LessMessage, new List<string>() {"你是多AI系统中负责好奇问题的AI，你需要根据提供的上下文中的异常来生成一些问题，并避免重复的问题，例如：[用户：我去外面住了；你：好奇脑：那你现在是把原来的房子退了吗？][用户：我刚刚吃完饭，现在在寝室；你：好奇脑：你中午吃了什么？]。并且不许人格化的语言，应该使用平淡而简单的疑问句。"});

        LessMessage.Add(new DeepSeekMessage("user", "系统：你不是上下文中的AI，请为上文对话中用户的内容按实例格式提出具有发散思维但具体的【最多两个】问题，问题要有深度，例如对突然的情绪变化，喜好更爱，时间异常，逻辑错误疑惑。并在开头加上:好奇脑结>\n不用一定要提出问题，可以输出为空"));

        //配置AI设置
        curious_request.messages.Clear();
        curious_request.messages.AddRange(LessMessage);

        print(LessMessage.Count);
        api.SendRequest(new RequestInfo(curious_request, LessMessage, null, GetSystemAIResponse, BrainError, null, false));
    }

    #endregion
    DeepSeekRequest simple_request = new()
    {
        model = "deepseek-v4-flash",
        temperature = 1f,
        max_tokens = 512,
        stream = false,
        messages = new() {
            new DeepSeekMessage()
            {
                role = "system",
                content = "身份：系统指令转译AI。职责：把系统下达的系统指令用不重复的多样化句式重新表述,做到简洁明了。请根据提供的tool，用指导性的语言提醒另一个AI使用工具。",
            },
            new DeepSeekMessage() {
                role = "user",
                content = "",
            }
        },
        tools = new(),
        tool_choice = "none",
        frequency_penalty = 0,
        presence_penalty = 0,
    };
    DeepSeekRequest curious_request = new()
    {
        model = "deepseek-v4-flash",
        temperature = 0.5f,
        max_tokens = 1024,
        stream = false,
        messages = new(),
        tools = new(),
        tool_choice = "none",
        frequency_penalty = 0,
        presence_penalty = 0,
        thinking = new(false),
    };
    public NoTimesCounter noTimesCounter;
}
