using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Globalization;
using System.IO.Compression;
using System.Collections;

// 包装类用于序列化整个列表
[System.Serializable]
public class ConversationHistory
{
    public List<DeepSeekMessage> messages;

    public ConversationHistory()
    {
        messages = new List<DeepSeekMessage>();
    }

    public ConversationHistory(List<DeepSeekMessage> messages)
    {
        this.messages = messages;
    }
}

[System.Serializable]
public class ConversationHistoryWrapper
{
    public List<DeepSeekMessage> messages;
    public string exportTime;
    public int messageCount;

    public ConversationHistoryWrapper() { }

    public ConversationHistoryWrapper(List<DeepSeekMessage> messages)
    {
        this.messages = messages;
        this.messageCount = messages?.Count ?? 0;
        this.exportTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }
}

// 包装类用于序列化AI长期记忆
[System.Serializable]
public class ConversationMemory
{
    public List<Memory> memory = new();
}
[System.Serializable]
public class Memory
{
    //记忆片段
    public string clip;
    //记忆时间
    public string dateTime;

    public int score = 50;

    public Memory(string _clip,int _score = 50)
    {
        clip = _clip;
        dateTime = DateTime.Now.ToString("G");
        score = _score;
    }
}
public class SLManager : MonoBehaviour
{

    public static SLManager instance;

    private void Start()
    {
        //交由SyncCharacterData保管保证顺序 instance = this;
        StartCoroutine(LoopModified());
    }

    string _persistentDataPath = "";
    public string persistentDataPath
    {
        get
        {
            if (_persistentDataPath == "") _persistentDataPath = Application.persistentDataPath;
            return _persistentDataPath;
        }
    }
    public bool QuickLoad(Chat chat, Action<DateTime> backLastTime = null)
    {
        bool combine = false;
        bool split= false;
        string importedTime = "";

        Action<DateTime> wrappedCallback = (dt) =>
        {
            importedTime = dt.ToString();  // 转换为字符串存入
            backLastTime?.Invoke(dt);      // 调用原有的回调（如果有）
        };

        if (ImportFromFolder(chat, chat.CharacterPath, "message.json", wrappedCallback) == null)
        {
            //没有存档就
            chat.messages.Clear();
        }
        else
        {
            combine = CombineMessage(chat, importedTime);
        }
        split = SplitMessage(chat);
        print($"读取时修改{split || combine}");
        return !(split || combine);
    }
    public void QuickSave(Chat chat)
    {
        ExportConversationToJson(chat, "message.json");
    }

    public int monoMessageMaxLength = 4096;

    public bool CombineMessage(Chat chat, string importedTime)
    {
        bool success = false;
        string SplitedMessages = Path.Combine(chat.CharacterPath, "SplitedMessages");
        int lastIndex = -1;
        if (File.Exists(Path.Combine(persistentDataPath, SplitedMessages, "0_message.json")))
        {
            do
            {
                lastIndex++;
            } while (File.Exists(Path.Combine(persistentDataPath, SplitedMessages, $"{lastIndex}_message.json")));
        }
        else
        {
            Debug.Log("头文件:0_message.json不存在");
        }
        if (lastIndex == -1) return false;
        Debug.Log($"SplitedMessages文件夹内含有{lastIndex}个文件");
        lastIndex--;

        while (lastIndex > -1 && chat.messages.Count < monoMessageMaxLength)
        {
            //尝试进行补齐
            List<DeepSeekMessage> lastMessageClip = ImportFromJson<ConversationHistoryWrapper>(SplitedMessages, $"{lastIndex}_message.json")?.messages;
            if (lastMessageClip == null)
            {
                Debug.Log($"未能成功读取兼容性合并对象:{lastIndex}_message.json");
                break;
            }
            try
            {
                Chat.chat.messages.InsertRange(0, lastMessageClip);
                //替换原有Message
                ExportConversationToJson(Chat.chat.messages, Chat.chat.CharacterPath, "message.json", false, importedTime);
            }
            catch
            {
                PopUp.instance.SetConfirm(null, "兼容性替换出现错误！", false);
                Debug.LogError("兼容性替换出现错误！");
                break;
            }
            finally
            {
                //删除被合并的Clip
                File.Delete(Path.Combine(persistentDataPath ,SplitedMessages, $"{lastIndex}_message.json"));
                success = true;
            }
            lastIndex--;
        }
        return success;
    }

    public bool SplitMessage(Chat chat)
    {
        bool error = false;

        if (chat.messages.Count <= monoMessageMaxLength * 2) return false;
        List<DeepSeekMessage> back_up = new();
        back_up.AddRange(chat.messages);
        ExportConversationToJson(back_up, chat.CharacterPath, $"back_up.json");

        try
        {
            string SplitedMessages = Path.Combine(chat.CharacterPath, "SplitedMessages");
            //以2048为阈值，1024为分界点分离对话记录
            while (chat.messages.Count > monoMessageMaxLength * 2)
            {
                List<DeepSeekMessage> splited = new();
                splited.AddRange(chat.messages);
                chat.messages.RemoveRange(0, monoMessageMaxLength);
                splited.RemoveRange(monoMessageMaxLength, splited.Count - monoMessageMaxLength);
                print(splited.Count);

                int file_index = 0;
                while (true)
                {
                    //先检查输出目录是否有相同文件名，如果有序列加一
                    if (File.Exists(Path.Combine(persistentDataPath, SplitedMessages, $"{file_index}_message.json")))
                    {
                        file_index++;
                    }
                    else
                    {
                        //导出
                        error = !string.IsNullOrEmpty(ExportConversationToJson(splited, SplitedMessages, $"{file_index}_message.json"));
                        file_index++;
                        break;
                    }
                }
            }
        }
        catch(Exception e)
        {
            Debug.LogError(e);
            error = true;

            ExportToJson(e.ToString(), chat.CharacterPath, "error.json");
        }

        //最后覆盖原消息
        if (!error) QuickSave(chat);
        return !error;
    }

    #region 导出方法
    /// <summary>
    /// 导出到软件目录指定文件夹下
    /// </summary>
    /// <param name="path"></param>
    /// <param name="fileName"></param>
    /// <param name="includeSystemMessages"></param>
    /// <returns></returns>
    public string ExportConversationToJson(Chat chat, string fileName, bool includeSystemMessages = true)
    {
        return ExportConversationToJson(chat.messages, chat.CharacterPath, fileName, includeSystemMessages);
    }
    /// <summary>
    /// 导出指定的消息列表
    /// </summary>
    /// <param name="path"></param>
    /// <param name="fileName"></param>
    /// <param name="includeSystemMessages"></param>
    /// <returns></returns>
    public string ExportConversationToJson(List<DeepSeekMessage> messages, string path, string fileName, bool includeSystemMessages = true, string exportTime = "")
    {
        try
        {
            // 准备要导出的消息列表
            List<DeepSeekMessage> messagesToExport = new List<DeepSeekMessage>();

            foreach (var msg in messages)
            {
                if (!includeSystemMessages && msg.role == "system")
                    continue;

                ////清空思维链
                //var instand = new DeepSeekMessage
                //{
                //    content = msg.content,
                //    role = msg.role,
                //    name = msg.name,
                //    tool_call_id = msg.tool_call_id,
                //    tool_calls = msg.tool_calls
                //};
                //messagesToExport.Add(instand);

                messagesToExport.Add(msg);
            }

            // 如果没有任何消息，返回null
            if (messagesToExport.Count == 0)
            {
                Debug.LogWarning("没有可导出的对话历史");
                return null;
            }

            // 创建包装对象
            var history = new ConversationHistoryWrapper(messagesToExport);
            if (!string.IsNullOrEmpty(exportTime)) history.exportTime = exportTime;
            // Newtonsoft.Json序列化设置
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.Indented,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = null  // 保持原字段名
                }
            };

            // 使用Newtonsoft.Json序列化
            string json = JsonConvert.SerializeObject(history, settings);

            // 确定文件名
            string actualFileName = string.IsNullOrEmpty(fileName) ?
                GenerateDefaultFileName() : fileName;

            if (!actualFileName.EndsWith(".json"))
                actualFileName += ".json";

            // 确定文件路径
            string filePath = Path.Combine(persistentDataPath, path);
            if (!Directory.Exists(filePath))
                Directory.CreateDirectory(filePath);
            filePath = Path.Combine(filePath, actualFileName);

            // 写入文件
            File.WriteAllText(filePath, json, Encoding.UTF8);

            //Debug.Log($"对话历史已导出到: {filePath}");

            return null;
        }
        catch (Exception e)
        {
            Debug.LogError($"导出失败: {e.Message}\n{e.StackTrace}");
            return e.Message;
        }
        finally
        {
            need_modified++;
        }
    }

    private int need_modified = 0;
    IEnumerator LoopModified()
    {
        while (true)
        {
            yield return null;
            if (need_modified > 0) SetModifiedDateTxt();
            need_modified = 0;
        }
    }

    /// <summary>
    /// 导出任意对象到JSON文件
    /// </summary>
    public string ExportToJson<T>(T data, string folderPath = "", string fileName = null, bool prettyPrint = true)
    {
        try
        {
            if (data == null)
            {
                Debug.LogWarning("导出数据为空");
                return null;
            }

            string json = JsonConvert.SerializeObject(data, prettyPrint ? Formatting.Indented : Formatting.None);
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning("JSON序列化失败");
                return null;
            }

            string actualFileName = string.IsNullOrEmpty(fileName) ?
                GenerateDefaultFileName() : fileName;

            if (!actualFileName.EndsWith(".json"))
                actualFileName += ".json";

            string basePath = persistentDataPath;

            if (!string.IsNullOrEmpty(folderPath))
            {
                basePath = Path.Combine(basePath, folderPath);
            }

            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
            }

            string filePath = Path.Combine(basePath, actualFileName);
            File.WriteAllText(filePath, json, Encoding.UTF8);

            return filePath;
        }
        catch (Exception e)
        {
            Debug.LogError($"导出失败: {e.Message}\n{e.StackTrace}");
            return null;
        }
    }

    // 生成默认文件名（带时间戳）
    private string GenerateDefaultFileName()
    {
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return $"conversation_{timestamp}.json";
    }

    readonly string modifiedDateItemName = "ModifiedDate.txt";

    public void SetModifiedDateTxt()
    {
        string filePath = Path.Combine(persistentDataPath, "Character", modifiedDateItemName);
        string content = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        File.WriteAllText(filePath, content);
        StartCoroutine(LimitUploadInterval());
    }


    public DateTime ParseModified(string input)
    {
        if (DateTime.TryParseExact(
               input,
               "yyyy-MM-dd HH:mm:ss",
               CultureInfo.InvariantCulture,
               DateTimeStyles.AssumeUniversal,
               out DateTime result))
        {
            return result;
        }
        return DateTime.MinValue;
    }
    public string GetModifiedDateTxt()
    {
        string filePath = Path.Combine(persistentDataPath, "Character", modifiedDateItemName);
        if (File.Exists(filePath))
        {
            return File.ReadAllText(filePath);
        }
        return null;
    }

    bool DoubleUpload = false;
    bool uploadInLimint = false;
    IEnumerator LimitUploadInterval()
    {
        if (uploadInLimint)
        { 
            DoubleUpload = true;
            yield break;
        }
        SyncCharacterData sync = SyncCharacterData.instance;
        uploadInLimint = true;
        sync.StartJudge();
        yield return new WaitForSeconds(30f);
        uploadInLimint = false;
        if (DoubleUpload)
        {
            DoubleUpload = false;
            StartCoroutine(LimitUploadInterval());
        }
    }


    #endregion

    #region 导入方法
    public T ImportFromJson<T>(string filePath) where T : new()
    {
        try
        {
            string basePath = Path.Combine(persistentDataPath, filePath);
            if (string.IsNullOrEmpty(basePath))
            {
                Debug.LogWarning("文件路径错误");
                return default;
            }

            if (!File.Exists(basePath))
            {
                Debug.LogWarning($"文件不存在: {basePath}");
                return default;
            }

            string json = File.ReadAllText(basePath, Encoding.UTF8);
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning("文件为空");
                return default;
            }

            T data = JsonConvert.DeserializeObject<T>(json);
            if (data == null)
            {
                Debug.LogWarning("文件解析失败");
                return default;
            }

            Debug.Log($"<color=green>✓ {typeof(T).Name} 导入成功</color>\n路径: {basePath}");
            return data;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"导入失败: {e.Message}");
            return default;
        }
    }

    public T ImportFromJson<T>(string folderPath, string fileName) where T : new()
    {
        try
        {
            string basePath = Path.Combine(persistentDataPath, folderPath, fileName);
            if (string.IsNullOrEmpty(basePath))
            {
                Debug.LogWarning("文件路径错误");
                return default;
            }

            if (!File.Exists(basePath))
            {
                Debug.LogWarning($"文件不存在: {basePath}");
                return default;
            }

            string json = File.ReadAllText(basePath, Encoding.UTF8);
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning("文件为空");
                return default;
            }

            T data = JsonConvert.DeserializeObject<T>(json);
            if (data == null)
            {
                Debug.LogWarning("文件解析失败");
                return default;
            }

            Debug.Log($"<color=green>✓ {typeof(T).Name} 导入成功</color>\n路径: {basePath}");
            return data;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"导入失败: {e.Message}");
            return default;
        }
    }


    /// <summary>
    /// 从JSON文件导入对话历史
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="clearCurrent">是否清空当前历史</param>
    /// <param name="appendToEnd">是否追加到末尾</param>
    /// <returns>导入的消息列表</returns>
    public List<DeepSeekMessage> ImportConversationFromJson(Chat chat, string filePath, bool clearCurrent = true, bool appendToEnd = true, Action<System.DateTime> backLastTime = null)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Debug.LogError($"文件不存在: {filePath}");
                return null;
            }

            // 读取文件内容
            string json = File.ReadAllText(filePath, Encoding.UTF8);

            // 尝试解析为ConversationHistory
            ConversationHistoryWrapper history = new();
            history = JsonUtility.FromJson<ConversationHistoryWrapper>(json);

            if (history == null || history.messages == null)
            {
                // 如果失败，尝试直接解析为消息列表
                Debug.Log("尝试解析为简单消息列表...");

                // 尝试读取原始JSON格式
                if (json.TrimStart().StartsWith("["))
                {
                    // 是数组格式
                    List<DeepSeekMessageWrapper> wrappedMessages = JsonUtility.FromJson<MessageListWrapper>(json).messages;
                    history = new ConversationHistoryWrapper();
                    history.messages = new List<DeepSeekMessage>();

                    foreach (var wrapped in wrappedMessages)
                    {
                        history.messages.Add(new DeepSeekMessage(wrapped.role, wrapped.content));
                    }
                }
            }

            if (history == null || history.messages == null || history.messages.Count == 0)
            {
                Debug.LogError("无法解析对话历史文件");
                return null;
            }

            // 处理当前对话历史
            if (clearCurrent)
            {
                chat.messages.Clear();
            }

            //返回上次对话的时间：
            DateTime.TryParse(history.exportTime, out DateTime lastDate);
            backLastTime?.Invoke(lastDate);

            if (appendToEnd)
            {
                chat.messages.AddRange(history.messages);
            }
            else
            {
                chat.messages.InsertRange(0, history.messages);
            }

            Debug.Log($"成功导入 {history.messages.Count} 条消息");

            // 打印导入的消息摘要
            foreach (var msg in history.messages)
            {
                string contentPreview = msg.content.Length > 50 ?
                    msg.content.Substring(0, 50) + "..." : msg.content;
                //Debug.Log($"导入: [{msg.role}] {contentPreview}");
            }

            return history.messages;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"导入失败: {e.Message}");
            return null;
        }
    }
    public List<DeepSeekMessage> ImportFromFolder(Chat chat, string path, string fileName,Action<DateTime> _backLastTime = null)
    {
        try
        {
            string folderPath = Path.Combine(persistentDataPath, path);

            if (!Directory.Exists(folderPath))
            {
                Debug.LogError($"导入文件夹不存在: {folderPath}");
                return null;
            }

            // 如果指定了文件名
            if (!string.IsNullOrEmpty(fileName))
            {
                string filePath = Path.Combine(folderPath, fileName);
                if (!File.Exists(filePath))
                {
                    Debug.LogError($"文件不存在: {filePath}");
                    return null;
                }
                return ImportConversationFromJson(chat, filePath,backLastTime: _backLastTime);
            }

            // 获取文件夹中所有JSON文件
            string[] jsonFiles = Directory.GetFiles(folderPath, "*.json");

            if (jsonFiles.Length == 0)
            {
                Debug.LogWarning($"文件夹中没有JSON文件: {folderPath}");
                return null;
            }

            // 导入第一个文件
            return ImportConversationFromJson(chat,jsonFiles[0], backLastTime: _backLastTime);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"导入失败: {e.Message}\n{e.StackTrace}");
            return null;
        }
    }
    #endregion

    #region 压缩解压

    public Action QuitSQLite;
    public byte[] CompressCharacterFolder(string aim = "Character", string to = "Character.zip")
    {
        string characterFolderPath = Path.Combine(persistentDataPath, aim);
        string zipFilePath = Path.Combine(persistentDataPath, to);

        //先退出所有SQlite连接
        QuitSQLite?.Invoke();

        // 如果目标 zip 文件已存在，可选择覆盖或抛出异常
        if (File.Exists(zipFilePath))
        {
            File.Delete(zipFilePath);  // 覆盖之前先删除旧文件
        }

        Directory.CreateDirectory(Path.GetDirectoryName(zipFilePath));

        // 将整个 Character 文件夹压缩为 zip
        ZipFile.CreateFromDirectory(
            sourceDirectoryName: characterFolderPath,
            destinationArchiveFileName: zipFilePath,
            compressionLevel: System.IO.Compression.CompressionLevel.Optimal,  // 压缩级别
            includeBaseDirectory: false // 是否在 zip 中保留根目录名
        );

        Debug.Log($"压缩成功：{zipFilePath}");

        return File.ReadAllBytes(zipFilePath);
    }

    public void DepressCharacterFolder(byte[] zip)
    {
        //先退出所有SQlite连接
        QuitSQLite?.Invoke();

        if (Directory.Exists(Path.Combine(persistentDataPath, "Character")))
        {
            //先备份
            try
            {
                CompressCharacterFolder("Character", Path.Combine("backup", $"CharacterBackUp{DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss")}.zip"));
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                return;
            }
        }
        //解压覆盖
        DepressBytes(zip);
    }

    public void DepressBytes(byte[] zip , string aim = "Character", string cache = "Character.zip")
    {
        string FolderPath = Path.Combine(persistentDataPath, aim);
        string zipFilePath = Path.Combine(persistentDataPath, cache);

        // 如果目标 zip 文件已存在，可选择覆盖或抛出异常
        if (File.Exists(zipFilePath))
        {
            File.Delete(zipFilePath);  // 覆盖之前先删除旧文件
        }
        File.WriteAllBytes(zipFilePath,zip);

        if (Directory.Exists(FolderPath))
        {
            Directory.Delete(FolderPath,true);
        }
        ZipFile.ExtractToDirectory(zipFilePath, FolderPath, true);
    }
    #endregion
}

// 包装类用于解析原始JSON数组
[System.Serializable]
public class DeepSeekMessageWrapper
{
    public string role;
    public string content;
}

[System.Serializable]
public class MessageListWrapper
{
    public List<DeepSeekMessageWrapper> messages;
}


