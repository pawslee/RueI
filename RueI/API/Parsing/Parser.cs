namespace RueI.API.Parsing;

extern alias mscorlib;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using LabApi.Features.Console;
using RueI.API.Elements;
using RueI.API.Elements.Parameters;
using RueI.API.Parsing.Enums;
using RueI.API.Parsing.Modifications;
using RueI.API.Parsing.Structs;
using RueI.Utils;
using RueI.Utils.Collections;
using RueI.Utils.Enums;
using RueI.Utils.Extensions;

/// <summary>
/// Parses <see cref="Element"/>s into <see cref="ParsedData"/>.
/// </summary>
internal static class Parser
{
    // TODO: support tangents and shit by manipulating size
    private const int AdjustedTagLength = Constants.MaxTagLength - 2; // so we don't count < and =
    private const AlignStyle NoAlignStyle = (AlignStyle)(-1);

    private static readonly mscorlib.System.Collections.Generic.Stack<AnimatableFloat> SizeStack = new();

    private static readonly CumulativeFloat Offset = new();
    private static readonly char[] CharBuffer = new char[Constants.MaxTagLength];
    private static readonly List<Modification> Modifications = new();

    private static readonly Trie<AlignStyle> AlignTrie = new(new[]
    {
        ("left", AlignStyle.Left),
        ("right", AlignStyle.Right),
    });

    private static readonly Trie<RichTextTag> TagTrie;
    private static readonly Trie<char> ReplacementTrie = new(new[]
    {
        ("br", '\n'),
        ("cr", '\r'),
        ("nbsp", '\u00a0'), // non breaking space
        ("zwsp", '\u200b'),
        ("zwj", '\u00AD'),
        ("shy", '\u00AD'),
    });

    private static string text = null!;

    private static IReadOnlyList<ContentParameter> currentParameters = null!;
    private static bool noparse;
    private static bool noparseParsesEscapeSeq;
    private static bool noparseParsesFormat;

    private static bool resolutionAlign;

    // saving the last position makes backtracking easier
    private static int lastPosition = 0;
    private static int position = -1; // TryGetNext increases this by 1

    private static AnimatableFloat lineHeight = AnimatableFloat.Invalid;
    private static AlignStyle alignment = NoAlignStyle;

    static Parser()
    {
        TagTrie = new(TagNames.Select(x => (x.Value, x.Key)));
    }

    /// <summary>
    /// Gets a <see cref="Dictionary{TKey, TValue}"/> containing the name of each <see cref="RichTextTag"/>.
    /// </summary>
    internal static Dictionary<RichTextTag, string> TagNames { get; } = new()
    {
        { RichTextTag.Noparse, "noparse" },
        { RichTextTag.LineHeight, "line-height" },
        { RichTextTag.VOffset, "voffset" },
        ////{ RichTextTag.Size, "size" }, TODO: support size
        { RichTextTag.Align, "align" },
        { RichTextTag.CloseNoparse, "/noparse" },
        { RichTextTag.CloseSize, "/size" },
        { RichTextTag.CloseVOffset, "/voffset" },
        { RichTextTag.CloseLineHeight, "/line-height" },
        { RichTextTag.CloseAlign, "/align" },
    };

    // TODO: rewrite all of this

    /// <summary>
    /// Parses an <see cref="Element"/> with the given text.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="element">The <see cref="Element"/> to use for settings.</param>
    /// <returns>A <see cref="ParsedData"/> representing the parsed version of the element.</returns>
    [SkipLocalsInit]
    public static ParsedData Parse(string text, Element element)
    {
        Reset();

        Parser.text = text;

        noparseParsesEscapeSeq = element.NoparseSettings.HasFlagFast(Elements.Enums.NoparseSettings.ParsesEscapeSequences);
        noparseParsesFormat = element.NoparseSettings.HasFlagFast(Elements.Enums.NoparseSettings.ParsesFormatItems);
        resolutionAlign = element.ResolutionBasedAlign;
        currentParameters = element.Parameters ?? Array.Empty<ContentParameter>();

        while (TryGetNext(out char ch))
        {
            HandleChar(ch);
        }

        if (alignment == AlignStyle.Right)
        {
            Modifications.Add(new AlignSpaceModification(false, position));
        }

        return new ParsedData(text, Offset.Clone(), Modifications.ToList());
    }

    private static void Reset()
    {
        SizeStack.Clear();
        Offset.Clear();

        position = -1;

        lineHeight = AnimatableFloat.Invalid;

        Modifications.Clear();

        alignment = NoAlignStyle;
    }

    private static void HandleChar(char ch)
    {
        if (TryParseFormatItem(true, out int _))
        {
            return;
        }

        switch (ch)
        {
            case '<':
                if (!TryParseTag())
                {
                    position = lastPosition;
                }

                break;
            case '\n':
                AddLinebreak();
                break;
        }
    }

    private static bool TryParseTag()
    {
        // TODO: check this code
        // keep track of count to ensure that our total size is less than Constants.MaxTagSize
        int count = 1;
        int start = position;

        Trie<RichTextTag>.RadixNode? node = TagTrie.Root;

        while (TryGetNext(out char ch))
        {
            if (!node.TryGetNode(TagHelpers.ToLowercaseFast(ch), out node))
            {
                break;
            }

            lastPosition = position; // no need for backtracking
            count++;
            RichTextTag tag = node.Value;

            if (tag != default)
            {
                // quick check to see if tag doesn't take in a parameter
                if (tag >= RichTextTag.Noparse)
                {
                    if (noparse && tag != RichTextTag.CloseNoparse)
                    {
                        break;
                    }

                    lastPosition = position;

                    // TODO: check this
                    if (!TryGetNext(out ch))
                    {
                        BreakTag();
                        return false;
                    }

                    switch (ch)
                    {
                        case ' ': // space works identical to = for tags that don't take a parameter
                        case '=':
                            if (tag == RichTextTag.Noparse) // if we have something like <noparse=...>, inside ... noparse is technically on
                            {
                                noparse = true;
                            }

                            while (TryGetNext(out ch))
                            {
                                if (count > Constants.MaxTagLength)
                                {
                                    BreakTag();

                                    return false;
                                }
                                else if (ch == '>')
                                {
                                    goto case '>';
                                }
                                else if (text[position] == '<' || TryParseFormatItem(true, out int _))
                                {
                                    BreakTag();

                                    return false;
                                }
                            }

                            if (tag == RichTextTag.Noparse)
                            {
                                noparse = false;
                            }

                            return false;
                        case '>':
                            switch (tag)
                            {
                                case RichTextTag.CloseSize:
                                    SizeStack.TryPop(out _);
                                    break;
                                case RichTextTag.CloseVOffset:
                                    break;
                                case RichTextTag.CloseLineHeight:
                                    lineHeight = AnimatableFloat.Invalid;
                                    break;
                                case RichTextTag.Noparse:
                                    noparse = true;
                                    break;
                                case RichTextTag.CloseNoparse:
                                    noparse = false;
                                    break;
                                case RichTextTag.CloseAlign:
                                    if (alignment == AlignStyle.Right)
                                    {
                                        Modifications.Add(new AlignSpaceModification(false, start));
                                    }

                                    alignment = NoAlignStyle;
                                    break;
                                default:
                                    break;
                            }

                            return true;
                        default:
                            BreakTag();
                            position = lastPosition;

                            return false;
                    }
                }
                else
                {
                    if (!TryGetNext(out char cur) || cur != '=')
                    {
                        // we've landed on a valid tag that takes a parameter,
                        // but the next char isn't = (e.g. <line-height> or <line-height!
                        // don't call HandleChar for the same reason we don't call HandleChar for <<<<<<
                        break;
                    }

                    if (tag == RichTextTag.Align)
                    {
                        if (!resolutionAlign)
                        {
                            lastPosition = position - 1;

                            return false;
                        }

                        lastPosition = position;

                        if (TryParseAlign(out AlignStyle align) && TryGetNext(out char terminator) && terminator == '>')
                        {
                            if (alignment != AlignStyle.Left && align == AlignStyle.Left) // right needs immediate padding
                            {
                                int newPosition = position + 1;

                                Modifications.Add(new AlignSpaceModification(true, newPosition));
                            }

                            alignment = align;
                        }

                        position = lastPosition;

                        return true;
                    }

                    // we never need to backtrack
                    if (TryParseMeasurements(ref count, out MeasurementInfo info))
                    {
                        AnimatableFloat value = default;

                        switch (tag)
                        {
                            case RichTextTag.LineHeight:
                                lineHeight = value = info.ToAnimatableFloat(Constants.DefaultLineHeight);
                                break;
                            case RichTextTag.Size:
                                float add = info.AddType != MeasurementInfo.AdditionType.Default && info.Unit == MeasurementUnit.Pixels ? Constants.EmSize : 0;
                                value = info.ToAnimatableFloat(Constants.EmSize, add);

                                SizeStack.Push(value);

                                break;
                            case RichTextTag.VOffset:
                                // TODO: support voffset
                                BreakTag();
                                return false;
                        }

                        int length = position - start + 1;

                        if (value.IsAnimated)
                        {
                            Modifications.Add(new AnimatedTagModification(start, length, tag, in value)); // TODO: check for off by one errors
                        }
                        else
                        {
                            Modifications.Add(new TagModification(start, length, tag, value.AddendOrValue)); // TODO: check for off by one errors
                        }

                        return true;
                    }

                    BreakTag();
                    break;
                }
            }
        }

        // position - 1 since TryGetNext increases position by 1
        lastPosition = position - 1;

        return false;
    }

    /// <summary>
    /// Tries to parse a format item.
    /// </summary>
    /// <remarks>
    /// If <see langword="false"/>, there is no format item. If <see langword="true"/>, <paramref name="num"/> is either
    /// the ID of the format item or <c>-1</c>, indicating that two characters were processed.
    /// </remarks>
    private static bool TryParseFormatItem(bool addItem, out int num)
    {
        // TODO: make sure this works with lastPosition
        // because format items are not handled by TMP, we get the raw character
        // rather than using TryGetNext, which uses escape sequences
        char cur = text[position];

        if (noparse && !noparseParsesFormat)
        {
            switch (cur)
            {
                case '{':
                    Modifications.Add(new CharModification(position, '{', true));
                    break;
                case '}':
                    Modifications.Add(new CharModification(position, '}', true));
                    break;
                default:
                    Unsafe.SkipInit(out num);
                    return false;
            }

            num = -1;
            return true;
        }

        // TODO: handle singular { or }
        if (cur == '{')
        {
            const int MaxNumLength = 5;

            if (MoveNext())
            {
                if (text[position] == '{')
                {
                    Modifications.Add(new NobreakModification(position, 2));

                    num = -1;

                    return true;
                }
            }
            else
            {
                num = 0;

                return false;
            }

            int start = position;
            int stopAt = Math.Min(text.Length, position + MaxNumLength);

            while (position < stopAt)
            {
                if (text[position] == '}')
                {
                    int length = position - start;

                    // {123}
                    // ^^^^^
                    // 01234
                    // we start at 1, end at 4, length = 3
                    if (int.TryParse(text.AsSpan(start, length), NumberStyles.None, null, out int result))
                    {
                        if (result >= currentParameters.Count)
                        {
                            if (addItem)
                            {
                                Modifications.Add(new InvalidFormatItemModification(start - 1, length + 2)); // TODO: check for potential off by one errors
                            }

                            // doesn't really matter what we do here
                            Unsafe.SkipInit(out num);
                            return false;
                        }

                        if (addItem)
                        {
                            Modifications.Add(new FormatItemModification(start - 1, length + 2, result)); // TODO: check for potential off by one errors
                        }

                        num = result;

                        return true;
                    }

                    Unsafe.SkipInit(out num);
                    return false;
                }

                position++;
            }
        }
        else if (cur == '}' && MoveNext() && text[position] == '}')
        {
            Modifications.Add(new NobreakModification(position - 1, 2));
            num = -1;

            return true;
        }

        Unsafe.SkipInit(out num);
        return false;
    }

    private static bool TryParseMeasurements(ref int count, out MeasurementInfo info)
    {
        // TODO: support weird leading tag shit
        // TODO: break if too many spaces, support otehr weird space shit
        const MeasurementUnit NotSet = (MeasurementUnit)(-1);

        MeasurementUnit unit = NotSet;

        MeasurementInfo.AdditionType type;

        int numDigits = 0;
        int decimalPoint = -1;

        bool comma = false;

        int paramId = -1;

        char ch;

        bool MoveNextMeasurement(ref int count)
        {
            if (TryGetNext(out ch) && ++count <= Constants.MaxTagLength) // TODO: check
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        if (MoveNextMeasurement(ref count))
        {
            type = ch switch
            {
                '-' => MeasurementInfo.AdditionType.SubtractiveOrNegative,
                '+' => MeasurementInfo.AdditionType.Additive,
                _ when TagHelpers.IsDigitFast(ch) => MeasurementInfo.AdditionType.Default,
                _ => MeasurementInfo.AdditionType.Absolute,
            };
        }
        else
        {
            // TODO: fix
            info = default;

            return false;
        }

        do
        {
            switch (ch)
            {
                case '.' when decimalPoint == -1:
                    decimalPoint = numDigits;
                    break;
                case '<':
                    goto EndLoop;
                case ',': // for some reason the comma has really weird behavior
                    comma = true;
                    break;
                case '>':
                    if (paramId == -1)
                    {
                        float value = decimalPoint == -1
                            ? TagHelpers.FromIntegerAndDecimal(CharBuffer[..numDigits], ReadOnlySpan<char>.Empty)
                            : TagHelpers.FromIntegerAndDecimal(CharBuffer[..decimalPoint], CharBuffer[decimalPoint..numDigits]);

                        if (value > Constants.MaxValueSize)
                        {
                            info = default;

                            return false;
                        }

                        info = new()
                        {
                            Unit = unit == NotSet ? MeasurementUnit.Pixels : unit,
                            AddType = type,
                            Value = value,
                        };
                    }
                    else
                    {
                        if (currentParameters[paramId] is not AnimatedParameter animated
                            || animated.Format != null // no format is allowed for tags
                            || animated.RoundToInt
                            || animated.Value.Any(x => x.value > Constants.MaxValueSize / 2))
                        {
                            info = default;
                            return false;
                        }

                        info = new()
                        {
                            Unit = unit == NotSet ? MeasurementUnit.Pixels : unit,
                            AddType = type,
                            Parameter = animated,
                            Value = 0,
                        };
                    }

                    return true;
                default:
                    if (TryParseFormatItem(false, out int num))
                    {
                        goto EndLoop; // TODO: add support for this

#pragma warning disable CS0162 // Unreachable code detected
                        if (num == -1) // escape sequence, e.g. {{ -> add additional
                        {
                            count++;
                        }
                        else
                        {
                            // TODO: remove this huge ass check
                            if (paramId != -1 || unit != NotSet || decimalPoint != -1 || numDigits != -1 || comma)
                            {
                                goto EndLoop;
                            }

                            paramId = num;
                            decimalPoint = 0; // so we don't also have to check paramId
                        }
#pragma warning restore CS0162 // Unreachable code detected
                    }
                    else if (TagHelpers.IsDigitFast(ch) && paramId == -1 && !comma)
                    {
                        CharBuffer[numDigits++] = ch;
                    }
                    else if (unit == NotSet && type != MeasurementInfo.AdditionType.Absolute)
                    {
                        switch (ch)
                        {
                            case 'e':
                                unit = MeasurementUnit.Pixels;
                                break;
                            case '%':
                                unit = MeasurementUnit.Percentage;
                                break;
                            case ' ': // if there is no leading values, space breaks (no idea why)
                            case 'p':
                                unit = MeasurementUnit.Pixels;
                                break;
                        }
                    }

                    break;
            }
        }
        while (MoveNextMeasurement(ref count));

    EndLoop:
        BreakTag();

        info = default;

        return false;
    }

    private static bool MoveNext() => ++position < text.Length;

    private static bool TryGetNext(out char ch)
    {
        if (!MoveNext())
        {
            Unsafe.SkipInit(out ch);

            return false;
        }

        lastPosition = position;

        char cur = text[position];

        // we handle <br> tags and other similar tags here since they have special behavior (they act more like escape sequences)
        if (cur == '<' && !noparse)
        {
            Trie<char>.RadixNode? node = ReplacementTrie.Root;

            while (++position < text.Length)
            {
                char replacementCh = text[position];

                if (replacementCh is '=' or ' ' or '>')
                {
                    if (node.Value != default)
                    {
                        ch = node.Value;

                        return true;
                    }

                    break;
                }

                if (!node.TryGetNode(text[position], out node))
                {
                    break;
                }
            }

            position = lastPosition;

            ch = '<';
            return true;
        }

        if (cur != '\\')
        {
            ch = cur;

            return true;
        }

        if (noparse && !noparseParsesEscapeSeq)
        {
            // additional backslash to ensure the escape sequence isn't handled
            Modifications.Add(new CharModification(position, '\\', false)); // TODO: check for potential off by one errors

            ch = '\\';

            return true;
        }

        if (++position < text.Length)
        {
            switch (text[position])
            {
                case '\\': // escaped \, so just advance to next \
                    ch = '\\';
                    return true;
                case 'v': // vertical tab
                case 'n': // \n
                    ch = '\n';
                    return true;
                case 't':
                    ch = '\t';
                    return true;
                case 'r':
                    ch = '\r';
                    return true;
                case 'U': // TODO: handle \U
                    Modifications.Add(new CharModification(position, '\\', false)); // TODO: check for potential off by one errors
                    break;
                case 'u':
                    // TODO: rewrite to be less dumb
                    // newPos is currently at |u, move to u|
                    const int UnicodeLiteralSize = 4;

                    // TODO: check
                    int start = position;
                    position += 4;

                    if (position < text.Length)
                    {
                        if (int.TryParse(text.AsSpan(start, UnicodeLiteralSize), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int value))
                        {
                            // TODO: check
                            ch = (char)value;

                            return true;
                        }
                    }

                    break;
            }
        }

        // backtrack to lastPosition
        position = lastPosition;

        ch = '\\';

        return true;
    }

    private static bool TryParseAlign(out AlignStyle align)
    {
        Trie<AlignStyle>.RadixNode node = AlignTrie.Root;

        while (TryGetNext(out char ch) && node.TryGetNode(ch, out node))
        {
            if (node.Value != default)
            {
                align = node.Value;

                return true;
            }
        }

        align = default;
        return false;
    }

    private static void BreakTag()
    {
        if (noparse)
        {
            Modifications.Add(new CloseNoparseModification(lastPosition));
            Modifications.Add(new NoparseModification(lastPosition));
        }
        else
        {
            Modifications.Add(new CloseNoparseModification(lastPosition));
        }
    }

    private static void AddLinebreak()
    {
        static void AddAnimatable(int length, in AnimatableFloat animatableFloat)
        {
            if (animatableFloat.IsAnimated)
            {
                Modifications.Add(new AnimatedLinebreakModification(animatableFloat, lastPosition, length));
            }
            else
            {
                Modifications.Add(new RawLinebreakModification(animatableFloat.AddendOrValue, lastPosition, length));
            }

            Offset.Add(animatableFloat);
        }

        int length = position - lastPosition + 1;

        // add alignment before
        if (alignment == AlignStyle.Right)
        {
            Modifications.Add(new AlignSpaceModification(false, lastPosition));
        }

        if (!lineHeight.IsInvalid)
        {
            AddAnimatable(length, in lineHeight);

            return;
        }
        else if (SizeStack.TryPeek(out AnimatableFloat value))
        {
            // TODO: change this so this is done at SizeStack
            AddAnimatable(length, in value);

            return;
        }
        else
        {
            Modifications.Add(new RawLinebreakModification(Constants.DefaultLineHeight, lastPosition, length));
            Offset.Add(Constants.DefaultLineHeight);
        }

        if (alignment == AlignStyle.Left)
        {
            Modifications.Add(new AlignSpaceModification(false, lastPosition));
        }
    }

    private struct MeasurementInfo
    {
        public MeasurementUnit Unit;

        public AdditionType AddType;

        public float Value;

        public AnimatedParameter? Parameter;

        internal enum AdditionType
        {
            Default,
            Absolute,
            Additive,
            SubtractiveOrNegative,
        }

        public readonly AnimatableFloat ToAnimatableFloat(float pointSize, float add = 0)
        {
            float multi = this.AddType == AdditionType.SubtractiveOrNegative ? -1f : 1f;
            multi *= Constants.DefaultLineHeight / pointSize;

            if (add == 0 && this.Unit != MeasurementUnit.Pixels)
            {
                add = 0; // TODO: check this
            }

            if (this.Parameter == null)
            {
                return this.Unit switch
                {
                    MeasurementUnit.Percentage => new(((this.Value / 100 * pointSize) + add) * multi),
                    MeasurementUnit.Ems => new(((this.Value * Constants.EmSize) + add) * multi),
                    _ => new((this.Value + add) * multi),
                };
            }
            else
            {
                bool abs = this.AddType != AdditionType.Default;
                return this.Unit switch
                {
                    MeasurementUnit.Percentage => new(this.Parameter, add, pointSize / 100 * multi, abs),
                    MeasurementUnit.Ems => new(this.Parameter, add, Constants.EmSize * multi, abs),
                    _ => new(this.Parameter, add, multi, abs),
                };
            }
        }
    }
}