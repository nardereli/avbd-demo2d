using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace AVBD
{
    public enum ConvexType
    {
        Sphere,
        Box
    }

    /// <summary>
    /// Simple convex collider used for rope collision tests.
    /// Sphere uses size.x as radius, Box uses size as half extents.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 16)]
    public struct ConvexCollider
    {
        public ConvexType type;
        public float3 position;
        public quaternion orientation;
        public float3 size;
        public float friction;
    }
}
