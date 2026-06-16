using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SetLayOutToLast : MonoBehaviour
{
    void Update()
    {
        Transform tf = transform.GetChild(0);
        if (tf.TryGetComponent(out LayoutElement _))
        {
            tf.SetAsLastSibling();
            Destroy(this);
        }
    }
}
