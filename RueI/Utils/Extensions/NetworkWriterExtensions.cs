namespace RueI.Utils.Extensions;

extern alias mscorlib;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

using Mirror;
using mscorlib::System.Buffers.Text;
using RueI.API.Parsing.Structs;

/// <summary>
/// Provides extensions for the <see cref="NetworkWriter"/> class to write <see cref="ReadOnlySpan{T}"/>s.
/// </summary>
internal static class NetworkWriterExtensions
{
    private static readonly UTF8Encoding Encoding = new(false, true);

    /// <summary>
    /// Writes a <see langword="string"/> without writing its size.
    /// </summary>
    /// <param name="writer">The <see cref="NetworkWriter"/> to write to.</param>
    /// <param name="str">The <see langword="string"/> to write.</param>
    /// <exception cref="IndexOutOfRangeException"><paramref name="str"/> is too long.</exception>
    internal static void WriteStringNoSize(this NetworkWriter writer, string str)
    {
        int maxByteCount = Encoding.GetMaxByteCount(str.Length);

        writer.EnsureLength(maxByteCount);

        int bytes = Encoding.GetBytes(str, writer.BufferSpan());
        if (bytes > 65534)
        {
            throw new IndexOutOfRangeException(string.Format("NetworkWriter.WriteString - Value too long: {0} bytes. Limit: {1} bytes", bytes, 65534));
        }

        writer.Position += bytes;
    }

    /// <summary>
    /// Writes a string using a <see langword="char"/> <see cref="ReadOnlySpan{T}"/>.
    /// </summary>
    /// <param name="writer">The <see cref="NetworkWriter"/> to write to.</param>
    /// <param name="span">The <see cref="Span{T}"/> to write.</param>
    /// <exception cref="IndexOutOfRangeException">Thrown when <paramref name="span"/> is larger
    /// than or equal to <see cref="ushort.MaxValue"/>.</exception>
    internal static void WriteCharsAndLength(this NetworkWriter writer, ReadOnlySpan<char> span)
    {
        const int USHORT_SIZE = sizeof(ushort);

        int maxByteCount = Encoding.GetMaxByteCount(span.Length);

        writer.EnsureLength(maxByteCount + USHORT_SIZE);

        int count = Encoding.GetBytes(span, writer.BufferSpan(USHORT_SIZE));

        if (count > NetworkWriter.MaxStringLength)
        {
            throw new IndexOutOfRangeException($"NetworkWriter.WriteSpan - Value too long: {count} bytes. Limit: {ushort.MaxValue} bytes");
        }

        writer.WriteUShort(checked((ushort)(count + 1))); // don't ask why this is checked, this is just how it does it's done in mirror
        writer.Position += count;
    }

    /// <summary>
    /// Writes a string using a <see langword="char"/> <see cref="ReadOnlySpan{T}"/>.
    /// </summary>
    /// <param name="writer">The <see cref="NetworkWriter"/> to write to.</param>
    /// <param name="span">The <see cref="Span{T}"/> to write.</param>
    /// <exception cref="IndexOutOfRangeException">Thrown when <paramref name="span"/> is larger
    /// than or equal to <see cref="ushort.MaxValue"/>.</exception>
    internal static void WriteChars(this NetworkWriter writer, ReadOnlySpan<char> span)
    {
        const int USHORT_SIZE = sizeof(ushort);

        int maxByteCount = Encoding.GetMaxByteCount(span.Length);

        writer.EnsureLength(maxByteCount + USHORT_SIZE);

        int count = Encoding.GetBytes(span, writer.buffer.AsSpan(writer.Position));

        writer.Position += count;
    }

    /// <summary>
    /// Writes a format item and adds a <see cref="NobreakInfo"/>.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="i">The ID of the format item.</param>
    /// <param name="nobreaks">A <see cref="List{T}"/> to add the <see cref="NobreakInfo"/> to.</param>
    internal static void WriteFormatItemNoBreak(this NetworkWriter writer, int i, List<NobreakInfo> nobreaks)
    {
        int start = writer.Position;

        writer.WriteFormatItem(i);

        nobreaks.Add(new()
        {
            Start = start,
            Length = writer.Position - start,
        });
    }

    /// <summary>
    /// Writes a format item without adding a <see cref="NobreakInfo"/>.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="i">The ID of the format item.</param>
    internal static void WriteFormatItem(this NetworkWriter writer, int i)
    {
        writer.WriteUtf8Char('{');
        writer.WriteIntAsString(i);
        writer.WriteUtf8Char('}');
    }

    /// <summary>
    /// Writes a positive <see langword="int"/> as a string, without allocating.
    /// </summary>
    /// <param name="writer">The <see cref="NetworkWriter"/> to write to.</param>
    /// <param name="value">The <see langword="int"/> to write.</param>
    internal static void WriteIntAsString(this NetworkWriter writer, int value)
    {
        const int MaxIntegerDigits = 10;

        writer.EnsureLength(MaxIntegerDigits);

        Span<byte> span = writer.BufferSpan();

        do
        {
            span[writer.Position++] = unchecked((byte)((value % 10) + '0'));

            value /= 10;
        }
        while (value > 0);

        // reverse since we added the values in reverse order
        span[..writer.Position].Reverse();
    }

    /// <summary>
    /// Writes a <see langword="float"/> as a string, without allocating.
    /// </summary>
    /// <param name="writer">The <see cref="NetworkWriter"/> to write to.</param>
    /// <param name="value">The <see langword="int"/> to write.</param>
    internal static void WriteFloatAsString(this NetworkWriter writer, float value)
    {
        const int Length = 32;

        int bytes;
        StandardFormat format = new('F', 5); // 5 decimals

        while (!Utf8Formatter.TryFormat(value, writer.BufferSpan(), out bytes, format))
        {
            writer.EnsureLength(Length);
        }

        writer.Position += bytes;
    }

    /// <summary>
    /// Writes a <see langword="char"/> as UTF8.
    /// </summary>
    /// <param name="writer">The <see cref="NetworkWriter"/> to write to.</param>
    /// <param name="ch">The <see langword="char"/> to write.</param>
    internal static void WriteUtf8Char(this NetworkWriter writer, char ch) => writer.WriteByte((byte)ch);

    /// <summary>
    /// Writes a <see langword="byte"/> <see cref="ReadOnlySpan{T}"/> without writing its size.
    /// </summary>
    /// <param name="writer">The <see cref="NetworkWriter"/> to write to.</param>
    /// <param name="bytes">The <see cref="ReadOnlySpan{T}"/> to write.</param>
    /// <param name="writeSize">Whether or not to write the size.</param>
    internal static void WriteBytes(this NetworkWriter writer, ReadOnlySpan<byte> bytes, bool writeSize)
    {
        if (writeSize)
        {
            writer.EnsureLength(bytes.Length + sizeof(ushort));

            writer.WriteUShort((ushort)(bytes.Length + 1));
        }
        else
        {
            writer.EnsureLength(bytes.Length);
        }

        bytes.CopyTo(writer.BufferSpan());

        writer.Position += bytes.Length;
    }

    /// <summary>
    /// Writes a <see cref="NetworkWriter"/> to this <see cref="NetworkWriter"/>.
    /// </summary>
    /// <param name="writer">The <see cref="NetworkWriter"/> to write to.</param>
    /// <param name="otherWriter">The <see cref="NetworkWriter"/> to write.</param>
    /// <param name="writeSize">Whether to write the size, as a <see langword="ushort"/>.</param>
    internal static void WriteNetworkWriter(this NetworkWriter writer, NetworkWriter otherWriter, bool writeSize) => writer.WriteBytes(otherWriter.ContentSpan(), writeSize);

    /// <summary>
    /// Gets the writable portion of a <see cref="NetworkWriter"/> as a <see cref="Span{T}"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Span<byte> BufferSpan(this NetworkWriter writer, int offset = 0) => writer.buffer.AsSpan(writer.Position + offset);

    /// <summary>
    /// Gets the writable portion of a <see cref="NetworkWriter"/> as a <see cref="Span{T}"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Span<byte> ContentSpan(this NetworkWriter writer) => writer.buffer.AsSpan(0, writer.Position);
}