using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AddPuntaje : MonoBehaviour
{

    [SerializeField]
    float healthPoints;

    [SerializeField]
    float weaponsPoints;

    [SerializeField]
    CounterManager counterManager;

    private void OnTriggerEnter(Collider other) {
        if(other.CompareTag("Player"))
        {
            switch(gameObject.tag){
                case "health": 
                    counterManager.addHealth(healthPoints);
                    Destroy( this.gameObject);
                    break;
                 case "weapons": 
                    counterManager.addWeapons(weaponsPoints);
                    Destroy( this.gameObject);
                    break;
                default: 
                    Debug.Log("none");    
                    break;
            }
        }
    }
}