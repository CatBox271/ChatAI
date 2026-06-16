using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InputLimit : MonoBehaviour
{
    InputField belimited;
    public float max;
    public float min;
    public float delta = 65536;
    private void Awake()
    {
        if(!belimited) TryGetComponent(out belimited);
        belimited?.onValueChanged.AddListener(Limit);
    }

    void Limit(string n)
    {
        if (float.TryParse(n, out float a))
        {
            if (a < min || a > max)
            {
                belimited.text = Mathf.Clamp(a, min, max).ToString();
            }
        }
    }

    public void Add()
    {
        if (float.TryParse(belimited.text, out float a))
        {
            a += delta;
            belimited.text = a.ToString();
            belimited.onEndEdit.Invoke("");
        }
    }

    public void Minus()
    {
        if (float.TryParse(belimited.text, out float a))
        {
            a -= delta;
            belimited.text = a.ToString();
            belimited.onEndEdit.Invoke("");
        }
    }
}
