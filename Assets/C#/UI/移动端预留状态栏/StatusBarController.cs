using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//专门用于显示安卓原生状态栏的组件

public class StatusBarController : MonoBehaviour
{
    void Start()
    {
        ShowStatusBar();
    }

    private void ShowStatusBar()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
    Screen.fullScreen     = false;
    using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
    using var activity    = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

    activity.Call
    (
        "runOnUiThread", new AndroidJavaRunnable
        (
            () =>
            {
                // WINDOW_FLAG_DRAWS_SYSTEM_BAR_BACKGROUNDS |
                // WINDOW_FLAG_FORCE_NOT_FULLSCREEN         | 
                // WINDOW_FLAG_LAYOUT_IN_SCREEN             | 
                // WINDOW_FLAG_TRANSLUCENT_STATUS
                var flags             = unchecked((int) 0x80000000) | 0x00000800 | 0x00000100 | 0x04000000;
                // VIEW_SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN |
                // SYSTEM_UI_FLAG_LIGHT_STATUS_BAR (black text and icons)
                var uiOptions         = 0x00000400 | 0x00002000;        

                // the outer unityPlayer and activity will be disposed by other thread
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity    = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");                        
                using var window      = activity.Call<AndroidJavaObject>("getWindow");
                using var view        = window.Call<AndroidJavaObject>("getDecorView");

                // 0xAARRGGBB
                // window.Call("setStatusBarColor",  unchecked((int) 0xFF000000));
                window.Call("setFlags", flags,       unchecked((int) 0xFFFFFFFF));
                view  .Call("setSystemUiVisibility", uiOptions);
            }
        )
    );
#endif
    }
}