using UnityEngine;
using UnityEngine.UI;
using TMPro;
/// <summary>
/// 必须和要测的Text或TMP放在一起
/// </summary>
public class TextLineCounter : MonoBehaviour
{
    public Text textComponent;
    public TextMeshProUGUI TMPComponment;

    // ------ 缓存变量（旧版 Text） ------
    private string _lastTextForText = null;
    private float _cachedHeightForText = 0f;

    // ------ 缓存变量（TMP） ------
    private string _lastTextForTMP = null;
    private float _cachedHeightForTMP = 0f;

    // ------ 外部调用入口 ------
    public float GetTextLineCount(bool A)
    {
        if (textComponent != null) return TextLineCount();
        if (TMPComponment != null) return A ? TMPLineCountA() : TMPLineCountB();
        return 0;
    }

    // ------ 旧版 Text 高度计算（带缓存） ------
    public float TextLineCount()
    {
        if (textComponent == null) return 0;

        string currentText = textComponent.text;

        // 如果文本内容没有变化，直接返回缓存值
        if (_lastTextForText == currentText)
            return _cachedHeightForText;

        // 文本有变化，重新计算
        TextGenerator generator = new TextGenerator();
        TextGenerationSettings settings = textComponent.GetGenerationSettings(
            textComponent.rectTransform.rect.size
        );

        float textHeight = generator.GetPreferredHeight(currentText, settings);

        // 更新缓存
        _lastTextForText = currentText;
        _cachedHeightForText = textHeight;

        return textHeight;
    }

    // ------ TMP 高度计算（带缓存） ------
    public float TMPLineCountA()
    {
        if (TMPComponment == null) return 0;

        string currentText = TMPComponment.text;

        // 如果文本内容没有变化，直接返回缓存值
        if (_lastTextForTMP == currentText)
            return _cachedHeightForTMP;

        // 文本有变化，重新计算
        string modified = currentText.Replace("\n", "\na");
        TMPComponment.ForceMeshUpdate();
        float height = TMPComponment.GetPreferredValues(modified).y;

        // 更新缓存
        _lastTextForTMP = currentText;
        _cachedHeightForTMP = height;

        return height;
    }

    public float TMPLineCountB()
    {
        if (TMPComponment == null) return 0;

        string currentText = TMPComponment.text;

        // 缓存：内容未变，直接返回上次高度
        if (_lastTextForTMP == currentText)
            return _cachedHeightForTMP;

        // --- 内容变化，临时修改文本并读取 preferredHeight ---
        // 1. 构造修改后的文本（每个换行符后追加 'a'）
        string modified = currentText.Replace("\n", "\na");

        // 2. 临时覆盖显示文本
        TMPComponment.text = modified;

        // 3. 强制重建网格，确保 preferredHeight 立即更新
        TMPComponment.ForceMeshUpdate();

        // 4. 读取实际渲染高度
        float height = TMPComponment.preferredHeight;

        // 5. 还原原始文本
        TMPComponment.text = currentText;
        // 可选：再次强制刷新以恢复显示（否则下一帧才更新，但通常无影响）
        TMPComponment.ForceMeshUpdate();

        // 6. 更新缓存
        _lastTextForTMP = currentText;
        _cachedHeightForTMP = height;

        return height;
    }
}