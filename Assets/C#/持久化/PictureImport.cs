using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using System;

public class PictureImport : MonoBehaviour
{
    public string PictureName;
    private Button button;
    public Image image;

    public static Action WhileGetNewPicture;
    private void Awake()
    {
        //if(TryGetComponent(out button)) button.onClick.AddListener(TryGetPic);
    }
    private void Start()
    {
        //string filePath = Path.Combine(Application.persistentDataPath, Chat.chat.CharacterPath, "Picture", PictureName + ".png");
        //if (!File.Exists(filePath)) return;

        ////≥¢ ‘º”‘ÿÕº∆¨
        //Texture2D texture = new Texture2D(2, 2);
        //if (texture.LoadImage(File.ReadAllBytes(filePath)))
        //{
        //    image.sprite = PickFile.Texture2Sprite(texture);
        //    image.enabled = true;
        //}
    }
    void TryGetPic()
    {
        PickFile.pick_file.PickImageFromGallery(GotPic);
    }

    void GotPic(Texture2D back_texture)
    {
        if (back_texture == null) return;
        string FolderPath = Path.Combine(Application.persistentDataPath, Chat.chat.CharacterPath, "Picture");
        if (!Directory.Exists(FolderPath)) Directory.CreateDirectory(FolderPath);
        //±£¥Ê
        File.WriteAllBytes(Path.Combine(FolderPath, PictureName + ".png"), back_texture.EncodeToPNG());
        //À¢–¬
        image.sprite = PickFile.Texture2Sprite(back_texture);
        image.enabled = true;

        WhileGetNewPicture?.Invoke();
    }


}
