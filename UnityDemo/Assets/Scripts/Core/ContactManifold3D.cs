using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;

namespace AVBD
{
    /// <summary>
    /// Contact manifold generated during broadphase collision detection.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 16)]
    public struct ContactManifold3D : IForce3D
    {
        public int bodyA;
        public int bodyB;
        public float3 normal;
        public float3 point;
        public float penetration;
        public float friction;

        public int Rows => 3;

        public void Initialize() { }

        public unsafe void ComputeConstraint(float3x3* J, float* C)
        {
            // Jacobian rows: normal first then two orthogonal tangents for friction
            float3 n = math.normalize(normal);
            // Build an orthonormal basis from the contact normal
            float3 t1 = math.normalize(math.any(math.abs(n) > new float3(0.707f))
                ? math.cross(n, new float3(0, 1, 0))
                : math.cross(n, new float3(1, 0, 0)));
            float3 t2 = math.cross(n, t1);

            *J = new float3x3(n, t1, t2);
            // C is penetration depth for normal row only. Tangential rows use friction
            *C = math.max(penetration, 0f);
        }

        public unsafe void ComputeDerivatives(float3x3* H)
        {
            *H = float3x3.zero;
        }
    }
}
