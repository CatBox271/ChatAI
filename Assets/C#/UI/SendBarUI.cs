using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class SendBarUI : MonoBehaviour
{

    [Header("白条设置")]
    public TextLineCounter lineCounter;
    public RectTransform rectTransform;
    public RectTransform TextInRange;
    public float default_height;
    public float OneLineHeight;
    [Header("输入栏设置")]
    public CorrectUI input_text;
    public CorrectUI input_button;
    public CorrectUI name_text;
    public float default_offsetY;
    [Header("输入栏检测区高度")]
    public float default_TextInHeight;
    // Update is called once per frame
    private float last;
    [Header("按钮丛初始高度")]
    public float default_ButtonList;
    [Header("名字初始高度")]
    public float default_name;
    public float default_name_hide;

    float scale;

    private void Update()
    {
        TextChanged(null);
    }

    public void TextChanged(string _)
    {
        //输入框增加的行数
        float height = lineCounter.GetTextLineCount(true);
        if (last != height)
        {
            last = height;

            scale = Mathf.Max(height - OneLineHeight, 0);
            //增加下方输入跳宽度
            rectTransform.sizeDelta = new(rectTransform.sizeDelta.x, default_height + scale);
            //文本向上偏移
            TextInRange.anchoredPosition = new(TextInRange.anchoredPosition.x, default_offsetY + scale * 0.5f);
            //增加输入区域宽度
            TextInRange.sizeDelta = new(TextInRange.sizeDelta.x, default_TextInHeight + scale);
            //按钮区向上偏移
            input_button.RelativePosition = new(0, default_ButtonList);
        }
    }
}
