﻿
/*
 * Custom template by Gabriele P.
 */
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Level5Manager))]
public class StatisticsLoggerL5 : StatisticsLoggerBase
{
    #region Events

    #endregion

    #region Editor Visible

    #endregion

    #region Private Members and Constants
    protected List<float> _towerPrecision = new List<float>();
    protected List<float> _rotSetupPrecision = new List<float>();
    protected List<float> _posSetupPrecision = new List<float>();
    protected List<float> _rotTowerPrecision = new List<float>();
    protected List<float> _posTowerPrecision = new List<float>();
    private float _timeStart, _timeStop;
    private uint  _grabtask;
    private bool _grabbing;
    private bool _movingInteraction;
    private int _itemCollisions;
	private bool _playerInside = false;
	private uint _counter = 0;
	private uint _inside = 0;
    #endregion

    #region Properties

    #endregion

    #region MonoBehaviour

    protected override void Initialize()
    {
        base.Initialize();
    }

    public void StartLogGrabbing(Destination d)
    {
        if (_grabtask == 0)
        {
            StartMasterLog("G");
            LocomotionManager.Instance.CurrentPlayerController.GetComponent<VRItemController>().OnDrop += LogDrop;
        }

        _timeStart = Time.time;
        Collisions = 0;
        _itemCollisions = 0;
        switch (_grabtask)
        {
            case 0:
                _errors = -1;
                break;
            case 1:
            case 2:
                _errors = -2;
                break;
        }
        _speeds.Clear();
        _grabtask++;
        _prevpos = LocomotionManager.Instance.CurrentPlayerController.position;
        _grabbing = true;
    }

    public void StartLogManipulation(Destination d)
    {
        _timeStart = Time.time;
        _speeds.Clear();
        StartMasterLog("M");
    }
    public void StartLogMovingInteraction()
    {
        _movingInteraction = true;
        _timeStart = Time.time;
        _errors = 0;

        StartMasterLog("MI");
    }
    public void PlayerOutRange()
    {
		_playerInside = false;
    }  
	
	public void PlayerInRange()
    {
		_playerInside = true;
    }

    public void LogInteractionError()
    {
        _errors++;
    }
    private void LogDrop(GenericItem i)
    {
        if (_grabbing || _movingInteraction)
            _errors++;
    }
    public override void LogCollisions(HitType type)
    {
        if (_grabbing)
        {
            if (type == HitType.Player)
                Collisions++;
            else if (type == HitType.Item)
                _itemCollisions++;
        }
        LocomotionManager.Instance.LeftController.GetComponent<VibrationController>().ShortVibration(.5f);
        LocomotionManager.Instance.RightController.GetComponent<VibrationController>().ShortVibration(.5f);
    }

    public void StopLogGrabbing(Destination d)
    {
        _timeStop = Time.time - _timeStart;
        var values = new List<string>
        {
            "" + _timeStop,
            "" + GetAverageSpeed(),
            "" + _errors,
            "" + Collisions,
            "" + _itemCollisions
        };
        WriteToCSV("G"+_grabtask, values, 1);
        if (_grabtask == 3)
            StopMasterLog();

        _grabbing = false;
    }
    public void StopLogManipulation(Destination d)
    {
        _timeStop = Time.time - _timeStart;
        var values = new List<string>
        {
            "" + _timeStop,
            "" + GetAvgPosSetupPrecision(),     //avg pos setup precision
            "" + GetAvgRotSetupPrecision(),     //avg rot setup precision
            "" + GetAvgPosTowerPrecision(),     //avg pos tower precision
            "" + GetAvgRotTowerPrecision(),     //avg rot tower precision
            "" + GetAverageSpeed(),             //avg setup precision
            "" + GetAvgTowerPrecision()         //avg tower precision
        };
        WriteToCSV("M", values, 2);
        StopMasterLog();
    }

    internal void LogSetupPrecision(float pos, float rot)
    {
        _posSetupPrecision.Add(pos);
        _rotSetupPrecision.Add(rot);
        _speeds.Add((pos + rot) /2);
    }
    internal void LogTowerPrecision(float pos, float rot)
    {
        _posTowerPrecision.Add(pos);
        _rotTowerPrecision.Add(rot);        
        _towerPrecision.Add((pos + rot) / 2);
    }

    public void StopLogMovingInteraction()
    {
        _movingInteraction = false;
        _timeStop = Time.time - _timeStart;
        var values = new List<string>
        {
            "" + _timeStop,
            "" + _errors,
            "" + GetPercTimeInside()       //times player gets out of range
        };
        WriteToCSV("MI", values, 3);
        StopMasterLog();
    }

    #endregion

    #region Public Methods

    #endregion

    #region Helper Methods     
    private float GetPercTimeInside()
    {
        if (_inside == 0) return 0;
        return ((float)_inside / (float)(_counter)) * 100;
    }	
    protected float GetAvgPosSetupPrecision()
    {
        float v = 0.0f;
        foreach (var s in _posSetupPrecision)
        {
            v += s;
        }
        return v / _posSetupPrecision.Count;
    }
    protected float GetAvgRotSetupPrecision()
    {
        float v = 0.0f;
        foreach (var s in _rotSetupPrecision)
        {
            v += s;
        }
        return v / _rotSetupPrecision.Count;
    }
    protected float GetAvgPosTowerPrecision()
    {
        float v = 0.0f;
        foreach (var s in _posTowerPrecision)
        {
            v += s;
        }
        return v / _posTowerPrecision.Count;
    }
    protected float GetAvgRotTowerPrecision()
    {
        float v = 0.0f;
        foreach (var s in _rotTowerPrecision)
        {
            v += s;
        }
        return v / _rotTowerPrecision.Count;
    }
    protected float GetAvgTowerPrecision()
    {
        float v = 0.0f;
        foreach (var s in _towerPrecision)
        {
            v += s;
        }
        return v / _towerPrecision.Count;
    }
    protected override void ComputeStatisticsStep()
    {
        if (_grabbing)
        {
            var t = (Time.time - _lastsample); // compute delta time
            var d = Mathf.Abs(Vector3.Distance(LocomotionManager.Instance.CurrentPlayerController.position, _prevpos)); // compute distance traveled
            var v = d / t; //compute speed

            _speeds.Add(v);
            _prevpos = LocomotionManager.Instance.CurrentPlayerController.position;
        }
		 if (_movingInteraction)
        {
            _counter++;
            if (_playerInside)
                _inside++;
        }

        base.ComputeStatisticsStep();
    }

    #endregion

    #region Events Callbacks

    #endregion

    #region Coroutines

    #endregion

}
