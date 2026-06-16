using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System;

public class CharacterSettingUI : MonoBehaviour
{
    public CharacterSetting characterSetting;
    public Chat chat;
    public DeepSeekAPIManager api;
    public Image BackGroundPic;

    [Header("UI")]
    public GameObject SettingPanel;
    public InputField character_name;
    public InputField oc;
    public InputField temperature;
    public InputField max_tokens;
    public InputField MaxTokens;
    public InputField pinnedMessages;
    public InputField APIKEY;
    public InputField mainApiUrl;
    public InputField auxApiUrl;
    public InputField auxApiKey;
    public InputField ttsSecretId;
    public InputField ttsSecretKey;
    public InputField NameText;
    public Dropdown SelectCharacter;
    public TrueFalseJudge developerMode;
    public TrueFalseJudge backGround;
    public TrueFalseJudge streamMode;
    public InputField AIPermission;
    public InputField FrequencyPenalty;
    public InputField PresencePenalty;
    public InputField APIModel;
    public Button ThinkMode;
    public Button TTSMode;
    public TrueFalseJudge keepReasoning;
    public TrueFalseJudge roleplayMode;

    private List<Dropdown.OptionData> All_Character = new();
    private Text ThinkButtonContent;
    private Text TTSButtonContent;

    private void Awake()
    {
        if (SettingPanel != null)
            SettingPanel.SetActive(false);
        if (ThinkMode != null) ThinkButtonContent = ThinkMode.GetComponentInChildren<Text>();
        if (TTSMode != null) TTSButtonContent = TTSMode.GetComponentInChildren<Text>();
    }

    private void Start()
    {
        if (characterSetting == null) characterSetting = GetComponent<CharacterSetting>(); // Ó¦Í¨¹ýInspectorÍÏ×§
        if (chat == null && characterSetting != null) chat = characterSetting.chat;
        if (api == null && characterSetting != null) api = characterSetting.api;

        if (SyncCharacterData.instance != null)
            SyncCharacterData.instance.Restart += Restart;

        BindEvents();
        Restart(); // Ê×´Î³õÊ¼»¯
    }

    void BindEvents()
    {
        character_name.onEndEdit.AddListener(OnCharacterNameEndEdit);
        oc.onEndEdit.AddListener(_ => Refresh());
        temperature.onEndEdit.AddListener(_ => Refresh());
        max_tokens.onEndEdit.AddListener(_ => Refresh());
        MaxTokens.onEndEdit.AddListener(_ => Refresh());
        pinnedMessages.onEndEdit.AddListener(_ => Refresh());
        developerMode.button.onClick.AddListener(() => Refresh());
        streamMode.button.onClick.AddListener(() => Refresh());
        backGround.button.onClick.AddListener(() => Refresh());
        APIKEY.onEndEdit.AddListener(SetAPI);
        mainApiUrl.onEndEdit.AddListener(SetMainApiUrl);
        auxApiUrl.onEndEdit.AddListener(SetAuxApiUrl);
        auxApiKey.onEndEdit.AddListener(SetAuxApiKey);
        if (ttsSecretId != null) ttsSecretId.onEndEdit.AddListener(SetTTSSecretId);
        if (ttsSecretKey != null) ttsSecretKey.onEndEdit.AddListener(SetTTSSecretKey);
        SelectCharacter.onValueChanged.AddListener(OnDropdownValueChanged);
        NameText.onEndEdit.AddListener(_ => Refresh());
        AIPermission.onEndEdit.AddListener(_ => Refresh());
        FrequencyPenalty.onEndEdit.AddListener(_ => Refresh());
        PresencePenalty.onEndEdit.AddListener(_ => Refresh());
        APIModel.onEndEdit.AddListener(_ => Refresh());
        ThinkMode.onClick.AddListener(SwitchThinkingMode);
        TTSMode.onClick.AddListener(SwitchTTSMode);
        keepReasoning.button.onClick.AddListener(() => Refresh());
        roleplayMode.button.onClick.AddListener(() => Refresh());
    }

    public void Restart()
    {
        characterSetting.DeleteAIs();
        LoadCharacterList();
        Show();
        Refresh(save: false);
        chat.noTimesCounter = characterSetting.now_setting?.noTimesCounter;
    }

    void LoadCharacterList()
    {
        All_Character.Clear();
        All_Character.Add(new Dropdown.OptionData("新建角色", null));

        string root = Path.Combine(Application.persistentDataPath, "Character");
        if (Directory.Exists(root))
        {
            foreach (string dir in Directory.GetDirectories(root))
            {
                All_Character.Add(new Dropdown.OptionData(Path.GetFileName(dir), null));
            }
        }

        if (SelectCharacter != null)
        {
            SelectCharacter.options = All_Character;
            int idx = All_Character.FindIndex(s => s.text == chat.Character);
            if (idx >= 0) SelectCharacter.value = idx;
        }
    }

    public void Show(bool newCharacter = false)
    {
        if (characterSetting.now_setting == null)
            characterSetting.now_setting = new CharacterSetting.ConversationCharacterSetting();

        var ns = characterSetting.now_setting;

        character_name.text = newCharacter ? "新角色名" : chat.Character;

        oc.text = ns.oc;
        temperature.text = ns.temperature.ToString();
        max_tokens.text = (ns.max_tokens / 1024f).ToString("F1");
        MaxTokens.text = (ns.MaxTokens / 1024f).ToString("F1");
        pinnedMessages.text = ns.pinnedMessageCount.ToString();
        developerMode.Set(ns.DeveloperMode);
        streamMode.Set(ns.StreamingMode);
        NameText.text = ns.UserName;
        // 主 API（per-character）- 首次迁移旧全局 Key
        mainApiUrl.text = ns.apiUrl;
        string savedKey = SecureStorage.Unprotect(ns.apiKeyEncrypted);
        APIKEY.text = savedKey;
        // 辅助 API + TTS（全局配置）
        var gcfg = GlobalApiConfig.Load(chat.Character);
        auxApiUrl.text = string.IsNullOrEmpty(gcfg.auxApiUrl) ? "https://api.deepseek.com/v1/chat/completions" : gcfg.auxApiUrl;
        auxApiKey.text = gcfg.auxApiKey;
        if (ttsSecretId != null) ttsSecretId.text = gcfg.ttsSecretId;
        if (ttsSecretKey != null) ttsSecretKey.text = gcfg.ttsSecretKey;
        SetAPI(APIKEY.text);
        SetMainApiUrl(mainApiUrl.text);
        SetAuxApiUrl(auxApiUrl.text);
        SetAuxApiKey(auxApiKey.text);
        backGround.Set(ns.ShowBackGround);
        AIPermission.text = ns.Permission.ToString();
        FrequencyPenalty.text = ns.frequency_penalty.ToString();
        PresencePenalty.text = ns.presence_penalty.ToString();
        APIModel.text = ns.APIModel;
        keepReasoning.Set(characterSetting.now_setting.KeepReasoningContent);
        roleplayMode.Set(characterSetting.now_setting.roleplayMode);

        ShowThinkingMode();
        ShowTTSMode();
    }

    void Refresh(string _ = "", bool save = true)
    {
        if (string.IsNullOrEmpty(character_name.text) || character_name.text == "新角色名") return;

        var ns = characterSetting.now_setting;
        if (ns == null) return;

        ns.oc = oc.text;
        if (double.TryParse(temperature.text, out double t)) ns.temperature = t;
        if (float.TryParse(max_tokens.text, out float mt)) ns.max_tokens = (int)(mt * 1024);
        if (float.TryParse(MaxTokens.text, out float mxt)) ns.MaxTokens = (int)(mxt * 1024);
        if (float.TryParse(pinnedMessages.text, out float pmc)) ns.pinnedMessageCount = (int)pmc;
        ns.DeveloperMode = developerMode.yes;
        ns.StreamingMode = streamMode.yes;
        ns.UserName = NameText.text;
        ns.ShowBackGround = backGround.yes;
        if (float.TryParse(AIPermission.text, out float perm)) ns.Permission = (int)perm;
        if (float.TryParse(FrequencyPenalty.text, out float fp)) ns.frequency_penalty = fp;
        if (float.TryParse(PresencePenalty.text, out float pp)) ns.presence_penalty = pp;
        ns.APIModel = APIModel.text;

        characterSetting.ApplyNowSetting();

        if (BackGroundPic != null)
            BackGroundPic.enabled = ns.ShowBackGround;

        ns.KeepReasoningContent = keepReasoning.yes;
        ns.roleplayMode = roleplayMode.yes;

        ShowThinkingMode();
        ShowTTSMode();

        if (save) characterSetting.Save();
    }

    public void Switch()
    {
        SettingPanel.SetActive(!SettingPanel.activeSelf);
        if (!SettingPanel.activeSelf)
        {
            BubbleManager.instance?.DeleteAll();
        }
        else
        {
            Show();
        }
    }

    void OnCharacterNameEndEdit(string input)
    {
        if (string.IsNullOrEmpty(input) || input == "新角色名") return;
        if (All_Character.Exists(s => s.text == input))
        {
            character_name.text += "※角色重复※";
            return;
        }

        chat.ChangeCharacter(input);
        characterSetting.Load();
        characterSetting.ApplyNowSetting();

        character_name.interactable = false;
        Refresh(save: true);
        All_Character.Add(new Dropdown.OptionData(input, null));
        SelectCharacter.value = All_Character.Count - 1;
        characterSetting.LoadAIs();
    }

    void OnDropdownValueChanged(int index)
    {
        if (All_Character.Count < 2) return;
        if (index == 0) // ÐÂ½¨½ÇÉ«
        {
            Show(true);
            character_name.interactable = true;
            chat.CharacterPath = "Character/";
            PlayerPrefs.SetString("Character", chat.Character);
        }
        else
        {
            string name = All_Character[index].text;
            character_name.interactable = false;
            chat.ChangeCharacter(name);
            characterSetting.Load();
            characterSetting.ApplyNowSetting();
            Show();
            Refresh(save: false);
        }
    }

    void SwitchThinkingMode()
    {
        characterSetting.now_setting.thinking = !characterSetting.now_setting.thinking;
        Refresh();
    }

    void ShowThinkingMode()
    {
        if (ThinkButtonContent == null) return;
        ThinkButtonContent.text = characterSetting.now_setting.thinking ? "思考模式" : "笨笨模式";
    }

    void SwitchTTSMode()
    {
        characterSetting.now_setting.TTS = !characterSetting.now_setting.TTS;
        Refresh();
    }

    void ShowTTSMode()
    {
        if (TTSButtonContent == null) return;
        TTSButtonContent.text = characterSetting.now_setting.TTS ? "语音" : "静音";
    }

    void SetAPI(string key)
    {
        if (api != null) api.apiKey = key;
        if (characterSetting.now_setting != null)
        {
            characterSetting.now_setting.apiKeyEncrypted = SecureStorage.Protect(key);
            characterSetting.Save();
        }
    }

    void SetMainApiUrl(string url)
    {
        if (api != null) api.apiUrl = url;
        if (characterSetting.now_setting != null)
        {
            characterSetting.now_setting.apiUrl = url;
            characterSetting.Save();
        }
    }

    void SetAuxApiUrl(string url)
    {
        if (api != null) api.auxApiUrl = url;
        var cfg = GlobalApiConfig.Load(chat.Character);
        GlobalApiConfig.Save(chat.Character, url, cfg.auxApiKey, cfg.ttsSecretId, cfg.ttsSecretKey);
    }

    void SetAuxApiKey(string key)
    {
        if (api != null) api.auxApiKey = key;
        var cfg = GlobalApiConfig.Load(chat.Character);
        GlobalApiConfig.Save(chat.Character, cfg.auxApiUrl, key, cfg.ttsSecretId, cfg.ttsSecretKey);
    }

    public void SetTTSSecretId(string id)
    {
        if (TencentCloudTTSManager.Instance != null) TencentCloudTTSManager.Instance.secretId = id;
        var cfg = GlobalApiConfig.Load(chat.Character);
        GlobalApiConfig.Save(chat.Character, cfg.auxApiUrl, cfg.auxApiKey, id, cfg.ttsSecretKey);
    }

    public void SetTTSSecretKey(string key)
    {
        if (TencentCloudTTSManager.Instance != null) TencentCloudTTSManager.Instance.secretKey = key;
        var cfg = GlobalApiConfig.Load(chat.Character);
        GlobalApiConfig.Save(chat.Character, cfg.auxApiUrl, cfg.auxApiKey, cfg.ttsSecretId, key);
    }

    private void OnDestroy()
    {
        if (SyncCharacterData.instance != null)
            SyncCharacterData.instance.Restart -= Restart;
    }
}