using System;
using System.Collections.Generic;
using UnityEngine;

public class BufferSystem : IDisposable
{
	private readonly List<BufferDescriptor> descriptors = new();
	private readonly List<GraphicsBuffer> resources = new();
	private readonly List<int> availableIndices = new();

	public GraphicsBuffer GetBuffer(int index)
	{
		return resources[index];
	}

	public int AddDescriptor(BufferDescriptor descriptor)
	{
		var descriptorIndex = descriptors.Count;
		descriptors.Add(descriptor);
		return descriptorIndex;
	}

	public int AllocateBuffer(int descriptorIndex)
	{
		var descriptor = descriptors[descriptorIndex];
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
			resource = new GraphicsBuffer(descriptor.target, descriptor.usageFlags, descriptor.count, descriptor.stride);
			resourceIndex = resources.Count;
			resources.Add(resource);
		}

		return resourceIndex;
	}

	public void ReleaseResource(int resourceIndex)
	{
		availableIndices.Add(resourceIndex);
	}

	public void FreeUnreleasedResources()
	{
		descriptors.Clear();
	}

	public void Dispose()
	{
		foreach (var buffer in resources)
			buffer.Release();
	}
}