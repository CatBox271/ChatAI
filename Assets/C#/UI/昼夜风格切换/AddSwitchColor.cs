using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AddSwitchColor : MonoBehaviour
{
    public Image image;
    public Text text;
    public TextMeshProUGUI textMeshPro;
    [Header("É«²Ê¿Õ¼ä")]
    public int colorArea;
    public void SwitchColor(Color col)
    {
        if(image) image.color = col;
        if(text) text.color = col;
        if(textMeshPro) textMeshPro.color = col;
    }
    private void Awake()
    {
        TryGetComponent(out image);
        TryGetComponent(out text);
        TryGetComponent(out textMeshPro);
    }
    void Start()
    {
        NightModeSwitch.instance.colorObjs.Add(new(colorArea, this));
        SwitchColor(NightModeSwitch.instance.GetColor(colorArea));
    }
}
