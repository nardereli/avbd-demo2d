using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace AVBD
{
    /// <summary>
    /// Rigid body representation used by the 3D solver.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 16)]
    public struct Body3D
    {
        public float3 position;
        public quaternion orientation;
        public float3 velocity;
        public float3 angularVelocity;
        public float mass;
        public float3x3 inertiaTensor;
        public float friction;

        /// <summary>
        /// Applies an impulse to the body, modifying linear and angular velocity.
        /// </summary>
        public void ApplyImpulse(float3 linearImpulse, float3 angularImpulse)
        {
            velocity += linearImpulse / math.max(mass, 1e-6f);
            angularVelocity += math.mul(inertiaTensor, angularImpulse);
        }

        /// <summary>
        /// Integrates velocity to update the transform.
        /// </summary>
        public void UpdateTransform(float dt)
        {
            position += velocity * dt;

            float angle = math.length(angularVelocity);
            if (angle > 1e-6f)
            {
                float3 axis = angularVelocity / angle;
                quaternion dq = quaternion.AxisAngle(axis, angle * dt);
                orientation = math.normalize(math.mul(dq, orientation));
            }
        }
    }
}
