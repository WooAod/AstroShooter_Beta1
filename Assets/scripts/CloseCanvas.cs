using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CloseCanvas : MonoBehaviour
{
    [SerializeField] private GameObject canvasToClose;

    public void CloseTheCanvas()
    {
        canvasToClose.SetActive(false);
    }
}
