﻿namespace RueI.Parsing;

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using NorthwoodLib.Pools;

using RueI.Parsing.Enums;
using RueI.Parsing.Records;

/// <summary>
/// Helps parse the content of elements.
/// </summary>
/// <include file='docs.xml' path='docs/members[@name="parser"]/Parser/*'/>
public class Parser
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Parser"/> class.
    /// </summary>
    /// <param name="tags">The list of tags to initialize with.</param>
    internal Parser(IEnumerable<RichTextTag> tags)
    {
        Dictionary<string, List<RichTextTag>> tagBuffer = new();

        foreach (RichTextTag tag in tags)
        {
            foreach (string name in tag.Names)
            {
                if (tagBuffer.TryGetValue(name, out List<RichTextTag> tagsOfName))
                {
                    tagsOfName.Add(tag);
                }
                else
                {
                    List<RichTextTag> richTextTags = new();
                    tagBuffer.Add(name, richTextTags);
                    richTextTags.Add(tag);
                }
            }
        }

        Dictionary<string, ReadOnlyCollection<RichTextTag>> dictionary = tagBuffer.ToDictionary(kv => kv.Key, kv => new ReadOnlyCollection<RichTextTag>(kv.Value));
        Tags = new(dictionary);
    }

    /// <summary>
    /// Gets the default <see cref="Parser"/>.
    /// </summary>
    public static Parser DefaultParser { get; } = new ParserBuilder().AddFromAssembly(typeof(Parser).Assembly).Build();

    /// <summary>
    /// Gets the tags that will be searched for when parsing.
    /// </summary>
    /// <remarks>
    /// Multiple tags can share the same name.
    /// </remarks>
    public ReadOnlyDictionary<string, ReadOnlyCollection<RichTextTag>> Tags { get; }

    /// <summary>
    /// Adds a character to a parser context.
    /// </summary>
    /// <param name="context">The context of the parser.</param>
    /// <param name="ch">The character to add.</param>
    public static void AddCharacter(ParserContext context, char ch)
    {
        float size = CalculateCharacterLength(context, ch);

        context.ResultBuilder.Append(ch);

        // TODO: any chars
        if (ch == ' ' || ch == '​') // zero width space
        {
            context.CurrentLineWidth += size;

            if (!context.NoBreak)
            {
                context.CurrentLineWidth += context.WidthSinceSpace;
                context.WidthSinceSpace = 0;
            }
        }
        else
        {
            if (context.CurrentLineWidth + context.WidthSinceSpace > context.DisplayAreaWidth)
            {
                CreateLineBreak(context);
            }

            context.WidthSinceSpace += size;
            if (context.Size > context.BiggestCharSize)
            {
                context.BiggestCharSize = context.Size;
            }
        }
    }

    /// <summary>
    /// Calculates the length of an <see cref="char"/> with a context.
    /// </summary>
    /// <param name="context">The context to parse the char under.</param>
    /// <param name="ch">The char to calculate the length for.</param>
    /// <returns>A float indicating the total length of the char.</returns>
    public static float CalculateCharacterLength(TextInfo context, char ch)
    {
        char functionalCase = context.CurrentCase switch
        {
            CaseStyle.Smallcaps or CaseStyle.Uppercase => char.ToUpper(ch),
            CaseStyle.Lowercase => char.ToLower(ch),
            _ => ch
        };

        if (context.IsMonospace)
        {
            return context.Monospacing + context.CurrentCSpace;
        }

        if (CharacterLengths.Lengths.TryGetValue(functionalCase, out float chSize))
        {
            float multiplier = context.Size / Constants.DEFAULTSIZE;
            if (context.CurrentCase == CaseStyle.Smallcaps && char.IsLower(ch))
            {
                multiplier *= 0.8f;
            }

            if (context.IsSuperOrSubScript)
            {
                multiplier *= 0.5f;
            }

            return chSize * multiplier;
        }
        else
        {
            // TODO: handle warnings
            return default;
        }
    }

    /// <summary>
    /// Generates the effects of a linebreak for a parser.
    /// </summary>
    /// <param name="context">The context of the parser.</param>
    public static void CreateLineBreak(ParserContext context)
    {
        if (context.WidthSinceSpace > Constants.DISPLAYAREAWIDTH)
        {
            context.CurrentLineWidth = 0;
        }
        else
        {
            context.CurrentLineWidth = context.WidthSinceSpace;
        }

        if (context.WidthSinceSpace > 0 || context.CurrentLineWidth > 0)
        {
            context.NewOffset += ((context.BiggestCharSize / Constants.DEFAULTSIZE * Constants.DEFAULTHEIGHT) - Constants.DEFAULTHEIGHT) / 2;
        }

        context.BiggestCharSize = 0;
        context.NewOffset += context.CurrentLineHeight;
        context.CurrentLineWidth += context.Indent;
    }

    /// <summary>
    /// Parses the tag attributes of a string.
    /// </summary>
    /// <param name="content">The content to parse.</param>
    /// <param name="attributes">The pairs of attributes.</param>
    /// <returns>true if the content is valid, otherwise false.</returns>,
    public static bool GetTagAttributes(string content, out Dictionary<string, string> attributes)
    {
        IEnumerable<string> result = content.Split('"')
                        .Select((element, index) => index % 2 == 0
                           ? element.Split(' ')
                           : new string[] { element })
                        .SelectMany(element => element);

        Dictionary<string, string> attributePairs = new();
        attributes = attributePairs;

        foreach (string possiblePair in result)
        {
            if (possiblePair == string.Empty)
            {
                return false;
            }

            string[] results = possiblePair.Split('=');

            if (results.Length != 2)
            {
                return false;
            }

            attributePairs.Add(results[0], results[1]);
        }

        return true;
    }

    /// <summary>
    /// Parses a rich text string.
    /// </summary>
    /// <param name="text">The string to parse.</param>
    /// <returns>A <see cref="ParsedData"/> containing information about the string.</returns>
    public ParsedData Parse(string text)
    {
        ParserState currentState = ParserState.CollectingTags;

        StringBuilder tagBuffer = StringBuilderPool.Shared.Rent(Constants.MAXTAGNAMESIZE);
        int tagBufferSize = 0;

        RichTextTag? currentTag = null;
        char? delimiter = null;

        StringBuilder paramBuffer = StringBuilderPool.Shared.Rent(30);

        using ParserContext context = new();

        void FailTagMatch() // not a tag, unload buffer
        {
            AddCharacter(context, '<');

            AvoidMatch(context);
            foreach (char ch in tagBuffer.ToString())
            {
                AddCharacter(context, ch);
            }

            foreach (char ch in paramBuffer.ToString())
            {
                AddCharacter(context, ch);
            }

            if (delimiter != null)
            {
                AddCharacter(context, delimiter.Value);
                delimiter = null;
            }

            tagBuffer.Clear();
            paramBuffer.Clear();

            currentTag = null;
            currentState = ParserState.CollectingTags;
            tagBufferSize = 0;
        }

        foreach (char ch in text)
        {
            if (ch == '<')
            {
                currentState = ParserState.DescendingTag;
                continue; // do NOT add as a character
            }
            else if (ch == '\n')
            {
                context.ResultBuilder.Append('\n');
                context.WidthSinceSpace = 0;
                CreateLineBreak(context);
                if (currentState != ParserState.CollectingTags)
                {
                    FailTagMatch();
                }

                continue; // do NOT add as a character
            }
            else if (currentState == ParserState.DescendingTag)
            {
                if ((ch > '\u0060' && ch < '\u007B') || ch == '-' || ch == '\\')
                {
                    if (tagBufferSize > Constants.MAXTAGNAMESIZE)
                    {
                        FailTagMatch();
                    }

                    tagBuffer.Append(ch);
                    continue; // do NOT add as a character
                }
                else if (ch == '>')
                {
                    if (TryGetBestMatch(tagBuffer.ToString(), TagStyle.NoParams, out RichTextTag? tag))
                    {
                        tag!.HandleTag(context, string.Empty);
                        continue;
                    }
                    else
                    {
                        FailTagMatch();
                    }
                }
                else if (ch == ' ' || ch == '=')
                {
                    TagStyle style = ch switch
                    {
                        ' ' => TagStyle.Attributes,
                        '=' => TagStyle.ValueParam,
                        _ => throw new ArgumentOutOfRangeException(nameof(ch)),
                    };

                    if (TryGetBestMatch(tagBuffer.ToString(), style, out RichTextTag? tag))
                    {
                        currentTag = tag;
                        delimiter = ch;

                        currentState = ParserState.CollectingParams;
                        continue;
                    }
                    else
                    {
                        FailTagMatch();
                    }
                }
                else
                {
                    FailTagMatch();
                }
            }
            else if (currentState == ParserState.CollectingParams)
            {
                if (ch == '>')
                {
                    if (currentTag!.HandleTag(context, paramBuffer.ToString()))
                    {
                        tagBuffer.Clear();
                        paramBuffer.Clear();

                        currentTag = null;
                        delimiter = null;
                        currentState = ParserState.CollectingTags;
                        tagBufferSize = 0;
                    }
                    else
                    {
                        FailTagMatch();
                    }
                }

                paramBuffer.Append(ch);
                continue; // do NOT add as a character
            }

            AddCharacter(context, ch);
        } // foreach

        context.ApplyClosingTags();
        if (context.WidthSinceSpace > 0 || context.CurrentLineWidth > 0)
        {
            context.NewOffset += context.BiggestCharSize - Constants.DEFAULTSIZE;
        }

        StringBuilderPool.Shared.Return(tagBuffer);
        StringBuilderPool.Shared.Return(paramBuffer);
        return new ParsedData(context.ResultBuilder.ToString(), context.NewOffset);
    }

    /// <summary>
    /// Exports this parser's <see cref="RichTextTag"/>s to a <see cref="ParserBuilder"/>.
    /// </summary>
    /// <param name="builder">The builder to export the tags to.</param>
    internal void ExportTo(ParserBuilder builder) => builder.AddTags(Tags.SelectMany(x => x.Value).Distinct());

    /// <summary>
    /// Avoids the client TMP matching a tag.
    /// </summary>
    /// <param name="context">The context of the parser.</param>
    private static void AvoidMatch(ParserContext context)
    {
        if (!context.IsMonospace && context.CurrentCSpace == 0)
        {
            context.ResultBuilder.Append('​'); // zero width space
        }
        else if (context.IsBold)
        {
            context.ResultBuilder.Append("<b>");
        }
        else
        {
            context.ResultBuilder.Append("</b>");
        }
    }

    private bool TryGetBestMatch(string name, TagStyle style, out RichTextTag? tag)
    {
        tag = null;

        if (Tags.TryGetValue(name, out ReadOnlyCollection<RichTextTag> filteredTags))
        {
            RichTextTag? chosenTag = filteredTags.FirstOrDefault(x => x.TagStyle == style);
            if (chosenTag != null)
            {
                tag = chosenTag;
                return true;
            }
        }

        return false;
    }

    private bool TryGetBestMatch(IEnumerable<RichTextTag> tags, TagStyle style, out RichTextTag? tag)
    {
        tag = null;

        RichTextTag? chosenTag = tags.FirstOrDefault(x => x.TagStyle == style);
        if (chosenTag != null)
        {
            tag = chosenTag;
            return true;
        }

        return false;
    }
}
