using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;
using System.Threading;

public class OldDynamicBone : MonoBehaviour
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

    class Particle
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

    List<Particle> m_Particles = new List<Particle>();
    Transform[] m_Transforms;

    void Start()
    {
        m_Transforms = new Transform[m_Root.gameObject.GetComponentsInChildren<Transform>().Length];
        AppendParticles(m_Root, -1);
    }

    void AppendParticles(Transform b, int parentIndex)
    {
        var p = new Particle();

        p.m_ParentIndex = parentIndex;
        p.m_Damping = m_Damping; p.m_Elasticity = m_Elasticity; p.m_Stiffness = m_Stiffness;
        p.m_Position = p.m_PrevPosition = b.position;
        p.m_LocalPosition = p.m_InitLocalPosition = b.localPosition;
        p.m_Rotation = p.m_PrevRotation = b.rotation;
        p.m_LocalRotation = p.m_InitLocalRotation = b.localRotation;

        int index = parentIndex + 1;
        m_Particles.Add(p); m_Transforms[parentIndex + 1] = b;

        for (int i = 0; i < b.childCount; ++i)
            AppendParticles(b.GetChild(i), index);
    }

    void LateUpdate()
    {
        Prepare();
        UpdateParticles();
        ApplyParticlesToTransforms();
    }

    void Prepare()
    {
        for (int i = 0; i < m_Particles.Count; ++i)
        {
            Particle p = m_Particles[i];
            {
                m_Transforms[i].localPosition = p.m_InitLocalPosition;
                m_Transforms[i].localRotation = p.m_InitLocalRotation;

                p.m_TransformPosition = m_Transforms[i].position;
                p.m_TransformLocalPosition = m_Transforms[i].localPosition;
                p.m_TransformLocalToWorldMatrix = m_Transforms[i].localToWorldMatrix;
            }
        }
    }

    void UpdateParticles()
    {
        int loop = 0;

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
                UpdateParticles1();
                UpdateParticles2();
            }
        }
        else
        {
            SkipUpdateParticles();
        }
    }

    void UpdateParticles1()
    {
        for (int i = 0; i < m_Particles.Count; ++i)
        {
            Particle p = m_Particles[i];
            if (p.m_ParentIndex >= 0)
            {
                // verlet integration
                float3 v = p.m_Position - p.m_PrevPosition;
                p.m_PrevPosition = p.m_Position;
                p.m_Position += v * (1 - p.m_Damping);
            }
            else
            {
                p.m_PrevPosition = p.m_Position;
                p.m_Position = p.m_TransformPosition;
            }
        }
    }

    void UpdateParticles2()
    {
        for (int i = 1; i < m_Particles.Count; ++i)
        {
            Particle p = m_Particles[i];
            Particle p0 = m_Particles[p.m_ParentIndex];

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
    }

    // only update stiffness and keep bone length
    void SkipUpdateParticles()
    {
        for (int i = 0; i < m_Particles.Count; ++i)
        {
            Particle p = m_Particles[i];
            if (p.m_ParentIndex >= 0)
            {
                Particle p0 = m_Particles[p.m_ParentIndex];

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
        }
    }

    void ApplyParticlesToTransforms()
    {
        for (int i = 1; i < m_Particles.Count; ++i)
        {
            Particle p = m_Particles[i];
            Particle p0 = m_Particles[p.m_ParentIndex];

            float3 localPos = m_Transforms[i].localPosition;
            float3 v0 = m_Transforms[p.m_ParentIndex].TransformDirection(localPos);
            float3 v1 = p.m_Position - p0.m_Position;
            Quaternion rot = Quaternion.FromToRotation(v0, v1);
            m_Transforms[p.m_ParentIndex].rotation = rot * m_Transforms[p.m_ParentIndex].rotation;

            m_Transforms[i].position = p.m_Position;
        }
    }
}
