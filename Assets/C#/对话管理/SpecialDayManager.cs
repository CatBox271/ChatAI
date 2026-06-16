using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using System.Collections;

[System.Serializable]
public class SpecialDay
{
    public string name;
    public string describe;

    public int year;
    public int month;
    public int day;
    public int hour;
    public int minute;

    public int weekday;

    public float duration;//时间延续单位小时

    public DateTime createdTime;
    public DateTime nextRemind;

    public Repeat repeat_mode;

    public SpecialDay(Repeat _repeat_mode,string _name,string _describe,int _year = 0, int _month = 0,int _day = 0, int _hour = 8, int _minute = 0,int _weekday = 0,float _duration = 24)
    {
        repeat_mode = _repeat_mode;
        name = _name;
        describe = _describe;

        year = _year;
        month = _month;
        day = _day;
        hour = _hour;
        minute = _minute;
        duration = _duration;
        weekday = _weekday;
        createdTime = DateTime.Now;
        CreatNextTime();
    }
    public void CreatNextTime()
    {
        DateTime now = DateTime.Now;
        nextRemind = new(
            year != 0 ? year : now.Year,
            month != 0 ? month : now.Month,
            day != 0 ? day : now.Day,
            hour, minute, 0);
        if (repeat_mode == Repeat.Week)
        {
            nextRemind = now.AddDays(weekday - (int)now.DayOfWeek);
            nextRemind = new(nextRemind.Year, nextRemind.Month, nextRemind.Day, hour, minute, 0);
        }
        SetNextTime();
    }
    public bool SetNextTime()
    {
        bool valued = false;
        DateTime now = DateTime.Now;
        switch (repeat_mode)
        {
            default:
                break;
            case Repeat.Year:
                while (now > nextRemind)
                {
                    nextRemind = nextRemind.AddYears(1);
                    valued = true;
                }
                break;
            case Repeat.Month:
                while (now > nextRemind)
                {
                    nextRemind = nextRemind.AddMonths(1);
                    valued = true;
                }
        break;
            case Repeat.Day:
                while (now > nextRemind)
                {
                    nextRemind = nextRemind.AddDays(1);
                    valued = true;
                }
        break;
            case Repeat.Week:
                //特殊
                while (now > nextRemind)
                {
                    nextRemind = nextRemind.AddDays(7);
                    valued = true;
                }
        break;
        }

        return valued;
    }

    public bool ItstheTime(out float nearTime ,out bool Valued)
    {
        DateTime now = DateTime.Now;
        nearTime = (float)(now - nextRemind).TotalMinutes;
        if (now > nextRemind)
        {
            Debug.Log((now - nextRemind).TotalHours);
            if ((now - nextRemind).TotalHours <= duration)
            {
                Valued = SetNextTime();
                //触发闹钟
                return true;
            }
            else
            {
                Debug.Log("跳过加一循环");
                Valued = SetNextTime();
            }
        }
        else
        {
            Valued = false;
        }
        return false;
    }
}

public enum Repeat
{
    None,
    Day,
    Week,
    Month,
    Year,
}

// 纪念日管理器
public class SpecialDayManager : MonoBehaviour
{
    public Chat chat;
    public DeepSeekAPIManager api;
    public List<SpecialDay> specialDays = new();
    string savePath { get { return Path.Combine(Application.persistentDataPath, chat.CharacterPath, "special_days.json"); } } 

    void Start()
    {
        LoadSpecialDays();
        chat.WhileCharacterChange += LoadSpecialDays;
        // 启动每日检查协程

    }

    // 保存到本地文件
    public void SaveSpecialDays()
    {
        string json = JsonConvert.SerializeObject(specialDays, Formatting.Indented);
        File.WriteAllText(savePath, json);
    }

    // 从文件加载
    public void LoadSpecialDays()
    {
        if (File.Exists(savePath))
        {
            string json = File.ReadAllText(savePath);
            specialDays = JsonConvert.DeserializeObject<List<SpecialDay>>(json) ?? new List<SpecialDay>();
        }
        StopCoroutine(DailyCheckRoutine());
        StartCoroutine(DailyCheckRoutine());
    }
    IEnumerator DailyCheckRoutine()
    {
        //等待一秒使CSetting加载完毕
        yield return new WaitForSeconds(5f);
        while (true)
        {
            float closet_interval = CheckForSpecialDays();
            if (closet_interval < 10) closet_interval = 1f;
            else if (closet_interval < 60) closet_interval = 10f;

            yield return new WaitForSecondsRealtime(closet_interval);
        }
    }
    // 检查并触发提醒
    private float CheckForSpecialDays()
    {
        bool nessave = false;
        float closet = -1;
        for (int i = specialDays.Count - 1; i > -1; i--)
        {
            var day = specialDays[i];
            if (day.ItstheTime(out float near,out bool valued))
            {
                // 触发提醒
                TriggerReminder(day);
                if (day.repeat_mode == Repeat.None)
                {
                    //删除当前闹钟
                    specialDays.RemoveAt(i);
                }
                else
                {
                    specialDays[i] = day;
                }
                nessave = true;

            }
            else
            {
                if (near > 0)
                {
                    if (closet < 0) closet = near;
                    else if (closet > near) closet = near;
                }
            }
            if (valued) nessave = true;
        }
        if (closet < 0) closet = 60;

        //print(closet);

        if(nessave)SaveSpecialDays();

        return closet;
    }
    // 触发系统提醒
    private void TriggerReminder(SpecialDay specialDay)
    {
        string content = $"🎉 纪念日提醒：{specialDay.name}\n{specialDay.describe}\n现在是{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}";
        Debug.LogWarning(content);
        //需要直接发给AI
        if (chat.Send.interactable)
        {
            //正常发送消息
            SendClockMessage(content);
        }
        else
        {
            //等待发送消息
            StartCoroutine(WaitForSendingAccessable(content, SendClockMessage));
        }
    }
    void SendClockMessage(string content)
    {
        chat.Send.interactable = false;
        api.OwnAddMessageSend(new DeepSeekMessage() { content = content,role = "user",name ="system"});
    }
    IEnumerator WaitForSendingAccessable(string content, Action<string> OnEndWaiting)
    {
        yield return new WaitUntil(() => chat.Send.interactable == true);
        OnEndWaiting?.Invoke(content);
    }
}