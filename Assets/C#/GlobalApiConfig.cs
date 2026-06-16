using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// API 配置（非设备绑定加密，可跨设备同步）
/// 存储路径：Application.persistentDataPath + "/Character/{name}/APIConfig.json"
/// </summary>
public static class GlobalApiConfig
{
    private static readonly byte[] GlobalKey;

    static GlobalApiConfig()
    {
        using var sha = SHA256.Create();
        GlobalKey = sha.ComputeHash(Encoding.UTF8.GetBytes("ICE-Bubble-Global-Config-Key-v1"));
    }

    private static string ConfigPath(string character) => Path.Combine(Application.persistentDataPath, "Character", character, "APIConfig.json");

    [Serializable]
    class ConfigData
    {
        public string auxApiUrl;
        public string auxApiKeyEncrypted;
        public string ttsSecretIdEncrypted;
        public string ttsSecretKeyEncrypted;
    }

    public static (string auxApiUrl, string auxApiKey, string ttsSecretId, string ttsSecretKey) Load(string character)
    {
        var result = (auxApiUrl: "", auxApiKey: "", ttsSecretId: "", ttsSecretKey: "");
        try
        {
            string path = ConfigPath(character);
            if (File.Exists(path))
            {
                var cfg = JsonConvert.DeserializeObject<ConfigData>(File.ReadAllText(path));
                if (cfg != null)
                {
                    result.auxApiUrl = cfg.auxApiUrl ?? "";
                    result.auxApiKey = Decrypt(cfg.auxApiKeyEncrypted);
                    result.ttsSecretId = Decrypt(cfg.ttsSecretIdEncrypted);
                    result.ttsSecretKey = Decrypt(cfg.ttsSecretKeyEncrypted);
                }
            }
        }
        catch (Exception e) { Debug.LogError($"[GlobalApiConfig] 加载失败: {e.Message}"); }
        return result;
    }

    public static void Save(string character, string auxApiUrl, string auxApiKey, string ttsSecretId, string ttsSecretKey)
    {
        try
        {
            string dir = Path.GetDirectoryName(ConfigPath(character));
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var cfg = new ConfigData
            {
                auxApiUrl = auxApiUrl,
                auxApiKeyEncrypted = Encrypt(auxApiKey),
                ttsSecretIdEncrypted = Encrypt(ttsSecretId),
                ttsSecretKeyEncrypted = Encrypt(ttsSecretKey),
            };
            File.WriteAllText(ConfigPath(character), JsonConvert.SerializeObject(cfg, Formatting.Indented));
        }
        catch (Exception e) { Debug.LogError($"[GlobalApiConfig] 保存失败: {e.Message}"); }
    }

    private static string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return "";
        try
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            using var aes = Aes.Create();
            aes.Key = GlobalKey;
            aes.GenerateIV();
            using var encryptor = aes.CreateEncryptor();
            byte[] cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            byte[] result = new byte[aes.IV.Length + cipherBytes.Length];
            Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
            Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);
            return Convert.ToBase64String(result);
        }
        catch { return Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText)); }
    }

    private static string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return "";
        try
        {
            byte[] fullBytes = Convert.FromBase64String(cipherText);
            if (fullBytes.Length < 17) return cipherText;
            byte[] iv = new byte[16];
            byte[] cipherBytes = new byte[fullBytes.Length - 16];
            Buffer.BlockCopy(fullBytes, 0, iv, 0, 16);
            Buffer.BlockCopy(fullBytes, 16, cipherBytes, 0, cipherBytes.Length);
            using var aes = Aes.Create();
            aes.Key = GlobalKey;
            aes.IV = iv;
            using var decryptor = aes.CreateDecryptor();
            byte[] plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (FormatException) { return cipherText; }
        catch (CryptographicException) { return cipherText; }
    }
}
