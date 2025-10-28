namespace RueI.API.Parsing;

using System;

using UnityEngine;

/// <summary>
/// Provides helper methods for working with tags.
/// </summary>
internal static class TagHelpers
{
    private const char LowercaseMask = (char)0b00100000; // takes advantage of the fact that capitals only differs by one bit

    /// <summary>
    /// Quickly converts a <see langword="char"/> to lowercase, provided that the <see langword="char"/> is valid for tags.
    /// </summary>
    /// <param name="ch">The <see langword="char"/> to convert to lowercase.</param>
    /// <returns><paramref name="ch"/> in lowercase.</returns>
    internal static char ToLowercaseFast(char ch) => ch switch
    {
        '/' => '/',
        '-' => '-',
        _ => (char)(ch | LowercaseMask),
    };

    /// <summary>
    /// Quickly determines if a <see cref="char"/> is a digit.
    /// </summary>
    /// <param name="ch">The <see langword="char"/> to check.</param>
    /// <returns><see langword="true"/> if the <paramref name="ch"/> represents a digit; otherwise, <see langword="false"/>.</returns>
    internal static bool IsDigitFast(char ch) => ch >= '0' && ch <= '9';

#pragma warning disable CS0419 // Ambiguous reference in cref attribute
    /// <summary>
    /// Parses a <see langword="float"/> from a <see cref="ReadOnlySpan{T}"/> containing
    /// an integer part and a <see cref="ReadOnlySpan{T}"/> containing the decimal part.
    /// </summary>
    /// <param name="integerPart">A <see cref="ReadOnlySpan{T}"/> of the integer part.</param>
    /// <param name="decimalPart">A <see cref="ReadOnlySpan{T}"/> of the decimal part.</param>
    /// <returns>The parsed <see langword="float"/>.</returns>
    internal static float FromIntegerAndDecimal(ReadOnlySpan<char> integerPart, ReadOnlySpan<char> decimalPart)
#pragma warning restore CS0419 // Ambiguous reference in cref attribute
    {
        const System.Globalization.NumberStyles Style = System.Globalization.NumberStyles.None;
        float value = 0;

        if (!integerPart.IsEmpty)
        {
            value += float.Parse(integerPart, Style);
        }

        if (!decimalPart.IsEmpty)
        {
            value += float.Parse(decimalPart, Style) / Mathf.Pow(10, decimalPart.Length);
        }

        return value;
    }
}