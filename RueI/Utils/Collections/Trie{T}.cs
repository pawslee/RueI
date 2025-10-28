namespace RueI.Utils.Collections;

using System;
using System.Collections.Generic;
using System.Linq;
using RueI.Utils.Extensions;

/// <summary>
/// Provides an implementation of a trie.
/// </summary>
/// <typeparam name="T">The value associated with the end.</typeparam>
internal sealed class Trie<T>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Trie{T}"/> class.
    /// </summary>
    /// <param name="enumerable">An <see cref="IEnumerable{T}"/> to use to create the <see cref="Trie{T}"/>.</param>
    public Trie(IEnumerable<(string Name, T Value)> enumerable)
    {
        this.Root = Build(enumerable.Select<(string, T), (StringSlice, T)>(x => (new StringSlice(x.Item1, 0), x.Item2)));
    }

    /// <summary>
    /// Gets the root node.
    /// </summary>
    public RadixNode Root { get; }

    private static RadixNode Build(IEnumerable<(StringSlice Slice, T Value)> values)
    {
        // TODO: check this code
        if (values.TryGetSingle(out var value))
        {
            if (value.Slice.Length == 0)
            {
                return new RadixNode(value.Value, null);
            }
        }

        var groups = values
            .GroupBy(x => x.Slice[0]) // get common prefix char
            .ToDictionary(
                g => g.Key,
                g => Build(g.Select(kv => (kv.Slice.AllExceptFirst(), kv.Value)))); // recursively build node

        return new RadixNode(default, groups);
    }

#pragma warning disable CS0419 // Ambiguous reference in cref attribute
    /// <summary>
    /// Provides a <see cref="ReadOnlySpan{T}"/> for a <see langword="string"/> that is not a <see langword="ref struct"/>.
    /// </summary>
    internal readonly struct StringSlice
#pragma warning restore CS0419 // Ambiguous reference in cref attribute
    {
        private readonly int start;
        private readonly string str;

        /// <summary>
        /// Initializes a new instance of the <see cref="StringSlice"/> struct.
        /// </summary>
        /// <param name="str">The <see cref="string"/> to slice.</param>
        /// <param name="start">The position at which to start.</param>
        public StringSlice(string str, int start)
        {
            this.str = str;
            this.start = start;
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="StringSlice"/> is empty.
        /// </summary>
        public bool IsEmpty => this.start == this.str.Length;

        /// <summary>
        /// Gets the length of the <see cref="StringSlice"/>.
        /// </summary>
        public int Length => this.str.Length - this.start;

        /// <summary>
        /// Gets the <see cref="char"/> at the given index.
        /// </summary>
        /// <param name="index">The index of the value.</param>
        /// <returns>The <see cref="char"/> at the index.</returns>
        public readonly char this[int index] => this.str[this.start + index];

#pragma warning disable CS0419 // Ambiguous reference in cref attribute
        /// <summary>
        /// Converts a <see cref="StringSlice"/> to a <see cref="ReadOnlySpan{T}"/>.
        /// </summary>
        /// <returns>A <see cref="ReadOnlySpan{T}"/>.</returns>
        public ReadOnlySpan<char> ToSpan() => this.str.AsSpan(this.start);
#pragma warning restore CS0419 // Ambiguous reference in cref attribute

        /// <summary>
        /// Gets a <see cref="StringSlice"/> that starts one <see cref="char"/> later than this <see cref="StringSlice"/>.
        /// </summary>
        /// <returns>A <see cref="StringSlice"/> that starts one  <see cref="char"/> later.</returns>
        public StringSlice AllExceptFirst() => new(this.str, this.start + 1);
    }

    /// <summary>
    /// Represents a node in a <see cref="Trie{T}"/>.
    /// </summary>
    public class RadixNode
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RadixNode"/> class.
        /// </summary>
        /// <param name="value">The <typeparamref name="T"/> to use for this node, or <see langword="null"/> if this is not an ending node.</param>
        /// <param name="nodes">A <see cref="Dictionary{TKey, TValue}"/> of sub-nodes, or <see langword="null"/> if this is a leaf.</param>
        internal RadixNode(T? value, Dictionary<char, RadixNode>? nodes)
        {
            this.Value = value;
            this.Nodes = nodes;
        }

        /// <summary>
        /// Gets a <see cref="Dictionary{TKey, TValue}"/> of sub-nodes.
        /// </summary>
        internal Dictionary<char, RadixNode>? Nodes { get; }

        /// <summary>
        /// Gets the value of the <see cref="RadixNode"/>, if it has one.
        /// </summary>
        internal T? Value { get; }

        /// <summary>
        /// Gets the node with the given prefix, or <see langword="null"/> if no node has that prefix.
        /// </summary>
        /// <param name="ch">The <see langword="char"/> with the prefix.</param>
        /// <returns>The <see cref="RadixNode"/> that represents values that start with <paramref name="ch"/>, or <see langword="null"/> if
        /// no sub-nodes start with <paramref name="ch"/>.</returns>
        internal RadixNode? this[char ch]
        {
            get
            {
                if (this.Nodes != null && this.Nodes.TryGetValue(ch, out var node))
                {
                    return node;
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Tries to get a <see cref="RadixNode"/> from the <see cref="Nodes"/> of this <see cref="RadixNode"/>.
        /// </summary>
        /// <param name="ch">The <see langword="char"/> to use as a prefix.</param>
        /// <param name="node">If this method returns <see langword="true"/>, the <see cref="RadixNode"/>.</param>
        /// <returns><see langword="true"/> if there was a <see cref="RadixNode"/> with that prefix; otherwise, false.</returns>
        internal bool TryGetNode(char ch, out RadixNode node)
        {
            if (this.Nodes != null && this.Nodes.TryGetValue(ch, out node))
            {
                return true;
            }

            node = null!;
            return false;
        }
    }
}