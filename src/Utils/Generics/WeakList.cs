using System;
using System.Collections.Generic;

namespace MyMod.Utils.Generics;

public class WeakList<T> : WeakCollection<T>, IList<T> where T : class
{
    public WeakList(List<T> fromList)
    {
        foreach (T item in fromList)
        {
            Add(item);
        }
    }

    public WeakList()
    {
    }

    public T this[int index]
    {
        get
        {
            if (!list[index].TryGetTarget(out T target))
            {
                Logger.LogWarning($"Returning an empty value: {this}");
            }

            return target;
        }

        set => list[index] = new WeakReference<T>(value);
    }

    public int IndexOf(T item)
    {
        foreach (WeakReference<T> weakRef in list)
        {
            if (weakRef.TryGetTarget(out T target) && target == item)
            {
                return list.IndexOf(weakRef);
            }
        }

        return -1;
    }

    public void Insert(int index, T item) => list.Insert(index, new WeakReference<T>(item));
    public void RemoveAt(int index) => list.RemoveAt(index);
}