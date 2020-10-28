using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager instance = null;
    public string account;

    void Awake()
    {
        if(instance == null){
            instance = this;
        }else if( instance != gameObject){
            Destroy(gameObject);
        }
        DontDestroyOnLoad(this);
    }

}
