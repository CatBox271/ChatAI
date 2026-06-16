using System;
using System.Collections;
using UnityEngine;

public class PickFile : MonoBehaviour
{
    public static PickFile pick_file;
    private void Awake()
    {
        pick_file = this;
    }
    public void PickImageFromGallery(Action<Texture2D> back_texture)
    {
        // 参数说明：标题、类型（"image/*"）、是否允许选择多张、完成回调
        NativeGallery.GetImageFromGallery((path) =>
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.Log("用户取消选择");
                return;
            }

            // 加载图片（插件提供加载方法，返回 Texture2D）
            StartCoroutine(LoadImageCoroutine(path, back_texture));
        }, "选择图片", "image/*");
    }

    IEnumerator LoadImageCoroutine(string path, Action<Texture2D> back_texture)
    {
        // 使用插件的方法异步加载图片（或直接用 UnityWebRequest）
        Texture2D texture = NativeGallery.LoadImageAtPath(path); // 可指定最大尺寸
        if (texture == null)
        {
            Debug.LogError("加载图片失败");
            yield break;
        }
        back_texture?.Invoke(MakeTextureReadable(texture));
    }

    public Texture2D MakeTextureReadable(Texture2D source)
    {
        if (source == null) return null;

        // 创建与源纹理相同尺寸的 RenderTexture
        RenderTexture renderTex = RenderTexture.GetTemporary(
            source.width,
            source.height,
            0,
            RenderTextureFormat.Default,
            RenderTextureReadWrite.Linear
        );

        // 将源纹理绘制到 RenderTexture
        Graphics.Blit(source, renderTex);

        // 创建可读的新 Texture2D
        Texture2D readableTex = new Texture2D(
            source.width,
            source.height,
            TextureFormat.RGBA32, // 常用格式，可根据需要修改
            false,                // 是否生成 mipmap
            true                  // 关键：设置为可读
        );

        // 从 RenderTexture 读取像素
        RenderTexture.active = renderTex;
        readableTex.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        readableTex.Apply();

        // 清理临时对象
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(renderTex);

        return readableTex;
    }

    public static Sprite Texture2Sprite(Texture2D texture)
    {
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
    }
}