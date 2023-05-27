using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;
using System.Collections.Generic;

public class JobManager : MonoBehaviour
{
    float m_DeltaTime;
    int m_UpdateRate = 60;
    NativeList<MyDynamicBone.Particle> m_Particles;
    List<Transform> m_Transforms;
    TransformAccessArray m_TransformArray;
    JobHandle handle;

    private static JobManager instance;

    public static JobManager Instance
    {
        get
        {
            if (!instance) instance = new GameObject("JobManager").AddComponent<JobManager>();
            return instance;
        }
    }

    void OnEnable()
    {
        m_Particles = new NativeList<MyDynamicBone.Particle>(Allocator.Persistent);
        m_Transforms = new List<Transform>();
    }

    void LateUpdate()
    {
        //InitTransforms() + Prepare()
        handle = new Prepare() { ps = m_Particles }.Schedule(m_TransformArray, handle); handle.Complete();

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
            for (int i = 0; i < loop; ++i)
            { handle = new UpdateParticles() { ps = m_Particles }.Schedule(m_TransformArray.capacity, 8); handle.Complete(); }
        else
        { handle = new SkipUpdateParticles() { ps = m_Particles }.Schedule(m_TransformArray.capacity, 8, handle); handle.Complete(); }

        //ApplyParticlesToTransforms();
        handle = new ApplyParticlesToTransforms() { ps = m_Particles }.Schedule(m_TransformArray); handle.Complete();
    }

    [BurstCompile]
    struct Prepare : IJobParallelForTransform
    {
        public NativeArray<MyDynamicBone.Particle> ps;

        public void Execute(int i, TransformAccess t)
        {
             MyDynamicBone.Particle p = ps[i];
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
        public NativeArray<MyDynamicBone.Particle> ps;

        public void Execute(int i)
        {
            MyDynamicBone.Particle p = ps[i];

            if (p.m_ParentIndex == -1)
            {
                p.m_PrevPosition = p.m_Position;
                p.m_Position = p.m_TransformPosition;
                return;
            }

            MyDynamicBone.Particle p0 = ps[i / 8 * 8 + p.m_ParentIndex];
            
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
        public NativeArray<MyDynamicBone.Particle> ps;

        public void Execute(int i)
        {
            MyDynamicBone.Particle p = ps[i];
            if (p.m_ParentIndex >= 0)
            {
                MyDynamicBone.Particle p0 = ps[i / 8 * 8 + p.m_ParentIndex];

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
        public NativeArray<MyDynamicBone.Particle> ps;

        public void Execute(int i, TransformAccess t)
        {
            MyDynamicBone.Particle p = ps[i];
            t.position = p.m_Position;
        }
    }

    void OnDestroy()
    {
        if (m_Particles.IsCreated) m_Particles.Dispose();
        if (m_TransformArray.isCreated) m_TransformArray.Dispose();
    }

    public void AddBone(MyDynamicBone db)
    {
        m_Particles.AddRange(db.m_Particles);
        m_Transforms.AddRange(db.m_Transforms);
        m_TransformArray = new TransformAccessArray(m_Transforms.ToArray());
    }
}
