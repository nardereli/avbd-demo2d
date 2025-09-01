using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace AVBD
{
    /// <summary>
    /// Base interface for constraint forces used by the solver.
    /// </summary>
    public interface IForce3D
    {
        int Rows { get; }
        void Initialize();
        unsafe void ComputeConstraint(float3x3* J, float* C);
        unsafe void ComputeDerivatives(float3x3* H);
    }

    public enum ForceType
    {
        Joint,
        Spring,
        Motor,
        Contact
    }

    /// <summary>
    /// Index into typed force arrays.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 16)]
    public struct ForceHandle
    {
        public ForceType Type;
        public int Index;
    }
}
