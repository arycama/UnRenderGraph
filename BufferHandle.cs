using System;
using System.Diagnostics;

[DebuggerDisplay("{index}")]
public readonly struct BufferHandle : IEquatable<BufferHandle>
{
	public readonly int index;
	public readonly int propertyId;
	public readonly bool isPersistent;

	public BufferHandle(int index, int propertyId, bool isPersistent)
	{
		this.index = index;
		this.propertyId = propertyId;
		this.isPersistent = isPersistent;
	}

	public override bool Equals(object obj)
	{
		return obj is BufferHandle handle && Equals(handle);
	}

	public bool Equals(BufferHandle other)
	{
		return index == other.index;
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(index);
	}

	public static bool operator ==(BufferHandle left, BufferHandle right)
	{
		return left.Equals(right);
	}

	public static bool operator !=(BufferHandle left, BufferHandle right)
	{
		return !(left == right);
	}

	public static implicit operator int(BufferHandle handle) => handle.index;

	public static implicit operator ResourceHandle(BufferHandle handle) => new(handle.index, ResourceHandleType.Buffer, handle.propertyId, handle.isPersistent);
}
