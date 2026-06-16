using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FitScreen : MonoBehaviour
{
    public bool CorrectByTine = true;
    public int SetFrame = 60;
    public FitSide _fitSide;

    public float _distance;
    private int PastFrame;

    private RectTransform ThisRect;
    public RectTransform ParentRect;

    public Vector2 Around;

    private void Awake()
    {
        ThisRect = GetComponent<RectTransform>();
        if (ParentRect == null) ParentRect = transform.parent.GetComponent<RectTransform>();
    }
    void OnEnable()
    {
        SetSizeDelat();
    }
    private void Update()
    {
        if (!CorrectByTine) return;
        PastFrame += 1;
        if (PastFrame < SetFrame) return;
        PastFrame = 0;
        SetSizeDelat();
    }

    private void SetSizeDelat()
    {
        float _ToWards;
        switch (_fitSide)
        {
            case FitSide.Left:
                _ToWards = Screen.width / 2f + ThisRect.anchoredPosition.x - ThisRect.rect.width * ThisRect.localScale.x / 2f;
                _ToWards -= _distance * ThisRect.localScale.x;
                ThisRect.sizeDelta += new Vector2(_ToWards * ThisRect.localScale.x, 0);
                break;
            case FitSide.Right:
                _ToWards = Screen.width /2f - ThisRect.anchoredPosition.x - ThisRect.rect.width * ThisRect.localScale.x / 2f;
                _ToWards -= _distance * ThisRect.localScale.x;
                ThisRect.sizeDelta += new Vector2(_ToWards * ThisRect.localScale.x, 0);
                break;
            case FitSide.Up:
                _ToWards = Screen.height / 2f - ThisRect.anchoredPosition.y - ThisRect.rect.height * ThisRect.localScale.y / 2f;
                _ToWards -= _distance * ThisRect.localScale.y;
                ThisRect.sizeDelta += new Vector2(0, _ToWards * ThisRect.localScale.y);
                break;
            case FitSide.Down:
                _ToWards = Screen.height / 2f + ThisRect.anchoredPosition.y - ThisRect.rect.height * ThisRect.localScale.y / 2f;
                _ToWards -= _distance * ThisRect.localScale.y;
                ThisRect.sizeDelta += new Vector2(0, _ToWards * ThisRect.localScale.y);
                break;
            case FitSide.InSide:
                if (ParentRect == null) return;
                ThisRect.sizeDelta = ParentRect.sizeDelta - Around;
                break;
            case FitSide.ToWidth:
                if (ParentRect == null) return;
                ThisRect.sizeDelta = new Vector2((ParentRect.sizeDelta - Around).x, ThisRect.sizeDelta.y);
                break;
            case FitSide.ToHeight:
                if (ParentRect == null) return;
                ThisRect.sizeDelta = new Vector2(ThisRect.sizeDelta.x, (ParentRect.sizeDelta - Around).y);
                break;
        }
    }

    public enum FitSide
    { 
        Left,
        Right,
        Up,
        Down,
        InSide,
        ToWidth,
        ToHeight,
    }
}
