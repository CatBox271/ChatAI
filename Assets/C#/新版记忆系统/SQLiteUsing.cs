using SQLite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

#region 数据库表定义
public class OriginalNode
{
    public int id;
    public List<int> memory;

    public void AddMemory(int id)
    {
        if (memory != null)
        {
            if(!memory.Contains(id)) memory.Add(id);
        }
        else memory = new() { id };
    }

    public void AddMemory(List<int> ids)
    {
        if (memory != null)
        {
            foreach (int id in ids)
            {
                if (!memory.Contains(id)) memory.Add(id);
            }
        }
        else memory = ids;
    }
}

[Table("Memories")]
public class MemClip
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string Content { get; set; }
    public long CreateTime { get; set; }
    public string AnotherTime { get; set; }
}
#endregion

public struct MemInfo
{
    public string Content;
    public List<long> Times;
    public string ToDetailText()
    {
        if (string.IsNullOrEmpty(Content)) return string.Empty;
        var timeStr = Times != null && Times.Count > 0
            ? string.Join(", ", Times.Select(t => DateTimeOffset.FromUnixTimeSeconds(t * 3600).ToLocalTime().ToString("yyyy-MM-dd HH")))
            : "";
        return $"出现时间: {timeStr},记忆内容：\n{Content}\n";
    }
}

public class SQManger
{
    private SQLiteConnection _db;
    private string _dbPath;

    public SQManger(string dbPath)
    {
        _dbPath = Path.Combine(dbPath, "SQMemory.db");
        Open();
        _db.CreateTable<MemClip>();
        SLManager.instance.QuitSQLite += Close;
        // 不再使用WAL模式，避免数据丢失
        // _db.Execute("PRAGMA journal_mode=WAL;");
    }

    public void Open()
    {
        if (_db != null) return;
        _db = new SQLiteConnection(_dbPath);
    }

    public void Close()
    {
        if (_db != null)
        {
            _db.Close();
            _db.Dispose();
            _db = null;
        }
    }

    public int AddMemory(string content, long? customHourTimestamp = null)
    {
        EnsureOpen();
        long createHour = customHourTimestamp ?? GetCurrentHourTimestamp();
        var mem = new MemClip
        {
            Content = content,
            CreateTime = createHour,
            AnotherTime = null
        };
        _db.Insert(mem);
        return mem.Id;
    }

    public int AddMemory(string content) => AddMemory(content, null);

    public void RecordOccurrence(int memoryId)
    {
        EnsureOpen();
        var mem = _db.Find<MemClip>(memoryId);
        if (mem == null) return;
        long currentHour = GetCurrentHourTimestamp();
        long offset = currentHour - mem.CreateTime;
        if (offset <= 0) return;
        var offsets = string.IsNullOrEmpty(mem.AnotherTime)
            ? new List<long>()
            : mem.AnotherTime.Split(',').Select(long.Parse).ToList();
        if (!offsets.Contains(offset))
        {
            offsets.Add(offset);
            mem.AnotherTime = string.Join(",", offsets);
            _db.Update(mem);
        }
    }

    public MemInfo GetMemoryInfo(int id)
    {
        EnsureOpen();
        var mem = _db.Find<MemClip>(id);
        if (mem == null) return new MemInfo { Content = null, Times = new List<long>() };
        var times = new List<long> { mem.CreateTime };
        if (!string.IsNullOrEmpty(mem.AnotherTime))
        {
            var offsets = mem.AnotherTime.Split(',').Select(long.Parse);
            times.AddRange(offsets.Select(off => mem.CreateTime + off));
        }
        times.Sort();
        return new MemInfo { Content = mem.Content, Times = times };
    }

    public List<MemInfo> GetMemoriesInfo(List<int> ids)
    {
        if (ids == null || ids.Count == 0) return new List<MemInfo>();
        EnsureOpen();
        var results = new List<MemInfo>();
        foreach (int id in ids)
        {
            var info = GetMemoryInfo(id);
            if (info.Content != null) results.Add(info);
        }
        return results;
    }

    public void DeleteMemory(int id)
    {
        EnsureOpen();
        _db.Delete<MemClip>(id);
    }

    private void EnsureOpen()
    {
        if (_db == null) Open();
    }

    private long GetCurrentHourTimestamp()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 3600;
    }
}