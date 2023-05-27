using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;
using System.Collections.Generic;

public class MyDynamicBone : MonoBehaviour
{
    public Transform m_Root = null;
    public float m_UpdateRate = 60.0f;
    [Range(0, 1)]
    public float m_Damping = 0.1f;
    [Range(0, 1)]
    public float m_Elasticity = 0.1f;
    [Range(0, 1)]
    public float m_Stiffness = 0.1f;
    // prepare data
    int m_Size = 8;
    float m_DeltaTime = 0;

    public struct Particle
    {
        public int m_ParentIndex;
        public float m_Damping;
        public float m_Elasticity;
        public float m_Stiffness;
        public float3 m_Position;
        public float3 m_PrevPosition;
        public float3 m_LocalPosition;
        public float3 m_InitLocalPosition;
        public quaternion m_Rotation;
        public quaternion m_PrevRotation;
        public quaternion m_LocalRotation;
        public quaternion m_InitLocalRotation;
        // prepare data
        public float3 m_TransformPosition;
        public float3 m_TransformLocalPosition;
        public float4x4 m_TransformLocalToWorldMatrix;
    }

    public NativeList<Particle> m_Particles;
    public List<Transform> m_Transforms;

    void Start()
    {
        m_Particles = new NativeList<Particle>(m_Size, Allocator.Persistent);
        m_Transforms = new List<Transform>(m_Size);

        AppendParticles(m_Root, -1, 0);
        JobManager.Instance.AddBone(this);
    }

    void AppendParticles(Transform b, int parentIndex, int index)
    {
        var p = new Particle();

        p.m_ParentIndex = parentIndex;
        p.m_Damping = m_Damping; p.m_Elasticity = m_Elasticity; p.m_Stiffness = m_Stiffness;
        p.m_Position = p.m_PrevPosition = b.position;
        p.m_LocalPosition = p.m_InitLocalPosition = b.localPosition;
        p.m_Rotation = p.m_PrevRotation = b.rotation;
        p.m_LocalRotation = p.m_InitLocalRotation = b.localRotation;

        m_Particles.Add(p);m_Transforms.Add(b);

        for (int i = 0; i < b.childCount; ++i)
            AppendParticles(b.GetChild(i), index, index + i + 1);
    }

    void OnDestroy()
    {
        if (m_Particles.IsCreated) m_Particles.Dispose();
    }
}