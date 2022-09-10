using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveDragon : MonoBehaviour
{

    public int Speed = 10;
    // Update is called once per frame
    void Update()
    {
        if (Input.GetKey(KeyCode.W))
        {
            transform.Translate(0,0,Speed*Time.deltaTime); 
        }
        
        if (Input.GetKey(KeyCode.S))
        {
            transform.Translate(0,0,-Speed*Time.deltaTime); 
        }
    }
}
