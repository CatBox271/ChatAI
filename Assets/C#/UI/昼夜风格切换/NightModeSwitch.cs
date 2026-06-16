using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;

public class NightModeSwitch : MonoBehaviour
{
    public static NightModeSwitch instance;
    public BackGroundManager bgManager;
    public struct ColorObj
    {
        public int colorArea;
        public AddSwitchColor obj;
        public ColorObj(int _colorArea,AddSwitchColor _obj)
        {
            colorArea = _colorArea;
            obj = _obj;
        }
    }

    public List<ColorObj> colorObjs = new();
    private void Awake()
    {
        instance = this;
    }

    [System.Serializable]
    public class SwitchColor
    {
        public Color Day = new(0.95f,0.95f,0.95f,1f);
        public Color Night = new(0.1f,0.1f,0.1f,1f);

        public Color GetColor(int state)
        {
            switch (state)
            {
                case 0:
                    return Day;
                case 1:
                    return Night;
                default:
                    return Color.red;
            }
        }
    }

    public Color GetColor(int colorArea)
    {
        if (colorArea > -1 && colorArea < switchColors.Count)
        {
            return switchColors[colorArea].GetColor(state);
        }
        return Color.red;   
    }

    public List<SwitchColor> switchColors;

    static int state = 0;
    public void SwitchTo(int night)
    {
        state = night;
        Flash();
        PlayerPrefs.SetInt("DarkMode", state);
    }
    public void Switch_Color()
    {
        if (state == 0) state = 1;
        else state = 0;
        Flash();
        PlayerPrefs.SetInt("DarkMode", state);
    }
    void Flash()
    {
        foreach (ColorObj item in colorObjs)
        {
            item.obj.SwitchColor(GetColor(item.colorArea));
        }
        bgManager.TurnTo(state == 0 ? "Day" : "Night");
    }
    private void Start()
    {
        state = PlayerPrefs.GetInt("DarkMode", 0);
        Flash();
    }
}
