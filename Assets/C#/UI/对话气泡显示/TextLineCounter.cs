using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TextLineCounter : MonoBehaviour
{
    public Text textComponent;
    public TextMeshProUGUI TMPComponment;
    public TMP_InputField inputField;

    // ------ 缓存变量（旧版 Text） ------
    private string _lastTextForText = null;
    private float _cachedHeightForText = 0f;

    // ------ 缓存变量（TMP） ------
    private string _lastTextForTMP = null;
    private float _cachedHeightForTMP = 0f;

    // ------ 外部调用入口 ------
    public float GetTextLineCount()
    {
        if (textComponent != null) return TextLineCount();
        if (TMPComponment != null) return TMPLineCount();
        return 0;
    }

    // ------ 旧版 Text 高度计算（带缓存） ------
    public float TextLineCount()
    {
        if (textComponent == null) return 0;

        string currentText = textComponent.text;

        if (_lastTextForText == currentText)
            return _cachedHeightForText;

        TextGenerator generator = new TextGenerator();
        TextGenerationSettings settings = textComponent.GetGenerationSettings(
            textComponent.rectTransform.rect.size
        );

        float textHeight = generator.GetPreferredHeight(currentText, settings);

        _lastTextForText = currentText;
        _cachedHeightForText = textHeight;

        return textHeight;
    }

    // ------ TMP 高度计算（带缓存） ------
    public float TMPLineCount()
    {
        if (TMPComponment == null) return 0;

        // 如果有 InputField，从 inputField 获取/修改文本
        bool hasInputField = inputField != null;
        string currentText = hasInputField ? inputField.text : TMPComponment.text;

        if (_lastTextForTMP == currentText)
            return _cachedHeightForTMP;

        // 构造修改后的文本（每个换行符后追加 'a'）
        string modified = currentText.Replace("\n", "\na");

        // 临时修改文本（有 InputField 就走 inputField，否则直接改 TMP）
        if (hasInputField)
        {
            inputField.text = modified;
            inputField.ForceLabelUpdate();
        }
        else
        {
            TMPComponment.text = modified;
            TMPComponment.ForceMeshUpdate();
        }

        // 读取高度
        float height = TMPComponment.preferredHeight;

        // 还原原始文本
        if (hasInputField)
        {
            inputField.text = currentText;
            inputField.ForceLabelUpdate();
        }
        else
        {
            TMPComponment.text = currentText;
            TMPComponment.ForceMeshUpdate();
        }

        _lastTextForTMP = currentText;
        _cachedHeightForTMP = height;

        return height;
    }
}