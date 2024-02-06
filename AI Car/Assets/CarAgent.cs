using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
public class CarAgent : Agent
{
    Rigidbody2D rBody;
    [Header("Car settings")]
    public float driftFactor = 0.95f;
    public float accelerationFactor = 30.0f;
    public float turnFactor = 3.5f;
    public float maxSpeed = 20;

    public GameObject floor;

    //Local variables
    float accelerationInput = 0;
    float steeringInput = 0;

    float rotationAngle = 0;
    int maxSteps = 100;
    int step = 0;


    float velocityVsUp = 0;
    void Start()
    {
        rBody = GetComponent<Rigidbody2D>();
    }

    public Transform Target;
    public override void OnEpisodeBegin()
    {
  

        this.rBody.angularVelocity = 0.0f;
        this.rBody.velocity = Vector2.zero;
        this.transform.localPosition = new Vector2(-0.15f, -0.15f);
        // Reset rotation angle variable if used in your steering logic
        rotationAngle = 0f;

        // Reset rotation using Euler angles
        this.transform.eulerAngles = new Vector3(0f, 0f, 0f); // Set the rotation to zero (facing upwards)

        Target.localPosition = new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f));
        //Target.localPosition = new Vector2(-0.15f, 0.35f);

    }

    /*    void FixedUpdate()
        {
            // Update the timer
            timeSinceStart += Time.fixedDeltaTime;

            // Penalize for every second that passes
            if (timeSinceStart % 1.0f <= Time.fixedDeltaTime)
            {
                AddReward(-0.01f); // Small negative reward
            }

            // Check if time limit has been exceeded
            if (timeSinceStart > timeLimit)
            {
                SetReward(-0.5f); // Penalize for not reaching the target in time
                Debug.Log("Time Limit Exceeded");
                EndEpisode();
            }

            // ... (existing physics code)
        }*/

    /*void OnTriggerEnter2D(Collider2D other)
    {

        // Reward the agent
        SetReward(1.0f);
        Debug.Log("WIN");
        // Optionally, end the episode if required
        EndEpisode();

    }*/

    private void HandleRewardAndEpisodeEnd()
    {
        float distanceToTarget = Vector2.Distance(this.transform.localPosition, Target.localPosition);

        if (distanceToTarget < 0.119f)
        {
            SetReward(1.0f);
            Debug.Log("WIN");
            ChangeFloorColor(Color.green); // Change floor color to green
            step = 0;
            EndEpisode();
        }
        else if (IsOffBounds())
        {
            SetReward(-1.0f);
            Debug.Log("LOSS");
            ChangeFloorColor(Color.red); // Change floor color to red
            step = 0;
            EndEpisode();
        }

        if (step > maxSteps)
        {
            SetReward(-distanceToTarget);
            MaxStepReached();
            step = 0;
        }
        
    }

    public void MaxStepReached()
    {
        SetReward(-1.0f);
        Debug.Log("Max Steps Reached - Task Not Completed");

        // Optionally, update the floor color to indicate failure
        ChangeFloorColor(Color.red);
        
        // End the episode
        EndEpisode();
    }

    private void ChangeFloorColor(Color color)
    {
        if (floor != null)
        {
            SpriteRenderer renderer = floor.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.color = color;
            }
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        //Target and Agent positions
        sensor.AddObservation(Target.localPosition);
        sensor.AddObservation(this.transform.localPosition);

        // Agent velocity
        sensor.AddObservation(rBody.velocity.x);
        sensor.AddObservation(rBody.velocity.y);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Discrete actions
        accelerationInput = actions.ContinuousActions[0];
        steeringInput = actions.ContinuousActions[1];

        // Reset inputs
        step += 1;


        // Handle movement: 0 = forward, 1 = backward, 2 = brake
        

        ApplyEngineForce();
        KillOrthogonalVelocity();
        ApplySteering();

        // Rewards and EndEpisode conditions
        HandleRewardAndEpisodeEnd();
    }



    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActionsOut = actionsOut.ContinuousActions;
        continuousActionsOut[0] = Input.GetAxis("Vertical");
        continuousActionsOut[1] = Input.GetAxis("Horizontal");
    }


    private bool IsOffBounds()
    {
        return this.transform.localPosition.y < -0.5 || this.transform.localPosition.y > 0.5
            || this.transform.localPosition.x < -0.5 || this.transform.localPosition.x > 0.5;
    }

    void ApplyEngineForce()
    {
        //Apply drag if there is no accelerationInput so the car stops when the player lets go of the accelerator
        if (accelerationInput == 0)
            rBody.drag = Mathf.Lerp(rBody.drag, 3.0f, Time.fixedDeltaTime * 3);
        else rBody.drag = 0;

        //Caculate how much "forward" we are going in terms of the direction of our velocity
        velocityVsUp = Vector2.Dot(transform.up, rBody.velocity);

        //Limit so we cannot go faster than the max speed in the "forward" direction
        if (velocityVsUp > maxSpeed && accelerationInput > 0)
            return;

        //Limit so we cannot go faster than the 50% of max speed in the "reverse" direction
        if (velocityVsUp < -maxSpeed * 0.5f && accelerationInput < 0)
            return;

        //Limit so we cannot go faster in any direction while accelerating
        if (rBody.velocity.sqrMagnitude > maxSpeed * maxSpeed && accelerationInput > 0)
            return;

        //Create a force for the engine
        Vector2 engineForceVector = transform.up * accelerationInput * accelerationFactor;

        //Apply force and pushes the car forward
        rBody.AddForce(engineForceVector, ForceMode2D.Force);
    }

    void ApplySteering()
    {
        //Limit the cars ability to turn when moving slowly
        float minSpeedBeforeAllowTurningFactor = (rBody.velocity.magnitude / 2);
        minSpeedBeforeAllowTurningFactor = Mathf.Clamp01(minSpeedBeforeAllowTurningFactor);

        //Update the rotation angle based on input
        rotationAngle -= steeringInput * turnFactor * minSpeedBeforeAllowTurningFactor;

        //Apply steering by rotating the car object
        rBody.MoveRotation(rotationAngle);
    }

    void KillOrthogonalVelocity()
    {
        //Get forward and right velocity of the car
        Vector2 forwardVelocity = transform.up * Vector2.Dot(rBody.velocity, transform.up);
        Vector2 rightVelocity = transform.right * Vector2.Dot(rBody.velocity, transform.right);

        //Kill the orthogonal velocity (side velocity) based on how much the car should drift. 
        rBody.velocity = forwardVelocity + rightVelocity * driftFactor;
    }

    float GetLateralVelocity()
    {
        //Returns how how fast the car is moving sideways. 
        return Vector2.Dot(transform.right, rBody.velocity);
    }

    public bool IsTireScreeching(out float lateralVelocity, out bool isBraking)
    {
        lateralVelocity = GetLateralVelocity();
        isBraking = false;

        //Check if we are moving forward and if the player is hitting the brakes. In that case the tires should screech.
        if (accelerationInput < 0 && velocityVsUp > 0)
        {
            isBraking = true;
            return true;
        }

        //If we have a lot of side movement then the tires should be screeching
        if (Mathf.Abs(GetLateralVelocity()) > 4.0f)
            return true;

        return false;
    }


    public float GetVelocityMagnitude()
    {
        return rBody.velocity.magnitude;
    }
}
