namespace DevelopersHub.ProceduralTilemapGenerator2D.Extensions
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using Unity.Collections;
    using Unity.Jobs;
    
    public static class NativeArrayExtensions
    {
        
        public static void Fill<T>(this NativeArray<T> array, T value) where T : struct
        {
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = value;
            }
        }
        
        // Extension method to fill a NativeArray with a specific value using the Job System
        public static void FillParallel<T>(this NativeArray<T> array, T value, int batchSize = 64) where T : struct
        {
            // Create and schedule the job
            var job = new FillJob<T> { Array = array, Value = value };
            job.Schedule(array.Length, batchSize).Complete();
        }

        // Job to fill the array in parallel
        private struct FillJob<T> : IJobParallelFor where T : struct
        {
            public NativeArray<T> Array;
            public T Value;
            public void Execute(int index)
            {
                Array[index] = Value;
            }
        }
        
    }
}