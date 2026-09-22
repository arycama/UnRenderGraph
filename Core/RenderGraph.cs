using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Unmath;
using static Unmath.Math;

public class RenderGraph : IDisposable
{
	// Renderpasses
	private readonly List<IRenderPass> renderPasses = new();
	private readonly NativeRenderPassSystem nativeRenderPassSystem = new();
	private readonly ResourceMap resourceMap = new();
	private readonly PassBuilder passBuilder;
	private readonly ResizableArray<int> firstWriteIndices = new();

	// Resources
	private readonly List<ViewInfo> viewInfos = new();
	private readonly ResizableArray<ResourceHandle> handles = new();
	private readonly ResizableArray<ResourceInfo> resourceInfo = new();
	private readonly FreeList<ResourceInfo> persistentResourceInfo = new();
	private readonly List<int> persistentResourcesToFree = new();
	private readonly List<BufferDescriptor> bufferDescriptors = new();
	private readonly BufferSystem bufferSystem = new();
	private readonly RenderTargetSystem renderTargetSystem = new();
	private readonly List<RenderTargetDescriptor> renderTargetDescriptors = new();
	private readonly List<RayTracingAccelerationStructure> rayTracingAccelerationStructures = new();
	private readonly List<Texture> textures = new();
	private readonly ConstantBufferBuilder constantBufferBuilder;
	private readonly ResizableArray<byte> constantBufferData = new();
	private readonly List<(BufferHandle handle, Range range)> constantBufferRanges = new();

	public int FrameIndex { get; private set; }

	public RenderGraph()
	{
		passBuilder = new(this);
		constantBufferBuilder = new(this);
	}

	public void Dispose()
	{
		bufferSystem.Dispose();
		renderTargetSystem.Dispose();
	}

	public void BeginCamera()
	{
		resourceMap.Clear();
	}

	public RenderTargetHandle GetTexture(RenderTargetDescriptor descriptor, int propertyId, bool isPersistent = false)
	{
		var descriptorIndex = renderTargetDescriptors.Count;
		renderTargetDescriptors.Add(descriptor);
		var resourceIndex = AddResource(descriptorIndex, propertyId, ResourceHandleType.RenderTarget, isPersistent);
		return new(resourceIndex, isPersistent);
	}

	public BufferHandle GetBuffer(BufferDescriptor descriptor, int propertyId, bool isPersistent = false)
	{
		var descriptorIndex = bufferDescriptors.Count;
		bufferDescriptors.Add(descriptor);
		var resourceIndex = AddResource(descriptorIndex, propertyId, ResourceHandleType.Buffer, isPersistent);
		return new(resourceIndex, isPersistent);
	}

	private ref ResourceInfo GetResource(ResourceHandle handle)
	{
		return ref handle.isPersistent ? ref persistentResourceInfo[handle] : ref resourceInfo[handle];
	}

	public void ReleasePersistentResource(ResourceHandle handle)
	{
		ref var resourceInfo = ref persistentResourceInfo[handle];
		resourceInfo.isPersistent = false;
		persistentResourcesToFree.Add(handle.index);
	}

	public RenderTargetIdentifier GetTextureResource(RenderTargetHandle handle)
	{
		var target = GetResource(handle);
		return renderTargetSystem.GetTexture(target.resourceIndex, target.isExternal);
	}

	public GraphicsBuffer GetBufferResource(BufferHandle handle)
	{
		var target = GetResource(handle);
		return bufferSystem.GetBuffer(target.resourceIndex);
	}

	public void SetResource<T>(T resource) where T : IRenderResource
	{
		resourceMap.SetResource(resource);
	}

	public T GetResource<T>()
	{
		return resourceMap.GetResource<T>();
	}

	public bool TryGetResource(Type type, out IRenderResource resource) => resourceMap.TryGetResource(type, out resource);

	public bool TryGetResource<T>(out T resource)
	{
		var hasResource = TryGetResource(typeof(T), out var temp);
		resource = hasResource ? (T)temp : default;
		return hasResource;
	}

	public PassBuilder AddRenderPass(string name)
	{
		passBuilder.Name = name;
		passBuilder.Index = renderPasses.Count;
		return passBuilder;
	}

	public ViewInfo GetViewInfo(ViewHandle handle) => viewInfos[handle.index];

	public void SetRenderPass(PassBuilder builder)
	{
		var inputStart = handles.Count;
		foreach (var resource in builder.Resources)
		{
			SetResourceReadIndex(resource, builder.Index);
			handles.Add(resource);
		}

		var resourceRange = inputStart..handles.Count;
		if (builder.DepthStencil.index != -1)
		{
			// Depth stencil is counted as write and read since it is also 'read' for depth tests
			SetResourceWriteIndex(builder.DepthStencil, builder.Index, builder.DepthSlice);
			SetResourceReadIndex(builder.DepthStencil, builder.Index);
		}

		foreach (var output in builder.Outputs)
		{
			// Outputs can be 'read' in the case of blending etc.
			SetResourceWriteIndex(output, builder.Index, builder.DepthSlice);
			SetResourceReadIndex(output, builder.Index);
		}

		foreach (var input in builder.Inputs)
			SetResourceReadIndex(input, builder.Index);

		// UAV resources are handled specially
		var uavStart = handles.Count;
		foreach (var handle in builder.UavOutputs)
		{
			handles.Add(handle);
			SetResourceWriteIndex(handle, builder.Index, 0);
			SetResourceReadIndex(handle, builder.Index);
		}

		var uavResourceRange = uavStart..handles.Count;

		var (nativePassIndex, isNewSubPass) = nativeRenderPassSystem.AddRenderPass(builder);

		var renderPass = builder.RenderPass;
		renderPass.ResourceRange = resourceRange;
		renderPass.UavResourceRange = uavResourceRange;
		renderPass.IsNewSubPass = isNewSubPass;
		renderPass.ViewHandle = builder.ViewHandle;
		renderPass.Name = builder.Name;
		renderPass.NativePassIndex = nativePassIndex;
		renderPass.Keywords = new(builder.Keywords);

		renderPasses.Add(renderPass);
	}

	public bool IsResourceWritten(ResourceHandle handle)
	{
		var resourceInfo = GetResource(handle);
		return resourceInfo.lastWriteIndex != -1;
	}

	private void SetResourceReadIndex(ResourceHandle handle, int index)
	{
		ref var target = ref GetResource(handle);
		target.lastReadIndex = index;
	}

	private void SetResourceWriteIndex(ResourceHandle handle, int index, int subResourceIndex)
	{
		ref var target = ref GetResource(handle);
		target.lastWriteIndex = index;

		var range = target.firstWriteIndexRange;
		var start = range.Start.Value;
		var end = range.End.Value;

		if (subResourceIndex != -1)
		{
			start += subResourceIndex;
			end = start + 1;
		}

		for (var i = start; i < end; i++)
		{
			ref var value = ref firstWriteIndices[i];
			if (value == -1)
				value = index;
		}
	}

	private int AddResource(int descriptorIndex, int propertyId, ResourceHandleType handleType, bool isPersistent = false)
	{
		// Store a range for each resource based on the number of slices it has
		var count = 1;
		if (handleType == ResourceHandleType.RenderTarget)
		{
			// Add the range for all slices
			var descriptor = renderTargetDescriptors[descriptorIndex];
			var viewInfo = GetViewInfo(descriptor.viewHandle);
			count = viewInfo.volumeDepth;
		}

		Span<int> values = stackalloc int[count];
		values.Fill(-1);
		var firstWriteIndexRange = firstWriteIndices.AddRange(values);
		var info = new ResourceInfo(descriptorIndex, propertyId, firstWriteIndexRange, handleType, isPersistent);

		int index;
		if (isPersistent)
		{
			index = persistentResourceInfo.Add(info);
		}
		else
		{
			index = resourceInfo.Count;
			resourceInfo.Add(info);
		}

		return index;
	}

	public void ExportTexture(RenderTargetHandle handle, RenderTargetIdentifier id)
	{
		var resourceIndex = renderTargetSystem.ExportTarget(id);
		ref var target = ref GetResource(handle);
		target.resourceIndex = resourceIndex;
		target.isExternal = true;
	}

	public TextureHandle GetTextureHandle(Texture texture, int propertyId)
	{
		AddResource(-1, propertyId, ResourceHandleType.Texture);
		var handle = new TextureHandle(resourceInfo.Count - 1);
		var resourceIndex = textures.Count;
		ref var target = ref GetResource(handle);
		target.resourceIndex = resourceIndex;
		target.isExternal = true;
		textures.Add(texture);
		return handle;
	}

	public RayTracingAccelerationStructureHandle GetRtasHandle(RayTracingAccelerationStructure structure, int propertyId)
	{
		AddResource(-1, propertyId, ResourceHandleType.RayTracingAccelerationStructure);
		var handle = new RayTracingAccelerationStructureHandle(resourceInfo.Count - 1);
		var resourceIndex = rayTracingAccelerationStructures.Count;
		ref var target = ref GetResource(handle);
		target.resourceIndex = resourceIndex;
		target.isExternal = true;
		rayTracingAccelerationStructures.Add(structure);
		return handle;
	}

	public ViewHandle AddViewInfo(Int2 size, int samples = 1, int volumeDepth = 1)
	{
		var index = viewInfos.Count;
		viewInfos.Add(new(size, samples, volumeDepth));
		return new(index);
	}

	private void AllocateTexture(ResourceHandle handle, ViewHandle viewHandle, RenderTargetDescriptor descriptor, bool isUav = false, int samples = 1)
	{
		ref var target = ref GetResource(handle);
		target.resourceIndex = renderTargetSystem.AllocateTarget(descriptor, viewInfos[viewHandle.index], samples, isUav);
	}

	public void AddConstantBufferData(ReadOnlySpan<byte> data, BufferHandle handle)
	{
		var range = constantBufferData.AddRange(data);
		constantBufferRanges.Add((handle, range));
		SetResourceWriteIndex(handle, 0, 0);
	}

	public ConstantBufferBuilder AddConstantBuffer(string name, out BufferHandle handle, bool isPersistent = false)
	{
		// Constant buffer gets built inside a using statement and then the actual descriptor is created after. So
		// a handle that indicates the next available index is returned so that it will point to the correct data once the builder has completed
		handle = new(resourceInfo.Count, isPersistent);
		constantBufferBuilder.PropertyName = name;
		return constantBufferBuilder;
	}

	private void FillConstantBuffers(CommandBuffer command)
	{
		foreach (var (handle, range) in constantBufferRanges)
		{
			// Don't allocate+fill buffers that are never read
			ref var target = ref GetResource(handle);
			if (target.lastReadIndex == -1)
				continue;

			var descriptor = bufferDescriptors[target.descriptorIndex];
			target.resourceIndex = bufferSystem.AllocateBuffer(descriptor);

			var data = constantBufferData.AsSpan(range);
			var buffer = GetBufferResource(handle);
			command.SetBufferData(buffer, data.AsArray());
		}
	}

	private void BeginNativeRenderPass(CommandBuffer command, int renderPassIndex, IRenderPass renderPass)
	{
		var nativePassDesc = nativeRenderPassSystem.GetDescriptor(renderPass.NativePassIndex);
		var viewHandle = renderPass.ViewHandle;
		var viewInfo = viewInfos[renderPass.ViewHandle.index];

		// Resolve the attachments to their final values
		var attachments = nativeRenderPassSystem.GetAttachments(nativePassDesc.attachments);
		var attachmendIndices = new FixedBuffer<AttachmentDescriptor>(stackalloc AttachmentDescriptor[8]);
		foreach (var texture in attachments)
		{
			ref var target = ref GetResource(texture);
			var descriptor = renderTargetDescriptors[target.descriptorIndex];
			var attachmentDesc = new AttachmentDescriptor
			{
				graphicsFormat = descriptor.format,
			};

			// Load the target if it has been written to before this renderpass, otherwise clear it if required
			var firstWriteIndex = firstWriteIndices[target.firstWriteIndexRange.Start.Value + Max(0, nativePassDesc.depthSlice)];
			var isFirstWrite = firstWriteIndex >= renderPassIndex;
			if (isFirstWrite)
			{
				if (descriptor.clear)
				{
					attachmentDesc.loadAction = RenderBufferLoadAction.Clear;
					attachmentDesc.clearColor = descriptor.clearColor;
					attachmentDesc.clearDepth = descriptor.clearDepth;
					attachmentDesc.clearStencil = descriptor.clearStencil;
				}
				else
					attachmentDesc.loadAction = RenderBufferLoadAction.DontCare;
			}
			else
			{
				// If this target has been written previously, it must be loaded
				attachmentDesc.loadStoreTarget = new(renderTargetSystem.GetTexture(target.resourceIndex, target.isExternal), 0, CubemapFace.Unknown, Max(0, nativePassDesc.depthSlice));
			}

			var isColor = descriptor.format switch
			{
				GraphicsFormat.D16_UNorm or GraphicsFormat.D24_UNorm or GraphicsFormat.D32_SFloat or GraphicsFormat.D16_UNorm_S8_UInt or GraphicsFormat.D24_UNorm_S8_UInt or GraphicsFormat.D32_SFloat_S8_UInt or GraphicsFormat.S8_UInt => false,
				_ => true,
			};

			// Resolve color if it's last write is within this pass
			var requiresResolve = viewInfo.samples > 1 && isColor && target.lastWriteIndex <= nativePassDesc.passEndIndex && target.lastReadIndex > nativePassDesc.passEndIndex && target.lastReadIndex > nativePassDesc.passEndIndex;

			// Store the msaa surface if this is depth and read later, since depth surfaces can not be resolved. Also store msaa if this surface is written to again later
			var requiresMsaaStore = viewInfo.samples > 1 && (!isColor && (target.lastWriteIndex <= nativePassDesc.passEndIndex || target.lastWriteIndex >= nativePassDesc.passEndIndex) || target.lastWriteIndex > nativePassDesc.passEndIndex) && target.lastReadIndex > nativePassDesc.passEndIndex;

			// Store the target if it is read outside of this renderpass, or if it is exported to an external resource
			var requiresStore = target.lastReadIndex > nativePassDesc.passEndIndex || target.isExternal || target.isPersistent;

			if (requiresResolve)
			{
				if (target.resourceIndex == -1)
					AllocateTexture(texture, viewHandle, descriptor);

				attachmentDesc.resolveTarget = new(renderTargetSystem.GetTexture(target.resourceIndex, target.isExternal), 0, CubemapFace.Unknown, Max(0, nativePassDesc.depthSlice));
				attachmentDesc.storeAction = RenderBufferStoreAction.Resolve;
			}
			else if (requiresMsaaStore)
			{
				// Depth targets can't be msaa resolved so we need to store the msaa version.
				if (target.resourceIndex == -1)
					AllocateTexture(texture, viewHandle, descriptor, false, viewInfo.samples);

				attachmentDesc.loadStoreTarget = new(renderTargetSystem.GetTexture(target.resourceIndex, target.isExternal), 0, CubemapFace.Unknown, Max(0, nativePassDesc.depthSlice));
			}
			else if (requiresStore)
			{
				// A store is required if the target is read outside of this nativePass, or it is exported
				if (target.resourceIndex == -1)
					AllocateTexture(texture, viewHandle, descriptor, false, 1);

				attachmentDesc.loadStoreTarget = new(renderTargetSystem.GetTexture(target.resourceIndex, target.isExternal), 0, CubemapFace.Unknown, Max(0, nativePassDesc.depthSlice));
			}
			else
			{
				attachmentDesc.storeAction = RenderBufferStoreAction.DontCare;
			}

			_ = attachmendIndices.Add(attachmentDesc);
		}

		Span<byte> debugNameUtf8 = stackalloc byte[Encoding.UTF8.GetByteCount(nativePassDesc.debugName)];
		_ = Encoding.UTF8.GetBytes(nativePassDesc.debugName, debugNameUtf8);

		var subPasses = nativeRenderPassSystem.GetSubPassDescriptors(nativePassDesc.subpasses);
		command.BeginRenderPass(viewInfo.size.x, viewInfo.size.y, nativePassDesc.volumeDepth, viewInfo.samples, attachmendIndices.Span.AsArray(), nativePassDesc.depthIndex, -1, subPasses.AsArray(), debugNameUtf8);
	}

	private void EndNativeRenderPass(CommandBuffer command, int lastNativePass, int passIndex)
	{
		command.EndRenderPass();

		// Free any resources from the previous pass if possible
		var nativePassDesc = nativeRenderPassSystem.GetDescriptor(lastNativePass);
		var attachments = nativeRenderPassSystem.GetAttachments(nativePassDesc.attachments);
		foreach (var attachment in attachments)
		{
			ref var target = ref GetResource(attachment);

			// Exported targets should never be released
			if (target.isExternal || target.isPersistent)
				continue;

			// If the target needs to be read later, it can't be released yet
			if (target.lastReadIndex > passIndex)
				continue;

			// Don't release targets that were never assigned
			if (target.resourceIndex == -1)
				continue;

			renderTargetSystem.ReleaseResource(target.resourceIndex);
			target.resourceIndex = -1;
		}
	}

	public void Execute(CommandBuffer command)
	{
		nativeRenderPassSystem.CloseIfNeeded(renderPasses.Count);

		FillConstantBuffers(command);

		var currentNativePass = -1;
		for (var i = 0; i < renderPasses.Count; i++)
		{
			var renderPass = renderPasses[i];
			if (renderPass.NativePassIndex != currentNativePass)
			{
				// End current pass if needed
				if (currentNativePass != -1)
				{
					EndNativeRenderPass(command, currentNativePass, i - 1);
					currentNativePass = -1;
				}

				if (renderPass.NativePassIndex > -1)
				{
					BeginNativeRenderPass(command, i, renderPass);
					currentNativePass = renderPass.NativePassIndex;
				}
			}
			else if (renderPass.IsNewSubPass)
				command.NextSubPass();

			// UAV resources are handled seperately so we need to write them here
			foreach (var handle in handles[renderPass.UavResourceRange])
			{
				ref var target = ref GetResource(handle);

				// If this is the first time it is written, we need to allocate a texture
				if (target.resourceIndex == -1)
				{
					if (handle.type == ResourceHandleType.RenderTarget)
						AllocateTexture(handle, renderTargetDescriptors[target.descriptorIndex].viewHandle, renderTargetDescriptors[target.descriptorIndex], true, 1);

					if (handle.type == ResourceHandleType.Buffer)
					{
						var descriptor = bufferDescriptors[target.descriptorIndex];
						target.resourceIndex = bufferSystem.AllocateBuffer(descriptor);
					}
				}

				if (handle.type == ResourceHandleType.RenderTarget)
				{
					var resource = renderTargetSystem.GetTexture(target.resourceIndex, target.isExternal);
					command.SetGlobalTexture(target.propertyId, resource);
				}

				if (handle.type == ResourceHandleType.Buffer)
				{
					var resource = bufferSystem.GetBuffer(target.resourceIndex);
					command.SetGlobalBuffer(target.propertyId, resource);
				}
			}

			// Set resources. Note this needs to happen after allocation, since we free any resources after this, and we don't want to accidentally free a resource that is being read
			foreach (var handle in handles[renderPass.ResourceRange])
			{
				ref var target = ref GetResource(handle);

				if (handle.type == ResourceHandleType.RenderTarget)
				{
					var resource = renderTargetSystem.GetTexture(target.resourceIndex, target.isExternal);
					command.SetGlobalTexture(target.propertyId, resource);
				}

				if (handle.type == ResourceHandleType.Buffer)
				{
					var resource = bufferSystem.GetBuffer(target.resourceIndex);
					if (resource.target.HasFlag(GraphicsBuffer.Target.Constant))
						command.SetGlobalConstantBuffer(resource, target.propertyId, 0, resource.stride);
					else
						command.SetGlobalBuffer(target.propertyId, resource);
				}

				if (handle.type == ResourceHandleType.Texture)
				{
					var resource = textures[target.resourceIndex];
					command.SetGlobalTexture(target.propertyId, resource);
				}

				if (handle.type == ResourceHandleType.RayTracingAccelerationStructure)
				{
					var resource = rayTracingAccelerationStructures[target.resourceIndex];
					command.SetGlobalRayTracingAccelerationStructure(target.propertyId, resource);
				}

				// If this is the last time a resource is read, it can be freed for the next pass
				if (i == target.lastReadIndex && !target.isExternal && !target.isPersistent)
				{
					if (handle.type == ResourceHandleType.RenderTarget)
						renderTargetSystem.ReleaseResource(target.resourceIndex);

					if (handle.type == ResourceHandleType.Buffer)
						bufferSystem.ReleaseResource(target.resourceIndex);

					target.resourceIndex = -1;
				}
			}

			foreach (var keyword in renderPass.Keywords)
				command.EnableKeyword(keyword);

			command.BeginSample(renderPass.Name);
			renderPass.Execute(command);
			command.EndSample(renderPass.Name);

			foreach (var keyword in renderPass.Keywords)
				command.DisableKeyword(keyword);

			// Free any UAVs. This needs to be done after the pass, otherwise we might allocate and free a texture before the pass starts, allowing another UAV to be assigned to the same texture
			foreach (var handle in handles[renderPass.UavResourceRange])
			{
				// If this is the last time a resource is read, it can be freed for the next pass
				ref var target = ref GetResource(handle);
				if (i != target.lastReadIndex || target.isExternal || target.isPersistent)
					continue;

				if (handle.type == ResourceHandleType.RenderTarget)
					renderTargetSystem.ReleaseResource(target.resourceIndex);

				if (handle.type == ResourceHandleType.Buffer)
					bufferSystem.ReleaseResource(target.resourceIndex);

				target.resourceIndex = -1;
			}
		}

		if (currentNativePass != -1)
			EndNativeRenderPass(command, currentNativePass, renderPasses.Count - 1);

		FrameIndex++;
	}

	public void Clear()
	{
		resourceInfo.Clear();
		renderPasses.Clear();
		viewInfos.Clear();
		nativeRenderPassSystem.Clear();
		handles.Clear();
		resourceMap.Clear();
		rayTracingAccelerationStructures.Clear();
		textures.Clear();
		constantBufferData.Clear();
		constantBufferRanges.Clear();
		firstWriteIndices.Clear();
		bufferDescriptors.Clear();
		renderTargetDescriptors.Clear();
		renderTargetSystem.FreeUnreleasedResources();

		foreach(var index in persistentResourcesToFree)
			persistentResourceInfo.Free(index);

		persistentResourcesToFree.Clear();

		// Need to reset some things
		for(var i = 0; i < persistentResourceInfo.Count; i++)
		{
			ref var info = ref persistentResourceInfo[i];
			info.lastReadIndex = -1;
			info.lastWriteIndex = -1;
		}
	}
}