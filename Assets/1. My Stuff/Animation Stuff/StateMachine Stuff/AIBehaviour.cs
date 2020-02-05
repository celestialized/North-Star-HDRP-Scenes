﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//using Message;

[DefaultExecutionOrder(100)]
public class AIBehaviour : MonoBehaviour
{
    //ANIMATOR STATES
    public static readonly int hashIdle = Animator.StringToHash("Idle");
    public static readonly int hashWalking = Animator.StringToHash("Walking");
    public static readonly int hashFalling = Animator.StringToHash("Falling");
    public static readonly int hashStunned = Animator.StringToHash("Stunned");
    public static readonly int hashGettingUp = Animator.StringToHash("GettingUp");

    private AIControllerM aiController;
    public AIControllerM AIController { get { return aiController; } }

    //METHOD-VARIABLES

    //Movement Variables
    public LayerMask collisionMask;
    public float groundCheckDistance = 0.02f;
    public float moveSpeed = 1f;
    public float slopeThreshold;

    //Rotation (Get Up) & Stun Variables
    public float getupSpeed = 1f;
    public float getupHeight = 0.15f;
    [Range(0.0f, 1.0f)]
    public float rotateToPoint;
    public float stunTime;

    //Edge Variables
    public float edgeCheckDistance;

    //Respawn Variables
    public Transform respawnPoint;
    public Transform deathPoint;

    //Debug Variables
    public bool logDot = false;
    public bool debugLines = false;

    //Bools and vars to trigger RigidBody manipulation in FixedUpdate()
    [SerializeField] private Rigidbody rb;

    private Vector3 slopeDirection;
    [SerializeField] private float turnSpeed;

    [SerializeField] private bool shouldTurn = false;


    //RESPAWN
    private void Update()
    {
        if (transform.position.y < deathPoint.position.y) Respawn();
    }


    //MOVEMENT
    private void FixedUpdate()
    {
        float steepness = aiController.Animator.GetFloat("Steepness");
        if (steepness > slopeThreshold && aiController.Animator.GetBool("Edge") == false)
        {
            //gameObject.transform.position += slopeDirection * moveSpeed / 1000;
            //rb.AddForce(slopeDirection * (moveSpeed * 1 - steepness), ForceMode.);

            //rb.MovePosition(slopeDirection * (moveSpeed * 1 - steepness) / 1000);
            rb.MovePosition(transform.position + (slopeDirection * moveSpeed * Time.deltaTime) / 100);

            //Debug.Log("Move position to " + (transform.position + (slopeDirection * moveSpeed * Time.deltaTime) / 100));
        }

        var temp = aiController.Animator.GetCurrentAnimatorStateInfo(0);

        if (!slopeDirection.Equals(Vector3.zero) && Vector3.Dot(transform.forward, slopeDirection) < 0.99)
        {
            if (temp.IsName("Walking") || temp.IsName("Idle"))
            {
                Debug.Log("Looking up slope!");
                //if (aiController.Animator.GetFloat("Rotation") > rotateToPoint)
                LookUpSlope(slopeDirection, turnSpeed);
            }
        }

        /*
        if (!slopeDirection.Equals(new Vector3(0, 0, 0)) && Vector3.Dot(transform.forward, slopeDirection) < 0.98 && aiController.Animator.GetBool("Ground") == true)
        {
            Debug.Log("Looking up slope!");
            if (aiController.Animator.GetFloat("Rotation") > rotateToPoint) LookUpSlope(slopeDirection, turnSpeed);
        }
        */
    }


    //METHODS
    private void OnEnable()
    {
        aiController = gameObject.GetComponent<AIControllerM>();
        aiController.Animator.Play(hashIdle, -1, 1);
        SceneLinkedSMB<AIBehaviour>.Initialise(aiController.Animator, this);
    }

    public IEnumerator StunnedTime()
    {
        yield return new WaitForSeconds(stunTime);
        aiController.Animator.SetBool("Stunned", false);
    }

    public Vector3? GetGroundNormal(float distance)
    {
        RaycastHit hit;
        Ray ray = new Ray(transform.position, Vector3.down);
        if (debugLines) Debug.DrawRay(transform.position, Vector3.down * distance, Color.red);

        if (Physics.Raycast(ray, out hit, distance, collisionMask))
        {
            return hit.normal;
        }
        else
        {
            return null;
        }
    }

    public void FixRotation(float speed, Vector3 slopeUp)
    {
        if (aiController.Animator.GetFloat("Rotation") < rotateToPoint)
        {
            //Start rotating back into place. This will automatically stop when I approach the rotateToPoint.
            Quaternion newRotation = Quaternion.FromToRotation(transform.up, slopeUp) * transform.rotation;
            transform.Translate(new Vector3(0, getupHeight, 0) * Time.deltaTime, Space.World);
            transform.rotation = Quaternion.Slerp(transform.rotation, newRotation, Time.deltaTime * speed);

            //Calculate my rotation dot and pass that into the Animator's Rotation float
            MonitorRotation(slopeUp);
        }
    }

    public void MonitorRotation(Vector3 groundSlope)
    {
        //Set current up direction to check against
        Vector3 currentUp = transform.up;

        //Get the dot product of the up direction of the slope and the current up direction
        float rotation = Vector3.Dot(groundSlope, currentUp);
        aiController.Animator.SetFloat("Rotation", rotation);

        if (logDot) Debug.Log("Dot between self and ground is " + rotation);
    }

    public void SetSteepness(Vector3 slopeNormal)
    {
        //Get the direction I should look (pointing up the slope)
        Vector3 cross = Vector3.Cross(Vector3.up, slopeNormal);
        slopeDirection = Vector3.Cross(-cross, slopeNormal).normalized;
        aiController.Animator.SetFloat("Steepness", Mathf.Abs(cross.x) + Mathf.Abs(cross.y) + Mathf.Abs(cross.z));

        if (debugLines)
        {
            Debug.DrawRay(transform.position, slopeNormal, Color.green);
            Debug.DrawRay(transform.position, slopeDirection, Color.blue);
        }
    }

    public void LookUpSlope(Vector3 slopeUpDirection, float turnForce)
    {
        /*
        Vector3 x = Vector3.Cross(transform.forward.normalized, slopeUpDirection.normalized);
        float theta = Mathf.Asin(x.magnitude);
        Vector3 w = x.normalized * theta / Time.fixedDeltaTime;

        Quaternion q = transform.rotation * rb.inertiaTensorRotation;
        var T = q * Vector3.Scale(rb.inertiaTensor, (Quaternion.Inverse(q) * w));

        rb.AddRelativeTorque(T * turnForce, ForceMode.Acceleration);
        */

        rb.MoveRotation(Quaternion.LookRotation(slopeDirection, transform.up));
    }

    public bool IsApproachingAnEdge(float distance, float edgeDistance, LayerMask mask)
    {
        //RaycastHit hit;
        Vector3 edgeCheck = transform.position + (transform.forward * edgeDistance);
        Ray ray = new Ray(edgeCheck, Vector3.down);
        if (debugLines) Debug.DrawRay(edgeCheck, Vector3.down * (groundCheckDistance * 2), Color.yellow);

        return !Physics.Raycast(ray, distance, mask);
    }

    private void Respawn()
    {
        transform.position = respawnPoint.position;
    }
}