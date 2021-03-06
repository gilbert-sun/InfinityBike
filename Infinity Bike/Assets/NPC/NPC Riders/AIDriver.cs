﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AIDriver : Movement
{   
    [SerializeField]
    public AiSettings aiSettings = new AiSettings();

    public TrackNode trackNode = null;
    [HideInInspector]
    public AiPid pid = null;
    public float maximumSteeringAngle = 45;

    public float motorTorque = 0;

    private int waypointNodeID;
    public int WaypointNodeID
    {
        get { return waypointNodeID; }
        set
        {
            waypointNodeID = value;
            trackNode.ClampIndex(ref waypointNodeID);
        }
    }
    
    public float velocitySqr = 0;
    private Vector3 nextWaypoint;

    private float timeAlive = 0;
    Respawn respawn = null;
    public int lookAhead = 2;

    private float TargetAngle
    {   
        get { return targetAngle; }
        set
        {
            if (float.IsNaN(value))
            { targetAngle = 0; }
            else
            { targetAngle = value; }

            if (targetAngle > maximumSteeringAngle)
            { targetAngle = maximumSteeringAngle; }
            else if (targetAngle < -maximumSteeringAngle)
            { targetAngle = -maximumSteeringAngle; }
            frontWheel.steerAngle = targetAngle;
        }
    }   
    
    void Start()
    {   
        MovementStart();

        pid = GetComponent<AiPid>();
        pid.UpdateErrorValue += () => 
        {   
            pid.errorVariable = (aiSettings.targetSqrSpeed - velocitySqr);
        };  

        aiSettings.SetRandomValues();

        timeAlive = 0;
        InitialiseRespawn();

    }

    void InitialiseRespawn()
    {
        respawn = GetComponent<Respawn>(); ;
        waypointNodeID = -1;
        respawn.AddLastToRespawnAction(pid.ResetValues);
        respawn.AddLastToRespawnAction(() => { Stop(breakForce); });
        respawn.AddLastToRespawnAction(() => { respawn.respawnNode = waypointNodeID - 1; });
        respawn.AddLastToRespawnAction(() => { timeAlive = 0; StartCoroutine(SetIdleOnATimer(2f)); });
        respawn.AddLastToRespawnAction(() => { respawn.RespawnObject(GetNextWayPoint(0), (GetNextWayPoint(0 + 1) - GetNextWayPoint(0)).normalized); });
    }   

    private void Update()
    {
        timeAlive += Time.deltaTime * rb.velocity.magnitude;

        velocitySqr = Vector3.Dot(rb.velocity, transform.forward) * Mathf.Abs(Vector3.Dot(rb.velocity, transform.forward));
        SetRotationUp();

        UpdateWaypointID();
        nextWaypoint = GetNextWayPoint(lookAhead);

        Vector3 trackDirection = (trackNode.GetNode(waypointNodeID + 1) - trackNode.GetNode(waypointNodeID)).normalized;
        if (Vector3.Dot(trackDirection, transform.forward) < 0)
        { respawn.CallRespawnAction(); }
    }   

    void FixedUpdate()
    {
        if (!IdleMode && isGrounded)
        {
            pid.RunPID();
            motorTorque = pid.controlVariable;
            Go(motorTorque);
        }   

        SetSteeringAngle();
        ApplyVelocityDrag(velocityDrag);
        if (trackNode.isLoopOpen && ((waypointNodeID + 1) >= (trackNode.GetNodeCount())))
        { IdleMode = true; }
    }

    protected override void EnterIdleMode()
    {
        Stop(breakForce);
    }

    protected override void ExitIdleMode()
    {

    }

    protected override void SetSteeringAngle()
    {

        Vector3 direction = transform.InverseTransformPoint(nextWaypoint);
        float dot = direction.x / direction.magnitude;

        TargetAngle = Mathf.Lerp(TargetAngle, dot * maximumSteeringAngle, aiSettings.turnSpeed * Time.fixedDeltaTime);
    }

    private Vector3 GetNextWayPoint(int lookAhead)
    {
        Vector3 targetPoint = trackNode.GetNode(waypointNodeID + lookAhead);

        Vector3 normal = WheelNormal;
        Vector3 forward = trackNode.GetNode(waypointNodeID + lookAhead) - trackNode.GetNode(waypointNodeID + lookAhead - 1);

        if (!isGrounded)
        { normal = Vector3.up; }

        Vector3 perpendicularDirection = Vector3.Cross(forward, normal).normalized;

        float offset = ((aiSettings.trajectoryOffset.amplitude * Mathf.Sin(aiSettings.trajectoryOffset.frequency * timeAlive + aiSettings.trajectoryOffset.timeOffSet)) + aiSettings.trajectoryOffset.transverseOffset);
        targetPoint += perpendicularDirection * offset;

        return targetPoint;
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        try
        {
            respawn.CallRespawnAction();
        }
        catch (System.NullReferenceException)
        { Debug.LogError(GetComponent<Respawn>().name); }
    }

    IEnumerator SetIdleOnATimer(float timeIdle)
    {
        IdleMode = true;
        do
        {
            yield return new WaitForSeconds(timeIdle);
        } while (!isGrounded);
        IdleMode = false;

    }

    void UpdateWaypointID()
    {
        WaypointNodeID = Respawn.FindNearestNode(trackNode, transform);
    }

}
