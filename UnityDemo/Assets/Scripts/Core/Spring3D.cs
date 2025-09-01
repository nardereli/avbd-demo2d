using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace AVBD
{
    /// <summary>
    /// Linear spring constraint between two bodies.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 16)]
    public struct Spring3D : IForce3D
    {
        public int bodyA;
        public int bodyB;
        public float restLength;
        public float stiffness;

        public int Rows => 1;

        public void Initialize() { }

        public unsafe void ComputeConstraint(float3x3* J, float* C)
        {
            // Placeholder implementation
            *J = float3x3.identity;
            *C = 0f;
        }

        public unsafe void ComputeDerivatives(float3x3* H)
        {
            *H = float3x3.zero;
        }
    }
}
