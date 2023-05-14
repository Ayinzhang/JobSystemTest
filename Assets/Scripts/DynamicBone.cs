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
        // prepare data
        int m_Size = 0;
        float m_DeltaTime = 0;
        float m_ObjectScale;
        static float3 m_ObjectMove;
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
            p.m_Damping = m_Damping;p.m_Elasticity = m_Elasticity;p.m_Stiffness = m_Stiffness;
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
            JobHandle handle = initTransforms.Schedule(m_Size, 16);
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
            //Prepare()
            m_ObjectScale = Mathf.Abs(transform.lossyScale.x);
            m_ObjectMove = (float3)transform.position - m_ObjectPrevPosition;
            m_ObjectPrevPosition = transform.position;

            Prepare prepare = new Prepare();
            prepare.ps = m_Particles; m_TransformArray = new TransformAccessArray(m_Transforms);
            JobHandle handle = prepare.Schedule(m_TransformArray);
            handle.Complete();

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
                    UpdateParticles1 updateParticles1 = new UpdateParticles1();
                    updateParticles1.ps = m_Particles;
                    handle = updateParticles1.Schedule(m_Size, 16);
                    handle.Complete();

                    UpdateParticles2 updateParticles2 = new UpdateParticles2();
                    updateParticles2.ps = m_Particles;
                    handle = updateParticles2.Schedule(m_Size, 16);
                    handle.Complete();
                }
            }
            else
            {
                SkipUpdateParticles skipUpdateParticles = new SkipUpdateParticles();
                skipUpdateParticles.ps = m_Particles;
                handle = skipUpdateParticles.Schedule(m_Size, 16);
                handle.Complete();
            }

            //ApplyParticlesToTransforms();

        }

        [BurstCompile]
        struct Prepare : IJobParallelForTransform
        {
            public NativeArray<Particle> ps;

            public void Execute(int i, TransformAccess t)
            {
                Particle p = ps[i];
                p.m_TransformPosition = t.position;
                p.m_TransformLocalPosition = t.localPosition;
                p.m_TransformLocalToWorldMatrix = t.localToWorldMatrix;
            }
        }

        [BurstCompile]
        struct UpdateParticles1 : IJobParallelFor
        {
            public NativeArray<Particle> ps;

            public void Execute(int i)
            {
                Particle p = ps[i];
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

        [BurstCompile]
        struct UpdateParticles2 : IJobParallelFor
        {
            public NativeArray<Particle> ps;

            public void Execute(int i)
            {
                if (i == 0) return;

                Particle p = ps[i];
                Particle p0 = ps[p.m_ParentIndex];

                float restLen;
                restLen = math.length(p0.m_TransformPosition - p.m_TransformPosition);

                // keep shape
                float4x4 m0 = p0.m_TransformLocalToWorldMatrix;
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

        [BurstCompile]
        struct SkipUpdateParticles : IJobParallelFor
        {
            public NativeArray<Particle> ps;

            public void Execute(int i)
            {
                Particle p = ps[i];
                if (p.m_ParentIndex >= 0)
                {
                    p.m_PrevPosition += m_ObjectMove;
                    p.m_Position += m_ObjectMove;

                    Particle p0 = ps[p.m_ParentIndex];

                    float restLen;
                    restLen = math.length(p0.m_TransformPosition - p.m_TransformPosition);

                    // keep shape
                    float4x4 m0 = p0.m_TransformLocalToWorldMatrix;
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
            Vector3 ax = Vector3.right;
            Vector3 ay = Vector3.up;
            Vector3 az = Vector3.forward;
            bool nx = false, ny = false, nz = false;

#if !UNITY_5_4_OR_NEWER
        // detect negative scale
        Vector3 lossyScale = transform.lossyScale;
        if (lossyScale.x < 0 || lossyScale.y < 0 || lossyScale.z < 0)
        {
            Transform mirrorObject = transform;
            do
            {
                Vector3 ls = mirrorObject.localScale;
                nx = ls.x < 0;
                if (nx)
                    ax = mirrorObject.right;
                ny = ls.y < 0;
                if (ny)
                    ay = mirrorObject.up;
                nz = ls.z < 0;
                if (nz)
                    az = mirrorObject.forward;
                if (nx || ny || nz)
                    break;

                mirrorObject = mirrorObject.parent;
            }
            while (mirrorObject != null);
        }
#endif

            for (int i = 0; i < m_ParticleTrees.Count; ++i)
            {
                ApplyParticlesToTransforms(m_ParticleTrees[i], ax, ay, az, nx, ny, nz);
            }
        }

        void ApplyParticlesToTransforms(Vector3 ax, Vector3 ay, Vector3 az, bool nx, bool ny, bool nz)
        {
            for (int i = 1; i < m_Particles.Count; ++i)
            {
                Particle p = m_Particles[i];
                Particle p0 = m_Particles[p.m_ParentIndex];

                if (p0.m_ChildCount <= 1)       // do not modify bone orientation if has more then one child
                {
                    Vector3 localPos;
                    localPos = p.m_Transform.localPosition;
                    Vector3 v0 = p0.m_Transform.TransformDirection(localPos);
                    Vector3 v1 = p.m_Position - p0.m_Position;
                    Quaternion rot = Quaternion.FromToRotation(v0, v1);
                    p0.m_Transform.rotation = rot * p0.m_Transform.rotation;
                }

                if (p.m_TransformNotNull)
                {
                    p.m_Transform.position = p.m_Position;
                }
            }
        }

        void OnDestroy()
        {
            m_Particles.Dispose();
            m_TransformArray.Dispose();
        }
    }
}