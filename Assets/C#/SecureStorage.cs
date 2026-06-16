using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>
/// API Key 安全存储。
/// 使用 AES 加密，密钥绑定到当前设备。
/// </summary>
public static class SecureStorage
{
    private static byte[] GetKey()
    {
        using var sha = SHA256.Create();
        string deviceSalt = SystemInfo.deviceUniqueIdentifier + "ICE-Bubble-API-Store-v1";
        return sha.ComputeHash(Encoding.UTF8.GetBytes(deviceSalt));
    }

    public static string Protect(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return "";
        try
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            using var aes = Aes.Create();
            aes.Key = GetKey();
            aes.GenerateIV();
            using var encryptor = aes.CreateEncryptor();
            byte[] cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            // 前置 IV
            byte[] result = new byte[aes.IV.Length + cipherBytes.Length];
            Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
            Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);
            return Convert.ToBase64String(result);
        }
        catch
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText));
        }
    }

    public static string Unprotect(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return "";
        try
        {
            byte[] fullBytes = Convert.FromBase64String(cipherText);
            if (fullBytes.Length < 17) return cipherText; // 太短，可能是旧数据
            byte[] iv = new byte[16];
            byte[] cipherBytes = new byte[fullBytes.Length - 16];
            Buffer.BlockCopy(fullBytes, 0, iv, 0, 16);
            Buffer.BlockCopy(fullBytes, 16, cipherBytes, 0, cipherBytes.Length);
            using var aes = Aes.Create();
            aes.Key = GetKey();
            aes.IV = iv;
            using var decryptor = aes.CreateDecryptor();
            byte[] plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (FormatException)
        {
            return cipherText; // 未加密的旧数据
        }
        catch (CryptographicException)
        {
            return cipherText; // 解密失败
        }
    }
}

