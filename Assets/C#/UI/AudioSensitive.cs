using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AudioSensitive : MonoBehaviour
{
    public MicrophoneASR asr;
    private Button button;
    private RectTransform rect;
    public Text text;
    // Start is called before the first frame update
    void Start()
    {
        button = GetComponent<Button>();
        rect = GetComponent<RectTransform>();
        button.onClick.AddListener(Judge);
        asr.spectralFluxThreshold = PlayerPrefs.GetFloat("音量阈值",1f);
    }

    void Judge()
    {
        var mouse_pos = Input.mousePosition;
        if (mouse_pos.x > rect.anchoredPosition.x + Screen.width / 2f)
        {
            asr.spectralFluxThreshold += 0.15f;
        }
        else
        {
            asr.spectralFluxThreshold -= 0.15f;
        }
        text.text = $"音量阈值："+(asr.spectralFluxThreshold = Mathf.Clamp(asr.spectralFluxThreshold, 0.05f, 3f)).ToString("0.##");
        PlayerPrefs.SetFloat("音量阈值", asr.spectralFluxThreshold);
    }
}
