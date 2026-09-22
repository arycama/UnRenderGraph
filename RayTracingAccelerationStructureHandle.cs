using System;
using System.Diagnostics;

[DebuggerDisplay("{index}")]
public readonly struct RayTracingAccelerationStructureHandle : IEquatable<RayTracingAccelerationStructureHandle>
{
	public readonly int index;
	public readonly int propertyId;

	public RayTracingAccelerationStructureHandle(int index, int propertyId)
	{
		this.index = index;
		this.propertyId = propertyId;
	}

	public override bool Equals(object obj)
	{
		return obj is RayTracingAccelerationStructureHandle handle && Equals(handle);
	}

	public bool Equals(RayTracingAccelerationStructureHandle other)
	{
		return index == other.index;
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(index);
	}

	public static bool operator ==(RayTracingAccelerationStructureHandle left, RayTracingAccelerationStructureHandle right)
	{
		return left.Equals(right);
	}

	public static bool operator !=(RayTracingAccelerationStructureHandle left, RayTracingAccelerationStructureHandle right)
	{
		return !(left == right);
	}

	public static implicit operator int(RayTracingAccelerationStructureHandle handle) => handle.index;

	public static implicit operator ResourceHandle(RayTracingAccelerationStructureHandle handle) => new(handle.index, ResourceHandleType.RayTracingAccelerationStructure, handle.propertyId, false);
}