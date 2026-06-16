using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class BackGroundManager : MonoBehaviour
{
    Dictionary<string, Texture2D> name2sprite = new();

    public Image background;
    public string nowTexture;
    // Start is called before the first frame update
    private void Awake()
    {
        PictureImport.WhileGetNewPicture += Refresh;
        Chat.chat.WhileCharacterChange += ReImport;
    }
    void Start()
    {
        LoadBackGround("Day");
        LoadBackGround("Night");
        TurnTo("Day");
    }
    bool LoadBackGround(string fileName)
    {
        string filePath = Path.Combine(Application.persistentDataPath, Chat.chat.CharacterPath, "Picture", $"{fileName}.png");
        if (!File.Exists(filePath)) return false;
        Texture2D texture = new Texture2D(2, 2);
        if (texture.LoadImage(File.ReadAllBytes(filePath)))
        {
            name2sprite[fileName] = texture;
            return true;
        }
        return false;
    }

    public void TurnTo(string state)
    {
        if (name2sprite.TryGetValue(state, out Texture2D texture))
        {
            background.sprite = PickFile.Texture2Sprite(texture);
            background.preserveAspect = true;
        }
        nowTexture = state;
    }

    void ReImport()
    {
        LoadBackGround("Day");
        LoadBackGround("Night");
        TurnTo(nowTexture);
    }

    void Refresh()
    {
        if (LoadBackGround(nowTexture))
        {
            TurnTo(nowTexture);
        }
    }
}
