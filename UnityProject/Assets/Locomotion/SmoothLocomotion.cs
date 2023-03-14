using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Valve.VR;
using System;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

static class AngleMath
{

    //   Destination | Source | Result
    //            45 |     30 |     15
    //            30 |     45 |    -15
    //           -45 |    -30 |    -15
    //           -30 |    -45 |     15
    //360 + 45 = 405 |     30 |     15
    //          -405 |    -30 |    -15
    // if from source, to destination is clockwise, the result is positive.
    public static float angleDifference(float sourceAngle, float destinationAngle)
    {
        var distance = (destinationAngle - sourceAngle) % 360;
        if (distance < -180)
            distance += 360;
        else if (distance > 179)
            distance -= 360;
        return distance;
    }

    //now, we want to do a median filter to filter out the huge spikes once in a while. (complete switch of direction for a frame)
    //however, you have to sort it for that, and sorting an angle is not really possible since it goes in a circle (0 and 360 degrees should be the same).
    //so, we sort based on the differences between the current and previous angles in the list (which can be done if using the smallestAngleDifference function). 
    //then, we find what original angle related to the median filtered difference by keeping track of the indices list while sorting.

    //example:
    //angle idx:      0  1  2  3  4  5 
    //angles list:    0  3 -1  9 -1  1  
    //differences:     3  4  10 10  2  
    // difference idx  0  1   2  3  4  
    //sort differenc:
    //sorted diff      2   3  4  10 10
    //sorted diff idx  4   0  1   2  3
    // median:         4, with index 1
    // so, we can refer in the original angles list with the index to get the corresponding angle: 3
    public static float GetMinDifferenceAngleBasedOnAngleDerivative(Queue<float> angles)
    {
        if (angles.Count <= 2)
            return angles.ElementAt<float>(0);

        float[] prevAngleArray = angles.ToArray();
        float[] angleDifferences = new float[prevAngleArray.Length - 1];

        for (int i = 0; i < prevAngleArray.Length; i++)
        {//yes, this is recalculated more often than required. It can be more efficient.
            if (i > 0)
                angleDifferences[i - 1] = Mathf.Abs(AngleMath.angleDifference(prevAngleArray[i], prevAngleArray[i - 1]));
        }

        var sorted = angleDifferences
            .Select((x, i) => new KeyValuePair<float, int>(x, i))
            .OrderBy(x => x.Key)
            .ToList();

        List<float> sortedAngleDifferences = sorted.Select(x => x.Key).ToList();
        List<int> idx = sorted.Select(x => x.Value).ToList(); //these are the indexes in sorted order from the original list, so you can refer back to the old elements.

        //string result = "";
        //foreach (var thing in sortedAngleDifferences)
        //    result += thing + ", ";
        //Debug.Log(result);

        //string result2 = "";
        //foreach (var thing in idx)
        //    result2 += thing + ", ";
        //Debug.Log(result2);

        // the first in the list is the one with the least change wrt the rest
        int minDifferenceIndex = idx[0];
        float minDifferenceAngle = angles.ElementAt<float>(minDifferenceIndex);
        return minDifferenceAngle;
    }



    static float GetMinDifferenceAngleToCurrent(Queue<float> angles, float comparisonAngle)
    {
        if (angles.Count <= 2)
            return angles.ElementAt<float>(0);

        float[] prevAngleArray = angles.ToArray();
        float[] diffWithComparisonAngle = new float[prevAngleArray.Length];

        for (int i = 0; i < prevAngleArray.Length; i++)
        {//yes, this is recalculated more often than required. It can be more efficient.
            diffWithComparisonAngle[i] = Mathf.Abs(angleDifference(prevAngleArray[i], comparisonAngle));
        }

        var sorted = diffWithComparisonAngle
            .Select((x, i) => new KeyValuePair<float, int>(x, i))
            .OrderBy(x => x.Key)
            .ToList();

        List<float> sortedAngleDifferences = sorted.Select(x => x.Key).ToList();
        List<int> idx = sorted.Select(x => x.Value).ToList(); //these are the indexes in sorted order from the original list, so you can refer back to the old elements.

        //string result = "";
        //foreach (var thing in sortedAngleDifferences)
        //    result += thing + ", ";
        //Debug.Log(result);

        //string result2 = "";
        //foreach (var thing in idx)
        //    result2 += thing + ", ";
        //Debug.Log(result2);

        // the first in the list is the one with the least change wrt the rest
        int minDifferenceIndex = idx[0];
        float minDifferenceAngle = angles.ElementAt<float>(minDifferenceIndex);
        return minDifferenceAngle;
    }

}



// If you want to move Player, create a child object under player
public class SmoothLocomotion : MonoBehaviour
{





    [System.Serializable]
    public class Foot
    {
        [System.NonSerialized]
        public Transform sphere;

        public float calibratedThreshold = 0; // when to say this shoe is lifted;

        public bool isRight = false;
        public bool trackingLost = false;

        public Foot(bool isRightFoot)
        {
            isRight = isRightFoot;
        }

        public Foot otherfoot; //needs to be set in Start

        [System.NonSerialized]
        public bool backFoot = false;

        public bool MovingForwards
        {
            get { return Vector3.Dot(FrontDirection, Velocity) > 0.0f; }
        }

        public bool AvgMovingForwards
        {
            get { return Vector3.Dot(FrontDirection, averageLocalVelocity) > 0.0f; }
        }

        [System.NonSerialized]
        private Vector3 localVelocity; // this is the local velocity which is not affected by the locomotion
        [System.NonSerialized]
        public Vector3 averageLocalVelocity;
        [System.NonSerialized]
        private Vector3 rotVelocity; //this is the global rotation of the foot. This is so you can get the angle relative to the floor
        [System.NonSerialized]
        public Vector3 prevPos;
        [System.NonSerialized]
        public Vector3 prevRot;



        // The direction is the negative average velocity of the standing foot: standing shoe driving backwards gives direction forwards (used for standing foot)
        public Quaternion AvgVelocityOrientation { 
            get {
                return Quaternion.LookRotation(Vector3.ProjectOnPlane(averageLocalVelocity, Vector3.up), Vector3.up);
            } 
        }

        public Transform tracker; //tracker is the raw tracker data
        public Transform footTransform; //footTransform is the tracker but rotated so it actually matches the foot direction.

        public float Height
        {
            get { return footTransform.position.y; }
        }

        public Vector3 FrontDirection
        {
            get { return footTransform.rotation * Vector3.up; }
        }


        //positive speed for moving forwards, negative speed for moving backwards wrt shoe orientation
        public float HorizontalSpeed
        {
            get
            {
                if (MovingForwards)
                {
                    return Vector3.ProjectOnPlane(Velocity, Vector3.up).magnitude;
                }
                else
                    return -Vector3.ProjectOnPlane(Velocity, Vector3.up).magnitude;
            }
        }

        public float AvgHorizontalSpeed
        {
            get
            {
                if (AvgMovingForwards)
                {
                    return Vector3.ProjectOnPlane(averageLocalVelocity, Vector3.up).magnitude;
                }
                else
                    return -Vector3.ProjectOnPlane(averageLocalVelocity, Vector3.up).magnitude;
            }
        }

        //includes velocity compensation for the lifted foot being driven by the standing shoe
        public Vector3 Velocity
        {
            get
            {
                Vector3 result = rawVelocity;

                // if only one foot is lifted, the other is driving that one, so the velocity of the driving shoe shoeld be added in negative.
                if (this.IsLifted_EasyThreshold && !otherfoot.IsLifted_EasyThreshold)
                {
                    result = rawVelocity - otherfoot.rawVelocity;
                }
                return result;
            }
        }

        //local velocity of the foot. Stays the same while tracking is lost.
        // Note, using this for the lifted foot gives a lower velocity than you want. Since the standing foot drives, the lifted foot has the same velocity offset. So for the lifted foot, that standing foot velocity should be added. This is done in Velocity.
        public Vector3 rawVelocity
        {
            get { return localVelocity; }
        }

        //calculates the 
        public void CalcVelocities()
        {
            if(!trackingLost)
            {
                if (prevPos != Vector3.zero)
                {
                    //localPosition of the tracker gives the correct velocity no matter the orientation of the tracker. (Horizontal motion stays horizontal, however you rotate it)
                    // the tracker is the parent of the footTransform. The velocity should be calculated by localvelocity. Since the tracker contains that localvelocity, use that (not foottransform, which does not move locally, but via the parent tracker).
                    localVelocity = (tracker.localPosition - prevPos) / Time.fixedDeltaTime;

                    //average for a more stable orientation. The non averaged velocity is used for motion though.
                    averageLocalVelocity = SmoothLocomotion.EWMA(averageLocalVelocity, localVelocity, 0.9f); // average jitters out

                    rotVelocity = (tracker.rotation.eulerAngles - prevRot);
                }
                prevPos = tracker.localPosition;
                prevRot = tracker.rotation.eulerAngles;
               
            }
            else
                prevPos = Vector3.zero;
        }

        // sets if it is lifted or standing based on a height threshold. 
        public void UpdateState(float liftedFootThresh, float standingFootThresh)
        {
            IsStanding_EasyThreshold = false;
            IsLifted_EasyThreshold = false;
            if (Height < calibratedThreshold + standingFootThresh)
            {
                IsStanding_EasyThreshold = true;
            }
            if (Height > calibratedThreshold + liftedFootThresh)
            {
                IsLifted_EasyThreshold = true;
            }
        }



        // ATTENTION: isStanding and isLifted EasyThreshold can be true at the same time. This is needed to be sensitive enough for any case. 
        public bool IsStanding_EasyThreshold { get; private set; }
        // ATTENTION: isStanding and isLifted EasyThreshold can be true at the same time. This is needed to be sensitive enough for any case. 
        public bool IsLifted_EasyThreshold { get; private set; }



        public void Calibrate()
        {
            calibratedThreshold = tracker.position.y;
        }

        public void SetFootColor()
        {

            Material material = sphere.gameObject.GetComponent<Renderer>().material;
            if (IsLifted_EasyThreshold && IsStanding_EasyThreshold)
            {
                material.color = Color.green;
            }
            else if (IsLifted_EasyThreshold)
            {
                material.color = Color.yellow;
            }
            else if (IsStanding_EasyThreshold)
            {
                material.color = Color.blue;
            }
            //if (!AvgMovingForwards)
            //    material.color = Color.red;
        }
    }





    //----------------------------------------------------------------------------- COMBINED FEET --------------------------------------------------------------------------------------------------









    // ------------------------- now, combine the separate feet to displacement:
    public CharacterController player; //make a CharacterController component on the parent of the SteamVRObjects to move the player and link it here.
    public float additionalHeight = 0.2f; //extra height of "forehead" on top of character height

    public Transform head;
    public Transform hip;
    public GameObject directionIndicator;

    public Foot leftFoot = new Foot(false);
    public Foot rightFoot = new Foot(true);

    private Foot _liftedLeadingFoot; // this foot is the one that determines the velocity for the StandingFoot locomotion
    private Foot _standingLeadingFoot;  // this foot is the one that determines the velocity for the LiftedFoot locomotion

    public float standingThres = 0.03f; //standingthresh must be < liftedthresh
    public float liftedThres = 0.05f; // it registers being lifted a bit higher than standing on the foot. 

    public OrientationController controllerType = OrientationController.Hip;

    public float speed = 1; // for scaling output speed (set in editor)

    public bool autoCalibrate = true;


    private Vector3 prevHeadPos = Vector3.zero;
    private Vector3 prevHipPos = Vector3.zero;

    public Vector3 HeadVelocity { get; private set; }
    public Vector3 HipVelocity { get; private set; }

    public float EWMA_RightSpeed_Abs { get; private set; } = 0;
    public float EWMA_LeftSpeed_Abs { get; private set; } = 0;
    public float currentLocomotionSpeed { get; private set; } = 0;
    public float previousLocomotionSpeed { get; private set; } = 0;

    // Direction for walking
    public Quaternion AverageFeetMoveOrientation { get; private set; } = Quaternion.identity;
    public Quaternion StandingFootMoveOrientation { get; private set; } = Quaternion.identity;
    public Quaternion LiftedFootMoveOrientation { get; private set; } = Quaternion.identity;
    public Quaternion HipMoveOrientation { get; private set; } = Quaternion.identity;
    public Quaternion HeadMoveOrientation { get; private set; } = Quaternion.identity;


    float _prevIncDirectionAngle = 0;
    float _incDirectionAngle = 0;
    public float incStandingFootDirectionAngle
    {
        get { return _incDirectionAngle; }
        private set
        {
            _incDirectionAngle += AngleMath.angleDifference(_prevIncDirectionAngle, value);
            _prevIncDirectionAngle = incStandingFootDirectionAngle;
        }
    }


    // set from the tracked objects
    public bool FeetTrackingLost { get { return RightFootTrackingLost || LeftFootTrackingLost; } }

    private bool _leftFootTrackingLost = false;
    public bool LeftFootTrackingLost
    {
        get { return _leftFootTrackingLost; }
        set {leftFoot.trackingLost = value;
            _leftFootTrackingLost = value;  }
    }

    private bool _rightFootTrackingLost = false;
    public bool RightFootTrackingLost { 
        get { return _rightFootTrackingLost; } 
        set { rightFoot.trackingLost = value;
              _rightFootTrackingLost = value; } 
    }


    public bool HipTrackingLost { get; set; } = false;
    public bool HeadTrackingLost { get; set; } = false;

    public Quaternion previousOrientation { get; private set; } = Quaternion.identity;

    //to perform median filter and such
    Queue<float> previousAngles;



    //Leadingfoot is the one determining the velocity
    public Foot LiftedLeadingFoot
    {
        get { return _liftedLeadingFoot; }
        set
        {
            if (value == null || _liftedLeadingFoot == null || _liftedLeadingFoot.isRight != value.isRight) // foot switch
            {
                _liftedLeadingFoot = value;
            }
        }
    }

    //Leadingfoot is the one determining the velocity
    public Foot StandingLeadingFoot
    {
        get { return _standingLeadingFoot; }
        set
        {
            if (value == null || _standingLeadingFoot == null || _standingLeadingFoot.isRight != value.isRight) // foot switch
            {
                _standingLeadingFoot = value;
            }
        }
    }




    public enum OrientationController
    {
        Head,
        Joystick,
        Hip,
        AverageShoes,
        StandingFootVelocity,
        LiftedFootVelocity,
        LeftShoe,
        RightShoe,
        Roomscale
    }


    // Updates based on the controllertype with usually the default displacement and the given orientation.
    // In some controllertypes, it overrides those, since they are defined in the controllertype.
    Vector3 MovementDeltaVector(Quaternion orientation, float currentLocomotionSpeed)
    {
        Vector3 movement = Vector3.zero;
        Vector3 displacement = Vector3.zero;

        switch (controllerType)
        {
            case OrientationController.Roomscale:
                    movement = Vector3.zero;
                break;
            default:
                    Vector3 reorientedDisplacement = orientation * new Vector3(0, 0, currentLocomotionSpeed * Time.fixedDeltaTime);
                    movement = reorientedDisplacement;
                break;
        }
        movement.y -= 9.81f * Time.fixedDeltaTime; //gravity


        return movement;
    }

    Vector3 AverageFeetOrientationDir 
    {
        get
        {
            Vector3 averageDirection = (leftFoot.FrontDirection + rightFoot.FrontDirection).normalized;
            return Vector3.ProjectOnPlane(averageDirection, Vector3.up);
        } 
    }

    bool averageAngleSet = false;
    float averageAngleStanding = 0;

    float prevAngle = 0;

    bool StandingOrientationSet = false;

    //Outputs the direction quaternion the person should move to
    //Note: Orientation will stay the same if the tracking is lost this frame
    Quaternion MoveOrientation(OrientationController controllerType)
    {



        if (!HeadTrackingLost)
            HeadMoveOrientation = Quaternion.AngleAxis(head.rotation.eulerAngles.y, Vector3.up);

        // --- Calculate all Orientations -------
        if (!HipTrackingLost)
            HipMoveOrientation = Quaternion.AngleAxis(hip.rotation.eulerAngles.y, Vector3.up);

        

        if (!FeetTrackingLost)
        {
            AverageFeetMoveOrientation = Quaternion.LookRotation(AverageFeetOrientationDir, Vector3.up);

            if(StandingLeadingFoot.averageLocalVelocity.magnitude > 0.07)
            {
                //currently this uses the raw angles without the smoothing.
                incStandingFootDirectionAngle = StandingLeadingFoot.AvgVelocityOrientation.eulerAngles.y - 180;

               

                if(!StandingOrientationSet)
                {
                    StandingOrientationSet = true;
                    StandingFootMoveOrientation = Quaternion.AngleAxis(incStandingFootDirectionAngle, Vector3.up);
                    prevAngle = incStandingFootDirectionAngle;
                }
                    

                else if (Mathf.Abs(AngleMath.angleDifference(incStandingFootDirectionAngle, prevAngle)) < 30)
                {
                    Debug.Log("newAngle " + incStandingFootDirectionAngle + " difference to prev " + AngleMath.angleDifference(incStandingFootDirectionAngle, StandingFootMoveOrientation.eulerAngles.y));
                    prevAngle = incStandingFootDirectionAngle;
                    StandingFootMoveOrientation = Quaternion.AngleAxis(incStandingFootDirectionAngle, Vector3.up);
                }
                    

                //remember the last x angles to filter
                //int windowSize = 32;
                //previousAngles.Enqueue(incStandingFootDirectionAngle);

                //if (previousAngles.Count > windowSize)
                //    previousAngles.Dequeue();

                //float sum = 0;
                //float average = 0;
                //foreach (var angle in previousAngles)
                //{
                //    sum += angle;
                //    average = sum / previousAngles.Count;
                //}

                //Debug.Log(average);

               // StandingFootMoveOrientation = Quaternion.AngleAxis(average, Vector3.up);
            }



            // median filter
            //float[] prevAngleArray = previousAngles.ToArray();

            //string result = "";
            //Array.Sort(prevAngleArray);
            //float median = prevAngleArray[windowSize / 2];

            //foreach (var x in previousAngles)
            //    result += x + ", ";

            //Debug.Log(result + " med: " + median);

            // StandingFootMoveOrientation = Quaternion.AngleAxis(median, Vector3.up);


            // EWMA filter

        }




        // LIFTEDFOOT
        // The direction is the average velocity of the lifted foot: lifted shoe moving forwards gives direction forwards.
        if (LiftedLeadingFoot != null)
                LiftedFootMoveOrientation = LiftedLeadingFoot.AvgVelocityOrientation;
            //else
            //    // when there is no lifted foot (double stance phase) a choice has to be made for the direction
            //    LiftedFootMoveOrientation = StandingFootMoveOrientation;







        // ---------------- select orientation to be used based on controllertype
        Quaternion orientation;
        switch (controllerType)
        {
            case OrientationController.Hip:
                orientation = HipMoveOrientation;
                break;
            case OrientationController.AverageShoes: // average of both shoes
                orientation = AverageFeetMoveOrientation;
                break;
            case OrientationController.LeftShoe:
                return leftFoot.footTransform.rotation;
            case OrientationController.RightShoe:
                return rightFoot.footTransform.rotation;
            case OrientationController.StandingFootVelocity:
                orientation = StandingFootMoveOrientation;
                break;
            case OrientationController.LiftedFootVelocity:
                orientation = LiftedFootMoveOrientation;
                break;
            case OrientationController.Head:
            case OrientationController.Roomscale:
            default:
                    orientation = HeadMoveOrientation;
                break;
        }

        return orientation;

    }

    public void SetStandingLeadingShoe()
    {
        //Standingfoot based on height (smallest hight is standing, except if it moves forwards
        //if (!FeetTrackingLost)
        //{
        //    StandingLeadingFoot = leftFoot.Height < rightFoot.Height ? leftFoot : rightFoot;
        //}

        // velocity based, no smallangledifference check
        //STANDINGFOOT purely based on negative speed(driving backwards)
        //if (!FeetTrackingLost)
        //{
        //    if (leftFoot.AvgHorizontalSpeed < rightFoot.AvgHorizontalSpeed)
        //        StandingLeadingFoot = leftFoot;
        //    else
        //        StandingLeadingFoot = rightFoot;
        //}

        // combination height and velocity
        if (!FeetTrackingLost)
        {
            // if a foot is lifted, it's automatically the other foot.
            if (leftFoot.IsLifted_EasyThreshold)
            {
                StandingLeadingFoot = rightFoot;
                return;
            }
            if (rightFoot.IsLifted_EasyThreshold)
            {
                StandingLeadingFoot = leftFoot;
                return;
            }
                
            // the one with the largest negative speed is going backwards the fastests (driven by the shoe)
            if (leftFoot.AvgHorizontalSpeed < rightFoot.AvgHorizontalSpeed)
                    StandingLeadingFoot = leftFoot;
            else
                    StandingLeadingFoot = rightFoot;                
        }

    }

    public void SetLiftedLeadingShoe()
    {
        //LiftedFoot
        bool bothLifted = leftFoot.IsLifted_EasyThreshold && rightFoot.IsLifted_EasyThreshold;
        bool bothNotLifted = !leftFoot.IsLifted_EasyThreshold && !rightFoot.IsLifted_EasyThreshold;

        if (bothNotLifted)
            LiftedLeadingFoot = null;
        else if (bothLifted)
        {
            Debug.Log("Both feet are lifted. Please calibrate since that is not true for normal operation.");
            LiftedLeadingFoot = null;
        }
        else if (leftFoot.IsLifted_EasyThreshold)
        {
            LiftedLeadingFoot = leftFoot;
        }
        else if (rightFoot.IsLifted_EasyThreshold)
        {
            LiftedLeadingFoot = rightFoot;
        }

    }


    //select if a shoe is standing/lifted, and if both are, which of the two should take the lead in the algorithm.
    public void SetLeadingShoe()
    {
        SetLiftedLeadingShoe();
        SetStandingLeadingShoe();
    }


    // get a vector between the left and right foot position, pointing towards the left foot.
    // get a vector from the right foot position pointing towards the toe of the right foot.
    // if the dot product with this vector and the forwards direction is positive, left is the front foot and right is the back foot.    

    public void SetBackAndFrontFoot()
    {
        //this is tested, and works as intended
        bool leftFootFront = Vector3.Dot(leftFoot.tracker.position - rightFoot.tracker.position, AverageFeetOrientationDir) > 0;
        if (leftFootFront)
        {
            leftFoot.backFoot = false;
            rightFoot.backFoot = true;
        }
        else
        {
            rightFoot.backFoot = false;
            leftFoot.backFoot = true;
        }
    }


    public void Calibrate()
    {
        leftFoot.Calibrate();
        rightFoot.Calibrate();

        GetComponent<AnimateTrackersFromFile>().WriteLocalTrackersOnce(); //calibrate foot arrow rotation from file                                                              
        //GameObject playerSpawnPoint = GameObject.Find("PlayerSpawnPoint");
        //GetComponent<GetPlayerPos>().SetGlobalPlayerPos(new Vector3(playerSpawnPoint.transform.position.x, playerSpawnPoint.transform.position.y + head.localPosition.y, playerSpawnPoint.transform.position.z));
        Debug.Log("Calibrated");
    }



    private void Start()
    {
       leftFoot.sphere = leftFoot.tracker.Find("Sphere");
       rightFoot.sphere = rightFoot.tracker.Find("Sphere");
        leftFoot.otherfoot = rightFoot;
        rightFoot.otherfoot = leftFoot;

       previousAngles = new Queue<float>();

        if (autoCalibrate)
            StartCoroutine(CalibrationAfterSeconds(0.25f));

    }

    IEnumerator CalibrationAfterSeconds(float waitTime)
    {
        yield return new WaitForSeconds(waitTime);
        Calibrate();
    }

    // Exponentially Weighted Moving Average. Without bias correction, since all values this is used on start at 0 anyway.
    static float EWMA(float prevAverage, float newValue, float rho)
    {
        float newAverage = rho * prevAverage + (1 - rho) * newValue;
        return newAverage;
    }

    // Exponentially Weighted Moving Average. Without bias correction, since all values this is used on start at 0 anyway.
    static Vector3 EWMA(Vector3 prevAverage, Vector3 newValue, float rho)
    {
        Vector3 newAverage = rho * prevAverage + (1 - rho) * newValue;
        return newAverage;
    }

    // This is the calculation for the speed we want to add to the head position to move forwards in VR, relating to the speed of the feet.
    // On average, the head should move as much as the feet do with this algorithm.
    // rho is the "smoothness" value between 0 and 1. The closer to 1, the more it averages out the movement. However, the longer until it will react to starting/stopping or speed changes.
    float CalcLocomotionSpeed(float rho = 0.95f)
    {
        float locoSpeed = previousLocomotionSpeed;

        if (!FeetTrackingLost)
        {
            //Absolute, to get both the input from the shoe driving backwards, and the shoe moving forwards. This results in twice the speed, so must be canceled later.
            EWMA_RightSpeed_Abs = EWMA(EWMA_RightSpeed_Abs, Math.Abs(rightFoot.HorizontalSpeed), rho);
            EWMA_LeftSpeed_Abs = EWMA(EWMA_LeftSpeed_Abs, Math.Abs(leftFoot.HorizontalSpeed), rho);
            float calculatedSpeed = (EWMA_LeftSpeed_Abs + EWMA_RightSpeed_Abs) / 4; //take the average: (a + b)/2, divided by 2 an extra time (since you abs gives both the foot driven backwards and the foot going forwards, which would result in double the speed).
            locoSpeed = calculatedSpeed * speed;

        }
        currentLocomotionSpeed = locoSpeed;
        previousLocomotionSpeed = currentLocomotionSpeed;

        return locoSpeed;
    }


    // these are needed mostly for data collection
    void CalcVelocities()
    {
        //note: no check for tracking since it's only for data collection.
        HeadVelocity = (head.localPosition - prevHeadPos) / Time.fixedDeltaTime;
        prevHeadPos = head.transform.localPosition;

        //note: no check for tracking since it's only for data collection.
        HipVelocity = (hip.localPosition - prevHipPos) / Time.fixedDeltaTime;
        prevHipPos = hip.transform.position;
    }


    // Update is called once per frame
    void FixedUpdate()
    {
        CapsuleFollowHeadset();
        SetBackAndFrontFoot();
        leftFoot.UpdateState(liftedThres, standingThres);
        rightFoot.UpdateState(liftedThres, standingThres);
        leftFoot.CalcVelocities();
        rightFoot.CalcVelocities();

        SetLeadingShoe();
        leftFoot.SetFootColor();
        rightFoot.SetFootColor();

        Quaternion orientation = MoveOrientation(controllerType);

        orientation = Quaternion.Lerp(directionIndicator.transform.localRotation, orientation, 0.1f);

        directionIndicator.transform.localRotation = orientation;

        Debug.DrawRay(directionIndicator.transform.position, orientation * Vector3.forward, Color.red);

        player.Move(MovementDeltaVector(orientation, CalcLocomotionSpeed()));

        CalcVelocities();
    }

    private void Update()
    {
        foreach (KeyCode kcode in Enum.GetValues(typeof(KeyCode)))
        {
            if (Input.GetKey(kcode))
                switch (kcode)
                {
                    case KeyCode.Y:
                     Calibrate(); //set foot height (only press with two feet on the ground)
                        break;
                    case KeyCode.S:
                        GetComponent<RecordTrackers>().WriteSingleCalibration();
                        break;

                }
        }
    }


    // this capsule contains the player body. It is important for the Charactercontroller (unity object), which helps automate controlling player movement.
    void CapsuleFollowHeadset()
    {
        player.height = head.position.y + additionalHeight;

        //below will place the capsule center on the head.
        Vector3 capsuleCenter = transform.InverseTransformPoint(head.position); // head position as if it was a child of this object, so position in the coordinate frame of the trackerWalkController if that is the object with this script.
        // since the centre of the capsule is indeed kind of a child of this transform, that is needed.
        player.center = new Vector3(capsuleCenter.x, player.height / 2 + player.skinWidth, capsuleCenter.z);

       
    }
}






#if UNITY_EDITOR
[CustomEditor(typeof(SmoothLocomotion))]
public class OrientationCalibrationEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if (GUILayout.Button("Calibrate"))
        {
            ((SmoothLocomotion)target).Calibrate();
        }
    }
}
#endif
