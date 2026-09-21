using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

public static class NativeArrayExtensions
{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
	private static AtomicSafetyHandle safetyHandle = AtomicSafetyHandle.Create();
#endif

	public static NativeArray<T> AsArray<T>(this Span<T> span) where T : unmanaged
	{
		var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray(span, Allocator.None);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
		NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, safetyHandle);
#endif

		return array;
	}
}
