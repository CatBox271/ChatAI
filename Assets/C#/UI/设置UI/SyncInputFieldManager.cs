using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SyncInputFieldManager : MonoBehaviour
{
    public InputField IP;
    public InputField Port;
    public SyncCharacterData sync;
    // Start is called before the first frame update
    void Start()
    {
        sync = SyncCharacterData.instance;

        IP.onSubmit.AddListener(IPInput);
        Port.onSubmit.AddListener(PortInput);

        IP.text = sync != null ? sync.serverIp : PlayerPrefs.GetString("SyncIP", "frp-sea.com");
        Port.text = sync != null ? sync.serverPort.ToString() : PlayerPrefs.GetInt("SyncPort", 64867).ToString();
    }

    void IPInput(string input)
    {
        if (sync != null) sync.serverIp = input;
        PlayerPrefs.SetString("SyncIP", input);
        print("setIp");
    }

    void PortInput(string input)
    {
        if (sync != null) sync.serverPort = int.Parse(input);
        PlayerPrefs.SetInt("SyncPort", int.Parse(input));
    }
}
