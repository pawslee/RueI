namespace RueI.API.Parsing.Modifications;

using System;

using RueI.API.Parsing;
using RueI.Utils.Extensions;

/// <summary>
/// Represents a modification that adds a singular <see langword="char"/>.
/// </summary>
internal class CharModification : Modification
{
    private readonly char ch;
    private readonly bool noBreak;

    /// <summary>
    /// Initializes a new instance of the <see cref="CharModification"/> class.
    /// </summary>
    /// <param name="position">The position of the modification.</param>
    /// <param name="ch">The <see langword="char"/> to add.</param>
    /// <param name="noBreak">Whether to prevent breaks between the added character and the next character.</param>
    internal CharModification(int position, char ch, bool noBreak)
        : base(position)
    {
        this.ch = ch;
        this.noBreak = noBreak;
    }

    /// <inheritdoc/>
    internal override void Apply(CombinerContext context, ref ReadOnlySpan<char> buffer)
    {
        context.ContentWriter.WriteUtf8Char(this.ch);

        if (this.noBreak)
        {
            context.Nobreaks.Add(new()
            {
                Start = context.ContentWriter.Position - 1,
                Length = 1,
            });
        }
    }
}