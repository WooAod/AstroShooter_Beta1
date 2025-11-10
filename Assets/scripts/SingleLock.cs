using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SingleLock : MonoBehaviour
{
    [SerializeField] private string preLevel; //前置关卡名称

    private void Awake()
    {
        LevelControl lc = LevelControl.Instance; //获取关卡控制单例
        bool interactable = false; //默认不可交互

        if (lc.IsLevelCompleted(preLevel))
        {
            //前置关卡完成，设置为可交互
            interactable = true;
        }

        //应用缓冲
        gameObject.GetComponent<Button>().interactable = interactable;
    }
}
