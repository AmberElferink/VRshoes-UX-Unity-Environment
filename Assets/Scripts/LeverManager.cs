﻿using PrattiToolkit;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class LeverManager : MonoBehaviour
{
    [SerializeField]
    ColliderEventsListener _collider;
    // Start is called before the first frame update
    [SerializeField]
    GenericItem _leverTarget;
    Vector3 _leverTargetPos;
    Quaternion _leverTargetRot;
    ControllerHand _targetGrabbedByHand = ControllerHand.Invalid;
    [SerializeField]
    Transform _arm;
    [SerializeField]
    Vector3 _armAxis = Vector3.right;
    [SerializeField]
    float _restAngle = -160;
    [SerializeField]
    float _pushAngle = -40;
    Quaternion _startRot;
    Collider _leftHandInArea, _rightHandInArea;
    bool _pushed;
    public UnityEvent OnLeverPushed;
    AudioSource _source;

    // Start is called before the first frame update
    void Awake()
    {
        _source = GetComponent<AudioSource>();
        _leverTargetPos = _leverTarget.transform.localPosition;
        _leverTargetRot = _leverTarget.transform.localRotation;
        _collider.OnTriggerEnterAction += LevaTriggerEnter;
        _collider.OnTriggerExitAction += LevaTriggerExit;
        //_arm.transform.localEulerAngles = _restAngle * _armAxis;
        _arm.transform.localRotation = Quaternion.AngleAxis(_restAngle, _armAxis);
        _startRot = _arm.transform.localRotation;
    }

    private void LevaTriggerExit(Collider c)
    {
        if (_leftHandInArea == c)
            _leftHandInArea = null;
        else if (_rightHandInArea == c)
            _rightHandInArea = null;
    }

    public void OnTargetGrabbed(ItemController i, ControllerHand hand)
    {
        _leverTarget.transform.parent = null;
        _targetGrabbedByHand = hand;
    }
    public void OnTargetDropped(ItemController i, ControllerHand hand)
    {
        _targetGrabbedByHand = ControllerHand.Invalid;
        _leverTarget.transform.parent = _arm.transform;
        _leverTarget.transform.localPosition = _leverTargetPos;
        _leverTarget.transform.localRotation = _leverTargetRot;
    }
    private void LevaTriggerEnter(Collider c)
    {
        if (c.tag == "Controller")
        {
            var m = c.GetComponent<ControllerManager>();
            if (m.Hand == ControllerHand.LeftHand)
                _leftHandInArea = c;
            else if (m.Hand == ControllerHand.RightHand)
                _rightHandInArea = c;
        }
    }

    public void ResetPush()
    {
        _pushed = false;
        _leverTarget.CanInteract(true, LocomotionManager.Instance.CurrentPlayerController.GetComponent<VRItemController>());
    }

    private void Update()
    {
        if (!_pushed)
        {
            Vector3 axis;
            float angle;
            if ((_targetGrabbedByHand == ControllerHand.LeftHand && !_leftHandInArea)
            || (_targetGrabbedByHand == ControllerHand.RightHand && !_rightHandInArea))
            {
                LocomotionManager.Instance.CurrentPlayerController.GetComponent<VRItemController>().DropItem(_leverTarget.transform, true);
            }
            var targetRot = _startRot;
            if (_targetGrabbedByHand != ControllerHand.Invalid)
            {
                var dirToTarget = _leverTarget.transform.position - _arm.position;

                var rot = Quaternion.FromToRotation(_arm.forward, dirToTarget);
                targetRot = rot * _arm.rotation;
                targetRot = Quaternion.Inverse(_arm.parent.rotation) * targetRot;

                targetRot.ToAngleAxis(out angle, out axis);
                angle = CheckLimits(angle, axis);
                targetRot = Quaternion.AngleAxis(angle, _armAxis);
                float currAngle;
                Vector3 currAxis;
                _arm.localRotation.ToAngleAxis(out currAngle, out currAxis);
                currAngle = currAngle * (currAxis.x + currAxis.y + currAxis.z);

                if (!UnityExtender.NearlyEqualRange(angle, currAngle, 1f))
                    VibrateHand(_targetGrabbedByHand);
            }
            _arm.localRotation = Quaternion.Slerp(_arm.localRotation, targetRot, .5f);
            _arm.localRotation.ToAngleAxis(out angle, out axis);
            angle = CheckLimits(angle, axis);

            if (UnityExtender.NearlyEqualRange(angle,_pushAngle, 1f))
            {
                _pushed = true;
                _source.Play();
                LocomotionManager.Instance.CurrentPlayerController.GetComponent<VRItemController>().DropItem(_leverTarget.transform, true);
                _leverTarget.CanInteract(false, LocomotionManager.Instance.CurrentPlayerController.GetComponent<VRItemController>());
                OnLeverPushed?.Invoke();
            }
        }
    }

    private void VibrateHand(ControllerHand hand)
    {
        var c = LocomotionManager.Instance.CurrentPlayerController.GetComponent<VRItemController>();
        if (hand == ControllerHand.LeftHand)
            c.LeftController.GetComponent<VibrationController>().ShortVibration();
        else if (hand == ControllerHand.RightHand)
            c.RightController.GetComponent<VibrationController>().ShortVibration();
    }

    private float CheckLimits(float angle, Vector3 axis)
    {
        float newangle = angle * (axis.x + axis.y + axis.z);
        if(_restAngle <= _pushAngle)
        {
            if (newangle < _restAngle)
                newangle = _restAngle;
            else if (newangle > _pushAngle)
                newangle = _pushAngle;
        }
        else
        {
            if (newangle > _restAngle)
                newangle = _restAngle;
            else if (newangle < _pushAngle)
                newangle = _pushAngle;
        }
        return newangle;
    }
}
