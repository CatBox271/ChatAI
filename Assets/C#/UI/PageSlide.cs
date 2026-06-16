using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 页面滑动组件，控制多个页面之间的水平滑动切换。
/// 适用于通过改变 RectTransform 偏移量或自定义组件的相对位置实现翻页效果。
/// </summary>
public class PageSlide : MonoBehaviour
{
    [Header("页面设置")]
    [Tooltip("存放所有页面的父容器 RectTransform（如果使用 anchoredPosition 滑动）")]
    public RectTransform pageContainer;

    [Tooltip("自定义的 CorrectUI 组件（如果使用 RelativePosition 滑动，二选一）")]
    public CorrectUI pageUI;

    [Tooltip("单个页面的宽度（像素），用于计算目标位置偏移")]
    public float pageWidth = 1200f;

    [Tooltip("最大页面数量")]
    public int maxPage = 2;

    [Tooltip("翻页动画时间（秒）")]
    public float slideDuration = 0.5f;

    [Header("翻页按钮")]
    public Button nextPageButton;
    public Button lastPageButton;

    [Header("当前页面")]
    [SerializeField] private int currentPage = 0;

    public int CurrentPage
    {
        get => currentPage;
        set => GoToPage(value);
    }

    private Coroutine slideCoroutine;

    private void Start()
    {
        // 自动绑定按钮点击事件
        if (nextPageButton != null)
            nextPageButton.onClick.AddListener(NextPage);
        if (lastPageButton != null)
            lastPageButton.onClick.AddListener(LastPage);

        // 初始定位到当前页面
        GoToPage(currentPage, immediate: true);
    }

    /// <summary>
    /// 切换到下一页
    /// </summary>
    public void NextPage()
    {
        if (currentPage + 1 < maxPage)
            GoToPage(currentPage + 1);
    }

    /// <summary>
    /// 切换到上一页
    /// </summary>
    public void LastPage()
    {
        if (currentPage > 0)
            GoToPage(currentPage - 1);
    }

    /// <summary>
    /// 切换到指定页面
    /// </summary>
    /// <param name="targetPage">目标页面索引（从0开始）</param>
    /// <param name="immediate">是否立即跳转，不播放动画</param>
    public void GoToPage(int targetPage, bool immediate = false)
    {
        targetPage = Mathf.Clamp(targetPage, 0, maxPage - 1);
        if (targetPage == currentPage && !immediate)
            return;

        currentPage = targetPage;

        if (slideCoroutine != null)
            StopCoroutine(slideCoroutine);

        float targetX = -currentPage * pageWidth;

        if (immediate)
        {
            SetPosition(targetX);
        }
        else
        {
            slideCoroutine = StartCoroutine(PageTo(targetX));
        }
    }

    /// <summary>
    /// 播放滑动动画
    /// </summary>
    private IEnumerator PageTo(float targetX)
    {
        float startX = GetCurrentPosition();
        float t = 0f;

        while (t < slideDuration)
        {
            t += Time.deltaTime;
            float normalized = t / slideDuration;           // 0 → 1
            float eased = Mathf.Pow(normalized, 2f);        // 缓动曲线：先慢后快
            float newX = Mathf.Lerp(startX, targetX, eased);
            SetPosition(newX);
            yield return null;
        }

        SetPosition(targetX); // 最终精确归位
        slideCoroutine = null;
    }

    /// <summary>
    /// 获取当前水平位置（根据所使用的组件）
    /// </summary>
    private float GetCurrentPosition()
    {
        if (pageUI != null)
            return pageUI.RelativePosition.x;
        else if (pageContainer != null)
            return pageContainer.anchoredPosition.x;
        else
        {
            Debug.LogError("PageSlide: 未设置 pageContainer 或 pageUI 引用！");
            return 0f;
        }
    }

    /// <summary>
    /// 设置水平位置
    /// </summary>
    private void SetPosition(float x)
    {
        if (pageUI != null)
        {
            Vector2 pos = pageUI.RelativePosition;
            pos.x = x;
            pageUI.RelativePosition = pos;
        }
        else if (pageContainer != null)
        {
            Vector2 pos = pageContainer.anchoredPosition;
            pos.x = x;
            pageContainer.anchoredPosition = pos;
        }
    }

    /// <summary>
    /// 在 Inspector 中调整 maxPage 时自动限制 currentPage
    /// </summary>
    private void OnValidate()
    {
        if (Application.isPlaying)
            return;
        currentPage = Mathf.Clamp(currentPage, 0, maxPage - 1);
    }
}