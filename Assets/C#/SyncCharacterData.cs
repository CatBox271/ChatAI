using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net.Sockets;
using System;
using System.Text;
using Newtonsoft.Json;

public class SyncCharacterData : MonoBehaviour
{
    [Header("同步文件服务器")]
    public string serverIp;
    public int serverPort = 6543;
    [Header("默认用户名")]
    public string user_name = "admin";
    public string password = "ice_bubble_1231";
    TcpClient _client;
    NetworkStream _stream;

    public static SyncCharacterData instance;

    public Action Restart;

    public bool stopSync;
    SLManager sl;

    bool loaded = false;
    private void Awake()
    {
        serverIp = PlayerPrefs.GetString("SyncIP", "frp-sea.com");
        serverPort = PlayerPrefs.GetInt("SyncPort", 64867);

        instance = this;
        TryGetComponent(out sl);
        SLManager.instance = sl;

        StartConnection(StartJudge);
    }

    public void StartConnection(Action success = null, Action fail = null)
    {
        if (_client != null && _client.Connected || _isConnecting) return;
        StartCoroutine(ConnectCoroutine(success,fail));
    }

    public void StartJudge()
    {
        if (stopSync) return;
        StartCoroutine(Jugde());
    }

    public void DownloadRefresh(bool success)
    {
        if (success)
        {
            Chat.chat.WhileCharacterChange?.Invoke();
            Restart?.Invoke();
        }
    }

    IEnumerator Jugde()
    {
        if (_client != null && _stream != null)
        {
            string got_time = "";
            yield return HandleCheckUpdate("Character", (timeResult) =>
             {
                 got_time = timeResult;
             });

            DateTime ServerDate, LocalDate;
            if (string.IsNullOrEmpty(got_time) || (ServerDate = sl.ParseModified(got_time)) < (LocalDate = sl.ParseModified(sl.GetModifiedDateTxt())))
            {
                //需要上传更新
                Debug.Log($"判断上传");
                Chat.chat.api.SaveSyncedApiConfig();
                //Notification.instance.AddPopUp("上传存档");
                yield return HandleUpload(null);
            }
            else
            {
                if (ServerDate > LocalDate)
                {
                    //需要下载
                    Debug.Log($"判断下载");
                    Notification.instance.AddPopUp("下载存档");
                    yield return HandleDownload("Character", DownloadRefresh);
                }
                else
                {
                    Debug.Log($"判断相同");
                    Notification.instance.AddPopUp("存档相同");
                }
            }
        }
    }

    private bool _isConnecting = false; // 防止重复连接
    private IEnumerator ConnectCoroutine(Action success = null,Action fail = null)
    {
        if (stopSync) yield break;
        _isConnecting = true;

        _client = new TcpClient();
        string realIP = string.IsNullOrEmpty(serverIp) ? "127.0.0.1" : serverIp;

        bool connected = false;
        Exception connectEx = null;

        // 异步开始连接
        _client.BeginConnect(realIP, serverPort, ar =>
        {
            try
            {
                _client.EndConnect(ar);
                connected = true;
            }
            catch (Exception e)
            {
                connectEx = e;
            }
        }, null);

        float timeout = 2.5f;
        float startTime = Time.time;
        while (!connected && Time.time - startTime < timeout)
        {
            yield return null; // 每帧检查，不阻塞主线程
        }

        if (!connected || !_client.Connected)
        {
            Debug.LogError(connectEx?.Message ?? "连接超时或失败");
            Notification.instance.AddPopUp("连接失败");
            _client?.Close();
            _client = null;

            fail?.Invoke();
            yield break;
        }

        _stream = _client.GetStream();

        Debug.Log("TCP 长连接已建立");

        _isConnecting = false;

        yield return HandleLogin(success , fail);
    }

    private void StopConnection()
    {
        _stream?.Close();
        _client?.Close();
        _stream = null;
        _client = null;
    }
    #region 对服务器的请求
    IEnumerator HandleLogin(Action OnResult = null, Action fail = null)
    {
        //登录
        object request = new {
            request = "login",
            username = user_name,
            password,
        };
        string json = JsonConvert.SerializeObject(request);

        yield return SendRequest(_stream, json);

        print("发送登录请求完毕");

        bool succuss = false;

        yield return GetResult(_stream, (result) => {
            //解析结果
            succuss = result == "success";
        });

        if (succuss)
        {
            OnResult?.Invoke();
            print("服务器登录成功");
        }
        else
        {
            fail?.Invoke();
            Notification.instance.AddPopUp("同步账号密码错误");
        }
    }
    IEnumerator HandleUpload(Action OnResult = null)
    {
        //先读取再上传
        byte[] zip = sl.CompressCharacterFolder();
        string modified_date = sl.GetModifiedDateTxt();

        //上传请求
        object request = new
        {
            request = "upload",
            file_name = "Character",
            modified_date,
        };
        string json = JsonConvert.SerializeObject(request);

        print(json);
        yield return SendRequest(_stream, json);

        print("上传请求完毕");

        //上传数据
        yield return SendRequest(_stream, zip);

        print("上传数据完毕");

        bool succuss = false;

        yield return GetResult(_stream, (result) => {
            //解析结果
            succuss = result == "success";
        });

        if (succuss)
        {
            OnResult?.Invoke();
            print("上传数据成功");
        }
    }

    IEnumerator HandleCheckUpdate(string fileName, Action<string> OnResult)
    {
        // 构造请求 JSON
        object request = new
        {
            request = "check_update",
            file_name = fileName
        };
        string json = JsonConvert.SerializeObject(request);

        // 发送请求
        yield return SendRequest(_stream, json);

        string modifiedDate = null;
        bool success = false;

        // 接收响应
        yield return GetResult(_stream, (result) =>
        {
            // 服务器成功时返回内容（非空），失败时返回空字符串（对应长度头 0）
            if (!string.IsNullOrEmpty(result))
            {
                modifiedDate = result;
                success = true;
            }
        });

        // 仅当成功时调用回调；失败时传递 null
        OnResult?.Invoke(success ? modifiedDate : null);
    }


    /// <summary>
    /// 下载指定文件的 .zip 数据并解压到 Character 文件夹
    /// </summary>
    /// <param name="fileName">文件名（不含后缀，例如 "Character"）</param>
    /// <param name="OnResult">结果回调，参数为是否成功</param>
    IEnumerator HandleDownload(string fileName, Action<bool> OnResult = null)
    {
        // 1. 构造下载请求
        object request = new
        {
            request = "download",
            file_name = fileName
        };
        string json = JsonConvert.SerializeObject(request);

        // 2. 发送 JSON 请求
        yield return SendRequest(_stream, json);

        // 3. 读取响应长度头（4 字节大端）
        byte[] lenBuff = new byte[4];
        yield return SafeRead(_stream, lenBuff, 4);
        if (lenBuff == null) // 连接异常
        {
            OnResult?.Invoke(false);
            yield break;
        }
        int respLen = System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt32(lenBuff, 0));

        // 4. 判断失败（长度 ≤ 0）
        if (respLen <= 0)
        {
            Debug.LogWarning("下载失败：服务器端文件不存在");
            OnResult?.Invoke(false);
            yield break;
        }

        // 5. 读取完整的 zip 二进制数据
        byte[] zipData = new byte[respLen];
        yield return SafeRead(_stream, zipData, respLen);
        if (zipData == null || zipData.Length != respLen)
        {
            Debug.LogError("下载数据不完整");
            OnResult?.Invoke(false);
            yield break;
        }

        // 6. 调用 SLManager 的解压方法（自动备份旧文件）
        sl.DepressCharacterFolder(zipData);
        Debug.Log("服务器文件下载并解压成功");
        OnResult?.Invoke(true);
    }
    #endregion

    byte[] Combine(byte[] a,byte[] b)
    {
        var combine = new byte[a.Length + b.Length];
        Buffer.BlockCopy(a, 0, combine, 0, a.Length);
        Buffer.BlockCopy(b, 0, combine, a.Length, b.Length);
        return combine;
    }

    byte[] GetByteLen(string input)
    {
        return BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(input.Length));
    }
    private IEnumerator SendRequest(NetworkStream stream, byte[] requestBytes)
    {
        byte[] requestLen = BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(requestBytes.Length));
        byte[] combine = Combine(requestLen, requestBytes);

        yield return SafeWrite(stream, combine);
    }

    private IEnumerator SendRequest(NetworkStream stream , string request)
    {
        byte[] requestBytes = Encoding.UTF8.GetBytes(request);
        byte[] requestLen = GetByteLen(request);
        byte[] combine = Combine(requestLen, requestBytes);

        yield return SafeWrite(stream, combine);
    }
    private IEnumerator GetResult(NetworkStream stream, Action<string> OnResult)
    {
        byte[] lenBuff = new byte[4];

        //读取4字节长度信息
        yield return SafeRead(stream, lenBuff, 4);
        int respLen = System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt32(lenBuff));
        if (respLen < 0) yield break;

        byte[] result = new byte[respLen];
        yield return SafeRead(stream, result, respLen);

        OnResult?.Invoke(Encoding.UTF8.GetString(result));
    }


    private IEnumerator SafeWrite(NetworkStream stream, byte[] data)
    {
        var task = stream.WriteAsync(data, 0, data.Length);
        yield return new WaitUntil(() => task.IsCompleted);
        if (task.IsFaulted)
            throw task.Exception;
    }
    private IEnumerator SafeRead(NetworkStream stream, byte[] buffer, int totalBytes)
    {
        int bytesRead = 0;
        while (bytesRead < totalBytes)
        {
            var readTask = stream.ReadAsync(buffer, bytesRead, totalBytes - bytesRead);
            yield return new WaitUntil(() => readTask.IsCompleted);
            if (readTask.IsFaulted)
            {
                yield break;
            }
            int read = readTask.Result;
            if (read == 0)
            {
                yield break;
            }
            bytesRead += read;
        }
    }
    private void OnApplicationQuit()
    {
        StopConnection();
    }
    private void OnDestroy()
    {
        StopConnection();
    }
}
