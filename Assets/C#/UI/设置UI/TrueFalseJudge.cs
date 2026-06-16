using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TrueFalseJudge : MonoBehaviour
{
    public bool yes;
    public Text text;
    public Button button;
    public void Set(bool _yes)
    {
        yes = _yes;
        text.text = yes ? "¡ñ" : "";
    }
    public void Switch()
    {
        yes = !yes;
        text.text = yes ? "¡ñ" : "";
    }
}
