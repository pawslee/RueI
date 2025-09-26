namespace RueI.API.Parsing.Modifications;

using System;
using System.Text;

using RueI.API.Parsing;
using RueI.Utils;
using RueI.Utils.Extensions;

/// <summary>
/// Represents a modification that adds a linebreak with a specialized alignment.
/// </summary>
internal class AlignSpaceModification : Modification
{
    private static readonly byte[] SpaceBytes = Encoding.UTF8.GetBytes("<space=");
    private readonly bool isLeft;

    /// <summary>
    /// Initializes a new instance of the <see cref="AlignSpaceModification"/> class.
    /// </summary>
    /// <param name="isLeft">Whether the current alignment is a left-alignment..</param>
    /// <param name="position">The position to add the <see cref="AlignSpaceModification"/> at.</param>
    internal AlignSpaceModification(bool isLeft, int position)
        : base(position)
    {
        this.isLeft = isLeft;
    }

    /// <inheritdoc/>
    internal override void Apply(CombinerContext context, ref ReadOnlySpan<char> buffer)
    {
        context.ContentWriter.WriteBytes(SpaceBytes, false);

        float aspectRatio = context.Hub.aspectRatioSync.AspectRatio;

        context.ContentWriter.WriteFloatAsString(PositionUtils.EdgeOffset(aspectRatio));
        context.ContentWriter.WriteUtf8Char('>');

        if (!this.isLeft)
        {
            context.ContentWriter.WriteChars("<size=0>.</size>");
        }
    }
}