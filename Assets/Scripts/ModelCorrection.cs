using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ModelCorrection : MonoBehaviour {

    public bool correctRotation = true; 
    [ShowIf("correctRotation", 0, ShowIfAttribute.Comparison.Not)]
    [SerializeField] private Vector3 properRotation;

    public bool correctPosition = true;
    [ShowIf("correctPosition", 0, ShowIfAttribute.Comparison.Not)]
    [SerializeField] private Vector3 translatePosition;

    public bool correctScale = true;
    [ShowIf("correctScale", 0, ShowIfAttribute.Comparison.Not)]
    [SerializeField] private Vector3 properScale;

    // Use to fix the rotation
    public void FixRotation()
    {
        transform.Rotate (properRotation);
    }

    // Use to fix the position
    public void FixPosition()
    {
        transform.Translate(translatePosition);
    }

    // Use to fix the scale
    public void FixScale()
    {
        transform.localScale = properScale;
    }

}
