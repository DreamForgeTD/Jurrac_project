using System.Collections.Generic;
using UnityEngine;

namespace DreamForgeTD
{
    public static class BulletForceFieldRegistry
    {
        private static readonly List<MonoBehaviour> RegisteredFields = new List<MonoBehaviour>(8);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            RegisteredFields.Clear();
        }

        public static void Register(MonoBehaviour fieldBehaviour)
        {
            if (fieldBehaviour == null || !(fieldBehaviour is IBulletForceField))
                return;

            if (!RegisteredFields.Contains(fieldBehaviour))
                RegisteredFields.Add(fieldBehaviour);
        }

        public static void Unregister(MonoBehaviour fieldBehaviour)
        {
            if (fieldBehaviour != null)
                RegisteredFields.Remove(fieldBehaviour);
        }

        public static Vector3 GetCombinedAcceleration(BulletMotionSample sample)
        {
            Vector3 acceleration = Vector3.zero;

            for (int i = RegisteredFields.Count - 1; i >= 0; i--)
            {
                MonoBehaviour behaviour = RegisteredFields[i];
                if (behaviour == null)
                {
                    RegisteredFields.RemoveAt(i);
                    continue;
                }

                if (behaviour.isActiveAndEnabled && behaviour is IBulletForceField field)
                    acceleration += field.GetAcceleration(sample);
            }

            return acceleration;
        }
    }
}
