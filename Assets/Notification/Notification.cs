using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Notification : MonoBehaviour
{
    public Text text;
    public CanvasGroup group;
    public CorrectUI correctUI;

    public float pop_up_size = 60f;

    public AnimationCurve ac;

    public static Notification instance;

    public float show_time;

    private float showed_time;

    private void Awake()
    {
        instance = this;
    }
    // Start is called before the first frame update
    void Start()
    {
        group.alpha = 0;
    }

    private Queue<string> pop_up = new();

    public void AddPopUp(string context)
    {
        pop_up.Enqueue(context);
    }

    bool isShow = false;

    private void Update()
    {
        if (!isShow)
        {
            if (pop_up.Count > 0)
            {
                //从表中选出
                text.text = pop_up.Dequeue();
                isShow = true;
                showed_time = 0;
            }
        }
        else
        {
            if (showed_time > show_time)
            {
                isShow = false;
            }
            var value = ac.Evaluate(Mathf.Clamp(showed_time / show_time, 0, 1));
            group.alpha = value;
            correctUI.SizePercentage = (pop_up_size - 10f) * value + 10f;
            showed_time += Time.deltaTime;

        }
    }
}
