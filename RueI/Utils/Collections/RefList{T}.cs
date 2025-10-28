namespace RueI.Utils.Collections;

using System;

/// <summary>
/// Represents a list that offers <see langword="ref"/> access to elements.
/// </summary>
/// <typeparam name="T">The type of elements to store.</typeparam>
internal sealed class RefList<T>
{
    private T[] values;

    /// <summary>
    /// Initializes a new instance of the <see cref="RefList{T}"/> class with no elements.
    /// </summary>
    public RefList()
    {
        this.values = Array.Empty<T>();
    }

    private RefList(RefList<T> original)
    {
        if (original.Capacity == 0)
        {
            this.values = Array.Empty<T>();
        }
        else
        {
            this.values = (T[])original.values.Clone();
            this.Count = original.Count;
        }
    }

    /// <summary>
    /// Gets the number of items in the <see cref="RefList{T}"/>.
    /// </summary>
    public int Count { get; private set; }

    private int Capacity => this.values.Length;

    /// <summary>
    /// Gets the item at the given index.
    /// </summary>
    /// <param name="index">The index to get the item at.</param>
    /// <returns>The item at the index.</returns>
    /// <remarks>
    /// The value is unspecified if <paramref name="index"/> is out of bounds.
    /// </remarks>
    public ref T this[int index] => ref this.values[index];

    /// <summary>
    /// Ensures that the <see cref="RefList{T}"/> has a certain amount of additional capacity.
    /// </summary>
    /// <param name="capacity">The capacity to add.</param>
    public void AddCapacity(int capacity)
    {
        if (capacity == 0)
        {
            return;
        }

        this.GrowTo(this.Capacity + capacity);
    }

    /// <summary>
    /// Adds an item to this <see cref="RefList{T}"/>.
    /// </summary>
    /// <param name="value">The item to add.</param>
    public void Add(in T value)
    {
        if (this.values.Length == this.Count)
        {
            int capacity;

            if (this.Capacity == 0)
            {
                capacity = 1;
            }
            else
            {
                capacity = this.Capacity * 2;
            }

            Array.Resize(ref this.values, capacity);
        }

        this[this.Count++] = value;
    }

    /// <summary>
    /// Adds a <see cref="RefList{T}"/> to the end of this <see cref="RefList{T}"/>.
    /// </summary>
    /// <param name="list">The <see cref="RefList{T}"/> to add.</param>
    public void AddList(RefList<T> list)
    {
        int length = list.Count;

        if (length == 0)
        {
            return;
        }

        this.AddCapacity(length);

        list.values.CopyTo(this.values, this.Count);
        this.Count += length;
    }

    /// <summary>
    /// Clears this <see cref="RefList{T}"/>.
    /// </summary>
    public void Clear()
    {
        this.Count = 0;
    }

    /// <summary>
    /// Creates a clone of the <see cref="RefList{T}"/>.
    /// </summary>
    /// <returns>A shallow-copy of the <see cref="RefList{T}"/>.</returns>
    public RefList<T> Clone() => new(this);

#pragma warning disable CS0419 // Ambiguous reference in cref attribute
    /// <summary>
    /// Gets the values of the <see cref="RefList{T}"/> as a <see cref="Span{T}"/>.
    /// </summary>
    /// <param name="start">The position to start at.</param>
    /// <returns>A <see cref="Span{T}"/> that encompasses all values from <paramref name="start"/> to <see cref="Count"/>.</returns>
    public Span<T> AsSpan(int start) => this.values.AsSpan(start, this.Count - start);
#pragma warning restore CS0419 // Ambiguous reference in cref attribute

    private void GrowTo(int capacity)
    {
        if (this.Capacity + this.Count < capacity)
        {
            Array.Resize(ref this.values, Math.Max(this.Capacity * 2, capacity));
        }
    }
}