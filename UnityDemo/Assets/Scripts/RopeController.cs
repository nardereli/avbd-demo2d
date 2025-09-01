using System.Collections.Generic;
using UnityEngine;

public class RopeController : MonoBehaviour
{
    public Transform startPoint;
    public Transform endPoint;
    public GameObject segmentPrefab;
    public int segmentCount = 20;
    public bool autoLength = true;
    [Range(0.1f,5f)] public float stretchLimit = 1f;

    private readonly List<ConfigurableJoint> joints = new List<ConfigurableJoint>();

    void Start()
    {
        if (segmentPrefab == null || startPoint == null || endPoint == null)
            return;
        BuildRope();
    }

    void BuildRope()
    {
        Vector3 dir = (endPoint.position - startPoint.position) / segmentCount;
        Transform prev = startPoint;
        for (int i = 0; i < segmentCount; i++)
        {
            GameObject seg = Instantiate(segmentPrefab, startPoint.position + dir * (i + 1), Quaternion.identity, transform);
            Rigidbody rb = seg.GetComponent<Rigidbody>();
            var joint = seg.AddComponent<ConfigurableJoint>();
            joint.connectedBody = prev.GetComponent<Rigidbody>();
            joint.xMotion = ConfigurableJointMotion.Limited;
            joint.yMotion = ConfigurableJointMotion.Limited;
            joint.zMotion = ConfigurableJointMotion.Limited;
            joint.autoConfigureConnectedAnchor = autoLength;
            SoftJointLimit limit = joint.linearLimit;
            limit.limit = stretchLimit;
            joint.linearLimit = limit;
            joints.Add(joint);
            prev = seg.transform;
        }
        var endJoint = endPoint.gameObject.AddComponent<ConfigurableJoint>();
        endJoint.connectedBody = prev.GetComponent<Rigidbody>();
        endJoint.autoConfigureConnectedAnchor = autoLength;
        joints.Add(endJoint);
    }

    public void SetAutoLength(bool value)
    {
        autoLength = value;
        foreach (var joint in joints)
            joint.autoConfigureConnectedAnchor = value;
    }

    public void SetStretchLimit(float value)
    {
        stretchLimit = value;
        foreach (var joint in joints)
        {
            SoftJointLimit limit = joint.linearLimit;
            limit.limit = value;
            joint.linearLimit = limit;
        }
    }
}

