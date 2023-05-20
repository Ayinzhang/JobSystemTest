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
    int m_Size;
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

    NativeArray<Particle> m_Particles;
    Transform[] m_Transforms;
    TransformAccessArray m_TransformArray;
    JobHandle handle;

    void Start()
    {
        m_Size = m_Root.gameObject.GetComponentsInChildren<Transform>().Length;
        m_Particles = new NativeArray<Particle>(m_Size, Allocator.Persistent);
        m_Transforms = new Transform[m_Size];

        AppendParticles(m_Root, -1, 0);
        m_TransformArray = new TransformAccessArray(m_Transforms);
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

        m_Particles[index] = p; m_Transforms[index] = b;

        for (int i = 0; i < b.childCount; ++i)
            AppendParticles(b.GetChild(i), index, index + i + 1);
    }

    void LateUpdate()
    {
        //InitTransforms() + Prepare()
        Prepare prepare = new Prepare();
        prepare.ps = m_Particles; 
        handle = prepare.Schedule(m_TransformArray);
        
        //UpdateParticles()
        int loop = 1;
        
        if (m_UpdateRate > 0)
        {
            float frameTime = 1.0f / m_UpdateRate;
            m_DeltaTime += Time.deltaTime;
            loop = 0;

            while (m_DeltaTime >= frameTime)
            {
                m_DeltaTime -= frameTime;
                if (++loop >= 3)
                {
                    m_DeltaTime = 0;
                    break;
                }
            }
        }

        if (loop > 0)
        {
            for (int i = 0; i < loop; ++i)
            {
                UpdateParticles updateParticles = new UpdateParticles();
                updateParticles.ps = m_Particles;
                handle = updateParticles.Schedule(m_Size, 8, handle);
            }
        }
        else
        {
            SkipUpdateParticles skipUpdateParticles = new SkipUpdateParticles();
            skipUpdateParticles.ps = m_Particles;
            handle = skipUpdateParticles.Schedule(m_Size, 8, handle);
        }

        //ApplyParticlesToTransforms();
        ApplyParticlesToTransforms applyParticlesToTransforms = new ApplyParticlesToTransforms();
        applyParticlesToTransforms.ps = m_Particles;
        handle = applyParticlesToTransforms.Schedule(m_TransformArray, handle);
        handle.Complete();
    }

    [BurstCompile]
    struct Prepare : IJobParallelForTransform
    {
        public NativeArray<Particle> ps;

        public void Execute(int i, TransformAccess t)
        {
            Particle p = ps[i];
            t.localPosition = p.m_InitLocalPosition;
            t.localRotation = p.m_InitLocalRotation;

            p.m_TransformPosition = t.position;
            p.m_TransformLocalPosition = t.localPosition;
            p.m_TransformLocalToWorldMatrix = t.localToWorldMatrix;
            ps[i] = p;
        }
    }

    [BurstCompile]
    struct UpdateParticles : IJobParallelFor
    {
        public NativeArray<Particle> ps;

        public void Execute(int i)
        {
            Particle p = ps[i];

            if (p.m_ParentIndex == -1)
            {
                p.m_PrevPosition = p.m_Position;
                p.m_Position = p.m_TransformPosition;
                return; 
            }

            Particle p0 = ps[p.m_ParentIndex];

            // verlet integration
            float3 v = p.m_Position - p.m_PrevPosition;
            p.m_PrevPosition = p.m_Position;
            p.m_Position += v * (1 - p.m_Damping);

            float restLen;
            restLen = math.length(p0.m_TransformPosition - p.m_TransformPosition);

            // keep shape
            float4x4 m0 = p.m_TransformLocalToWorldMatrix;
            m0.c3.xyz = p0.m_Position;
            float3 restPos = math.mul(m0, new float4(p.m_TransformLocalPosition, 1)).xyz;

            float3 d = restPos - p.m_Position;
            p.m_Position += d * p.m_Elasticity;

            float len = math.length(d);
            float maxlen = restLen * (1 - p.m_Stiffness) * 2;
            if (len > maxlen)
                p.m_Position += d * ((len - maxlen) / len);

            // keep length
            float3 dd = p0.m_Position - p.m_Position;
            float leng = math.length(dd);
            if (leng > 0)
                p.m_Position += dd * ((leng - restLen) / leng);

            ps[i] = p;
        }
    }

    [BurstCompile]
    struct SkipUpdateParticles : IJobParallelFor
    {
        public NativeArray<Particle> ps;
        
        public void Execute(int i)
        {
            Particle p = ps[i];
            if (p.m_ParentIndex >= 0)
            {
                Particle p0 = ps[p.m_ParentIndex];

                float restLen;
                restLen = math.length(p0.m_TransformPosition - p.m_TransformPosition);

                // keep shape
                float4x4 m0 = p.m_TransformLocalToWorldMatrix;
                m0.c3.xyz = p0.m_Position;
                float3 restPos;
                restPos = math.mul(m0, new float4(p.m_TransformLocalPosition, 1)).xyz;

                float3 d = restPos - p.m_Position;
                p.m_Position += d * p.m_Elasticity;

                d = restPos - p.m_Position;
                float len = math.length(d);
                float maxlen = restLen * (1 - p.m_Stiffness) * 2;
                if (len > maxlen)
                    p.m_Position += d * ((len - maxlen) / len);

                // keep length
                float3 dd = p0.m_Position - p.m_Position;
                float leng = math.length(dd);
                if (leng > 0)
                    p.m_Position += dd * ((leng - restLen) / leng);
            }
            else
            {
                p.m_PrevPosition = p.m_Position;
                p.m_Position = p.m_TransformPosition;
            }
            ps[i] = p;
        }
    }

    [BurstCompile]
    struct ApplyParticlesToTransforms : IJobParallelForTransform
    {
        public NativeArray<Particle> ps;

        public void Execute(int i, TransformAccess t)
        {
            Particle p = ps[i];
            t.position = p.m_Position;
        }
    }

    void OnDestroy()
    {
        if (m_Particles.IsCreated) m_Particles.Dispose();
        if (m_TransformArray.isCreated) m_TransformArray.Dispose();
    }
}