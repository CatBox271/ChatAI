using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class AITask : MonoBehaviour
{
    public Chat chat;
    private SLManager SL { get { return SLManager.instance; } }
    public class Task
    {
        //简述
        public string brief;
        //待处理任务描述
        public string description;
        //提供任务的回调工具,名字需要加上随机序列号防止重复
        public Tool task_tool;
    }
    
    public Dictionary<string,List<Task>> TaskBar = new();
    public void SetAnTask(string task_from,string brief, string description, Tool task_tool = null)
    {
        List<Task> list = new();
        if (!TaskBar.TryGetValue(task_from, out list))
        {
            list = TaskBar[task_from] = new();
        }

        list.Add(new Task
        {
            brief = brief,
            description = description,
            task_tool = task_tool,
        });

        SaveTask();
    }
    public string GetAllTasks()
    {
        StringBuilder final = new();
        final.Append("任务栏：");
        if (TaskBar.Count != 0)
        {
            foreach (var task in TaskBar)
            {
                if (task.Value != null)
                {
                    final.Append("\n\n来源：");
                    final.Append(task.Key);
                    final.Append(",有");
                    final.Append(task.Value.Count);
                    final.Append("个待处理的提交。\n");
                    final.Append("分别是:");
                    for (int i = 0; i < task.Value.Count;i++)
                    {
                        final.Append("任务");
                        final.Append(i);
                        final.Append(":");
                        final.Append(task.Value[i].brief);
                        final.Append("\n");
                    }
                }
            }
        }
        else
        {
            final.Append("空");
        }
        return final.ToString();
    }

    //这里的选择任务后的临时工具功能需要实现

    public string SelectOneTask(string task_from, int index)
    {
        StringBuilder final = new();
        if (!TaskBar.TryGetValue(task_from, out List<Task> all_task)) return "当前来源不存在任务";
        if (all_task.Count <= index || index <= -1) return $"当前来源不存在序列{index}";

        var item = all_task[index];
        final.Append("当前选中的任务:\n");
        final.Append(item.description);
        if (item.task_tool != null)
        {
            final.Append("\n添加了一个临时tool:");
            final.Append(item.task_tool.function.name);
            final.Append("。\n现在可以在提供的tool列表里调用它了");
        }

        return final.ToString();
    }
    /// <summary>
    /// 移除字典里列表的第一项
    /// </summary>
    /// <param name="task_from">Key</param>
    /// <returns></returns>
    public string RemoveOneTask(string task_from, int index)
    {
        if (TaskBar.ContainsKey(task_from))
        {
            var list = TaskBar[task_from];
            if (list != null && list.Count > index && index > -1)
            {
                string task_brief = list[index].brief;
                if (list.Count == 1)
                {
                    //移除这个key
                    TaskBar.Remove(task_from);
                }
                else
                {
                    //移除第一项
                    list.RemoveAt(index);
                }
                SaveTask();
                return $"移除任务{task_brief}成功";
            }
        }
        SaveTask();
        return $"移除失败,任务已经被移除";
    }

    public string RemoveAllTask()
    {
        TaskBar.Clear();
        SaveTask();
        return "已清空任务栏";
    }

    //由Chat添加到WhileCharacterChange
    public void LoadTask()
    {
        TaskBar.Clear();
        var load = SL.ImportFromJson<Dictionary<string, List<Task>>>(chat.CharacterPath, "Tasks.json");
        if (load != null && load.Count != 0)
        {
            foreach (var item in load)
            {
                TaskBar[item.Key] = item.Value;
            }
        }
    }
    public void SaveTask()
    {
        SL.ExportToJson(TaskBar, chat.CharacterPath, "Tasks");
    }
}
