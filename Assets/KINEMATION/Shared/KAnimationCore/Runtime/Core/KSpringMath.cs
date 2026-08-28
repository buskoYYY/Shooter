// Designed by KINEMATION, 2023

using UnityEngine;

namespace KINEMATION.Shared.KAnimationCore.Runtime.Core
{
    public struct FloatSpringState
    {
        public float Velocity;
        public float Error;
        
        public void Reset()
        {
            Error = Velocity = 0f;
        }
    }
    
    public class KSpringMath
    {
        public static float FloatSpringInterp(float current, float target, float speed, float criticalDamping,
            float stiffness, float scale, ref FloatSpringState state)
        {
            return FloatSpringInterp(current, target, speed, criticalDamping, stiffness, scale, ref state,
                Time.deltaTime);
        }

        public static float FloatSpringInterp(float current, float target, float speed, float criticalDamping,
            float stiffness, float scale, ref FloatSpringState state, float deltaTime)
        {
            float interpSpeed = Mathf.Min(deltaTime * speed, 1f);

            if (!Mathf.Approximately(interpSpeed, 0f))
            {
                float damping = 2 * Mathf.Sqrt(stiffness) * criticalDamping;
                float error = target * scale - current;
                float errorDeriv = error - state.Error;
                state.Velocity += error * stiffness * interpSpeed + errorDeriv * damping;
                state.Error = error;

                float value = current + state.Velocity * interpSpeed;
                return value;
            }

            return current;
        }
    }
}