using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FleeingTarget : MonoBehaviour
{

    // Update is called once per frame
    void Update()
    {
        var hit = Physics.Raycast(transform.position, transform.forward, 3f);
        if (hit)
        {
            transform.Rotate(new Vector3(Random.value, Random.value, Random.value), Random.Range(45, 180));
        }
        else
        {
            transform.position += transform.forward * (Time.deltaTime * 10);
        }
    }
}
