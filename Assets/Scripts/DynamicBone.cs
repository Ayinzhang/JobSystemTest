using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using System.Collections.Generic;

public class DynamicBone : MonoBehaviour
{
    public List<Transform> m_Roots = null;
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
    float m_DeltaTime = 0;
    
    struct Particle
    {
        public Transform m_Transform;
        public int m_ParentIndex;
        public int m_ChildCount;

        public float3 m_Position;
        public float3 m_PrevPosition;
        public float3 m_InitLocalPosition;
        public Quaternion m_InitLocalRotation;
        
        // prepare data
        public float3 m_TransformPosition;
        public float3 m_TransformLocalPosition;
        public float4x4 m_TransformLocalToWorldMatrix;
    }

    struct ParticleTree
    {
        public Transform m_Root;
        public float3 m_LocalGravity;
        public float4x4 m_RootWorldToLocalMatrix;
        public List<Particle> m_Particles;
    }

    List<ParticleTree> m_ParticleTrees;

    void Start()
    {
        if (m_Roots != null)
            for (int i = 0; i < m_Roots.Count; ++i)
                AppendParticleTree(m_Roots[i]);

        for (int i = 0; i < m_ParticleTrees.Count; ++i)
            AppendParticles(m_ParticleTrees[i], m_ParticleTrees[i].m_Root, -1, 0);
    }

    void AppendParticleTree(Transform root)
    {
        var pt = new ParticleTree();
        pt.m_Root = root;
        pt.m_RootWorldToLocalMatrix = root.worldToLocalMatrix;
        m_ParticleTrees.Add(pt);
    }

    void AppendParticles(ParticleTree pt, Transform b, int parentIndex, float boneLength)
    {
        var p = new Particle();

        p.m_Transform = b; p.m_ParentIndex = parentIndex;
        p.m_Position = p.m_PrevPosition = b.position;
        p.m_InitLocalPosition = b.localPosition;
        p.m_InitLocalRotation = b.localRotation;

        int index = pt.m_Particles.Count;
        pt.m_Particles.Add(p);
        
        for (int i = 0; i < b.childCount; ++i)
            AppendParticles(pt, b.GetChild(i), index, boneLength);   
    }
}