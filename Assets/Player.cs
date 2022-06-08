using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlanetaryGravity))]
public class Player : MonoBehaviour
{
    public Camera playerCamera;

    public float walkSpeed = 1f;

    public float walkAccel = 10f;

    public float jumpStrength = 2500f;

    public float maximumSlopeCutoff = 0.7f;

    private bool grounded = false;

    private PlanetaryGravity pGravity;

    private float mouseY = 0f;

    private Rigidbody rb;

    private ContactPoint cp;

    void Start()
    {
        pGravity = GetComponent<PlanetaryGravity>();
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        // get an up vector that points away from gravity
        Vector3 up = -pGravity.gravity;
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        // correct the forward and right vectors so that they are ortho-normal (form right angles with) the up vector
        Vector3.OrthoNormalize(ref up, ref forward, ref right);

        // retrieve mouse velocity (NOT position!) 
        float mouseX = Input.GetAxisRaw("Mouse X");
        // accumulate y so that it can be clamped
        mouseY += Input.GetAxisRaw("Mouse Y");
        // clamp y between -90 and 90
        mouseY = Mathf.Clamp(mouseY, -90f, 90f);

        // generate a quaternion that looks forward with up as it's up vector
        transform.rotation = Quaternion.LookRotation(forward, up);

        // NOTE: mouseX and mouseY map to lookRotY and lookRotX respectively
        // generate local space x axis quaternion from mouseY (controls vertical/up & down camera rotation)
        Quaternion lookRotX = Quaternion.Euler(new Vector3(-mouseY, 0, 0));
        // generate local space y axis rotation be rotating around the up vector by mouseX
        Quaternion lookRotY = Quaternion.AngleAxis(mouseX, up);

        // accumulate player y axis rotation rotation
        transform.rotation *= lookRotY;
        // set player x axis rotation (we already accumulated x axis up above)
        playerCamera.transform.localRotation = lookRotX;

        float walk = Input.GetAxisRaw("Vertical");
        float strafe = Input.GetAxisRaw("Horizontal");

        if(grounded)
        {
            Vector3 walkDir = forward;
            // correct walkDir vector so that character can walk up slopes, but only if slope is not too extreme
            if (Vector3.Dot(cp.normal, up) > maximumSlopeCutoff)
            {
                walkDir = Vector3.Cross(right, cp.normal);
            }
            Vector3 move = walkDir * walk + right * strafe;
            if(rb.velocity.magnitude < walkSpeed)
            {
                rb.velocity += move.normalized * walkAccel * Time.deltaTime;

                // when not walking, decelerate forward movement
                if (walk == 0)
                {
                    Vector3 forwardsVelocity = Vector3.Dot(rb.velocity, walkDir) * walkDir;
                    rb.velocity -= forwardsVelocity * walkAccel * Time.deltaTime;
                }

                // when not strafing, decelerate strafe movement
                if (strafe == 0)
                {
                    Vector3 sidewaysVelocity = Vector3.Dot(rb.velocity, right) * right;
                    rb.velocity -= sidewaysVelocity * walkAccel * Time.deltaTime;
                }
            }
            
        }

        if (Input.GetAxisRaw("Jump") > 0 && grounded)
        {
            grounded = false;
            GetComponent<Rigidbody>().AddForce(up * jumpStrength);
        }
    }

    public void OnCollisionEnter(Collision collision)
    {
        if(collision.gameObject.GetComponent<PlanetChunky>())
        {
            grounded = true;
            cp = collision.GetContact(0);
        }
    }

    public void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.GetComponent<PlanetChunky>())
        {
            grounded = false;
        }
    }
}
