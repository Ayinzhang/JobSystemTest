using UnityEngine;
using System.Collections.Generic;
using System.Threading;

public class OldDynamicBone : MonoBehaviour
{
#if UNITY_5_3_OR_NEWER
    [Tooltip("The roots of the transform hierarchy to apply physics.")]
#endif
    public Transform m_Root = null;
    public List<Transform> m_Roots = null;

#if UNITY_5_3_OR_NEWER
    [Tooltip("Internal physics simulation rate.")]
#endif
    public float m_UpdateRate = 60.0f;

    public enum UpdateMode
    {
        Normal,
        AnimatePhysics,
        UnscaledTime,
        Default
    }
    public UpdateMode m_UpdateMode = UpdateMode.Default;

#if UNITY_5_3_OR_NEWER
    [Tooltip("How much the bones slowed down.")]
#endif
    [Range(0, 1)]
    public float m_Damping = 0.1f;
    public AnimationCurve m_DampingDistrib = null;

#if UNITY_5_3_OR_NEWER
    [Tooltip("How much the force applied to return each bone to original orientation.")]
#endif
    [Range(0, 1)]
    public float m_Elasticity = 0.1f;
    public AnimationCurve m_ElasticityDistrib = null;

#if UNITY_5_3_OR_NEWER
    [Tooltip("How much bone's original orientation are preserved.")]
#endif
    [Range(0, 1)]
    public float m_Stiffness = 0.1f;
    public AnimationCurve m_StiffnessDistrib = null;

#if UNITY_5_3_OR_NEWER
    [Tooltip("How much character's position change is ignored in physics simulation.")]
#endif
    [Range(0, 1)]
    public float m_Inert = 0;
    public AnimationCurve m_InertDistrib = null;

#if UNITY_5_3_OR_NEWER
    [Tooltip("How much the bones slowed down when collide.")]
#endif
    public float m_Friction = 0;
    public AnimationCurve m_FrictionDistrib = null;

#if UNITY_5_3_OR_NEWER
    [Tooltip("Each bone can be a sphere to collide with colliders. Radius describe sphere's size.")]
#endif
    public float m_Radius = 0;
    public AnimationCurve m_RadiusDistrib = null;

#if UNITY_5_3_OR_NEWER
    [Tooltip("If End Length is not zero, an extra bone is generated at the end of transform hierarchy.")]
#endif
    public float m_EndLength = 0;

#if UNITY_5_3_OR_NEWER
    [Tooltip("If End Offset is not zero, an extra bone is generated at the end of transform hierarchy.")]
#endif
    public Vector3 m_EndOffset = Vector3.zero;

#if UNITY_5_3_OR_NEWER
    [Tooltip("The force apply to bones. Partial force apply to character's initial pose is cancelled out.")]
#endif
    public Vector3 m_Gravity = Vector3.zero;

#if UNITY_5_3_OR_NEWER
    [Tooltip("The force apply to bones.")]
#endif
    public Vector3 m_Force = Vector3.zero;

#if UNITY_5_3_OR_NEWER
    [Tooltip("Control how physics blends with existing animation.")]
#endif
    [Range(0, 1)]
    public float m_BlendWeight = 1.0f;

#if UNITY_5_3_OR_NEWER
    [Tooltip("Bones exclude from physics simulation.")]
#endif
    public List<Transform> m_Exclusions = null;

    public enum FreezeAxis
    {
        None, X, Y, Z
    }
#if UNITY_5_3_OR_NEWER
    [Tooltip("Constrain bones to move on specified plane.")]
#endif	
    public FreezeAxis m_FreezeAxis = FreezeAxis.None;

#if UNITY_5_3_OR_NEWER
    [Tooltip("Disable physics simulation automatically if character is far from camera or player.")]
#endif
    public bool m_DistantDisable = false;
    public Transform m_ReferenceObject = null;
    public float m_DistanceToObject = 20;

    Vector3 m_ObjectMove;
    Vector3 m_ObjectPrevPosition;
    float m_ObjectScale;

    float m_Time = 0;
    float m_Weight = 1.0f;
    int m_PreUpdateCount = 0;

    class Particle
    {
        public Transform m_Transform;
        public int m_ParentIndex;
        public int m_ChildCount;
        public float m_Damping;
        public float m_Elasticity;
        public float m_Stiffness;
        public float m_Inert;
        public float m_Friction;
        public float m_Radius;
        public float m_BoneLength;
        public bool m_isCollide;
        public bool m_TransformNotNull;

        public Vector3 m_Position;
        public Vector3 m_PrevPosition;
        public Vector3 m_EndOffset;
        public Vector3 m_InitLocalPosition;
        public Quaternion m_InitLocalRotation;

        // prepare data
        public Vector3 m_TransformPosition;
        public Vector3 m_TransformLocalPosition;
        public Matrix4x4 m_TransformLocalToWorldMatrix;
    }

    class ParticleTree
    {
        public Transform m_Root;
        public Vector3 m_LocalGravity;
        public Matrix4x4 m_RootWorldToLocalMatrix;
        public float m_BoneTotalLength;
        public List<Particle> m_Particles = new List<Particle>();

        // prepare data
        public Vector3 m_RestGravity;
    }

    List<ParticleTree> m_ParticleTrees = new List<ParticleTree>();

    // prepare data
    float m_DeltaTime;

    static int s_UpdateCount;
    static int s_PrepareFrame;

    void Start()
    {
        SetupParticles();
    }

    void LateUpdate()
    {
        InitTransforms();
        Prepare();
        UpdateParticles();
        ApplyParticlesToTransforms();
    }

    void Prepare()
    {
        m_DeltaTime = Time.deltaTime;

        m_ObjectScale = Mathf.Abs(transform.lossyScale.x);
        m_ObjectMove = transform.position - m_ObjectPrevPosition;
        m_ObjectPrevPosition = transform.position;

        for (int i = 0; i < m_ParticleTrees.Count; ++i)
        {
            ParticleTree pt = m_ParticleTrees[i];

            for (int j = 0; j < pt.m_Particles.Count; ++j)
            {
                Particle p = pt.m_Particles[j];
                if (p.m_TransformNotNull)
                {
                    p.m_TransformPosition = p.m_Transform.position;
                    p.m_TransformLocalPosition = p.m_Transform.localPosition;
                    p.m_TransformLocalToWorldMatrix = p.m_Transform.localToWorldMatrix;
                }
            }
        }
    }

    void UpdateParticles()
    {
        float dt = m_DeltaTime;
        int loop = 0;
        
        if (m_UpdateRate > 0)
        {
            float frameTime = 1.0f / m_UpdateRate;
            m_Time += dt;
            loop = 0;

            while (m_Time >= frameTime)
            {
                m_Time -= frameTime;
                if (++loop >= 3)
                {
                    m_Time = 0;
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
            //SkipUpdateParticles();
        }
    }

    public void SetupParticles()
    {
        m_ParticleTrees.Clear();

        if (m_Root != null)
        {
            AppendParticleTree(m_Root);
        }

        for (int i = 0; i < m_ParticleTrees.Count; ++i)
        {
            ParticleTree pt = m_ParticleTrees[i];
            AppendParticles(pt, pt.m_Root, -1, 0);
        }
    }

    void AppendParticleTree(Transform root)
    {
        if (root == null)
            return;

        var pt = new ParticleTree();
        pt.m_Root = root;
        m_ParticleTrees.Add(pt);
    }

    void AppendParticles(ParticleTree pt, Transform b, int parentIndex, float boneLength)
    {
        var p = new Particle();
        p.m_Transform = b;
        p.m_TransformNotNull = b != null;
        p.m_ParentIndex = parentIndex;

        p.m_Position = p.m_PrevPosition = b.position;
        p.m_InitLocalPosition = b.localPosition;
        p.m_InitLocalRotation = b.localRotation;
        p.m_Damping = m_Damping; p.m_Elasticity = m_Elasticity; p.m_Stiffness = m_Stiffness;

        if (parentIndex >= 0)
        {
            boneLength += (pt.m_Particles[parentIndex].m_Transform.position - p.m_Position).magnitude;
            p.m_BoneLength = boneLength;
            pt.m_BoneTotalLength = Mathf.Max(pt.m_BoneTotalLength, boneLength);
            ++pt.m_Particles[parentIndex].m_ChildCount;
        }

        int index = parentIndex + 1;
        pt.m_Particles.Add(p);

        if (b != null)
        {
            for (int i = 0; i < b.childCount; ++i)
            {
                Transform child = b.GetChild(i);
                AppendParticles(pt, child, index, boneLength);
            }
        }
    }

    void InitTransforms()
    {
        for (int i = 0; i < m_ParticleTrees.Count; ++i)
        {
            InitTransforms(m_ParticleTrees[i]);
        }
    }

    void InitTransforms(ParticleTree pt)
    {
        for (int i = 0; i < pt.m_Particles.Count; ++i)
        {
            Particle p = pt.m_Particles[i];
            if (p.m_TransformNotNull)
            {
                p.m_Transform.localPosition = p.m_InitLocalPosition;
                p.m_Transform.localRotation = p.m_InitLocalRotation;
            }
        }
    }

    void UpdateParticles1()
    {
        for (int i = 0; i < m_ParticleTrees.Count; ++i)
        {
            UpdateParticles1(m_ParticleTrees[i]);
        }
    }

    void UpdateParticles1(ParticleTree pt)
    {
        for (int i = 0; i < pt.m_Particles.Count; ++i)
        {
            Particle p = pt.m_Particles[i];
            if (p.m_ParentIndex >= 0)
            {
                // verlet integration
                Vector3 v = p.m_Position - p.m_PrevPosition;
                p.m_PrevPosition = p.m_Position;
                float damping = p.m_Damping;
                p.m_Position += v * (1 - damping);
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
        for (int i = 0; i < m_ParticleTrees.Count; ++i)
        {
            UpdateParticles2(m_ParticleTrees[i]);
        }
    }

    void UpdateParticles2(ParticleTree pt)
    {
        var movePlane = new Plane();

        for (int i = 1; i < pt.m_Particles.Count; ++i)
        {
            Particle p = pt.m_Particles[i];
            Particle p0 = pt.m_Particles[p.m_ParentIndex];

            float restLen;
            restLen = (p0.m_TransformPosition - p.m_TransformPosition).magnitude;

            // keep shape
            float stiffness = Mathf.Lerp(1.0f, p.m_Stiffness, m_Weight);
            if (stiffness > 0 || p.m_Elasticity > 0)
            {
                Unity.Mathematics.float4x4 m = p.m_TransformLocalToWorldMatrix;
                m.c3.xyz = p0.m_Position;
                Matrix4x4 m0 = p0.m_TransformLocalToWorldMatrix;
                m0.SetColumn(3, p0.m_Position);
                m0 = m;
                Vector3 restPos;
                restPos = m0.MultiplyPoint3x4(p.m_TransformLocalPosition);

                Vector3 d = restPos - p.m_Position;
                p.m_Position += d * p.m_Elasticity;

                if (stiffness > 0)
                {
                    d = restPos - p.m_Position;
                    float len = d.magnitude;
                    float maxlen = restLen * (1 - stiffness) * 2;
                    if (len > maxlen)
                    {
                        p.m_Position += d * ((len - maxlen) / len);
                    }
                }
            }

            // keep length
            Vector3 dd = p0.m_Position - p.m_Position;
            float leng = dd.magnitude;
            if (leng > 0)
            {
                p.m_Position += dd * ((leng - restLen) / leng);
            }
        }
    }

    void SkipUpdateParticles()
    {
        for (int i = 0; i < m_ParticleTrees.Count; ++i)
        {
            SkipUpdateParticles(m_ParticleTrees[i]);
        }
    }

    // only update stiffness and keep bone length
    void SkipUpdateParticles(ParticleTree pt)
    {
        for (int i = 0; i < pt.m_Particles.Count; ++i)
        {
            Particle p = pt.m_Particles[i];
            if (p.m_ParentIndex >= 0)
            {
                Particle p0 = pt.m_Particles[p.m_ParentIndex];

                float restLen;
                restLen = (p0.m_TransformPosition - p.m_TransformPosition).magnitude;

                // keep shape
                float stiffness = p.m_Stiffness;
                if (stiffness > 0)
                {
                    Unity.Mathematics.float4x4 m = p.m_TransformLocalToWorldMatrix;
                    m.c3.xyz = p0.m_Position;
                    Matrix4x4 m0 = p0.m_TransformLocalToWorldMatrix;
                    m0.SetColumn(3, p0.m_Position);
                    m0 = m;
                    Vector3 restPos;
                    restPos = m0.MultiplyPoint3x4(p.m_TransformLocalPosition);

                    Vector3 d = restPos - p.m_Position;
                    float len = d.magnitude;
                    float maxlen = restLen * (1 - stiffness) * 2;
                    if (len > maxlen)
                    {
                        p.m_Position += d * ((len - maxlen) / len);
                    }
                }

                // keep length
                Vector3 dd = p0.m_Position - p.m_Position;
                float leng = dd.magnitude;
                if (leng > 0)
                {
                    p.m_Position += dd * ((leng - restLen) / leng);
                }
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
        for (int i = 0; i < m_ParticleTrees.Count; ++i)
        {
            ApplyParticlesToTransforms(m_ParticleTrees[i]);
        }
    }

    void ApplyParticlesToTransforms(ParticleTree pt)
    {
        for (int i = 1; i < pt.m_Particles.Count; ++i)
        {
            Particle p = pt.m_Particles[i];
            Particle p0 = pt.m_Particles[p.m_ParentIndex];

            Vector3 localPos = p.m_Transform.localPosition;
            Vector3 v0 = p0.m_Transform.TransformDirection(localPos);
            Vector3 v1 = p.m_Position - p0.m_Position;
            Quaternion rot = Quaternion.FromToRotation(v0, v1);
            p0.m_Transform.rotation = rot * p0.m_Transform.rotation;

            p.m_Transform.position = p.m_Position;
        }
    }
}
