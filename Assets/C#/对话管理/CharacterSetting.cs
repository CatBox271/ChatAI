using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;

[System.Serializable]
public class NoTimesCounter
{
    public int no_save;
    public int no_load;
    public int no_dreaming;
    public int random_dream;

    public List<string> AI_SYSTEM_MESSAGE = new();
    public void RemindText()
    {
        no_save++;
        no_load++;
        no_dreaming++;
    }

    public bool NeedSave()
    {
        if (no_save > 20)
        {
            no_save = 0;
            return true;
        }
        return false;
    }
}

public class CharacterSetting : MonoBehaviour
{
    [System.Serializable]
    public class ConversationCharacterSetting
    {
        public string oc = "一个可爱的AI助手";
        public double temperature = 0.7;
        public int max_tokens = 2048;
        public int MaxTokens = 32768;
        public int pinnedMessageCount = 300;
        public bool DeveloperMode = false;
        public bool thinking = false;
        public string UserName = "用户";
        public string APIModel = "deepseek-v4-flash";
        public string apiUrl = "https://api.deepseek.com/v1/chat/completions";
        public string apiKeyEncrypted = "";
        public NoTimesCounter noTimesCounter;
        public bool ShowBackGround = true;
        public int Permission = 0;
        public float frequency_penalty = 0f;
        public float presence_penalty = 0f;
        public bool StreamingMode = true;
        public bool TTS = true;
        public List<string> allChat = new();
        public int reuse_from = -1;
        public bool KeepReasoningContent = false;
        public bool roleplayMode = true;   // ÊÇ·ñÔÚÉÏÏÂÎÄÖÐ±£Áô AI µÄË¼¿¼ÄÚÈÝ£¨reasoning_content£©
    }

    public bool Host = true;

    public DeepSeekAPIManager api;
    public Chat chat;

    public ConversationCharacterSetting now_setting;

    public static bool DeveloperMode;

    private void Awake()
    {
        // UI ³õÊ¼»¯ÒÑÒÆÖÁ CharacterSettingUI£¬ÕâÀïÎÞÐè²Ù×÷
    }

    private void Start()
    {
        if (!Host)
        {
            // ¸±¿Í»§¶Ë£º¼ÓÔØ²¢Ó¦ÓÃ
            Load();
            ApplyNowSetting();
            SyncFromHostIfNeeded();
        }
        // Ö÷¿Í»§¶ËÓÉ CharacterSettingUI ¸ºÔð´¥·¢
    }

    public void Load()
    {
        now_setting = SLManager.instance.ImportFromJson<ConversationCharacterSetting>(chat.CharacterPath, "CharacterSetting.json") ?? new();
        if (chat != null)
        {
            chat.noTimesCounter = now_setting.noTimesCounter;
        }
    }

    public void Save()
    {
        if (now_setting == null || chat == null) return;
        now_setting.reuse_from = api.reuseFrom;
        SLManager.instance.ExportToJson(now_setting, chat.CharacterPath, "CharacterSetting");
    }

    public void CounterSave(NoTimesCounter set)
    {
        if (Host && MultipleChatManager.all_ai_chat != null)
        {
            now_setting.allChat.Clear();
            foreach (var ai in MultipleChatManager.all_ai_chat)
            {
                now_setting.allChat.Add(ai.Character);
            }
        }
        now_setting.noTimesCounter = set;
        Save();
    }

    public void ApplyNowSetting()
    {
        if (now_setting == null || api == null) return;

        api.角色设定 = now_setting.oc;
        api.requestData.temperature = now_setting.temperature;
        api.requestData.max_tokens = now_setting.max_tokens;
        api.MaxTokens = now_setting.MaxTokens;
        api.pinnedMessageCount = now_setting.pinnedMessageCount;
        api.requestData.stream = now_setting.StreamingMode;
        api.requestData.frequency_penalty = now_setting.frequency_penalty;
        api.requestData.presence_penalty = now_setting.presence_penalty;
        api.requestData.thinking = new ThinkingConfig(now_setting.thinking);
        api.requestData.model = now_setting.APIModel;

        // 主 API 配置（per-character）
        if (!string.IsNullOrEmpty(now_setting.apiUrl))
            api.apiUrl = now_setting.apiUrl;
        if (!string.IsNullOrEmpty(now_setting.apiKeyEncrypted))
            api.apiKey = SecureStorage.Unprotect(now_setting.apiKeyEncrypted);

        DeveloperMode = now_setting.DeveloperMode;

        if (chat != null)
            chat.noTimesCounter = now_setting.noTimesCounter;
    }

    public void SyncFromHostIfNeeded()
    {
        if (Host || Chat.chat == null) return;
        api.requestData.thinking = Chat.chat.api.requestData.thinking;
        api.requestData.stream = Chat.chat.api.requestData.stream;
        now_setting.TTS = Chat.chat.CSetting.now_setting.TTS;
    }

    // ¼ÓÔØ¶àAI½ÇÉ«
    public void LoadAIs()
    {
        if (now_setting == null || chat == null) return;
        var chaters = now_setting.allChat;
        if (chaters.Count >= 2)
        {
            foreach (string chater in chaters)
            {
                if (chater == chat.Character) continue;
                chat.useTool.CreatAIBody(chater);
            }
        }
    }

    public void DeleteAIs()
    {
        Transform allChat = GameObject.Find("ALLChat")?.transform;
        if (allChat == null) return;
        for (int i = allChat.childCount - 1; i >= 0; i--)
        {
            Destroy(allChat.GetChild(i));
        }
    }
}