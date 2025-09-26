namespace RueI.API.Elements.Parameters;

using global::Utils.Networking;
using Mirror;

/// <summary>
/// Represents a parameter for a value that changes over time.
/// </summary>
/// <remarks>
/// This is the RueI equivalent of the base-game <see cref="Hints.AnimationCurveHintParameter"/>.
/// </remarks>
public class AnimatedParameter : FormattableParameter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AnimatedParameter"/> class.
    /// </summary>
    /// <param name="value">The <see cref="AnimatedValue"/> to use.</param>
    public AnimatedParameter(AnimatedValue value)
        : base(null)
    {
        if (value.IsNull)
        {
            // TODO: implement null check ig
        }

        this.Value = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AnimatedParameter"/> class
    /// with a given format.
    /// </summary>
    /// <param name="value">The <see cref="AnimatedValue"/> to use.</param>
    /// <param name="format">The format to use.</param>
    /// <remarks>
    /// Note that a format is not allowed if the <see cref="AnimatedParameter"/> is inside a
    /// line-height or size tag.
    /// </remarks>
    public AnimatedParameter(AnimatedValue value, string format)
        : base(format)
    {
        this.Value = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AnimatedParameter"/> class
    /// with a given format and the option to round the value to an <see langword="int"/>.
    /// </summary>
    /// <param name="value">The <see cref="AnimatedValue"/> to use.</param>
    /// <param name="format">The format to use.</param>
    /// <param name="roundToInt">Whether to round the value to an <see langword="int"/>.</param>
    /// <remarks>
    /// <para>
    /// Note that a format is not allowed and <paramref name="roundToInt"/> must be <see langword="false"/>
    /// if the <see cref="AnimatedParameter"/> is inside a line-height or size tag.
    /// </para>
    /// Setting <paramref name="roundToInt"/> to <see langword="true"/> allows for formats that only work on
    /// <see cref="int"/>s, such as hexadecimal, to work.
    /// </remarks>
    public AnimatedParameter(AnimatedValue value, string format, bool roundToInt)
        : this(value, format)
    {
        this.RoundToInt = roundToInt;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AnimatedParameter"/> class
    /// with the option to round the value to an <see langword="int"/>.
    /// </summary>
    /// <param name="value">The <see cref="AnimatedValue"/> to use.</param>
    /// <param name="roundToInt">Whether to round the value to an <see langword="int"/>.</param>
    public AnimatedParameter(AnimatedValue value, bool roundToInt)
        : this(value)
    {
        this.RoundToInt = roundToInt;
    }

    /// <summary>
    /// Gets a value indicating whether the float should be rounded to an <see langword="int"/>.
    /// </summary>
    public bool RoundToInt { get; } // corresponds to "integral"

    /// <summary>
    /// Gets the offset for the value.
    /// </summary>
    public double Offset { get; init; }

    /// <summary>
    /// Gets the <see cref="AnimatedValue"/> for the <see cref="AnimatedParameter"/>.
    /// </summary>
    public AnimatedValue Value { get; }

    /// <inheritdoc/>
    internal override HintParameterReaderWriter.HintParameterType HintParameterType => HintParameterReaderWriter.HintParameterType.AnimationCurve;

    /// <inheritdoc/>
    internal override void Write(NetworkWriter writer)
    {
        writer.WriteDouble(this.Offset);
        writer.WriteString(this.Format);
        writer.WriteBool(this.RoundToInt);
        this.Value.Write(writer);
    }

    /// <summary>
    /// Writes a transformed <see cref="AnimatedParameter"/> to a <see cref="NetworkWriter"/>.
    /// </summary>
    /// <param name="writer">The <see cref="NetworkWriter"/> to write to.</param>
    /// <param name="multiplier">A value to multiply the frames of the <see cref="AnimatedValue"/> by.</param>
    /// <param name="addend">A value to add to all of the keyframes of the <see cref="AnimatedValue"/>.</param>
    internal void WriteTransformed(NetworkWriter writer, float multiplier, float addend)
    {
        writer.WriteDouble(this.Offset);
        writer.WriteString(this.Format);
        writer.WriteBool(this.RoundToInt);
        this.Value.WriteTransformed(writer, multiplier, addend);
    }

    private static void WriteFormat(NetworkWriter writer, char formatChar, int precision) => writer.WriteString(formatChar + precision.ToString());
}