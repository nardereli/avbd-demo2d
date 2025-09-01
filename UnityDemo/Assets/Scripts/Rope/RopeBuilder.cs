using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace AVBD
{
    /// <summary>
    /// Utility MonoBehaviour that builds a simple rope composed of capsule
    /// segments connected by <see cref="Joint3D"/> constraints.  The first
    /// segment may be static by assigning it zero mass.
    /// </summary>
    public class RopeBuilder : MonoBehaviour
    {
        [Header("Rope Parameters")]
        public int segmentCount = 5;
        public float totalLength = 5f;
        [Tooltip("Maximum stretch factor before inserting a new segment")] 
        public float stretchLimit = 1.2f;
        [Tooltip("Solver drag coefficient applied to all segments")]
        public float drag = 0.0f;
        [Tooltip("Solver post-update drag coefficient")]
        public float postDrag = 0.0f;
        public bool autoLength = false;

        public Solver3D solver;

        readonly List<GameObject> m_Segments = new List<GameObject>();
        readonly List<Body3D> m_Bodies = new List<Body3D>();
        readonly List<Joint3D> m_Joints = new List<Joint3D>();
        readonly List<ForceHandle> m_Handles = new List<ForceHandle>();

        float m_SegmentLength;
        bool m_Dirty = false;

        void Start()
        {
            m_SegmentLength = (segmentCount > 0) ? totalLength / segmentCount : 0.5f;
            BuildInitialRope();
            SyncSolver();
        }

        void Update()
        {
            if (autoLength)
            {
                MonitorAutoLength();
            }

            // Update transforms from solver state
            if (solver != null && solver.bodies.IsCreated)
            {
                for (int i = 0; i < m_Segments.Count; ++i)
                {
                    var b = solver.bodies[i];
                    m_Segments[i].transform.position = b.position;
                }
            }

            solver?.Step();
        }

        /// <summary>
        /// Creates the initial set of segments and joints.
        /// </summary>
        void BuildInitialRope()
        {
            for (int i = 0; i < segmentCount; ++i)
            {
                var go = new GameObject($"Segment_{i}");
                go.transform.parent = transform;
                go.transform.localPosition = new Vector3(0, -m_SegmentLength * i, 0);
                var col = go.AddComponent<CapsuleCollider>();
                col.height = m_SegmentLength;
                col.radius = 0.05f;
                col.direction = 1; // Y axis
                m_Segments.Add(go);

                var body = new Body3D
                {
                    position = go.transform.position,
                    orientation = quaternion.identity,
                    velocity = float3.zero,
                    angularVelocity = float3.zero,
                    mass = (i == 0) ? 0f : 1f,
                    inertiaTensor = float3x3.identity,
                    friction = 0f
                };
                m_Bodies.Add(body);

                if (i > 0)
                {
                    var joint = new Joint3D
                    {
                        bodyA = i - 1,
                        bodyB = i,
                        anchorA = new float3(0, -m_SegmentLength * 0.5f, 0),
                        anchorB = new float3(0, m_SegmentLength * 0.5f, 0)
                    };
                    m_Joints.Add(joint);
                    m_Handles.Add(new ForceHandle { Type = ForceType.Joint, Index = m_Joints.Count - 1 });
                }
            }
            m_Dirty = true;
        }

        /// <summary>
        /// Synchronizes managed lists with the solver's native arrays.
        /// </summary>
        void SyncSolver()
        {
            if (!m_Dirty || solver == null) return;
            m_Dirty = false;

            if (solver.bodies.IsCreated) solver.bodies.Dispose();
            if (solver.joints.IsCreated) solver.joints.Dispose();
            if (solver.forces.IsCreated) solver.forces.Dispose();

            solver.bodies = new NativeArray<Body3D>(m_Bodies.Count, Allocator.Persistent);
            solver.joints = new NativeArray<Joint3D>(m_Joints.Count, Allocator.Persistent);
            solver.forces = new NativeArray<ForceHandle>(m_Handles.Count, Allocator.Persistent);

            for (int i = 0; i < m_Bodies.Count; ++i) solver.bodies[i] = m_Bodies[i];
            for (int i = 0; i < m_Joints.Count; ++i) solver.joints[i] = m_Joints[i];
            for (int i = 0; i < m_Handles.Count; ++i) solver.forces[i] = m_Handles[i];

            solver.drag = drag;
            solver.postDrag = postDrag;
        }

        /// <summary>
        /// Monitors the stretch of the controllable end and inserts/removes
        /// segments if necessary.
        /// </summary>
        void MonitorAutoLength()
        {
            if (solver == null || !solver.bodies.IsCreated || solver.bodies.Length < 2) return;

            int last = solver.bodies.Length - 1;
            float3 p0 = solver.bodies[last - 1].position;
            float3 p1 = solver.bodies[last].position;
            float dist = math.distance(p0, p1);

            if (dist > m_SegmentLength * stretchLimit)
            {
                InsertSegment();
            }
            else if (dist < m_SegmentLength * 0.5f && solver.bodies.Length > 2)
            {
                RemoveSegment();
            }
        }

        void InsertSegment()
        {
            int index = m_Bodies.Count;
            var prevGO = m_Segments[index - 1];
            var go = new GameObject($"Segment_{index}");
            go.transform.parent = transform;
            go.transform.position = prevGO.transform.position - new Vector3(0, m_SegmentLength, 0);
            var col = go.AddComponent<CapsuleCollider>();
            col.height = m_SegmentLength;
            col.radius = 0.05f;
            col.direction = 1;
            m_Segments.Add(go);

            var body = new Body3D
            {
                position = go.transform.position,
                orientation = quaternion.identity,
                velocity = float3.zero,
                angularVelocity = float3.zero,
                mass = 1f,
                inertiaTensor = float3x3.identity,
                friction = 0f
            };
            m_Bodies.Add(body);

            var joint = new Joint3D
            {
                bodyA = index - 1,
                bodyB = index,
                anchorA = new float3(0, -m_SegmentLength * 0.5f, 0),
                anchorB = new float3(0, m_SegmentLength * 0.5f, 0)
            };
            m_Joints.Add(joint);
            m_Handles.Add(new ForceHandle { Type = ForceType.Joint, Index = m_Joints.Count - 1 });

            segmentCount = m_Bodies.Count;
            m_Dirty = true;
            SyncSolver();
        }

        void RemoveSegment()
        {
            int last = m_Bodies.Count - 1;
            if (last <= 1) return; // keep at least two segments

            var go = m_Segments[last];
            m_Segments.RemoveAt(last);
            Destroy(go);

            m_Bodies.RemoveAt(last);
            m_Joints.RemoveAt(m_Joints.Count - 1);
            m_Handles.RemoveAt(m_Handles.Count - 1);

            segmentCount = m_Bodies.Count;
            m_Dirty = true;
            SyncSolver();
        }
    }
}

