using System;
using System.Collections.Generic;
using UnityEngine;

public class BufferSystem : IDisposable
{
	private readonly List<GraphicsBuffer> resources = new();
	private readonly List<int> availableIndices = new();

	public GraphicsBuffer GetBuffer(int index)
	{
		return resources[index];
	}

	public int AllocateBuffer(BufferDescriptor descriptor)
	{
		var resourceIndex = -1;
		GraphicsBuffer resource = null;
		for (var i = 0; i < availableIndices.Count; i++)
		{
			var bufferIndex = availableIndices[i];
			var buffer = resources[bufferIndex];
			if (buffer.stride != descriptor.stride)
				continue;

			if (buffer.count != descriptor.count)
				continue;

			if (buffer.target != descriptor.target)
				continue;

			if (buffer.usageFlags != descriptor.usageFlags)
				continue;

			resource = buffer;
			resourceIndex = bufferIndex;
			availableIndices.RemoveAt(i);
			break;
		}

		if (resource == null)
		{
			resourceIndex = resources.Count;
			resource = new GraphicsBuffer(descriptor.target, descriptor.usageFlags, descriptor.count, descriptor.stride) { name = $"{resourceIndex} {descriptor.stride}x{descriptor.count} {descriptor.target} {descriptor.usageFlags}" };
			resources.Add(resource);
		}

		return resourceIndex;
	}

	public void ReleaseResource(int resourceIndex)
	{
		availableIndices.Add(resourceIndex);
	}

	public void Dispose()
	{
		foreach (var buffer in resources)
			buffer.Release();
	}
}