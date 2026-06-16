using UnityEngine;
using UnityEngine.Rendering;

//用于去除开屏动画的代码不要动
public class SkipLogo
{
    // 指定此方法在加载时立即调用，在显示启动动画前之前执行
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
    private static void BeforeSplashScreen()
    {
        //平台适配
#if UNITY_WEBGL
        // 如果在 WebGL 平台上运行，立即停止启动动画
        SplashScreen.Stop(SplashScreen.StopBehavior.StopImmediate);
#else
        // 如果不是在 WebGL 平台上，启动一个异步任务来停止启动动画
        System.Threading.Tasks.Task.Run(() =>
        {
            SplashScreen.Stop(SplashScreen.StopBehavior.StopImmediate);
        });
#endif
    }
}