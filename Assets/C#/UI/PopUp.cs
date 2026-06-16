using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;

public class PopUp : MonoBehaviour
{
    public Button Confirm;
    public Button Cancel;
    public Text content;
    public GameObject PopUpObject;


    public static PopUp instance;

    private void Awake()
    {
        instance = this;
    }

    struct NeedConfirm
    {
        public Action<bool> Confirmed;
        public string _content;
        public NeedConfirm(Action<bool> need, string text)
        {
            Confirmed = need;
            _content = text;
        }
    }

    List<NeedConfirm> needConfirms = new();
    void Start()
    {
        Confirm.onClick.AddListener(Yes);
        Cancel.onClick.AddListener(No);
    }

    public void SetConfirm(Action<bool> need, string text, bool cover = true)
    {
        if (cover)
        {
            needConfirms.Clear();
        }
        needConfirms.Add(new NeedConfirm(need, text));
        ShowFirst();
    }

    void Yes()
    {
        needConfirms[0].Confirmed?.Invoke(true);
        Close();
    }
    void No()
    {
        needConfirms[0].Confirmed?.Invoke(false);
        Close();
    }

    void ShowFirst()
    {
        content.text = needConfirms[0]._content;
        Open();
    }
    void Close()
    {
        needConfirms.RemoveAt(0);
        if (needConfirms.Count == 0) PopUpObject.SetActive(false);
        else ShowFirst();
    }

    void Open()
    {
        PopUpObject.SetActive(true);
    }
}
