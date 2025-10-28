namespace RueI.API.Elements.Parameters;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

/// <summary>
/// Represents an animated value using a Unity <see cref="AnimationCurve"/>.
/// </summary>
/// <remarks>
/// Provides a lightweight, read-only wrapper for an <see cref="AnimationCurve"/>
/// that can be serialized through Mirror networking.
/// </remarks>
public readonly struct AnimatedValue : IEnumerable<Keyframe>
{
    private readonly AnimationCurve curve;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnimatedValue"/> struct from an <see cref="AnimationCurve"/>.
    /// </summary>
    /// <param name="curve">The curve to wrap.</param>
    public AnimatedValue(AnimationCurve curve)
    {
        if (curve == null)
        {
            throw new ArgumentNullException(nameof(curve));
        }

        if (curve.length < 2)
        {
            throw new ArgumentException("Curve needs at least two keyframes.", nameof(curve));
        }

        if (curve.length > 257)
        {
            throw new ArgumentException("Curve has too many keyframes (max: 257).", nameof(curve));
        }

        // Clone to ensure immutability
        this.curve = new AnimationCurve(curve.keys);
    }
#pragma warning disable SA1614 // Element parameter documentation should have text
    /// <summary>
    /// Initializes a new instance of the <see cref="AnimatedValue"/> struct.
    /// </summary>
    /// <param name="frames"></param>
    public AnimatedValue(IEnumerable<Keyframe> frames)
#pragma warning restore SA1614 // Element parameter documentation should have text
    {
        if (frames == null)
        {
            throw new ArgumentNullException(nameof(frames));
        }

        var keyArray = frames.ToArray();

        if (keyArray.Length < 2)
        {
            throw new ArgumentException("Needs at least two keyframes.", nameof(frames));
        }

        if (keyArray.Length > 257)
        {
            throw new ArgumentException("Too many keyframes (max: 257).", nameof(frames));
        }

        this.curve = new AnimationCurve(keyArray);
    }

#pragma warning disable SA1614 // Element parameter documentation should have text
    /// <summary>
    /// Initializes a new instance of the <see cref="AnimatedValue"/> struct.
    /// </summary>
    /// <param name="frames"></param>
    public AnimatedValue(params Keyframe[] frames)
#pragma warning restore SA1614 // Element parameter documentation should have text
    {
        if (frames == null)
        {
            throw new ArgumentNullException(nameof(frames));
        }

        if (frames.Length < 2)
        {
            throw new ArgumentException("Needs at least two keyframes.", nameof(frames));
        }

        if (frames.Length > 257)
        {
            throw new ArgumentException("Too many keyframes (max: 257).", nameof(frames));
        }

        this.curve = new AnimationCurve(frames);
    }

    /// <summary>
    /// Gets a value indicating whether gets whether this instance is effectively null or uninitialized.
    /// </summary>
    internal bool IsNull => this.curve == null;

    /// <summary>
    /// Evaluates the curve at a specific time.
    /// </summary>
    /// <param name="time">The time at which to evaluate.</param>
    /// <returns>The evaluated value.</returns>
    public float Evaluate(float time) => this.curve.Evaluate(time);

    /// <summary>
    /// Returns an enumerator for the underlying keyframes.
    /// </summary>
    /// <returns>An enumerator for the keyframes.</returns>
    public IEnumerator<Keyframe> GetEnumerator() =>
        ((IEnumerable<Keyframe>)this.curve.keys).GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

    /// <summary>
    /// Writes this <see cref="AnimatedValue"/> to a Mirror <see cref="NetworkWriter"/>.
    /// </summary>
    /// <param name="writer">The writer to serialize into.</param>
    internal void Write(NetworkWriter writer)
    {
        if (this.curve == null)
        {
            writer.WriteByte(0);
            return;
        }

        var keys = this.curve.keys;
        int length = keys.Length;

        writer.WriteByte((byte)(length - 2)); // maintain same format

        for (int i = 0; i < length; i++)
        {
            var frame = keys[i];
            writer.WriteFloat(frame.time);
            writer.WriteFloat(frame.value);
            writer.WriteFloat(frame.inTangent);
            writer.WriteFloat(frame.outTangent);
            writer.WriteFloat(frame.inWeight);
            writer.WriteFloat(frame.outWeight);
        }
    }

    /// <summary>
    /// Writes this <see cref="AnimatedValue"/> to a Mirror <see cref="NetworkWriter"/>,
    /// applying a linear transform to each keyframe value.
    /// </summary>
    /// <param name="writer">The writer to serialize into.</param>
    /// <param name="multiplier">The value multiplier.</param>
    /// <param name="addend">The additive offset.</param>
    internal void WriteTransformed(NetworkWriter writer, float multiplier, float addend)
    {
        if (this.curve == null)
        {
            writer.WriteByte(0);
            return;
        }

        var keys = this.curve.keys;
        int length = keys.Length;

        writer.WriteByte((byte)(length - 2));

        for (int i = 0; i < length; i++)
        {
            var frame = keys[i];
            writer.WriteFloat(frame.time);
            writer.WriteFloat((frame.value * multiplier) + addend);
            writer.WriteFloat(frame.inTangent * multiplier);
            writer.WriteFloat(frame.outTangent * multiplier);
            writer.WriteFloat(frame.inWeight);
            writer.WriteFloat(frame.outWeight);
        }
    }
}