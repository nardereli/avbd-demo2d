using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace AVBD
{
    /// <summary>
    /// Simple 3D joint constraint connecting two bodies.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 16)]
    public struct Joint3D : IForce3D
    {
        public int bodyA;
        public int bodyB;
        public float3 anchorA;
        public float3 anchorB;

        public int Rows => 6;

        public void Initialize() { }

        public unsafe void ComputeConstraint(float3x3* J, float* C)
        {
            // Placeholder Jacobian and constraint value for demo purposes
            *J = float3x3.identity;
            *C = 0f;
        }

        public unsafe void ComputeDerivatives(float3x3* H)
        {
            *H = float3x3.zero;
        }
    }
}
