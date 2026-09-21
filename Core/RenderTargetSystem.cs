using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class RenderTargetSystem : IDisposable
{
	private readonly List<RenderTargetIdentifier> renderTargets = new();
	private readonly List<RenderTargetDescriptor> descriptors = new();
	private readonly List<RenderTexture> renderTextures = new();
	private readonly List<int> availableTargets = new();
	private readonly List<int> renderTargetIndices = new();

	public void Dispose()
	{
		foreach (var target in renderTextures)
		{
			// can be null due to renderdoc loading..
			if(target != null && target.IsCreated())
				target.Release();
		}
	}

	public RenderTargetIdentifier GetTexture(int index)
	{
		return renderTargets[index];
	}

	public RenderTargetDescriptor GetDescriptor(int index)
	{
		return descriptors[index];
	}

	public int ExportTarget(RenderTargetIdentifier id)
	{
		var index = renderTargets.Count;
		renderTargets.Add(id);
		renderTargetIndices.Add(-1);
		return index;
	}

	public int AddDescriptor(RenderTargetDescriptor descriptor)
	{
		var descriptorIndex = descriptors.Count;
		descriptors.Add(descriptor);
		return descriptorIndex;
	}

	public int AllocateTarget(int descriptorIndex, ViewInfo viewInfo, int samples, bool isUav)
	{
		var descriptor = descriptors[descriptorIndex].GetRenderTextureDescriptor(viewInfo, samples, isUav);

		var resourceIndex = -1;
		RenderTexture resource = null;
		for (var i = 0; i < availableTargets.Count; i++)
		{
			var targetIndex = availableTargets[i];
			var target = renderTextures[targetIndex];

			if (target.graphicsFormat != descriptor.graphicsFormat || target.depthStencilFormat != descriptor.depthStencilFormat || target.stencilFormat != descriptor.stencilFormat)
				continue;

			if (target.dimension != descriptor.dimension)
				continue;

			if (target.width != descriptor.width || target.height != descriptor.height || target.volumeDepth != descriptor.volumeDepth)
				continue;

			if (target.enableRandomWrite != descriptor.enableRandomWrite || target.antiAliasing != descriptor.msaaSamples || target.bindTextureMS != descriptor.bindMS)
				continue;

			resource = target;
			resourceIndex = targetIndex;
			availableTargets.RemoveAt(i);
			break;
		}

		if (resource == null)
		{
			resource = new RenderTexture(descriptor) { hideFlags = HideFlags.HideAndDontSave, name = descriptor.ToString() };
			_ = resource.Create();
			resourceIndex = renderTextures.Count;
			renderTextures.Add(resource);
		}

		var index = renderTargets.Count;
		renderTargets.Add(resource);
		renderTargetIndices.Add(resourceIndex);
		return index;
	}

	public void ReleaseResource(int resourceIndex)
	{
		availableTargets.Add(renderTargetIndices[resourceIndex]);
	}

	public void FreeUnreleasedResources()
	{
		renderTargets.Clear();
		descriptors.Clear();
		renderTargetIndices.Clear();
	}
}
