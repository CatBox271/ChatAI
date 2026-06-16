using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ActiveMicrophone : MonoBehaviour
{
    public MicrophoneASR microphone;
    public Text text; 
    public bool mode = false;

    public void SwitchMode()
    {
        mode = !mode;
        text.text = mode ? "”Ô“Ù ‰»Î" : "Œƒ◊÷ ‰»Î";

        microphone.SetListening(mode);
    }
}
