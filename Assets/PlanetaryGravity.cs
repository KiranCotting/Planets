using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlanetaryGravity : MonoBehaviour
{
    public Planet planet;

    public Vector3 gravity;

    private static readonly double G = 6.67408f;

    void FixedUpdate()
    {
        float m1 = planet.GetComponent<Rigidbody>().mass;
        float m2 = GetComponent<Rigidbody>().mass;
        Vector3 r = planet.transform.position - transform.position;
        double grav = (G * m1 * m2) / (r.sqrMagnitude * 10000);
        gravity = r.normalized * (float) grav;
        GetComponent<Rigidbody>().AddForce(gravity);
    }
}
