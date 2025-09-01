using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace AVBD
{
    /// <summary>
    /// Adaptive variational balancing dynamics solver for 3D rigid bodies.
    /// </summary>
    public class Solver3D : MonoBehaviour
    {
        [Header("Solver Parameters")]
        public int iterations = 10;
        public float alpha = 0.0f;
        public float beta = 0.0f;
        public float gamma = 0.0f;
        public float3 gravity = new float3(0, -9.81f, 0);
        public float drag = 0.0f;
        public float postDrag = 0.0f;
        public float deltaTime = 1f / 60f;
        [Header("Rope Collision")]
        public float capsuleRadius = 0.05f;
        public float capsuleHalfLength = 0.25f;

        public NativeArray<Body3D> bodies;
        public NativeArray<ForceHandle> forces;
        public NativeArray<Joint3D> joints;
        public NativeArray<Spring3D> springs;
        public NativeArray<Motor3D> motors;
        public NativeList<ContactManifold3D> contacts;
        public NativeList<ConvexCollider> convexColliders;

        void Awake()
        {
            bodies = new NativeArray<Body3D>(0, Allocator.Persistent);
            forces = new NativeArray<ForceHandle>(0, Allocator.Persistent);
            joints = new NativeArray<Joint3D>(0, Allocator.Persistent);
            springs = new NativeArray<Spring3D>(0, Allocator.Persistent);
            motors = new NativeArray<Motor3D>(0, Allocator.Persistent);
            contacts = new NativeList<ContactManifold3D>(Allocator.Persistent);
            convexColliders = new NativeList<ConvexCollider>(Allocator.Persistent);
        }

        void OnDestroy()
        {
            if (bodies.IsCreated) bodies.Dispose();
            if (forces.IsCreated) forces.Dispose();
            if (joints.IsCreated) joints.Dispose();
            if (springs.IsCreated) springs.Dispose();
            if (motors.IsCreated) motors.Dispose();
            if (contacts.IsCreated) contacts.Dispose();
            if (convexColliders.IsCreated) convexColliders.Dispose();
        }

        public void Step()
        {
            contacts.Clear();
            RopeCollisionJobs.GenerateContacts(bodies, convexColliders.AsArray(), contacts, capsuleHalfLength, capsuleRadius);

            // Warm start placeholder

            // Integrate inertial positions
            var integrate = new IntegrateJob
            {
                bodies = bodies,
                gravity = gravity,
                drag = drag,
                dt = deltaTime
            };
            integrate.Schedule(bodies.Length, 64).Complete();

            int handleCount = forces.Length + contacts.Length;
            var allHandles = new NativeArray<ForceHandle>(handleCount, Allocator.Temp);
            for (int i = 0; i < forces.Length; ++i) allHandles[i] = forces[i];
            for (int i = 0; i < contacts.Length; ++i)
                allHandles[forces.Length + i] = new ForceHandle { Type = ForceType.Contact, Index = i };

            // Constraint iterations (primal/dual updates)
            for (int i = 0; i < iterations; ++i)
            {
                var constraintJob = new ConstraintJob
                {
                    handles = allHandles,
                    joints = joints,
                    springs = springs,
                    motors = motors,
                    contacts = contacts.AsDeferredJobArray()
                };
                constraintJob.Schedule(handleCount, 32).Complete();
            }

            allHandles.Dispose();

            // Velocity post-update
            var post = new PostUpdateJob
            {
                bodies = bodies,
                postDrag = postDrag,
                dt = deltaTime
            };
            post.Schedule(bodies.Length, 64).Complete();
        }

        public void RegisterCollider(ConvexCollider collider)
        {
            convexColliders.Add(collider);
        }

        public void ClearColliders()
        {
            convexColliders.Clear();
        }

        [BurstCompile]
        private struct IntegrateJob : IJobParallelFor
        {
            public NativeArray<Body3D> bodies;
            public float3 gravity;
            public float drag;
            public float dt;

            public void Execute(int index)
            {
                Body3D b = bodies[index];
                b.velocity += gravity * dt;
                b.velocity *= (1f - drag * dt);
                b.UpdateTransform(dt);
                bodies[index] = b;
            }
        }

        [BurstCompile]
        private unsafe struct ConstraintJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<ForceHandle> handles;
            public NativeArray<Joint3D> joints;
            public NativeArray<Spring3D> springs;
            public NativeArray<Motor3D> motors;
            [ReadOnly] public NativeArray<ContactManifold3D> contacts;

            public void Execute(int index)
            {
                var handle = handles[index];
                switch (handle.Type)
                {
                    case ForceType.Joint:
                    {
                        var j = joints[handle.Index];
                        j.Initialize();
                        float3x3 J; float C;
                        j.ComputeConstraint(&J, &C);
                        joints[handle.Index] = j;
                        break;
                    }
                    case ForceType.Spring:
                    {
                        var s = springs[handle.Index];
                        s.Initialize();
                        float3x3 J; float C;
                        s.ComputeConstraint(&J, &C);
                        springs[handle.Index] = s;
                        break;
                    }
                    case ForceType.Motor:
                    {
                        var m = motors[handle.Index];
                        m.Initialize();
                        float3x3 J; float C;
                        m.ComputeConstraint(&J, &C);
                        motors[handle.Index] = m;
                        break;
                    }
                    case ForceType.Contact:
                    {
                        var c = contacts[handle.Index];
                        c.Initialize();
                        float3x3 J; float C;
                        c.ComputeConstraint(&J, &C);
                        break;
                    }
                }
            }
        }

        [BurstCompile]
        private struct PostUpdateJob : IJobParallelFor
        {
            public NativeArray<Body3D> bodies;
            public float postDrag;
            public float dt;

            public void Execute(int index)
            {
                Body3D b = bodies[index];
                b.velocity *= (1f - postDrag * dt);
                bodies[index] = b;
            }
        }
    }
}
