using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;

public class SmallDemo : MonoBehaviour
{
    int dataCount = (int)1e7;

    float time;
    Vector3[] a;
    NativeArray<int3> b;

    void Start()
    {
        a = new Vector3[dataCount];
        time = Time.realtimeSinceStartup;
        for (int i = 0; i < dataCount; ++i)
            a[i] = new Vector3(i, i, i);
        Debug.Log("顺序直接赋值" + dataCount + "个用时" + (Time.realtimeSinceStartup - time) + "秒");

        b = new NativeArray<int3>(dataCount, Allocator.Persistent);
        CountInOrder countInOrder = new CountInOrder();
        countInOrder.data = b;
        JobHandle handle = countInOrder.Schedule(dataCount, 64);
        time = Time.realtimeSinceStartup;
        handle.Complete();
        Debug.Log("并行直接赋值" + dataCount + "个用时" + (Time.realtimeSinceStartup - time) + "秒");
    }

    [BurstCompile]
    struct CountInOrder : IJobParallelFor
    {
        public NativeArray<int3> data;

        public void Execute(int i)
        {
            data[i] = new int3(i, i, i);
        }
    }
}
