namespace RueI.API.Elements.Parameters;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mirror;

using RueI.Utils.Collections;

using UnityEngine;
using NetworkKeyframe = global::Utils.Networking.AnimationCurveReaderWriter.NetworkKeyframe;

/// <summary>
/// Represents an animated value.
/// </summary>
/// <remarks>
/// The <see cref="AnimatedValue"/> struct provides a way to represent an <see cref="AnimationCurve"/>
/// while remaining read-only.
/// </remarks>
public readonly struct AnimatedValue : IEnumerable<Keyframe>
{
    // TODO: add easier operations
    private readonly List<NetworkKeyframe> frames;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnimatedValue"/> struct from an <see cref="AnimationCurve"/>.
    /// </summary>
    /// <param name="curve">The <see cref="AnimationCurve"/> to use.</param>
    public AnimatedValue(AnimationCurve curve)
    {
        if (curve == null)
        {
            throw new ArgumentNullException(nameof(curve));
        }

        int length = curve.length;

        if (length > 257)
        {
            throw new ArgumentException("Argument has too many keyframes (max: 257)", nameof(curve));
        }

        if (length < 2)
        {
            throw new ArgumentException("Argument needs at least 2 keyframes: ", nameof(curve));
        }

        this.frames = new(length);

        for (int i = 0; i < length; i++)
        {
            this.frames.Add(new NetworkKeyframe(curve[i]));
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AnimatedValue"/> struct from an <see cref="IEnumerable{T}"/> of frames.
    /// </summary>
    /// <param name="frames">An <see cref="IEnumerable{T}"/> to use.</param>
    public AnimatedValue(IEnumerable<Keyframe> frames)
    {
        this.frames = new(frames.Select(x => new NetworkKeyframe(x)));

        if (this.frames.Count > 257)
        {
            throw new ArgumentException("Argument has too many keyframes (max: 257):", nameof(frames));
        }

        if (this.frames.Count < 2)
        {
            throw new ArgumentException("Argument needs at least 2 keyframes: ", nameof(frames));
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AnimatedValue"/> struct from a <see cref="Keyframe"/> collection.
    /// </summary>
    /// <param name="frames">A <see cref="Keyframe"/> collection to use.</param>
    public AnimatedValue(params Keyframe[] frames)
    {
        if (frames.Length > 257)
        {
            throw new ArgumentException("Argument has too many keyframes (max: 257)", nameof(frames));
        }

        if (frames.Length < 2)
        {
            throw new ArgumentException("Argument needs at least 2 keyframes: ", nameof(frames));
        }

        this.frames = new(frames.Length);

        for (int i = 0; i < frames.Length; i++)
        {
            this.frames[i] = new(frames[i]);
        }
    }

    /// <summary>
    /// Gets a value indicating whether this <see cref="AnimatedValue"/> is null.
    /// </summary>
    internal readonly bool IsNull => this.frames == null;

    /// <summary>
    /// Gets an <see cref="IEnumerator{T}"/> for the keyframes of this <see cref="AnimatedValue"/>.
    /// </summary>
    /// <returns>An <see cref="IEnumerator{T}"/> that can be used to enumerate over the keyframes of this <see cref="AnimatedValue"/>.</returns>
    public IEnumerator<Keyframe> GetEnumerator() => new EnumeratorAdapter<NetworkKeyframe, Keyframe>(this.frames.GetEnumerator(), x => x.Keyframe);

    /// <summary>
    /// Gets an <see cref="IEnumerator"/> for the keyframes of this <see cref="AnimatedValue"/>.
    /// </summary>
    /// <returns>An <see cref="IEnumerator"/> that can be used to enumerate over the keyframes of this <see cref="AnimatedValue"/>.</returns>
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

    /// <summary>
    /// Writes this <see cref="AnimatedValue"/> to a <see cref="NetworkWriter"/>.
    /// </summary>
    /// <param name="writer">The <see cref="NetworkWriter"/> to write to.</param>
    internal void Write(NetworkWriter writer)
    {
        int length = this.frames.Count;

        writer.WriteByte((byte)(length - 2));

        int i = 0;
        while (i < this.frames.Count)
        {
            byte flag = 0;
            byte offset = 0;

            do
            {
                this.frames[i].WriteMetaTable(ref flag, ref offset);
                i++;
            }
            while (offset < 8 && i < length);

            writer.WriteByte(flag);
        }

        for (int j = 0; j < length; j++)
        {
            this.frames[j].WriteData(writer);
        }
    }

    /// <summary>
    /// Writes this <see cref="AnimatedValue"/> to a <see cref="NetworkWriter"/>.
    /// </summary>
    /// <param name="writer">The <see cref="NetworkWriter"/> to write to.</param>
    /// <param name="multiplier">A value to multiply the keyframes by.</param>
    /// <param name="addend">A value to add to the keyframes.</param>
    internal void WriteTransformed(NetworkWriter writer, float multiplier, float addend)
    {
        int length = this.frames.Count;

        writer.WriteByte((byte)(length - 2));

        int i = 0;
        while (i < this.frames.Count)
        {
            byte flag = 0;
            byte offset = 0;

            do
            {
                this.frames[i].WriteMetaTable(ref flag, ref offset);
                i++;
            }
            while (offset < 8 && i < length);

            writer.WriteByte(flag);
        }

        for (int j = 0; j < length; j++)
        {
            NetworkKeyframe networkFrame = this.frames[i];
            Keyframe frame = networkFrame.Keyframe;

            writer.WriteFloat(frame.time);
            writer.WriteFloat((frame.value * multiplier) + addend);

            if (networkFrame.Tangental)
            {
                // we only need to multiply the in/out tangents - do not add
                writer.WriteFloat(frame.inTangent * multiplier);
                writer.WriteFloat(frame.outTangent * multiplier);
            }

            if (networkFrame.Weighted)
            {
                writer.WriteFloat(frame.inWeight);
                writer.WriteFloat(frame.outWeight);
            }
        }
    }
}