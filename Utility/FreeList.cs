using System.Collections.Generic;

public class FreeList<T>
{
	private readonly Stack<int> availableIndices = new();
	private readonly ResizableArray<T> items = new();

	public int Count => items.Count;

	public ref T this[int i] => ref items[i];

	public int Add(T item)
	{
		if (availableIndices.TryPop(out var index))
		{
			items[index] = item;
		}
		else
		{
			index = items.Count;
			items.Add(item);
		}

		return index;
	}

	public void Free(int index)
	{
		items[index] = default;
		availableIndices.Push(index);
	}

	public void Clear()
	{
		items.Clear();
		availableIndices.Clear();
	}
}