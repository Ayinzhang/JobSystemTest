using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;
using System.Collections.Generic;

namespace DynamicBone
{
    public struct Particle
    {
        public int m_ParentIndex;

        public float3 m_Position;
        public float3 m_PrevPosition;
        public float3 m_LocalPosition;
        public float3 m_InitLocalPosition;
        public quaternion m_Rotation;
        public quaternion m_PrevRotation;
        public quaternion m_LocalRotation;
        public quaternion m_InitLocalRotation;
    }

    public class DynamicBone : MonoBehaviour
    {
        public Transform m_Root = null;
        public float m_UpdateRate = 60.0f;
        [Range(0, 1)]
        public float m_Damping = 0.1f;
        [Range(0, 1)]
        public float m_Elasticity = 0.1f;
        [Range(0, 1)]
        public float m_Stiffness = 0.1f;
        [Range(0, 1)]
        public float m_Inert = 0;
        // prepare data
        int m_Size = 0;
        float m_DeltaTime = 0;
        float m_ObjectScale;
        float3 m_ObjectMove;
        float3 m_ObjectPrevPosition;

        NativeArray<Particle> m_Particles;
        Transform[] m_Transforms;
        TransformAccessArray m_TransformArray;

        void Start()
        {
            m_Size = m_Root.gameObject.GetComponentsInChildren<Transform>().Length;
            m_Particles = new NativeArray<Particle>(m_Size, Allocator.Persistent);
            m_Transforms = new Transform[m_Size];

            AppendParticles(m_Root, -1);
        }

        void AppendParticles(Transform b, int parentIndex)
        {
            var p = new Particle();

            p.m_ParentIndex = parentIndex;
            p.m_Position = p.m_PrevPosition = b.position;
            p.m_LocalPosition = p.m_InitLocalPosition = b.localPosition;
            p.m_Rotation = p.m_PrevRotation = b.rotation;
            p.m_LocalRotation = p.m_InitLocalRotation = b.localRotation;

            int index = parentIndex + 1;
            m_Particles[index] = p; m_Transforms[index] = b;

            for (int i = 0; i < b.childCount; ++i)
                AppendParticles(b.GetChild(i), index);
        }

        void Update()
        {
            InitTransforms initTransforms = new InitTransforms();
            initTransforms.ps = m_Particles;
            JobHandle handle = initTransforms.Schedule(m_Size, 4);
            handle.Complete();
        }

        [BurstCompile]
        struct InitTransforms : IJobParallelFor
        {
            public NativeArray<Particle> ps;

            public void Execute(int i)
            {
                var p = ps[i]; 
                p.m_LocalPosition = p.m_InitLocalPosition;
                p.m_LocalRotation = p.m_InitLocalRotation;
            }
        }

        void LateUpdate()
        {
            m_ObjectScale = Mathf.Abs(transform.lossyScale.x);
            m_ObjectMove = (float3)transform.position - m_ObjectPrevPosition;
            m_ObjectPrevPosition = transform.position;

            //Prepare();
            //UpdateParticles();
            //ApplyParticlesToTransforms();
        }

        void OnDestroy()
        {
            m_Particles.Dispose();
            m_TransformArray.Dispose();
        }
    }
}