using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;

public class SmallDemo : MonoBehaviour
{
    int dataCount = (int)1e7;

    float time;
    int3[] a;
    NativeArray<int3> b;

    void Update()
    {
        a = new int3[dataCount];
        time = Time.realtimeSinceStartup;
        for (int i = 0; i < dataCount; ++i)
            a[i] = new int3(i, i, i);
        Debug.Log("顺序直接赋值" + dataCount + "个用时" + (Time.realtimeSinceStartup - time) + "秒");

        b = new NativeArray<int3>(dataCount, Allocator.TempJob);
        time = Time.realtimeSinceStartup;
        JobHandle orderHandle = new CountInOrder() { data = b }.Schedule(dataCount, 64);
        orderHandle.Complete();
        Debug.Log("并行直接赋值" + dataCount + "个用时" + (Time.realtimeSinceStartup - time) + "秒");
        b.Dispose();
    }

    [BurstCompile]
    struct CountInOrder : IJobParallelFor
    {
        [WriteOnly] public NativeArray<int3> data;

        public void Execute(int i)
        {
            data[i] = new int3(i, i, i);
        }
    }
}
