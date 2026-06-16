using System.Collections.Generic;
using UnityEngine;
using System.IO;

/// <summary>
/// 提示词管理器，从 Resources 文件夹加载 .txt 文件，带缓存，同步读取。
/// 文件路径示例：Assets/Resources/Prompts/transform_system.txt
/// 加载时使用资源路径 "Prompts/transform_system"（不含扩展名）
/// </summary>
public static class Prompt
{
    private static Dictionary<string, string> _cache = new Dictionary<string, string>();

    /// <summary>
    /// 获取提示词内容
    /// </summary>
    /// <param name="resourcePath">Resources 下的路径，不含扩展名，例如 "Prompts/transform_system"</param>
    /// <param name="defaultValue">如果文件不存在，返回的默认值（可选）</param>
    /// <returns>提示词文本</returns>
    public static string GetPrompt(string resourcePath, string defaultValue = "")
    {
        if (_cache.TryGetValue(resourcePath, out string cached))
            return cached;

        TextAsset asset = Resources.Load<TextAsset>(Path.Combine("Prompts", resourcePath));
        if (asset == null)
        {
            Debug.LogError($"提示词文件未找到: Resources/{resourcePath}.txt");
            return defaultValue;
        }

        string content = asset.text;
        _cache[resourcePath] = content;
        return content;
    }

    /// <summary>
    /// 清空缓存（必要时调用）
    /// </summary>
    public static void ClearCache()
    {
        _cache.Clear();
    }
}