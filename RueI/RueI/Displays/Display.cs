﻿namespace RueI.Displays;

using RueI.Extensions;
using RueI.Elements;
using RueI.Displays.Interfaces;

/// <summary>
/// Represents a display attached to a <see cref="DisplayCore"/>.
/// </summary>
/// <include file='docs.xml' path='docs/members[@name="display"]/Display/*'/>
public class Display : DisplayBase, IElementContainer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Display"/> class.
    /// </summary>
    /// <param name="hub">The <see cref="ReferenceHub"/> to assign the display to.</param>
    public Display(ReferenceHub hub)
        : base(hub)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Display"/> class.
    /// </summary>
    /// <param name="coordinator">The <see cref="DisplayCore"/> to assign the display to.</param>
    public Display(DisplayCore coordinator)
        : base(coordinator)
    {
    }

    /// <summary>
    /// Gets the elements of this display.
    /// </summary>
    public List<IElement> Elements { get; } = new();

    /// <inheritdoc/>
    public override IEnumerable<IElement> GetAllElements() => Elements.FilterDisabled();
}