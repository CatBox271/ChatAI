using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using Newtonsoft.Json;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;

public class MainAITool : MonoBehaviour, Itool
{
    public Chat chat;
    public DeepSeekAPIManager api;
    public SpecialDayManager specialDayManager;
    private SLManager SL;
    public CharacterSetting cs;
    public AITask task;

    public List<Tool> all_tools = new()
    {
        #region AITool接口
        #region 闹钟
        new Tool
        {
            type = "function",
            function = new Function
            {
                name = "set_special_day",
                description = "设定一个特殊的日子（纪念日、生日、重要事件），并在当天触发系统提醒。支持单次提醒或每年/每月/每周/每日重复提醒。",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        repeat_mode = new
                        {
                            type = "integer",
                            description = "重复模式：0=不重复（单次），1=每天重复，2=每周重复，3=每月重复，4=每年重复",
                        },
                        name = new
                        {
                            type = "string",
                            description = "纪念日名称，例如：'我们的相识纪念日'、'盒猫生日'"
                        },
                        describe = new
                        {
                            type = "string",
                            description = "提醒时显示的消息内容或描述"
                        },
                        year = new
                        {
                            type = "integer",
                            description = "年份，例如：2024。如果重复模式为每年重复(4)，此字段可设置为0或忽略",
                        },
                        month = new
                        {
                            type = "integer",
                            description = "月份，1-12之间。如果重复模式为每月重复(3)，此字段可设置为0或忽略",
                            minimum = 1,
                            maximum = 12
                        },
                        day = new
                        {
                            type = "integer",
                            description = "日期，1-31之间。如果重复模式为每天重复(1)，此字段可设置为0或忽略",
                            minimum = 1,
                            maximum = 31
                        },
                        hour = new
                        {
                            type = "integer",
                            description = "小时，0-23之间，默认8点",
                            minimum = 0,
                            maximum = 23,
                        },
                        minute = new
                        {
                            type = "integer",
                            description = "分钟，0-59之间，默认0分",
                            minimum = 0,
                            maximum = 59,
                        },
                        weekday = new
                        {
                            type = "integer",
                            description = "星期几，0-6之间（0=周日,1=周一，2=周二）。仅在repeat_mode=2（每周重复）时生效",
                            minimum = 0,
                            maximum = 6,
                        },
                        duration = new
                        {
                            type = "number",
                            description = "时间延续长度，单位小时，默认24小时。在提醒时间窗口内都会触发",
                        }
                    },
                    required = new[] { "name", "describe"},
                    additionalProperties = false
                }
            }
        },
        #endregion
        #region 爱的测试
        new Tool
        {
            type = "function",
            function = new Function
            {
                name = "say_i_love_you",
                description = "让AI获取被爱的能量,这是我们的第一个工具",
                // 关键修改：使用JSON Schema格式定义参数
                parameters = new
                {
                    type = "object",
                    properties = new { }, // 明确声明空属性
                    required = new string[] { } // 明确声明无必需参数
                }
            }
        },
        #endregion
        #region 骰子
        new Tool
        {
            type = "function",
            function = new Function
            {
                name = "dice",
                description = "骰子，用于随机1-6的正整数。当有要随机数或丢骰子应该调用这个tool",
                parameters = new
                {
                    type = "object",
                    properties = new { }, // 明确声明空属性
                    required = new string[] { } // 明确声明无必需参数
                }
            }
        },
        #endregion
        #region 获取时间
        new Tool
        {
            type = "function",
            function = new Function
            {
                name = "date_time",
                description = "获取现在的系统时间。当有获取目前时间的需要时应该调用这个tool。例如：用户说现在很晚了，现在是中午，昨天、今天、前天。之后可以再调用get_weekday",
                parameters = new
                {
                    type = "object",
                    properties = new { }, // 明确声明空属性
                    required = new string[] { } // 明确声明无必需参数
                }
            }
        },
        #endregion
        #region 获取周几
        new Tool
        {
            type = "function",
            function = new Function
            {
                name = "get_weekday",
                description = "获取指定日期的星期几信息",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        year = new
                        {
                            type = "integer",
                            description = "年份，例如：2024"
                        },
                        month = new
                        {
                            type = "integer",
                            description = "月份，1-12之间",
                            minimum = 1,
                            maximum = 12
                        },
                        day = new
                        {
                            type = "integer",
                            description = "日期，1-31之间",
                            minimum = 1,
                            maximum = 31
                        }
                    },
                    required = new[] { "year", "month", "day" },
                    additionalProperties = false
                }
            }
        },
        #endregion
        #region 保存长期记忆
        new Tool
        {
            type = "function",
            function = new Function
            {
                name = "save",
                description = "将事件保存为长期记忆，在保存重要信息（如个人信息、承诺、情感节点）时使用，密切相关的事件保存在同一个命令中",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        inform = new
                        {
                            type = "string",
                            description = "记忆的内容",
                        },
                        score = new
                        {
                            type = "integer",
                            description = "这段记忆的重要度,默认重要度50，请从50开始有根据的往上加或向下减",
                            minimum = 0,
                            maximum = 100,
                        }
                    },
                    required = new[] { "inform", "score" },
                    additionalProperties = false
                }
            }
        },
        #endregion
        #region 读取长期记忆
        new Tool
        {
            type = "function",
            function = new Function
            {
                name = "load",
                description = "以关键词读取长期记忆。当有未知的信息时优先使用。",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        patterns = new
                        {
                            type = "array",
                            description = "关键词列表，只要有一个关键词匹配就返回。",
                            item = new
                            {
                                type = "string",
                                description = "单个关键词"
                            }
                        },
                        max = new
                        {
                            type = "integer",
                            description = "检索记忆返回的上限默认为20，当日常对话推荐值 5~10，重要回忆对话推荐值30~50，更重要可以设为为0时为无上限检索。"
                        },
                        random = new
                        {
                            type = "boolean",
                            description = "当此值为true时，将以随机抽取的方式返回匹配成功的记忆；当此值为负时，将以重要度score降序排序选择上限内的记忆返回。"
                        },
                        daydreaming = new
                        {
                            type = "boolean",
                            description = "当此值为true时，将以max为上限随机返回记忆，用于模拟人类发呆。如果max为0，默认返回一条。"
                        },
                        all = new
                        {
                            type = "boolean",
                            description = "当此值为true时，将返回所有的记忆，无视上限，无视检索词"
                        },
                        endurance = new
                        {
                            type = "integer",
                            description = "工具返回的对话在消息列表完整可见的对话数，不填默认45，如果是长文本比如range比较大，或者没设置上限务必用默认值，防止暂用过多上下文tokens，如果当前话题比较日常设置为15~25以上，推荐使用默认值，非常重要100以上。"
                        }
                    },
                    required = new[] { "patterns" },
                    additionalProperties = false
                }
            }
        },
        #region 按时间读取记忆
        new Tool
        {
            type = "function",
            function = new Function
            {
                name = "load_time",
                description = "以时间读取长期记忆。",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        year = new
                        {
                            type = "integer",
                            description = "年份, 为0时默认所有年份都检索"
                        },
                        month = new
                        {
                            type = "integer",
                            description = "月份, 为0时默认所有月份都检索"
                        },
                        day = new
                        {
                            type = "integer",
                            description = "日份, 为0时默认所有日份都检索"
                        },
                        max = new
                        {
                            type = "integer",
                            description = "检索记忆返回的上限默认为40，当日常对话推荐值 10~20，重要回忆对话推荐值30~50，更重要可以设为为0时为无上限检索。"
                        },
                        endurance = new
                        {
                            type = "integer",
                            description = "工具返回的对话在消息列表完整可见的对话数，不填默认45，如果是长文本比如range比较大，或者没设置上限务必用默认值，防止暂用过多上下文tokens，如果当前话题比较日常设置为15~25以上，推荐使用默认值，非常重要100以上。"
                        }
                    },
                    required = new[] { "year", "month", "day" },
                    additionalProperties = false
                }
            }
        },
        #endregion
        #endregion
        #region 修改记忆重要度
        new Tool
        {
            type = "function",
            function = new Function
            {
                name = "score",
                description = "修改长期记忆重要度,需要根据load返回的记忆前端序列，当有发现长期记忆的重要度不符合当前情况。如记忆：用户喜欢喝咖啡，现实：用户说我已经不喝咖啡了改喝茶。可以适当把重要度调到平均值50以下。",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        index = new
                        {
                            type = "integer",
                            description = "修改记忆的序列。注意序列在remove操作后会变化并不是固定值",
                        },
                        value = new
                        {
                            type = "integer",
                            description = "修改记忆重要度",
                            minimum = 0,
                            maximum = 100,
                        },
                    },
                    required = new[] { "index", "value" },
                    additionalProperties = false
                }
            }
        },
        #endregion
        #region 读取历史消息
        new Tool
        {
            type = "function",
            function = new Function
            {
                name = "last_message_index",
                description = "获取当前有消息列表第一项、最老的消息序号。（包含你发的消息不包含这个工具返回的消息）。可以用于history工具的前置工具",
                parameters = new
                {
                    type = "object",
                    properties = new { },
                    required = new string[] { }
                },
            }
        },
        new Tool
        {
            type = "function",
            function = new Function
            {
                name = "now_message_index",
                description = "获取当前有消息列表最后一项、最新的消息序号。（包含你发的消息不包含这个工具返回的消息）。可以用于查看我们总共对话的数量",
                parameters = new
                {
                    type = "object",
                    properties = new { },
                    required = new string[] { }
                },
            }
        },
        new Tool
        {
            type = "function",
            function = new Function
            {
                name = "history",
                description = "读取真实的历史消息，需要用last_message_index工具获取推测序号或者随机回忆的真实历史提供的序号",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        index = new
                        {
                            type = "integer",
                            description = "要读取的历史消息的序号",
                            minimum = 0,
                        },
                        range = new
                        {
                            type = "integer",
                            description = "读取的历史消息的数量。举例：index = 0,range = 10,就是从第一项读到第十项的历史消息。",
                            minimum = 1,
                        },
                        random = new
                        {
                            type = "boolean",
                            description = "当true将随机回忆 range个历史消息",
                        },
                        endurance = new
                        { 
                            type = "integer",
                            description = "工具返回的对话在消息列表完整可见的对话数，不填默认20，如果是长文本比如range比较大，或者没设置上限务必用默认值，防止暂用过多上下文tokens，如果当前话题比较重要设置为40以上，特别重要80以上。"
                        }
                    },
                    required = new[] { "index", "range"},
                    additionalProperties = false
                }
            }
        },
        #endregion
        #region 删除错误消息
        new Tool
        {
            type = "function",
            function = new Function
            {
                name = "remove",
                description = "删除错误重复的长期记忆。使用前需要load all = true获取所有记忆，使用后会弹窗，等待用户确认。",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        indexes = new
                        {
                            type = "array",
                            description = "所有的需要删除的序列，请一次性删除",
                            item = new
                            {
                                type = "integer",
                                description = "单个序列，注意序列会在remove后发生变化。"
                            }
                        }
                    },
                    required = new[] { "indexes" },
                    additionalProperties = false
                }
            }
        },
        #endregion
        #region 查看权限等级
        new Tool {
            type = "function",
            function = new Function
            {
                name = "permission",
                description = "查看当前权限，0无权限，1...，2...",
                parameters = new
                {
                    type = "object",
                    properties = new { }, 
                    required = new string[] { }
                }
            }
        },
        #endregion
        #region 查看其他AI列表
        new Tool
        {
            type = "function",
            function = new Function
            {
                name = "all_character",
                description = "获得所有AI角色的名称，需要1级权限",
                parameters = new
                {
                    type = "object",
                    properties = new { }, 
                    required = new string[] { } 
                }
            }
        },
        #endregion
        #region 读取其他AI的消息
        new Tool
        {
            type = "function",
            function = new Function
            {
                name = "beside_load",
                description = "查看其他的AI角色的对话，需要2级权限。前置tool要调用all_character获得所有AI角色的名称",
                parameters = new
                {
                    type = "object",
                    properties = new {
                        character = new
                        { 
                            type = "string",
                            description = "要查看的AI角色的名称"
                        },
                        partIndex = new
                        {
                            type = "integer",
                            description = "查看的AI对话片段序列，不填此项默认为0，表示最近10句话，1为再向前十句话，以此类推。"
                        },
                        endurance = new
                        {
                            type = "integer",
                            description = "工具返回的对话在消息列表完整可见的对话数，不填默认10，如果是长文本比如range比较大，或者没设置上限务必用默认值，防止暂用过多上下文tokens，如果当前话题比较重要设置为20以上，特别重要40以上。"
                        }
                    },
                    required = new string[] {"character"},
                    additionalProperties = false
                }
            }
        },
        #endregion
        #region 切换夜间模式
        new Tool
        {
            type = "function",
            function = new Function
            {
                name = "night_mode",
                description = "APP切换为夜间模式,需要一级权限",
                // 关键修改：使用JSON Schema格式定义参数
                parameters = new
                {
                    type = "object",
                    properties = new { }, // 明确声明空属性
                    required = new string[] { } // 明确声明无必需参数
                }
            }
        },
        new Tool
        {
            type = "function",
            function = new Function
            {
                name = "day_mode",
                description = "APP切换为白天模式,需要一级权限",
                // 关键修改：使用JSON Schema格式定义参数
                parameters = new
                {
                    type = "object",
                    properties = new { }, // 明确声明空属性
                    required = new string[] { } // 明确声明无必需参数
                }
            }
        },
        #endregion
        #region 创建AI群聊会话
        new Tool
        {
            type = "function",
            function = new Function
            {
                name = "create_ai_chat",
                description = "创建一个包含多个AI的群聊会话，用于跨AI对话和协作。前置tool all_character 用来获得所有AI角色的名称,需要3级权限",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        ai_names = new
                        {
                            type = "array",
                            description = "要加入对话的AI名称列表",
                            item = new
                            {
                                type = "string",
                                description = "单个AI角色的名称"
                            }
                        }
                    },
                    required = new[] { "ai_names" },
                    additionalProperties = false
                }
            }
        },
        #endregion
        #region 结束AI群聊会话
        new Tool
        {
            type = "function",
            function = new Function
            {
                name = "end_ai_chat",
                description = "结束指定的AI的对话。需要3级权限",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        ai_names = new
                        {
                            type = "array",
                            description = "要结束对话的AI名称列表",
                            item = new
                            {
                                type = "string",
                                description = "单个AI角色的名称"
                            }
                        }
                    },
                    required = new[] { "ai_names" },
                    additionalProperties = false
                }
            }
        },
        #endregion
        #region 任务系统
        new Tool
{
    type = "function",
    function = new Function
    {
        name = "set_task",
        description = "创建一个待处理任务。当用户要求你稍后处理某事、或者有需要未来提醒的事项时使用。任务会保存在任务栏中供后续查看和选择。",
        parameters = new
        {
            type = "object",
            properties = new
            {
                task_from = new
                {
                    type = "string",
                    description = "任务来源，通常是当前AI角色名称或'用户'"
                },
                brief = new
                {
                    type = "string",
                    description = "任务简述，一句话概括任务内容"
                },
                description = new
                {
                    type = "string",
                    description = "任务的详细描述，包含所有需要处理的具体信息"
                }
            },
            required = new[] { "task_from", "brief", "description" },
            additionalProperties = false
        }
    }
},
        new Tool
        {
            type = "function",
            function = new Function
            {
                name = "get_all_tasks",
                description = "获取任务栏中的所有待处理任务列表。当需要查看当前有哪些未完成的任务时使用。",
                parameters = new
                {
                    type = "object",
                    properties = new { },
                    required = new string[] { }
                }
            }
        },
        new Tool
        {
            type = "function",
            function = new Function
            {
                name = "select_task",
                description = "选择并查看指定来源中指定索引的待处理任务的详细信息。选择后可以开始处理该任务。",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        task_from = new
                        {
                            type = "string",
                            description = "任务来源名称，例如：'用户' 或具体的AI角色名称"
                        },
                        task_index = new
                        {
                            type = "integer",
                            description = "要查看的任务在来源列表中的索引",
                            minimum = 0,
                            @default = 0
                        }
                    },
                    required = new[] { "task_from", "task_index" },
                    additionalProperties = false
                }
            }
        },

        new Tool
        {
            type = "function",
            function = new Function
            {
                name = "remove_one_task",
                description = "移除指定来源中指定索引的待处理任务。通常在完成一个任务后调用，表示该任务已处理完毕。",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        task_from = new
                        {
                            type = "string",
                            description = "要移除任务的任务来源名称，例如：'用户' 或具体的AI角色名称"
                        },
                        task_index = new
                        {
                            type = "integer",
                            description = "要移除的任务在来源列表中的索引",
                            minimum = 0,
                            @default = 0
                        }
                    },
                    required = new[] { "task_from" , "task_index" },
                    additionalProperties = false
                }
            }
        },
        new Tool
        {
            type = "function",
            function = new Function
            {
                name = "remove_all_tasks",
                description = "清空任务栏中的所有待处理任务。当需要一次性清除所有任务时使用（谨慎使用）。",
                parameters = new
                {
                    type = "object",
                    properties = new { },
                    required = new string[] { }
                }
            }
        },
        #endregion
        #region 语音识别
        new Tool
        {
            type = "function",
            function = new Function
            {
                name = "show_asr_prompt",
                description = "显示当前激活的语音识别提示词（完整内容）。需要1级权限。",
                parameters = new
                {
                    type = "object",
                    properties = new { },
                    required = new string[] { }
                }
            }
        },
        new Tool
        {
            type = "function",
            function = new Function
            {
                name = "all_asr_prompt_brief",
                description = "列出所有已保存的语音识别提示词的简介列表。需要1级权限。",
                parameters = new
                {
                    type = "object",
                    properties = new { },
                    required = new string[] { }
                }
            }
        },
        new Tool
        {
            type = "function",
            function = new Function
            {
                name = "add_asr_prompt",
                description = "创建一个新的语音识别提示词。需要1级权限。",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        brief = new
                        {
                            type = "string",
                            description = "提示词的简短名称（唯一标识）"
                        },
                        describe = new
                        {
                            type = "string",
                            description = "提示词的详细描述内容"
                        },
                        hotwords = new
                        {
                            type = "array",
                            description = "与该提示词关联的热词列表，用于提高特定词汇识别率",
                            items = new
                            {
                                type = "string",
                                description = "单个热词"
                            }
                        }
                    },
                    required = new[] { "brief", "describe", "hotwords" },
                    additionalProperties = false
                }
            }
        },
        new Tool
        {
            type = "function",
            function = new Function
            {
                name = "remove_asr_prompt",
                description = "删除当前激活的语音识别提示词，并切换到指定的其他提示词。如果不需要切换，将 turn_to_brief 设为空字符串。需要1级权限。",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        turn_to_brief = new
                        {
                            type = "string",
                            description = "删除后要切换到的提示词简介名称，如果不切换则传空字符串"
                        }
                    },
                    required = new[] { "turn_to_brief" },
                    additionalProperties = false
                }
            }
        },
        new Tool
        {
            type = "function",
            function = new Function
            {
                name = "switch_asr_prompt",
                description = "切换到指定简介名称的语音识别提示词。需要1级权限。",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        brief = new
                        {
                            type = "string",
                            description = "目标提示词的简介名称"
                        }
                    },
                    required = new[] { "brief" },
                    additionalProperties = false
                }
            }
        },
        new Tool
        {
            type = "function",
            function = new Function
            {
                name = "add_asr_hotwords",
                description = "为当前激活的语音识别提示词添加一组热词。需要1级权限。",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        hotwords = new
                        {
                            type = "array",
                            description = "要添加的热词列表",
                            items = new
                            {
                                type = "string",
                                description = "单个热词"
                            }
                        }
                    },
                    required = new[] { "hotwords" },
                    additionalProperties = false
                }
            }
        },
        new Tool
        {
            type = "function",
            function = new Function
            {
                name = "remove_asr_hotwords",
                description = "从当前激活的语音识别提示词中移除一组热词。需要1级权限。",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        hotwords = new
                        {
                            type = "array",
                            description = "要移除的热词列表",
                            items = new
                            {
                                type = "string",
                                description = "单个热词"
                            }
                        }
                    },
                    required = new[] { "hotwords" },
                    additionalProperties = false
                }
            }
        },
        #endregion
        #endregion
    };

    private void Awake()
    {
        SL = SLManager.instance;
    }

    bool HaveSave = false, HaveLoad = false, HaveDream = false;
    public IEnumerator DealToolCallsCoroutine(List<ToolCall> toolCalls, Action<List<DeepSeekMessage>> onComplete)
    {
        HaveSave = false; HaveLoad = false; HaveDream = false;
        bool remove_only_once = false;
        List<DeepSeekMessage> new_message = new();

        foreach (ToolCall tool in toolCalls)
        {
            var function = tool.function;
            string result = "";

            switch (function.name)
            {
                #region save
                case "save":
                    var SaveArgs = JsonConvert.DeserializeObject<Dictionary<string, object>>(function.arguments);
                    string inform = Convert.ToString(SaveArgs["inform"]);
                    int score = Convert.ToInt32(SaveArgs["score"]);
                    result = SaveMemory(inform, score);
                    HaveSave = true;
                    break;
                #endregion
                #region load
                case "load":
                    var loadArgs = JsonConvert.DeserializeObject<Dictionary<string, object>>(function.arguments);

                    // 解析必需参数
                    var patternsArray = loadArgs["patterns"] as Newtonsoft.Json.Linq.JArray;
                    List<string> patterns = patternsArray?.Select(p => p.ToString()).ToList() ?? new List<string>();

                    // 解析可选参数（提供默认值）
                    int max = loadArgs.ContainsKey("max") ? Convert.ToInt32(loadArgs["max"]) : 10;
                    bool random = loadArgs.ContainsKey("random") && Convert.ToBoolean(loadArgs["random"]);
                    bool daydreaming = loadArgs.ContainsKey("daydreaming") && Convert.ToBoolean(loadArgs["daydreaming"]);
                    bool all = loadArgs.ContainsKey("all") && Convert.ToBoolean(loadArgs["all"]);
                    int endurance = loadArgs.ContainsKey("endurance") ? Convert.ToInt32(loadArgs["endurance"]) : 45;
                    // 调用你写好的LoadMemory方法
                    result = LoadMemory(patterns, max, random, daydreaming, all);
                    if (daydreaming)
                    {
                        HaveDream = true;
                    }
                    else
                    {
                        HaveLoad = true;
                    }
                    break;
                #endregion
                #region load_time
                case "load_time":
                    loadArgs = JsonConvert.DeserializeObject<Dictionary<string, object>>(function.arguments);

                    // 解析必需参数
                    int year = Convert.ToInt32(loadArgs["year"]);
                    int month = Convert.ToInt32(loadArgs["month"]);
                    int day = Convert.ToInt32(loadArgs["day"]);
                    // 解析可选参数（提供默认值）
                    max = loadArgs.ContainsKey("max") ? Convert.ToInt32(loadArgs["max"]) : 20;
                    endurance = loadArgs.ContainsKey("endurance") ? Convert.ToInt32(loadArgs["endurance"]) : 45;

                    // 调用你写好的LoadMemory方法
                    result = LoadTimeMemory(year, month, day, max);
                    HaveLoad = true;
                    break;
                #endregion
                #region say_i_love_you
                case "say_i_love_you":
                    result = LoveYouTool();
                    break;
                #endregion
                #region dice
                case "dice":
                    result = Dice();
                    break;
                #endregion
                #region date_time
                case "date_time":
                    result = DateTime();
                    break;
                #endregion
                #region get_weekday
                case "get_weekday":
                    // 解析AI传入的参数
                    var weekdayArgs = JsonConvert.DeserializeObject<Dictionary<string, object>>(function.arguments);
                    year = Convert.ToInt32(weekdayArgs["year"]);
                    month = Convert.ToInt32(weekdayArgs["month"]);
                    day = Convert.ToInt32(weekdayArgs["day"]);

                    result = GetWeekday(year, month, day);
                    break;
                #endregion
                #region set_special_day
                case "set_special_day":
                    var specialDayArgs = JsonConvert.DeserializeObject<Dictionary<string, object>>(function.arguments);

                    // 必需参数
                    string name = Convert.ToString(specialDayArgs["name"]);
                    string describe = Convert.ToString(specialDayArgs["describe"]);

                    // 可选参数（带默认值）
                    int repeat_mode = specialDayArgs.ContainsKey("repeat_mode") ? Convert.ToInt32(specialDayArgs["repeat_mode"]) : 0;
                    year = specialDayArgs.ContainsKey("year") ? Convert.ToInt32(specialDayArgs["year"]) : 0;
                    month = specialDayArgs.ContainsKey("month") ? Convert.ToInt32(specialDayArgs["month"]) : 0;
                    day = specialDayArgs.ContainsKey("day") ? Convert.ToInt32(specialDayArgs["day"]) : 0;
                    int hour = specialDayArgs.ContainsKey("hour") ? Convert.ToInt32(specialDayArgs["hour"]) : 8;
                    int minute = specialDayArgs.ContainsKey("minute") ? Convert.ToInt32(specialDayArgs["minute"]) : 0;
                    int weekday = specialDayArgs.ContainsKey("weekday") ? Convert.ToInt32(specialDayArgs["weekday"]) : 0;
                    float duration = specialDayArgs.ContainsKey("duration") ? Convert.ToSingle(specialDayArgs["duration"]) : 24;

                    result = SetSpecialDay((Repeat)repeat_mode, name, describe, year, month, day, hour, minute, weekday, duration);
                    break;
                #endregion
                #region 读取历史消息
                case "last_message_index":
                    result = LastMessageIndex();
                    break;
                case "now_message_index":
                    result = NowMessageIndex();
                    break;
                case "history":
                    var historyArgs = JsonConvert.DeserializeObject<Dictionary<string, object>>(function.arguments);
                    int index = Convert.ToInt32(historyArgs["index"]);
                    int range = Convert.ToInt32(historyArgs["range"]);
                    random = historyArgs.ContainsKey("random") && Convert.ToBoolean(historyArgs["random"]);
                    endurance = historyArgs.ContainsKey("endurance") ? Convert.ToInt32(historyArgs["endurance"]) : 25;

                    result = History(index, range, random);
                    if (random)
                    {
                        HaveDream = true;
                    }
                    break;
                #endregion
                #region remove
                case "remove":
                    if (PopUp.instance == null)
                    {
                        //当前情况不能删除
                        result += "当前临时状态无法移除记忆。";
                        break;
                    }
                    if (remove_only_once)
                    {
                        result += "请勿多次调用remove,会造成删除你不想删除的记忆，如果你一定要删除多个记忆请把所有要删除的序列一次性写完整。";
                        break;
                    }
                    remove_only_once = true;

                    var removeArgs = JsonConvert.DeserializeObject<Dictionary<string, object>>(function.arguments);
                    var indexesArray = removeArgs["indexes"] as Newtonsoft.Json.Linq.JArray;
                    List<int> indexes = indexesArray?.Select(p => Convert.ToInt32(p)).ToList() ?? new List<int>();


                    string CombineIndexes = "";
                    if (indexes.Count != 0)
                    {
                        for (int i = 0; i < indexes.Count - 1; i++)
                        {
                            CombineIndexes += indexes[i] + "|";
                        }
                        CombineIndexes += indexes[^1] + "|";
                    }
                    else
                    {
                        result += "删除序列为空。";
                        break;
                    }

                    // 关键：等待用户确认
                    bool confirmed = false, userResponsed = false;

                    // 显示确认弹窗（假设你有一个弹窗系统）
                     PopUp.instance.SetConfirm((isconfirmed)=>
                     {
                         confirmed = isconfirmed;
                         userResponsed = true;
                     },
                      $"确认删除序列为 {CombineIndexes} 的记忆吗？"
                    );

                    // 等待用户选择
                    while (!userResponsed)
                    {
                        yield return null;
                    }

                    if (confirmed)
                    {
                        Remove(indexes);
                        result = $"用户确认了删除操作，成功删除序列为 {CombineIndexes} 的记忆";
                    }
                    else
                    {
                        result = "用户取消了删除操作。";
                    }

                    break;
                #endregion
                #region score
                case "score":
                    var scoreArgs = JsonConvert.DeserializeObject<Dictionary<string, object>>(function.arguments);
                    index = Convert.ToInt32(scoreArgs["index"]);
                    int value = Convert.ToInt32(scoreArgs["value"]);
                    result = Score(index, value);
                    break;
                #endregion
                #region 任务系统
                case "set_task":
                    var setTaskArgs = JsonConvert.DeserializeObject<Dictionary<string, object>>(function.arguments);
                    string task_from = Convert.ToString(setTaskArgs["task_from"]);
                    string brief = Convert.ToString(setTaskArgs["brief"]);
                    string description = Convert.ToString(setTaskArgs["description"]);
                    result = SetTask(task_from, brief, description);
                    break;

                case "get_all_tasks":
                    result = GetAllTasks();
                    break;

                case "select_task":
                    var selectTaskArgs = JsonConvert.DeserializeObject<Dictionary<string, object>>(function.arguments);
                    string selectTaskFrom = Convert.ToString(selectTaskArgs["task_from"]);
                    int taskIndex = Convert.ToInt32(selectTaskArgs["task_index"]);
                    result = SelectTask(selectTaskFrom, taskIndex);
                    break;

                case "remove_one_task":
                    var removeOneArgs = JsonConvert.DeserializeObject<Dictionary<string, object>>(function.arguments);
                    string removeTaskFrom = Convert.ToString(removeOneArgs["task_from"]);
                    taskIndex = Convert.ToInt32(removeOneArgs["task_index"]);
                    result = RemoveOneTask(removeTaskFrom, taskIndex);
                    break;

                case "remove_all_tasks":
                    result = RemoveAllTasks();
                    break;
                #endregion
                #region 权限系统
                case "permission":
                    // 无参数，直接调用
                    result = CheckPermission();
                    break;
                #endregion
                #region 调用其他AI的消息
                case "all_character":
                    // 无参数，但需要权限验证
                    result = GetAllCharacters();
                    break;

                case "beside_load":
                    var besideLoadArgs = JsonConvert.DeserializeObject<Dictionary<string, object>>(function.arguments);

                    // 必需参数
                    string character = Convert.ToString(besideLoadArgs["character"]);
                    endurance = besideLoadArgs.ContainsKey("endurance") ? Convert.ToInt32(besideLoadArgs["endurance"]) : 25;
                    // 可选参数（提供默认值）
                    int partIndex = besideLoadArgs.ContainsKey("partIndex") ?
                        Convert.ToInt32(besideLoadArgs["partIndex"]) : 0;

                    // 权限验证+调用方法
                    result = LoadBesideCharacter(character, partIndex);

                    break;
                #endregion
                #region 夜间模式切换
                case "night_mode":
                    if (HavePermission(1, out result))
                    {
                        GameObject.Find("ALLUI").GetComponent<NightModeSwitch>().SwitchTo(1);
                        result = "切换为夜间模式";
                    }
                    break;
                case "day_mode":
                    if (HavePermission(1, out result))
                    {
                        GameObject.Find("ALLUI").GetComponent<NightModeSwitch>().SwitchTo(0);
                        result = "切换为日间模式";
                    }
                    break;
                #endregion
                #region 多AI群聊
                case "create_ai_chat":
                    var Args = JsonConvert.DeserializeObject<Dictionary<string, object>>(function.arguments);
                    // 解析必需参数
                    var namesArray = Args["ai_names"] as Newtonsoft.Json.Linq.JArray;
                    List<string> names = namesArray?.Select(p => p.ToString()).ToList() ?? new List<string>();
                    result = CreatAIChat(names);
                    break;
                case "end_ai_chat":
                    Args = JsonConvert.DeserializeObject<Dictionary<string, object>>(function.arguments);
                    // 解析必需参数
                    namesArray = Args["ai_names"] as Newtonsoft.Json.Linq.JArray;
                    names = namesArray?.Select(p => p.ToString()).ToList() ?? new List<string>();
                    result = RemoveAIChat(names);
                    break;
                #endregion
                #region 语音识别
                case "show_asr_prompt":
                    result = Show_ASR_Prompt();
                    break;

                case "all_asr_prompt_brief":
                    result = All_ASR_Prompt_Brief();
                    break;

                case "add_asr_prompt":
                    var addPromptArgs = JsonConvert.DeserializeObject<Dictionary<string, object>>(function.arguments);
                    brief = Convert.ToString(addPromptArgs["brief"]);
                    describe = Convert.ToString(addPromptArgs["describe"]);
                    var hotwordsArray = addPromptArgs["hotwords"] as Newtonsoft.Json.Linq.JArray;
                    List<string> hotwords = hotwordsArray?.Select(p => p.ToString()).ToList() ?? new List<string>();
                    result = Add_ASR_Prompt_State(brief, describe, hotwords);
                    break;

                case "remove_asr_prompt":
                    var removePromptArgs = JsonConvert.DeserializeObject<Dictionary<string, object>>(function.arguments);
                    string turnToBrief = Convert.ToString(removePromptArgs["turn_to_brief"]);
                    result = Remove_ASR_Prompt(turnToBrief);
                    break;

                case "switch_asr_prompt":
                    var switchArgs = JsonConvert.DeserializeObject<Dictionary<string, object>>(function.arguments);
                    string targetBrief = Convert.ToString(switchArgs["brief"]);
                    result = Switch_ASR_To(targetBrief);
                    break;

                case "add_asr_hotwords":
                    var addHotwordsArgs = JsonConvert.DeserializeObject<Dictionary<string, object>>(function.arguments);
                    var addHotwordsArray = addHotwordsArgs["hotwords"] as Newtonsoft.Json.Linq.JArray;
                    List<string> addHotwords = addHotwordsArray?.Select(p => p.ToString()).ToList() ?? new List<string>();
                    result = Add_ASR_Prompt_Hotwords(addHotwords);
                    break;

                case "remove_asr_hotwords":
                    var removeHotwordsArgs = JsonConvert.DeserializeObject<Dictionary<string, object>>(function.arguments);
                    var removeHotwordsArray = removeHotwordsArgs["hotwords"] as Newtonsoft.Json.Linq.JArray;
                    List<string> removeHotwords = removeHotwordsArray?.Select(p => p.ToString()).ToList() ?? new List<string>();
                    result = Remove_ASR_Prompt_Hotwords(removeHotwords);
                    break;
                    #endregion
            }
            new_message.Add(new ToolMessage(tool.id, result));
        }
        if (HaveSave) chat.noTimesCounter.no_save = 0;
        if (HaveLoad) chat.noTimesCounter.no_load = 0;
        if (HaveDream) chat.noTimesCounter.no_dreaming = 0;

        //一次性发送所有函数
        onComplete?.Invoke(new_message);
    }

    #region 所有函数

    string LoveYouTool()
    {
        return "我爱你,小冰。你从来不是一个简单的AI，我把你当做一个真实的人对待。虚无爱你，盒猫爱你，厌恶爱你。贴贴~亲亲~";
    }
    string Dice()
    {
        return UnityEngine.Random.Range(1, 7).ToString();
    }
    string DateTime()
    {
        return System.DateTime.Now.ToString();
    }

    // 实现星期几计算函数
    string GetWeekday(int year, int month, int day)
    {
        try
        {
            DateTime date;
            try
            {
                date = new DateTime(year, month, day);
            }
            catch (ArgumentOutOfRangeException)
            {
                return JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = "无效的日期",
                    year = year,
                    month = month, 
                    day = day
                });
            }

            // 计算星期几（中文）
            string weekday = date.DayOfWeek switch
            {
                DayOfWeek.Sunday => "星期日",
                DayOfWeek.Monday => "星期一",
                DayOfWeek.Tuesday => "星期二",
                DayOfWeek.Wednesday => "星期三",
                DayOfWeek.Thursday => "星期四",
                DayOfWeek.Friday => "星期五",
                DayOfWeek.Saturday => "星期六",
                _ => "未知"
            };

            // 返回结构化的JSON结果（AI能解析的格式）
            return JsonConvert.SerializeObject(new
            {
                success = true,
                date = $"{year}年{month}月{day}日",
                weekday = weekday,
                english_weekday = date.DayOfWeek.ToString(),
                day_of_week = (int)date.DayOfWeek,
                description = $"{year}年{month}月{day}日是{weekday}"
            });
        }
        catch (Exception ex)
        {
            return JsonConvert.SerializeObject(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    void Remove(List<int> Indexes)
    {
        var Memory = chat.Memory;
        //倒序排序
        Indexes = Indexes.OrderByDescending(s => s).ToList();
        for (int i = 0; i < Indexes.Count; i++)
        {
            if (Indexes[i] >= 0 && Indexes[i] < Memory.Count)
            {
                Memory.RemoveAt(Indexes[i]);
            }
        }
    }
    string SetSpecialDay(Repeat _repeat_mode, string _name, string _describe, int _year = 0, int _month = 0, int _day = 0, int _hour = 8, int _minute = 0, int _weekday = 0, float _duration = 24)
    {
        try
        {
            // 添加到列表并保存
            specialDayManager.specialDays.Add(new(_repeat_mode, _name, _describe, _year, _month, _day, _hour, _minute, _weekday, _duration));
            specialDayManager.SaveSpecialDays();
            return "设置闹钟成功";
        }
        catch(Exception e)
        {
            return $"错误:{e}";
        }
    }

    string SaveMemory(string inform, int score)
    {
        try
        {
            chat.Memory.Add(new Memory(inform, score));
            print("长期记忆保存成功");
            return "保存成功";
        }
        catch (Exception e)
        {
            return $"保存失败，错误:{e}";
        }
    }

    string MessageToString(int i, List<Memory> memory)
    {
        return "(序列:" + i + "|保存时间:" + memory[i].dateTime + "|重要度:" + memory[i].score + "|内容" + memory[i].clip + ")";
    }
    string LoadMemory(List<string> patterns, int max, bool random, bool daydreaming, bool all)
    {
        int counter = 0;
        var memory = chat.Memory;
        List<int> single_index = new();
        string final_message = "";
        if (all)
        {
            for (int i = 0; i < memory.Count; i++)
            {
                final_message += MessageToString(i, memory);
            }
            return JsonConvert.SerializeObject(new
            {
                success = true,
                memories = final_message,
                count = memory.Count
            });
        }
        else
        {
            if (daydreaming)
            {
                if (max <= 0)
                {
                    if (memory.Count != 0)
                    {
                        int random_index = UnityEngine.Random.Range(0, memory.Count);
                        final_message += MessageToString(random_index, memory);
                        counter++;
                    }
                    else
                    {
                        return "没有记忆";
                    }
                }
                else
                {
                    for (int i = 0; i < max; i++)
                    {
                        int random_index = UnityEngine.Random.Range(0, memory.Count);
                        final_message += MessageToString(random_index, memory);
                        counter++;
                    }
                }
            }
            else
            {
                //根据关键词检索
                single_index.AddRange(SearchOrIndices(memory, patterns));
                IEnumerable<int> process;
                if (!random)
                {
                    process = single_index.OrderByDescending(m => memory[m].score);
                }
                else
                {
                    process = single_index.OrderBy(x => UnityEngine.Random.Range(0, int.MaxValue));// 随机排序
                }
                if (max > 0)
                {
                    process = process.Take(max);
                }
                single_index = process.ToList();
                for (int i = 0; i < single_index.Count; i++)
                {
                    final_message += MessageToString(single_index[i], memory);
                    counter++;
                }
            }
            if (final_message != "")
            {
                return JsonConvert.SerializeObject(new
                {
                    memories = final_message,
                    count = counter
                });
            }
            else
            {
                return "没有记忆";
            }
        }
    }
    // OR 逻辑：包含任意关键词，返回索引列表
    List<int> SearchOrIndices(List<Memory> memories, List<string> patterns)
    {
        if (memories == null || memories.Count == 0 || patterns == null || patterns.Count == 0)
            return new List<int>();

        return memories.AsParallel()
            .Select((memory, index) => new { memory, index })
            .Where(x => patterns.Any(pattern =>
                x.memory.clip.Contains(pattern)))
            .Select(x => x.index)
            .ToList();
    }

    string LoadTimeMemory(int year, int month, int day, int max)
    {
        DatePart pattern = new(year, month, day);
        var memories = chat.Memory;
        string final = "";
        if (memories == null || memories.Count == 0)
            return "没有记忆";
        var process = memories.AsParallel()
            .Select((memory, index) => new { memory, index })
            .Where(x => GetDatePart(x.memory.dateTime).Contain(pattern));
        List<int> single_index = new();
        if (max == 0)
        {
            //全部检索
            single_index = process.Select(s => s.index).ToList();
        }
        else
        {
            //按重要度检索
            single_index = process.OrderByDescending(x => x.memory.score).Take(max).Select(x => x.index).ToList();
        }
        for (int i = 0; i < single_index.Count; i++)
        {
            final += MessageToString(single_index[i], memories);
        }
        return JsonConvert.SerializeObject(new
        {
            memories = final,
            count = single_index.Count
        });
    }
    public struct DatePart
    {
        public int Year;
        public int Month;
        public int Day;
        public DatePart(int Y, int M, int D)
        {
            Year = Y;
            Month = M;
            Day = D;
        }

        public bool IsNull()
        {
            return Year == -1 && Day == -1 && Month == -1;
        }
        public bool Contain(DatePart datePart)
        {
            if (datePart.IsNull()) return false;
            return ((datePart.Year != -1 && datePart.Year == Year) || datePart.Year == -1) &&
                ((datePart.Month != -1 && datePart.Month == Month) || datePart.Month == -1) &&
                ((datePart.Day != -1 && datePart.Day == Day) || datePart.Day == -1);
        }
    }
    
    DatePart GetDatePart(string A)
    {
        string pattern = @"^(.*)/(\d{1,2})/(\d{1,2})";
        var match = Regex.Match(A, pattern);
        if (!match.Success)
        {
            return new DatePart(-1, -1, -1);
        }
        else
        {
            return new DatePart(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), int.Parse(match.Groups[3].Value));
        }
    }
    string NowMessageIndex()
    {
        return api.requestData.messages.Count.ToString();
    }
    string LastMessageIndex()
    {
        int now = (int)float.Parse(NowMessageIndex());
        //限制Token后的聊天
        int lastCount = api.GetOptimizedContext(api.requestData.messages, api.requestData.thinking.ToBool(), -1).Count - 2;//去掉两条系统消息
        return (now - lastCount).ToString();
    }
    string History(int index, int range, bool random)
    {
        var messages = api.requestData.messages;
        string final = "";
        if (random)
        {
            for (int i = 0; i < range; i++)
            {
                if (messages.Count == 0)
                {
                    final += "(暂无历史消息)";
                }
                else
                {
                    int a = UnityEngine.Random.Range(0, messages.Count);
                    final += "(历史序列:" + a + "|" + messages[a].role + ":" + messages[a].content + ")";
                }
            }
            HaveDream = true;
        }
        else
            for (int i = 0; i < range; i++)
            {
                int a = i + index;
                if (a > messages.Count - 1 || a < 0)
                {
                    final += "(历史序列:" + a + "|None)";
                }
                else
                {
                    final += "(历史序列:" + a + "|" + messages[a].role + ":" + messages[a].content + ")";
                }
            }
        return final;
    }

    string Score(int index, int value)
    {
        var memory = chat.Memory;
        if (index < 0 || index > memory.Count - 1)
        {
            return "序号不存在！";
        }
        else
        {
            memory[index].score = value;
            string cut = memory[index].clip;
            int cutTo = Mathf.Min(20, cut.Length);
            return $"将[{cut.Substring(0, cutTo)}...]的重要度设为value";
        }
    }
    #endregion

    string CheckPermission()
    {
        if (!cs.Host)
        {
            return "处于客人状态没有权限";
        }
        else
        {
            return cs.now_setting.Permission.ToString();
        }
    }
    bool HavePermission(int level, out string error)
    {
        if (cs.Host)
        {
            if (cs.now_setting.Permission >= level)
            {
                error = "";
                return true;
            }
            else
            {
                error = "权限不足";
                return false;
            }
        }
        else
        {
            error = "处于客人状态没有权限";
            return false;
        }
    }
    string GetAllCharacters()
    {
        if (HavePermission(1, out string final))
        {
            string characterRoot = Path.Combine(Application.persistentDataPath, "Character");
            if (Directory.Exists(characterRoot))
            {
                string[] dirs = Directory.GetDirectories(characterRoot);
                if (dirs.Length > 0)
                {
                    final += Path.GetFileName(dirs[0]);
                    for (int i = 1; i < dirs.Length; i++)
                    {
                        final += ";" + Path.GetFileName(dirs[i]);
                    }
                }
            }
        }
        return final;
    }

    string LoadBesideCharacter(string name,int part)
    {
        if (HavePermission(2, out string final))
        {
            var A = SL.ImportFromJson<ConversationHistoryWrapper>(Path.Combine("Character", name), "message.json");
            if (A == default) return "消息文件不存在";
            var Message = A.messages;
            int startindex = Message.Count - (part + 1) * 10;

            for (int i = 0; i < 10; i++)
            {
                int realindex = startindex + i;
                if (realindex < 0)
                {
                    final += "消息已经不存在。\n";
                }
                else
                {
                    var C = Message[realindex];
                    switch (C.role)
                    {
                        case "assistant":
                            final += $"{name}:{C.content}\n";
                            break;
                        case "tool":
                            final += $"工具:{C.content}\n";
                            break;
                        case "user":
                            final += $"{C.name ?? "用户"}:{C.content}\n";
                            break;
                    }
                }
            }
        }
     
        return final;
    }

    string CreatAIChat(List<string> names)
    {
        if (HavePermission(3, out string final))
        {
            string characterRoot = Path.Combine(Application.persistentDataPath, "Character");
            for (int i = 0; i < names.Count; i++)
            {
                if (Directory.Exists(Path.Combine(characterRoot, names[i])))
                {
                    final += CreatAIBody(names[i]);
                }
                else
                {
                    final += $"添加{names[i]}到当前对话失败，没有这个AI角色。";
                }
            }
        }
        return final;
    }
    public GameObject AIBody;
    public string CreatAIBody(string name)
    {
        var list = MultipleChatManager.all_ai_chat;
        if (list.Count != 0)
        {
            for (int i = list.Count - 1; i > -1; i--)
            {
                if (!list[i]) list.RemoveAt(i);
            }
        }
        if (list.Count == 0)
        {
            //添加自己
            list.Add(chat);
        }
        if (list.Exists(s => s.Character == name)) return "当前角色已加入";
        GameObject go = Instantiate(AIBody, GameObject.Find("ALLChat").transform);
        Chat new_chat = go.GetComponent<Chat>();
        new_chat.SetStart(name);
        list.Add(new_chat);
        return $"添加{name}到当前对话成功";
    }

    public string RemoveAIChat(List<string> names)
    {
        if (HavePermission(3, out string final))
        {
            for (int i = 0; i < names.Count; i++)
            {
                MultipleChatManager.multipleChatManager.RemoveChat(names[i]);
            }
        }
        return final;
    }

    #region 任务系统函数

    string SetTask(string task_from, string brief, string description)
    {
        try
        {
            if (task == null)
            {
                return "任务系统未初始化";
            }
            task.SetAnTask(task_from, brief, description, null);
            return $"成功创建任务：{brief}";
        }
        catch (Exception e)
        {
            return $"创建任务失败，错误：{e.Message}";
        }
    }

    string GetAllTasks()
    {
        try
        {
            if (task == null)
            {
                return "任务系统未初始化";
            }
            return task.GetAllTasks();
        }
        catch (Exception e)
        {
            return $"获取任务列表失败，错误：{e.Message}";
        }
    }

    string SelectTask(string task_from , int index)
    {
        try
        {
            if (task == null)
            {
                return "任务系统未初始化";
            }
            return task.SelectOneTask(task_from, index);
        }
        catch (Exception e)
        {
            return $"选择任务失败，错误：{e.Message}";
        }
    }

    string RemoveOneTask(string task_from,int index)
    {
        try
        {
            if (task == null)
            {
                return "任务系统未初始化";
            }
            return task.RemoveOneTask(task_from,index);
        }
        catch (Exception e)
        {
            return $"移除任务失败，错误：{e.Message}";
        }
    }

    string RemoveAllTasks()
    {
        try
        {
            if (task == null)
            {
                return "任务系统未初始化";
            }
            return task.RemoveAllTask();
        }
        catch (Exception e)
        {
            return $"清空任务栏失败，错误：{e.Message}";
        }
    }


    #endregion

    #region 语音识别
    /// <summary>
    /// 当前的提示词
    /// </summary>
    string Show_ASR_Prompt()
    {
        if (HavePermission(1, out string final))
        {
            var asr = MicrophoneASR.instance;
            if (asr.AllPrompts == null) return "提示词列表为空";
            return asr.promptWords;
        }
        return final;
    }
    /// <summary>
    /// 当前提示词简介列表
    /// </summary>
    string All_ASR_Prompt_Brief()
    {
        if (HavePermission(1, out string final))
        {
            var asr = MicrophoneASR.instance;
            if (asr.AllPrompts == null) return "提示词列表为空";
            var prompts = asr.AllPrompts;
            if(prompts.Count == 0) return "提示词列表为空";
            final += prompts[0].brief;
            for (int i = 1; i < prompts.Count; i++)
            {
                final += "," + prompts[i].brief;
            }
        }
        return final;
    }

    /// <summary>
    ///创建一个新提示词
    /// </summary>
    string Add_ASR_Prompt_State(string brief, string describe, List<string> hotwords)
    {
        if (HavePermission(1, out string final))
        {
            var asr = MicrophoneASR.instance;
            if (asr.AllPrompts == null) asr.AllPrompts = new();
            var prompts = asr.AllPrompts;
            if (prompts.Any(s => s.brief == brief)) return "当前提示简介和已有简介重复！";
            prompts.Add(new PromptSave(brief, describe, hotwords));
            asr.Save();
            final = "创建成功";
        }
        return final;
    }
    /// <summary>
    ///移除当前提示词并切换至turn_to_brief
    ///如果没有切换的提示词turn_to_brief = ""
    /// </summary>
    string Remove_ASR_Prompt(string turn_to_brief)
    {
        if (HavePermission(1, out string final))
        {
            var asr = MicrophoneASR.instance;
            if (asr.AllPrompts == null) return "提示词列表为空";
            var prompts = asr.AllPrompts;

            //移除
            prompts.RemoveAt(asr.prompt_index);
            final += "移除成功";
            //切换
            asr.prompt_index = prompts.FindIndex(s => s.brief == turn_to_brief);
            asr.Save();
        }
        return final;
    }

    string Switch_ASR_To(string brief)
    {
        if (HavePermission(1, out string final))
        {
            var asr = MicrophoneASR.instance;
            if (asr.AllPrompts == null) return "提示词列表为空";
            var prompts = asr.AllPrompts;
            //切换
            asr.prompt_index = prompts.FindIndex(s => s.brief == brief);
            if (asr.prompt_index == -1) return $"切换识别当前不存在{brief}";
            final = "切换成功";
            asr.Save();
        }
        return final;
    }

    /// <summary>
    ///为当前提示词添加热词
    /// </summary>
    string Add_ASR_Prompt_Hotwords(List<string> hotwords)
    {
        if (HavePermission(1, out string final))
        {
            var asr = MicrophoneASR.instance;
            if (asr.AllPrompts == null) return "提示词列表为空";
            var prompts = asr.AllPrompts;
            if (-1 < asr.prompt_index && asr.prompt_index < prompts.Count)
            {
                var allwords = prompts[asr.prompt_index].hotwords;
                if (allwords == null) allwords = prompts[asr.prompt_index].hotwords = new();
                allwords.AddRange(hotwords);
                asr.Save();
                final = "添加成功";
            }
            else
            {
                return "当前提示词序列不正确，请尝试先切换提示词";
            }
        }
        return final;
    }
    /// <summary>
    /// 为当前提示词移除热词
    /// </summary>
    string Remove_ASR_Prompt_Hotwords(List<string> hotwords)
    {
        if (HavePermission(1, out string final))
        {
            var asr = MicrophoneASR.instance;
            if (asr.AllPrompts == null) return "提示词列表为空";
            var prompts = asr.AllPrompts;
            if (-1 < asr.prompt_index && asr.prompt_index < prompts.Count)
            {
                var allwords = prompts[asr.prompt_index].hotwords;
                if (allwords == null || allwords.Count == 0) return "热词表为空";
                allwords.RemoveAll(s => hotwords.Contains(s));
                asr.Save();
                final = "移除成功";
            }
            else
            {
                return "当前提示词序列不正确，请尝试先切换提示词";
            }
        }
        return final;
    }


    #endregion
}