using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text.RegularExpressions;

public class TextBubble : MonoBehaviour
{
    public RectTransform rectTransform;
    public RectTransform bubbleOutLineRect;
    public RectTransform CopyButton;
    public Image bubbleOutLine;
    public BubbleManager bm;
    public TextLineCounter line_counter;
    public CorrectUI cu;
    [Header("在MessageList中的顺序")]
    public int BubbleIndex;
    [Header("高度位置设置")]
    public float startH;

    public TextMeshProUGUI context;

    public CanvasGroup alpha_set;

    public bool Completely = true;

    public Vector2 ScreenRange;

    public bool new_load;
    public bool last_load;
    public bool show;
    public int start_from;


    private bool out_line = true;

    private float _text_height;
    float TextHeight { get
        {
            if (_text_height <= 0)
            {
                _text_height = line_counter.GetTextLineCount(false);
            }
            else
            {
                if (!Completely)
                {

                    _text_height = line_counter.GetTextLineCount(false);
                }
            }
            return _text_height;
        }
    }

    public void ChunkText(bool adjust = true)
    {
        context.text = UserAIInsert(adjust);
        context.ForceMeshUpdate(true);
    }

    bool think_text = false;
    public void SwitchThinkModeShowState()
    {
        if (think_text = !think_text)
        {
            context.text = $"<color=#888888><size={baseFontSize - 5}>{bm.all_message[BubbleIndex].reasoning_content}</size></color>";
        }
        else
        {
            //normal
            ChunkText();
        }
    }

    public void SetValue(BubbleManager _bm, GameObject ss, int B_index, float _startH, int up_down = 0)
    {
        //重置
        _text_height = -1;

        ScreenRange = new(Screen.height / -2f, Screen.height / 2f);
        bm = _bm;
        cu.Aim = ss;
        cu.CheckReady();
        cu.RefreshSize();
        BubbleIndex = B_index;

        ChunkText();

        startH = _startH;
        //用于判断位置要怎么调整
        start_from = up_down;

        alpha_set.alpha = 0;

        gameObject.SetActive(true);

        CopyButton.gameObject.SetActive(false);
        bubbleOutLine.gameObject.SetActive(false);

        StartCoroutine(AdjustTransform());
    }
    IEnumerator AdjustTransform()
    {
        bool finish = false;
        bool showed = false;
        float addH = 0;
        do
        {
            yield return null;
            if (TextHeight == 0) continue;

            addH = TextHeight / 2f * start_from * rectTransform.localScale.y;
            bubbleOutLineRect.sizeDelta = new(bubbleOutLineRect.sizeDelta.x, TextHeight + 50f);
            CopyButton.anchoredPosition = new(CopyButton.anchoredPosition.x, TextHeight / -2f - 100f);

            Vector3 v3 = transform.localPosition;
            v3.y = startH + addH + bm.H;
            transform.localPosition = v3;

            bubbleOutLine.gameObject.SetActive(out_line);
            CopyButton.gameObject.SetActive(true);

            if (Completely)
            {
                finish = true;
                startH += addH;
            }
            else
            {
                if (!showed)
                {
                    alpha_set.alpha = 1;
                    showed = true;
                }
            }
        } while (!finish);

        if (!showed)
        {
            StartCoroutine(SmoothIn());
            showed = true;
        }
        //确认时最后一项
        if (BubbleIndex == bm.all_message.Count - 1)
        {
            //可以在刷新位置时调整BubbleManager的位置
            //bm.H = -startH;
            //bm.moving = true;
        }
    }
    IEnumerator SmoothIn()
    {
        int a = 0;
        alpha_set.alpha = a;
        do
        {
            alpha_set.alpha += Time.deltaTime * 2f;
            yield return null;
        }
        while (a < 1);
    }
    string UserAIInsert(bool adjust = true)
    {
        string A = "";
        var Message = bm.all_message[BubbleIndex];
        string renmoveCommand = Message.content;
        switch (Message.role)
        {
            case "user":
                if (Message.name != null && Message.name != "")
                {
                    A = Message.name + ":\n  ";
                }

                if (!CharacterSetting.DeveloperMode)
                {
                    RemoveToAI(renmoveCommand, out renmoveCommand);
                }
                else
                {
                    if (renmoveCommand.StartsWith("Memory:") || renmoveCommand.StartsWith("System:")|| renmoveCommand.StartsWith("History:"))
                    {
                        A = "系统:\n  ";
                        cu.RelativePosition = new(-48, 0);
                    }
                }

                if (adjust)
                {
                    out_line = true;
                    cu.SizePercentage = 7.25f;
                    cu.RelativePosition = new(48, 0);
                    cu.RefreshSize();
                    cu.RefreshPos();
                }
                break;
            case "assistant":
                A = Chat.chat.Character + ":\n  ";

                if (!CharacterSetting.DeveloperMode) RemoveToAI(renmoveCommand, out renmoveCommand);

                if (adjust)
                {
                    out_line = false;
                    cu.SizePercentage = 7.917f;
                    cu.RelativePosition = new(0, 0);
                    cu.RefreshSize();
                    cu.RefreshPos();
                }
                break;
            default:
                if (adjust)
                {
                    out_line = true;
                    cu.SizePercentage = 7.917f;
                    cu.RelativePosition = new(0, 0);
                    cu.RefreshSize();
                    cu.RefreshPos();
                }
                break;
        }
        if (string.IsNullOrWhiteSpace(renmoveCommand)) renmoveCommand = "(无)";

        if (CharacterSetting.DeveloperMode) return renmoveCommand;
        else return A + ConvertMarkdownToUnityRichText(renmoveCommand);
    }

    private static readonly Regex CombinedRemoveRegex = new(
        @"<only_ai>.*?</only_ai>|" +           // 1. 完整 <only_ai>...</only_ai>
        @"<(only_ai|user_name):[^>]*>|" +      // 2. <only_ai:...> 和 <user_name:...>
        @"\[(tts_|ser:)[^\]]*\]|" +            // 3. [tts_...] 和 [ser:...]
        @"<[^>]*\z|" +                         // 4. 末尾不完整的尖括号标签
        @"\[[^\]]*\z",                         // 5. 末尾不完整的方括号标签
        RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// 移除字符串中的 AI 专用标签、语音前缀及末尾不完整的标记（一次正则替换完成）。
    /// </summary>
    private void RemoveToAI(string origin, out string command)
    {
        command = string.IsNullOrEmpty(origin) ? (origin ?? string.Empty)
                                               : CombinedRemoveRegex.Replace(origin, "");
    }
    int a = 0;
    void Update()
    {
        if (!Completely) return;
        Vector3 v3 = transform.localPosition;
        v3.y = startH + bm.H;
        transform.localPosition = v3;

        a++;
        if (a < 10) return;
        a = 0;

        Inside();
        TryExpand();
    }

    bool last_show;

    public void CopyBuffer()
    {
        GUIUtility.systemCopyBuffer = context.text;
    }

    public void ReplayVoice()
    {
        TTSStreamProcessor.Instance.ProcessFullText(BubbleIndex, bm.all_message[BubbleIndex].content);
    }
    void TryExpand()
    {
        if (new_load)
        {
            bm.TryInstance(BubbleIndex, 1, startH - (TextHeight / 2f + bm.消息间距) * rectTransform.localScale.y);
        }
        if (last_load)
        {
            bm.TryInstance(BubbleIndex, -1, startH + (TextHeight / 2f + bm.消息间距) * rectTransform.localScale.y);
        }
    }
    void Inside()
    {
        float height = TextHeight * rectTransform.localScale.y ;
        float y = rectTransform.anchoredPosition.y;
        Vector2 bar_range = new(y - height / 2f, y + height / 2f);
        //下界大于屏幕下界则需要加载后续消息
        new_load = bar_range[0] > ScreenRange[0];
        //上界小于屏幕上界则需要加载前面消息
        last_load = bar_range[1] < ScreenRange[1];
        //下界大于屏幕上界或上界小于屏幕下界，则隐藏该消息
        show = !(bar_range[0] > ScreenRange[1] || bar_range[1] < ScreenRange[0]);
        //超出屏幕2倍边界执行休眠回到Pool中
        if (bar_range[0] > ScreenRange[1] * 2f || bar_range[1] < ScreenRange[0] * 2f)
        {
            bm.BackPool(this);
        }
    }

    [Header("字体大小设置")]
    [SerializeField] private int baseFontSize = 63; // 你的基础字体大小
    [SerializeField] private int h1Size = 95;      // 一级标题大小
    [SerializeField] private int h2Size = 82;      // 二级标题大小
    [SerializeField] private int h3Size = 70;      // 三级标题大小

    /// <summary>
    /// 修复的Markdown转Unity富文本方法
    /// </summary>
    public string ConvertMarkdownToUnityRichText(string markdownText)
    {
        if (string.IsNullOrEmpty(markdownText))
            return markdownText;

        string result = markdownText;

        // 先处理代码块 ```，避免代码块内部的其他Markdown语法被误转换
        result = ConvertAllBacktickCode(result);

        // 再处理行内代码 `（注意：这里的行内代码不会影响代码块内的内容，因为代码块已经被处理了）
        result = Regex.Replace(result, @"`(.*?)`",
            $"<color=#888888>$1</color>");

        // **文本** 转换为 <b>文本</b>
        result = Regex.Replace(result, @"\*\*(.*?)\*\*", "<b>$1</b>");

        // *文本* 或 _文本_ 转换为 <i>文本</i>
        result = Regex.Replace(result, @"\*(?!\*)(.*?)\*", "<i>$1</i>");
        result = Regex.Replace(result, @"_(?!_)(.*?)_", "<i>$1</i>");

        // ~~文本~~ 转换为 <s>文本</s>
        result = Regex.Replace(result, @"~~(.*?)~~", "<s>$1</s>");

        // # 标题 转换为 <size=88><b>标题</b></size>
        result = Regex.Replace(result, @"^# (.*?)$",
            $"<size={h1Size}><b>$1</b></size>",
            RegexOptions.Multiline);

        // ## 标题 转换为 <size=77><b>标题</b></size>
        result = Regex.Replace(result, @"^## (.*?)$",
            $"<size={h2Size}><b>$1</b></size>",
            RegexOptions.Multiline);

        // ### 标题 转换为 <size=70><b>标题</b></size>
        result = Regex.Replace(result, @"^### (.*?)$",
            $"<size={h3Size}><b>$1</b></size>",
            RegexOptions.Multiline);

        // 处理分割线 --- 转换为 <color=#666666>────────────</color>
        result = Regex.Replace(result, @"^---\s*$",
            "<color=#666666>──────────────────</color>",
            RegexOptions.Multiline);

        return result;
    }

    /// <summary>
    /// 处理三个反引号的代码块
    /// </summary>
    private string ConvertAllBacktickCode(string text)
    {
        // 先处理三个反引号的多行代码块
        string result = Regex.Replace(
            text,
            @"```(\w*)\s*\n(.*?)```",
            match =>
            {
                string code = match.Groups[2].Value;
                return $"\n<color=#888888><size={baseFontSize - 5}>{code}</size></color>\n";
            },
            RegexOptions.Singleline
        );

        // 再处理行内代码（包括两个反引号和单个反引号）
        result = Regex.Replace(
            result,
            @"`+([^`]+)`+",
            match => $"<color=#888888><size={baseFontSize - 5}>{match.Groups[1].Value}</size></color>"
        );

        return result;
    }
}
