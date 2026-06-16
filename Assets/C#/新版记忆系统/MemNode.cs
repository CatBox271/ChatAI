using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class MemNodes
{
    public Dictionary<string, OriginalNode> OriginalNodes = new();
    public Dictionary<string, int> NodeNameToId = new();
    public string allNode = "";
    public int _nextNodeId = 0;

    // 持久化 ban 记录：记忆ID -> 剩余ban轮次
    public Dictionary<int, int> memoryBanCountdown = new();

    [JsonIgnore] public StringBuilder _allNodes = new();
    [JsonIgnore] public SQManger memorySystem;

    public void Startdb(string path)
    {
        memorySystem = new(path);
    }

    /// <summary>
    /// 为多个关键词组关联同一条记忆
    /// </summary>
    public bool AddMemoryForGroups(List<string[]> keywordGroups, string content, long? timestampHour = null)
    {
        if (keywordGroups == null || keywordGroups.Count == 0) return false;
        int memId = timestampHour.HasValue
            ? memorySystem.AddMemory(content, timestampHour.Value)
            : memorySystem.AddMemory(content);

        foreach (var keywords in keywordGroups)
            foreach (var kw in keywords)
                GetOrCreateNode(kw).AddMemory(memId);
        return true;
    }

    [JsonIgnore] readonly int maxSingleLayCount = 3;
    [JsonIgnore] readonly int banCount = 50;
    /// <summary>
    /// 查询多个关键词对应的记忆（一次两两交集 + ban + 每组随机核心记忆，每个关键词随机联想记忆）
    /// </summary>
    public bool GetMemory(string[] keywords, int maxPerGroup, out string[] memory, out string[] associate)
    {
        associate = null;
        memory = null;
        if (keywords == null || keywords.Length == 0) return false;

        // 1. 递减所有 ban 轮次
        var expired = new List<int>();
        var keys = new List<int>(memoryBanCountdown.Keys);
        foreach (var key in keys)
        {
            if (memoryBanCountdown[key] > 1)
                memoryBanCountdown[key] = memoryBanCountdown[key] - 1;
            else
                expired.Add(key);
        }
        foreach (var id in expired)
            memoryBanCountdown.Remove(id);

        // 2. 收集所有有记忆的节点集合
        List<HashSet<int>> memorySets = new List<HashSet<int>>();
        foreach (var kw in keywords)
        {
            var node = GetNode(kw);
            if (node?.memory == null || node.memory.Count == 0) continue;
            memorySets.Add(new HashSet<int>(node.memory));
        }

        // 3. 计算两两交集
        List<HashSet<int>> intersectSets = new List<HashSet<int>>();
        for (int i = 0; i < memorySets.Count; i++)
        {
            for (int j = i + 1; j < memorySets.Count; j++)
            {
                var inter = new HashSet<int>(memorySets[i]);
                inter.IntersectWith(memorySets[j]);
                if (inter.Count > 0)
                    intersectSets.Add(inter);
            }
        }

        // 4. 核心记忆：过滤 ban 并随机取最多 maxPerGroup 条
        var random = new System.Random();
        var finalIds = new HashSet<int>();
        foreach (var set in intersectSets)
        {
            var validIds = set.Where(id => !memoryBanCountdown.ContainsKey(id)).ToList();
            if (validIds.Count == 0) continue;
            if (maxPerGroup != -1 && validIds.Count > maxPerGroup)
                validIds = validIds.OrderBy(_ => random.Next()).Take(maxPerGroup).ToList();
            finalIds.UnionWith(validIds);
        }
        foreach (var id in finalIds)
            memoryBanCountdown[id] = banCount;

        // 5. 联想记忆：每个关键词节点随机抽一条未 ban 的记忆（不受交集限制）
        var associateIds = new HashSet<int>();
        foreach (var kw in keywords)
        {
            var node = GetNode(kw);
            Debug.Log(kw);
            if (node?.memory == null || node.memory.Count == 0) continue;
            var validIds = node.memory.Where(id => !memoryBanCountdown.ContainsKey(id)).ToList();
            if (validIds.Count > 0)
            {
                int picked = validIds[random.Next(validIds.Count)];
                associateIds.Add(picked);
            }
        }
        foreach (var id in associateIds)
            memoryBanCountdown[id] = 15;

        // 6. 获取记忆内容
        memory = finalIds.Count > 0
            ? memorySystem.GetMemoriesInfo(finalIds.ToList()).Select(i => i.ToDetailText()).ToArray()
            : Array.Empty<string>();

        associate = associateIds.Count > 0
            ? memorySystem.GetMemoriesInfo(associateIds.ToList()).Select(i => i.ToDetailText()).ToArray()
            : Array.Empty<string>();

        return true;
    }

    public OriginalNode GetOrCreateNode(string nodeName)
    {
        if (OriginalNodes.TryGetValue(nodeName, out var node))
            return node;
        int newId = _nextNodeId++;
        var newNode = new OriginalNode { id = newId };
        OriginalNodes[nodeName] = newNode;
        NodeNameToId[nodeName] = newId;

        if (_allNodes.Length == 0) _allNodes = new(allNode);
        else _allNodes.Append(',');
        _allNodes.Append(nodeName);
        allNode = _allNodes.ToString();

        return newNode;
    }

    public OriginalNode GetNode(string nodeName)
    {
        OriginalNodes.TryGetValue(nodeName, out var node);
        return node;
    }

    /// <summary>
    /// 重命名节点，保持 id 和记忆列表不变
    /// </summary>
    public bool RenameNode(string oldName, string newName)
    {
        if (string.IsNullOrEmpty(oldName) || string.IsNullOrEmpty(newName))
            return false;
        if (!OriginalNodes.TryGetValue(oldName, out var node))
        {
            Debug.LogWarning($"重命名失败：节点 {oldName} 不存在");
            return false;
        }
        if (OriginalNodes.ContainsKey(newName))
        {
            Debug.LogWarning($"重命名失败：目标节点 {newName} 已存在，请先合并");
            return false;
        }

        OriginalNodes.Remove(oldName);
        OriginalNodes[newName] = node;

        NodeNameToId.Remove(oldName);
        NodeNameToId[newName] = node.id;

        RebuildAllNodes();
        return true;
    }

    /// <summary>
    /// 将多个源节点的记忆合并到一个目标节点，删除源节点
    /// </summary>
    public bool MergeNodes(string targetName, string[] sourcesNames)
    {
        if (string.IsNullOrEmpty(targetName) || sourcesNames == null || sourcesNames.Length == 0)
            return false;

        var targetNode = GetOrCreateNode(targetName);

        foreach (var srcName in sourcesNames)
        {
            if (srcName == targetName) continue;
            if (!OriginalNodes.TryGetValue(srcName, out var srcNode))
            {
                Debug.LogWarning($"合并警告：源节点 {srcName} 不存在，跳过");
                continue;
            }

            if (srcNode.memory != null)
                targetNode.AddMemory(srcNode.memory);

            OriginalNodes.Remove(srcName);
            NodeNameToId.Remove(srcName);
        }

        RebuildAllNodes();
        return true;
    }

    /// <summary>
    /// 删除指定节点
    /// </summary>
    public bool DeleteNode(string nodeName)
    {
        if (string.IsNullOrEmpty(nodeName)) return false;
        if (!OriginalNodes.TryGetValue(nodeName, out var node))
        {
            Debug.LogWarning($"删除失败：节点 {nodeName} 不存在");
            return false;
        }

        OriginalNodes.Remove(nodeName);
        NodeNameToId.Remove(nodeName);
        RebuildAllNodes();
        return true;
    }

    private void RebuildAllNodes()
    {
        _allNodes.Clear();
        bool first = true;
        foreach (var key in OriginalNodes.Keys)
        {
            if (!first) _allNodes.Append(',');
            _allNodes.Append(key);
            first = false;
        }
        allNode = _allNodes.ToString();
    }
}