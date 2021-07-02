using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CircuitController : MonoBehaviour
{
    private LineRenderer _circuitPath;
    private Vector3[] _pathPos;
    private float[] _cumArcLength;
    private float _totalLength;

    public float CircuitLength
    {
        get { return _totalLength; }
    }

    void Start()
    {
        _circuitPath = GetComponent<LineRenderer>();
        int numPoints = _circuitPath.positionCount;
        _pathPos = new Vector3[numPoints];
        _cumArcLength = new float[numPoints];
        _circuitPath.GetPositions(_pathPos);

        // Compute circuit arc-length
        _cumArcLength[0] = 0;

        for (int i = 1; i < _pathPos.Length; ++i)
        {
            float length = (_pathPos[i] - _pathPos[i - 1]).magnitude;
            _cumArcLength[i] = _cumArcLength[i - 1] + length;
        }

        _totalLength = _cumArcLength[_cumArcLength.Length - 1];
    }

    public Vector3 GetSegment(int idx)
    {
        return _pathPos[idx + 1] - _pathPos[idx];
    }

    public float ComputeClosestPointArcLength(Vector3 posIn, out int segIdx, out Vector3 posProjOut, out float distOut)
    {
        int minSegIdx = 0;
        float minArcL = float.NegativeInfinity;
        float minDist = float.PositiveInfinity;
        Vector3 minProj = Vector3.zero;

        // Check segments for valid projections of the point
        for (int i = 0; i < _pathPos.Length - 1; ++i)
        {
            Vector3 calc = _pathPos[i + 1] - _pathPos[i];
            Vector3 pathVec = calc.normalized;
            float segLength = calc.magnitude;


            Vector3 carVec = (posIn - _pathPos[i]);
            float dotProd = Vector3.Dot(carVec, pathVec);

            if (dotProd < 0)
                continue;

            if (dotProd > segLength)
                continue; // Passed

            Vector3 proj = _pathPos[i] + dotProd * pathVec;
            float dist = (posIn - proj).magnitude;
            if (dist < minDist)
            {
                minDist = dist;
                minProj = proj;
                minSegIdx = i;
                minArcL = _cumArcLength[i] + dotProd;
            }
        }

        // If there was no valid projection check nodes
        if (float.IsPositiveInfinity(minDist)) //minDist == float.PositiveInfinity
        {
            for (int i = 0; i < _pathPos.Length - 2; ++i)
            {
                float dist = (posIn - _pathPos[i]).magnitude;
                if (dist < minDist)
                {
                    minDist = dist;
                    minSegIdx = i;
                    minProj = _pathPos[i];
                    minArcL = _cumArcLength[i];
                }
            }
        }

        segIdx = minSegIdx;
        posProjOut = minProj;
        distOut = minDist;

        return minArcL;
    }
}