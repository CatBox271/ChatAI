using UnityEngine;

public class CorrectUI : MonoBehaviour
{
    public RectTransform RectReference;
    private RectTransform ThisRect;
    public bool CorrectByTine = true;
    public int SetFrame = 60;
    private int PastFrame;
    public bool CorrectPostion = true;
    public Solution solution = Solution.right_top;
    public Vector2 RelativePosition = new(0, 0);
    public bool CorrectSize = true;
    public GameObject Aim;
    private RectTransform AimScale;
    public float SizePercentage = 80;
    public bool MatainAspect = true;
    private bool IsSame;
    public bool Ready;
    public bool Vertical;//-y
    public bool Horizontal;//x

    private void Awake()
    {
        CheckReady();
    }

    public void CheckReady()
    {
        if (Aim != null)
        {
            AimScale = Aim.GetComponent<RectTransform>();
            if (Aim.GetComponent<CorrectUI>() == null)
            {
                Ready = true;
            }
        }
        ThisRect = GetComponent<RectTransform>();
        if (RectReference == null)
        {
            RectReference = GetComponent<RectTransform>();
            IsSame = true;
        }
    }

    void OnEnable()
    {
        if (CorrectSize) RefreshSize();
        if (CorrectPostion) RefreshPos();
        if (Aim == null) Ready = true;
    }
    void LateUpdate()
    {
        if (Aim != null && !Ready && Aim.GetComponent<CorrectUI>().Ready)
        {
            if (CorrectSize) RefreshSize();
            if (CorrectPostion) RefreshPos();
            Ready = true;

        }
        if (!CorrectByTine) return;
        PastFrame += 1;
        if (PastFrame < SetFrame) return;
        PastFrame = 0;
        if (CorrectSize) RefreshSize();
        if (CorrectPostion) RefreshPos();

    }



    public void RefreshSize()
    {
        if (Aim == null)
        {
            float rx, ry;
            Vector2 rl;
            Vector2 tl = ThisRect.localScale;
            if (IsSame)
            {
                rx = ThisRect.rect.width;
                ry = ThisRect.rect.height;
                rl = Vector2.one;
            }
            else
            {
                rx = RectReference.rect.width;
                ry = RectReference.rect.height;
                rl = RectReference.localScale;
            }
            if (MatainAspect)
            {
                if (solution == Solution.into)
                {
                    if (rx * rl.x * tl.x / Screen.width <= ry * rl.y * tl.y / Screen.height)
                    {
                        float rate = SizePercentage * Screen.width / rx / 100 / rl.x / tl.x;
                        ThisRect.localScale *= rate;
                    }
                    else
                    {
                        float rate = SizePercentage * Screen.height / ry / 100 / rl.y / tl.y;
                        ThisRect.localScale *= rate;
                    }
                }
                else
                {
                    if (rx * rl.x * tl.x / Screen.width >= ry * rl.y * tl.y / Screen.height)
                    {
                        float rate = SizePercentage * Screen.width / rx / 100 / rl.x / tl.x;
                        ThisRect.localScale *= rate;
                    }
                    else
                    {
                        float rate = SizePercentage * Screen.height / ry / 100 / rl.y / tl.y;
                        ThisRect.localScale *= rate;
                    }
                }
            }
            else
            {
                float x = SizePercentage * Screen.width / rx / 100 / rl.x / tl.x;
                float y = SizePercentage * Screen.height / ry / 100 / rl.y / tl.y;
                ThisRect.localScale = new Vector3(tl.x * x, tl.y * y, ThisRect.localScale.z);
            }
            return;
        }
        Vector3 o = AimScale.localScale * SizePercentage / 100;
        if (Horizontal)
        {
            o.x *= -1;
        }
        if (Vertical)
        {
            o.y *= -1;
        }
        ThisRect.localScale = o;

    }
    public void RefreshPos()
    {
        if (solution == Solution.into) return;
        float x, y;
        if (Aim == null)
        {
            x = (Screen.width - RectReference.rect.width * RectReference.localScale.x) / 2;
            y = (Screen.height - RectReference.rect.height * RectReference.localScale.y) / 2;
            if (!IsSame)
            {
                x *= ThisRect.localScale.x;
                y *= ThisRect.localScale.y;
            }
        }
        else
        {
            x = (Screen.width - RectReference.rect.width * AimScale.localScale.x * SizePercentage / 100) / 2;
            y = (Screen.height - RectReference.rect.height * AimScale.localScale.y * SizePercentage / 100) / 2;
        }

        Vector2 RP = new(0, 0);
        switch (solution)
        {
            case Solution.left_top:
                RP = new Vector2(-x, y);
                break;
            case Solution.right_top:
                RP = new Vector2(x, y);
                break;
            case Solution.left_buttom:
                RP = new Vector2(-x, -y);
                break;
            case Solution.right_buttom:
                RP = new Vector2(x, -y);
                break;
            case Solution.left_middle:
                RP = new Vector2(-x, 0);
                break;
            case Solution.right_middle:
                RP = new Vector2(x, 0);
                break;
            case Solution.top_middle:
                RP = new Vector2(0, y);
                break;
            case Solution.buttom_middle:
                RP = new Vector2(0, -y);
                break;
        }
        RP += new Vector2(RelativePosition.x * RectReference.localScale.x - RectReference.position.x + Screen.width / 2, RelativePosition.y * RectReference.localScale.y - RectReference.position.y + Screen.height / 2);
        ThisRect.anchoredPosition += RP;
    }
    public enum Solution
    {
        left_top,
        right_top,
        left_buttom,
        right_buttom,
        left_middle,
        right_middle,
        top_middle,
        buttom_middle,
        middle,
        into
    }
}
