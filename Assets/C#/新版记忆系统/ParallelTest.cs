using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using Newtonsoft.Json;

public class ParallelTest : MonoBehaviour
{
    public int requestCount = 100;
    public bool runTest = false;

    private string auxUrl = "https://api.deepseek.com/v1/chat/completions";
    private string auxKey => GlobalApiConfig.Load(PlayerPrefs.GetString("Character", "")).auxApiKey;
    private JsonSerializerSettings jsonSettings = new() { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.None };

    void Update()
    {
        if (runTest)
        {
            runTest = false;
            StartCoroutine(RunTest());
        }
    }

    System.Collections.IEnumerator RunTest()
    {
        var swTotal = Stopwatch.StartNew();
        var swEach = new Stopwatch[requestCount];
        long[] times = new long[requestCount];
        int completed = 0;

        var payload = new
        {
            model = "deepseek-v4-flash",
            temperature = 0.2,
            max_tokens = 100,
            stream = false,
            messages = new[] {
                new { role = "user", content = "回复一个数字：42" }
            }
        };
        string jsonData = JsonConvert.SerializeObject(payload, jsonSettings);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);

        for (int i = 0; i < requestCount; i++)
        {
            int idx = i;
            swEach[idx] = Stopwatch.StartNew();

            var req = UnityEngine.Networking.UnityWebRequest.PostWwwForm(auxUrl, "");
            req.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", $"Bearer {auxKey}");

            var op = req.SendWebRequest();
            op.completed += (_) =>
            {
                swEach[idx].Stop();
                times[idx] = swEach[idx].ElapsedMilliseconds;
                req.Dispose();
                System.Threading.Interlocked.Increment(ref completed);
            };
        }

        yield return new WaitUntil(() => completed >= requestCount);
        swTotal.Stop();

        Array.Sort(times);
        long p50 = times[requestCount / 2];
        long p90 = times[(int)(requestCount * 0.9)];
        long p99 = times[(int)(requestCount * 0.99)];
        long min = times[0];
        long max = times[requestCount - 1];

        UnityEngine.Debug.Log($"[并行测试] {requestCount}请求 总{swTotal.Elapsed.TotalSeconds:F1}s " +
                  $"min={min}ms p50={p50}ms p90={p90}ms p99={p99}ms max={max}ms");
    }
}
