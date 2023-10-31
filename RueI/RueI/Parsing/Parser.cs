﻿namespace RueI
{
    using System.Collections.ObjectModel;
    using System.Runtime.CompilerServices;
    using System.Text;

    using NorthwoodLib.Pools;

    using RueI.Enums;
    using RueI.Parsing;
    using RueI.Parsing.Tags;
    using RueI.Records;

    /// <summary>
    /// Helps parse the content of elements.
    /// </summary>
    public class Parser
    {
        private readonly ParserNode baseNodes = new();
        private readonly List<RichTextTag> tags = new();

        /// <summary>
        /// Gets the current tags of the parser.
        /// </summary>
        public ReadOnlyCollection<RichTextTag> CurrentTags => tags.AsReadOnly();

        /// <summary>
        /// Adds a tag to the parser.
        /// </summary>
        /// <param name="tag">The tag to add.</param>
        public void AddTag(RichTextTag tag)
        {
            tags.Add(tag);
            Reassemble();
        }

        /// <summary>
        /// Parses a rich text string.
        /// </summary>
        /// <param name="text">The string to parse.</param>
        /// <returns>A <see cref="ParsedData"/> containing information about the string.</returns>
        public ParsedData Parse(string text)
        {
            ReadOnlyDictionary<char, float> charSizes = Constants.CharacterLengths;

            ParserState currentState = ParserState.CollectingTags;
            ParserNode? currentNode = null;
            StringBuilder tagBuffer = StringBuilderPool.Shared.Rent();

            ParamProcessor? paramProcessor = null;

            StringBuilder sb = StringBuilderPool.Shared.Rent();
            HashSet<string> tagsToEnd = new();

            bool shouldParse = false;
            float currentHeight = Constants.DEFAULTHEIGHT; // in pixels
            float size = Constants.DEFAULTSIZE;
            float newOffset = 0;
            float currentLineWidth = 0;
            float widthSinceSpace = 0;

            float currentCSpace = 0;
            bool isMonospace = false;
            bool isBold = false;
            bool noBreak = false;

            Stack<float> sizeStack = new();
            int colorTags = 0;

            CaseStyle currentCase = CaseStyle.Smallcaps;

            void AddCharacter(char ch)
            {
                char functionalCase = currentCase switch
                {
                    CaseStyle.Smallcaps or CaseStyle.Uppercase => char.ToUpper(ch),
                    CaseStyle.Lowercase => char.ToLower(ch),
                    _ => ch
                };

                if (charSizes.TryGetValue(functionalCase, out float chSize))
                {
                    float multiplier = size / 35;
                    if (currentCase == CaseStyle.Smallcaps && char.IsLower(ch))
                    {
                        multiplier *= 0.8f;
                    }

                    if (isMonospace && currentLineWidth != 0)
                    {
                        widthSinceSpace += currentCSpace;
                    }
                    else
                    {
                        widthSinceSpace += (chSize * multiplier) + currentCSpace;
                        if (isBold)
                        {
                            widthSinceSpace += Constants.BOLDINCREASE * multiplier;
                        }
                    }

                    sb.Append(ch);

                    if (currentLineWidth + widthSinceSpace > Constants.DISPLAYAREAWIDTH)
                    {
                        CreateLineBreak();
                    }
                    else if (ch == ' ')
                    {
                        if (!noBreak)
                        {
                            widthSinceSpace = 0;
                        }
                    }
                }
                else
                {
                    // TODO: handle warnings
                }
            }

            void FailTagMatch() // not a tag, unload buffer
            {
                sb.Append("<​"); // zero width space guarantees that the tag isnt matched
                foreach (char ch in tagBuffer.ToString())
                {
                    AddCharacter(ch);
                }   

                tagBuffer.Clear();

                currentState = ParserState.CollectingTags;
                paramProcessor = null;
            }

            void FailTagMatchParams(string paramBuffer)
            {
                FailTagMatch();

                foreach (char ch in paramBuffer)
                {
                    AddCharacter(ch);
                }
            }

            void CreateLineBreak()
            {
                if (widthSinceSpace < Constants.DISPLAYAREAWIDTH)
                {
                    currentLineWidth = widthSinceSpace;
                    newOffset += currentHeight;
                }
                else
                {
                    currentLineWidth = 0;
                }
            }

            ParserContext GenerateContext()
            {
                return new(sb, currentHeight, currentLineWidth, size, newOffset, currentCSpace, shouldParse, isMonospace, isBold, currentCase, sizeStack, colorTags);
            }

            void LoadContext(ParserContext context)
            {
                (_, currentHeight, currentLineWidth, size, newOffset, currentCSpace, shouldParse, isMonospace, isBold, currentCase, _, colorTags) = context;
            }

            foreach (char ch in text)
            {
                if (ch == '<')
                {
                    currentState = ParserState.DescendingTag;
                    currentNode = baseNodes;
                    continue; // do NOT add as a character
                }
                else if (ch == '\n')
                {
                    sb.Append('\n');
                    if (currentState != ParserState.CollectingTags)
                    {
                        FailTagMatch();
                    }

                    continue; // do NOT add as a character
                }
                else if (currentState == ParserState.DescendingTag)
                {
                    if ((ch > '\u0060' && ch < '\u007B') || ch == '-') // descend deeper into node
                    {
                        if (currentNode?.Branches?.TryGetValue(ch, out ParserNode node) == true)
                        {
                            currentNode = node;
                            tagBuffer.Append(ch);
                            continue; // do NOT add as a character
                        }
                        else
                        {
                            FailTagMatch();
                        }
                    }
                    else if (ch == '=')
                    {
                        if (currentNode?.Tag != null)
                        {
                            if (ch == '=' && currentNode.Tag.TryGetNewProcessor(out ParamProcessor? processor))
                            {
                                paramProcessor = processor;

                                tagBuffer.Append('=');
                                continue; // do NOT add as a character
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
                    else if (ch == '>')
                    {
                        if (currentNode?.Tag != null)
                        {
                            if (currentNode.Tag is NoParamsTagBase noParams)
                            {
                                ParserContext context = GenerateContext();
                                noParams.HandleTag(context);
                                LoadContext(context);

                                tagBuffer.Clear();
                                continue; // do NOT add as a character
                            }
                            else if (currentState == ParserState.CollectingColorParams && paramProcessor != null)
                            {
                                bool wasSuccessful = paramProcessor.GetFinishResult(GenerateContext, LoadContext, out string? unloaded);
                                if (!wasSuccessful)
                                {
                                    FailTagMatchParams(unloaded!);
                                }
                            }
                            else
                            {
                                FailTagMatch();
                            }
                        }
                    }
                    else
                    {
                        FailTagMatch();
                    }
                }
                else if (currentState == ParserState.CollectingMeasureParams)
                {
                    paramProcessor?.Add(ch);
                    continue; // do NOT add as a character
                }

                AddCharacter(ch);
            }

            StringBuilderPool.Shared.Return(tagBuffer);

            return new ParsedData(StringBuilderPool.Shared.ToStringReturn(sb), newOffset);
        }

        /// <summary>
        /// Calculates the length of an <see cref="IEnumerable{T}"/> containing characters.
        /// </summary>
        /// <param name="chars">The characters to calculate the length for.</param>
        /// <param name="context">The context to parse the string under.</param>
        /// <returns>A float indicating the total length of the characters.</returns>
        public float CalculateCharacterLength(IEnumerable<char> chars, ParserContext context)
        {
            float buffer = 0;
            foreach (char ch in chars)
            {
                if (Constants.CharacterLengths.TryGetValue(ch, out float value))
                {

                }
            }

            return buffer;
        }

        private void Reassemble()
        {
            //  baseNodes.Branches.Clear();

            foreach (RichTextTag tag in tags)
            {
                foreach (string name in tag.Names)
                {
                    ParserNode currentNode = baseNodes;

                    foreach (char ch in name)
                    {
                        if (!currentNode.Branches.TryGetValue(ch, out ParserNode node))
                        {
                            node = new ParserNode();
                            currentNode.Branches.Add(ch, node);
                        }

                        currentNode = node;
                    }

                    currentNode.Tag = tag;
                }
            }
        }

        internal float CalculateCharacterLength(char ch, ParserContext context)
        {
            float multiplier = context.Size;
            if (Constants.CharacterLengths.TryGetValue(ch, out float value))
            {
                return 1;
            } else
            {
                return 0;
            }
        }
    }
}
