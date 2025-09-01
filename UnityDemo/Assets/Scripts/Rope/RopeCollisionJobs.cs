using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace AVBD
{
    /// <summary>
    /// Jobs for broadphase and narrowphase collision detection on rope segments.
    /// </summary>
    public static class RopeCollisionJobs
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct Capsule
        {
            public float3 a;
            public float3 b;
            public float radius;
            public int body;
        }

        [BurstCompile]
        struct BuildCapsuleJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Body3D> bodies;
            public NativeArray<Capsule> capsules;
            public float halfLength;
            public float radius;

            public void Execute(int index)
            {
                Body3D b = bodies[index];
                float3 offset = new float3(0, halfLength, 0);
                float3 axis = math.mul(b.orientation, offset);
                capsules[index] = new Capsule
                {
                    a = b.position - axis,
                    b = b.position + axis,
                    radius = radius,
                    body = index
                };
            }
        }

        [BurstCompile]
        struct CollisionJob : IJob
        {
            [ReadOnly] public NativeArray<Capsule> capsules;
            [ReadOnly] public NativeArray<ConvexCollider> colliders;
            public NativeList<ContactManifold3D> contacts;
            public float cellSize;

            public void Execute()
            {
                var grid = new NativeMultiHashMap<int, int>(capsules.Length * 2, Allocator.Temp);
                for (int i = 0; i < capsules.Length; ++i)
                {
                    float3 mid = (capsules[i].a + capsules[i].b) * 0.5f;
                    int3 cell = (int3)math.floor(mid / cellSize);
                    grid.Add(Hash(cell), i);
                }

                for (int i = 0; i < capsules.Length; ++i)
                {
                    Capsule capA = capsules[i];
                    float3 mid = (capA.a + capA.b) * 0.5f;
                    int3 cell = (int3)math.floor(mid / cellSize);
                    for (int dx = -1; dx <= 1; ++dx)
                    for (int dy = -1; dy <= 1; ++dy)
                    for (int dz = -1; dz <= 1; ++dz)
                    {
                        int3 neigh = cell + new int3(dx, dy, dz);
                        int hash = Hash(neigh);
                        NativeMultiHashMapIterator<int> it;
                        int other;
                        if (grid.TryGetFirstValue(hash, out other, out it))
                        {
                            do
                            {
                                if (other <= i) continue;
                                Capsule capB = capsules[other];
                                if (CapsuleCapsule(capA, capB, out ContactManifold3D m))
                                    contacts.Add(m);
                            }
                            while (grid.TryGetNextValue(out other, ref it));
                        }
                    }
                }

                for (int i = 0; i < capsules.Length; ++i)
                {
                    Capsule cap = capsules[i];
                    for (int j = 0; j < colliders.Length; ++j)
                    {
                        if (CapsuleConvex(cap, colliders[j], out ContactManifold3D m))
                            contacts.Add(m);
                    }
                }

                grid.Dispose();
            }
        }

        public static void GenerateContacts(NativeArray<Body3D> bodies, NativeArray<ConvexCollider> colliders, NativeList<ContactManifold3D> contacts, float halfLength, float radius)
        {
            if (!bodies.IsCreated) return;
            var capsules = new NativeArray<Capsule>(bodies.Length, Allocator.TempJob);
            new BuildCapsuleJob { bodies = bodies, capsules = capsules, halfLength = halfLength, radius = radius }
                .Schedule(bodies.Length, 32).Complete();

            var job = new CollisionJob
            {
                capsules = capsules,
                colliders = colliders,
                contacts = contacts,
                cellSize = math.max(radius, halfLength) * 2f
            };
            job.Schedule().Complete();

            capsules.Dispose();
        }

        static bool CapsuleCapsule(Capsule a, Capsule b, out ContactManifold3D manifold)
        {
            manifold = default;
            float3 pA, pB;
            float dist = SegmentSegmentClosest(a.a, a.b, b.a, b.b, out pA, out pB);
            float pen = a.radius + b.radius - dist;
            if (pen <= 0f) return false;

            float3 normal = dist > 1e-6f ? math.normalize(pA - pB) : new float3(0, 1, 0);
            manifold = new ContactManifold3D
            {
                bodyA = a.body,
                bodyB = b.body,
                normal = normal,
                point = (pA + pB) * 0.5f,
                penetration = pen,
                friction = 0.5f
            };
            return true;
        }

        static bool CapsuleConvex(Capsule cap, ConvexCollider col, out ContactManifold3D manifold)
        {
            manifold = default;
            switch (col.type)
            {
                case ConvexType.Sphere:
                    float3 q = ClosestPointSegment(cap.a, cap.b, col.position);
                    float dist = math.distance(q, col.position);
                    float pen = cap.radius + col.size.x - dist;
                    if (pen <= 0f) return false;
                    float3 n = dist > 1e-6f ? math.normalize(q - col.position) : new float3(0, 1, 0);
                    manifold = new ContactManifold3D
                    {
                        bodyA = cap.body,
                        bodyB = -1,
                        normal = n,
                        point = q - n * cap.radius,
                        penetration = pen,
                        friction = col.friction
                    };
                    return true;
            }
            return false;
        }

        static float SegmentSegmentClosest(float3 p1, float3 q1, float3 p2, float3 q2, out float3 c1, out float3 c2)
        {
            float3 d1 = q1 - p1;
            float3 d2 = q2 - p2;
            float3 r = p1 - p2;
            float a = math.dot(d1, d1);
            float e = math.dot(d2, d2);
            float f = math.dot(d2, r);
            float c = math.dot(d1, r);
            float b = math.dot(d1, d2);
            float denom = a * e - b * b;

            float s = (denom != 0f) ? math.clamp((b * f - c * e) / denom, 0f, 1f) : 0f;
            float t = (b * s + f) / e;
            if (t < 0f)
            {
                t = 0f;
                s = math.clamp(-c / a, 0f, 1f);
            }
            else if (t > 1f)
            {
                t = 1f;
                s = math.clamp((b - c) / a, 0f, 1f);
            }

            c1 = p1 + d1 * s;
            c2 = p2 + d2 * t;
            return math.distance(c1, c2);
        }

        static float3 ClosestPointSegment(float3 a, float3 b, float3 p)
        {
            float3 ab = b - a;
            float t = math.dot(p - a, ab) / math.dot(ab, ab);
            t = math.clamp(t, 0f, 1f);
            return a + ab * t;
        }

        static int Hash(int3 cell)
        {
            unchecked
            {
                return cell.x * 73856093 ^ cell.y * 19349663 ^ cell.z * 83492791;
            }
        }
    }
}
