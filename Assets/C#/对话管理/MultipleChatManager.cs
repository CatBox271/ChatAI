using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;
using System;
using System.Text;
using System.Linq;

public class MultipleChatManager : MonoBehaviour
{
    public static List<Chat> all_ai_chat = new();
    public static MultipleChatManager multipleChatManager;
    public DeepSeekAPIManager api;

    private void Awake()
    {
        multipleChatManager = this;
    }
    bool startFromUser;
    readonly string systemMessage = "你是一个系统AI，你需要根据目前对话内容判断接下来该哪个角色说话了，注意：如果最后一句话是大家怎么看则随机一个人回答，。注意<user_name:盒猫><user_name:用户>,中盒猫、和用户代表当前对话的角色名称。你必须调用tool:say_who进行回答。当最后一句话的实际性内容直接指明了一个其他人的名字，不用犹豫直接从名字列表里选择他。【注意】避免同一个角色一直讲话超过两次，尊重上文中除了系统指令的最后一个人的意愿。";
    string finialMessage
    {
        get
        {
            return $"系统：从接下来的列表中选择下一个最可能讲话的人的正确的名称,避免使用爱称、小名。你必须调用tool:say_who进行回答。\n{NowCharaters()}";
        }
    }

    public void AddMessageToAll(string CharacterName, string context, bool finish_tool = true,bool start_from_user = false)
    {
        startFromUser = start_from_user;
        //排除all_ai_chat中相同名字的ai
        //然后全部添加消息
        foreach (Chat aim_chat in all_ai_chat)
        {
            if (aim_chat.Character == CharacterName) continue;
            aim_chat.messages.Add(new DeepSeekMessage() { role = "user", content = $"<user_name:{CharacterName}>{context}", name = CharacterName });
            //检查save_load调用
            aim_chat.RemindNonRepeat();
            aim_chat.SaveTalk();
        }
        //然后接下来调用AI决定谁要回答。并把要回答的AI加上提示的内容。
        if (finish_tool)
        {
            Multiple_request.messages = api.GetOptimizedContext(api.requestData.messages, Multiple_request.thinking.ToBool(), 4096);
            Multiple_request.messages.Add(new DeepSeekMessage("system", systemMessage));
            Multiple_request.messages.Add(new DeepSeekMessage("user", finialMessage));

            print(Multiple_request.messages.Count);
            SendRequest(Multiple_request, onChatManagerResponse);
        }
    }

    string NowCharaters()
    {
        string final = "";
        foreach (var a in all_ai_chat)
        {
            final += a.Character + ";";
        }
        final += api.chat.CSetting.now_setting.UserName;
        return final;
    }
    void onChatManagerResponse(string Character)
    { //最后处理名字结果
        if (Character == "")
        {
            //随机一个人回答 
            print("A");
            Chat aim = all_ai_chat[UnityEngine.Random.Range(0, all_ai_chat.Count)];
            var message = aim.messages;
            print($"随机到对象{aim.Character}");
            //正式发送消息
            aim.api.OwnChatSend();
        }
        else
        {
            if (!all_ai_chat.Exists(s => s.Character == Character))
            {
                //可能是用户要回答了
                print("用户作答");
                api.chat.Send.interactable = true;
            }
            else
            {   //按目标作答
                Chat aim = all_ai_chat.Find(s => s.Character == Character);
                var message = aim.messages;
                print($"系统判断现在应该{Character}说话");
                //正式发送消息
                aim.api.OwnChatSend();
            }
        }
    }

    public static List<string> talk_process;

    public static bool IsMany()
    {
        return all_ai_chat.Count > 1;
    }

    DeepSeekRequest Multiple_request = new()
    {
        model = "deepseek-v4-flash",
        temperature = 0f,
        max_tokens = 2048,
        stream = false,
        messages = new(),
        thinking = new(true),
        tools = new List<Tool>() { 
        new Tool {
              type = "function",
            function = new Function
            {
                name = "say_who",
                description = "指定接下来该谁说话",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        who = new
                        {
                            type = "string",
                            description = "准确的角色名称,为空时代表随机一个角色"
                        },
                    },
                    required = new[] { "who" },
                    additionalProperties = false
                }
            }
        }
        },
        tool_choice = "auto",
        frequency_penalty = 0,
        presence_penalty = 0,
    };

    public void RemoveChat(string name)
    {
        var a = all_ai_chat.Find(s => s.Character == name);
        if (a != null)
        {
            Destroy(a.gameObject);
        }
        all_ai_chat.Remove(a);
    }

    public void SendRequest(DeepSeekRequest deepSeekRequest, Action<string> onResponse)
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
        string jsonData = JsonConvert.SerializeObject(deepSeekRequest, settings);

        StartCoroutine(Sending(jsonData, onResponse));
    }

    IEnumerator Sending(string jsonData, Action<string> onResponse)
    {
        // 创建UnityWebRequest
        UnityWebRequest request = new UnityWebRequest("https://api.deepseek.com/v1/chat/completions", "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);

        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", $"Bearer {api.apiKey}");

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

                print($"多AI系统判断:{aiResponse}\n{back_message.reasoning_content}");

                if (back_message.tool_calls != null && back_message.tool_calls.Count > 0)
                {
                    //解析工具
                    foreach (ToolCall tool in back_message.tool_calls)
                    {
                        var function = tool.function;
                        switch (function.name)
                        {
                            case "say_who":
                                var Args = JsonConvert.DeserializeObject<Dictionary<string, object>>(function.arguments);
                                string who = Convert.ToString(Args["who"]);
                                //最后根据结果执行函数
                                onResponse?.Invoke(who);
                                break;
                            default:
                                onResponse?.Invoke("");
                                break;
                        }
                    }

                }
                else
                {
                    onResponse?.Invoke("");
                    print("D");
                }
            }
            else
            {
                onResponse?.Invoke("");
                print("C");
            }
        }
        else
        {
            onResponse?.Invoke("");
            Debug.LogError($"多AI管理系统错误：{request.error}");
        }
        request.Dispose();
    }
}
