using System;
using System.Diagnostics;

[DebuggerDisplay("{index}")]
public readonly struct ImportedBufferHandle : IEquatable<ImportedBufferHandle>
{
	public readonly int index;

	public ImportedBufferHandle(int index)
	{
		this.index = index;
	}

	public override bool Equals(object obj)
	{
		return obj is ImportedBufferHandle handle && Equals(handle);
	}

	public bool Equals(ImportedBufferHandle other)
	{
		return index == other.index;
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(index);
	}

	public static bool operator ==(ImportedBufferHandle left, ImportedBufferHandle right)
	{
		return left.Equals(right);
	}

	public static bool operator !=(ImportedBufferHandle left, ImportedBufferHandle right)
	{
		return !(left == right);
	}

	public static implicit operator int(ImportedBufferHandle handle) => handle.index;

	public static implicit operator ResourceHandle(ImportedBufferHandle handle) => new(handle, ResourceHandleType.ImportedBuffer);
}
