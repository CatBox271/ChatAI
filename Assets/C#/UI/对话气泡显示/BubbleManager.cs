using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Linq;

public class BubbleManager : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public float 消息间距;
    public GameObject Bubble;

    public DeepSeekAPIManager api;

    public bool moving;
    public float H;
    [System.NonSerialized]
    public List<DeepSeekMessage> all_message;

    // 合并为一个字典：key = message_index, value = TextBubble
    [Header("激活的聊天气泡")]
    public Dictionary<int, TextBubble> ActiveBubbles = new Dictionary<int, TextBubble>();

    [Header("未激活的BubblePool")]
    public List<TextBubble> BubblesPool = new List<TextBubble>();

    public bool need_spawn;

    public GameObject SizeSample;

    public static BubbleManager instance;

    private void Awake()
    {
        instance = this;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        //开始将所有的项目移动
        moving = true;
        last_mouse = Input.mousePosition;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        //移动结束
        moving = false;
    }

    private void Start()
    {
        all_message = api.requestData.messages;
        need_spawn = true;
    }

    private Vector2 last_mouse;
    private void Update()
    {
        //滑动
        BubbleMotion();
        if (need_spawn)
        {
            //尝试生成泡泡
            int get = all_message.FindLastIndex(s => s.role != "system");
            if (get <= -1) return;
            if (all_message[get].content.StartsWith("Memory:"))
            {
                if (!CharacterSetting.DeveloperMode) return;
            }
            //先生成最后一项
            BubbleInstance(get, 0);
            need_spawn = false;
        }
    }

    public void LastestTextRefresh(bool complete)
    {
        int last = all_message.Count - 1;

        // 遍历所有激活的气泡
        foreach (var kvp in ActiveBubbles)
        {
            var bubble = kvp.Value;
            if (kvp.Key == last)
            {
                // 最新项
                bubble.Completely = complete;
                bubble.ChunkText(false);
            }
            else
            {
                bubble.Completely = true;
            }
        }
    }

    public void BubbleInstance(int message_index, float start_H, int up_down = 0)
    {
        // 检查是否已经存在
        if (ActiveBubbles.ContainsKey(message_index))
        {
            Debug.Log($"气泡 {message_index} 已存在，跳过生成");
            return;
        }

        // 确保池中有足够的气泡
        if (BubblesPool.Count == 0)
        {
            for (int i = 0; i < 5; i++)
            {
                GameObject go = Instantiate(Bubble, transform);
                go.SetActive(false);
                go.GetComponent<CorrectUI>().Aim = SizeSample;
                var tb = go.GetComponent<TextBubble>();
                tb.bm = this;
                BubblesPool.Add(tb);
            }
        }

        // 从池中取第一个
        var selected = BubblesPool[0];
        if (message_index == all_message.Count - 1)
            selected.Completely = !api.StreamIsGetting;

        selected.SetValue(this, SizeSample, message_index, start_H, up_down);

        // 添加到字典
        ActiveBubbles.Add(message_index, selected);
        // 从池中移除
        BubblesPool.RemoveAt(0);
    }

    public void BackPool(TextBubble tb)
    {
        tb.gameObject.SetActive(false);
        tb.Completely = true;

        // 从字典中移除
        if (ActiveBubbles.ContainsValue(tb))
        {
            // 找到对应的 key
            int keyToRemove = -1;
            foreach (var kvp in ActiveBubbles)
            {
                if (kvp.Value == tb)
                {
                    keyToRemove = kvp.Key;
                    break;
                }
            }
            if (keyToRemove != -1)
            {
                ActiveBubbles.Remove(keyToRemove);
            }
        }

        // 放回池中
        BubblesPool.Add(tb);
    }

    // 根据索引获取气泡
    public TextBubble GetBubbleByIndex(int message_index)
    {
        if (ActiveBubbles.TryGetValue(message_index, out TextBubble bubble))
        {
            return bubble;
        }
        return null;
    }

    // 检查气泡是否存在
    public bool HasBubble(int message_index)
    {
        return ActiveBubbles.ContainsKey(message_index);
    }

    public void TryInstance(int message_index, int new_last, float start_H)
    {
        int final = -1;
        int now_index = message_index + new_last;
        if (-1 < now_index && now_index < all_message.Count)
        {
            do
            {
                if (0 > now_index || now_index >= all_message.Count) break;
                //判断message_index 是否 合法
                if (all_message[now_index].role != "system")
                {
                    if ((!all_message[now_index].content.StartsWith("Memory:") &&
                         !all_message[now_index].content.StartsWith("History:") &&
                         all_message[now_index].role != "tool" &&
                         all_message[now_index].name != "system") ||
                        CharacterSetting.DeveloperMode)
                    {
                        //AI消息不为空
                        if (!(all_message[now_index].role == "assistant" && all_message[now_index].content == ""))
                        {
                            final = now_index;
                            break;
                        }
                    }
                }
                now_index += new_last;
            } while (-1 < now_index && now_index < all_message.Count);
        }

        //过滤非法
        if (final == -1) return;
        //过滤已经有了的
        if (ActiveBubbles.ContainsKey(final)) return;
        //正式生成
        BubbleInstance(final, start_H, -new_last);
    }

    List<float> velocity = new List<float>();
    bool slide = false;
    float V;

    void BubbleMotion()
    {
        if (moving)
        {
            if (!slide)
            {
                velocity.Clear();
            }
            Vector2 pos = Input.mousePosition;
            if (last_mouse != pos)
            {
                float val = pos.y - last_mouse.y;
                H += val;
                last_mouse = pos;
                KeepVel(val);
            }
            else
            {
                KeepVel(0);
            }
            slide = true;
        }
        else
        {
            if (slide)
            {
                V = 0;
                for (int i = 0; i < velocity.Count; i++)
                {
                    V += velocity[i];
                }
                V /= 10f;
                slide = false;
            }
            H += V;
            V *= 0.99f;
        }
    }

    public void AdjustFollow(int messageIndex,float relative)
    {
        var follow_bubble = ActiveBubbles.Where(bubbles => bubbles.Key > messageIndex).Select(item => item.Value).ToList();
        //重新移动
        foreach (TextBubble bubble in follow_bubble)
        {
            bubble.startH += relative;
        }
    }

    public void DeleteAll()
    {
        // 清除所有激活的气泡
        ActiveBubbles.Clear();
        BubblesPool.Clear();
        H = 0;

        // 销毁所有子物体
        for (int i = transform.childCount - 1; i > -1; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }

        need_spawn = true;
    }

    void KeepVel(float f)
    {
        velocity.Add(f);
        if (velocity.Count > 10) velocity.RemoveAt(0);
    }
}