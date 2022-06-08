using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class GameManager : MonoBehaviour
{
    public Planet planet;

    private static GameManager _instance = null;

    public static GameManager Instance
    {
        get
        {
            if(_instance == null)
            {
                _instance = FindObjectOfType<GameManager>();
                if(_instance == null)
                {
                    _instance = new GameObject("GameManager").AddComponent<GameManager>();
                }
            }
            return _instance;
        }
    }

    void Awake()
    {
        if(!_instance)
        {
            _instance = this;
        } else
        {
            Destroy(this);
        }
    }
}
