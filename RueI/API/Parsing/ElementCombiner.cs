namespace RueI.API.Parsing;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using global::Utils.Networking;
using Hints;
using LabApi.Features.Console;
using Mirror;

using RueI.API.Elements;
using RueI.API.Elements.Parameters;
using RueI.API.Parsing.Modifications;
using RueI.API.Parsing.Structs;
using RueI.Utils;
using RueI.Utils.Extensions;

/// <summary>
/// Combines multiple <see cref="Element"/>s into a single <see cref="TextHint"/>
/// and sends it to a <see cref="ReferenceHub"/>.
/// </summary>
internal static class ElementCombiner
{
    private const ushort MaxStringLength = ushort.MaxValue - 2;

    private const int TextHintID = 1;

    private static readonly CumulativeFloat CumulativeOffset = new();
    private static readonly CumulativeFloat SubOffset = new();
    private static readonly List<NobreakInfo> Nobreaks = new();

    private static readonly AlphaCurveHintEffect AlphaCurveHint = new(UnityEngine.AnimationCurve.Constant(0, 999999, 1));

    private static readonly ParameterHandler ParameterHandler = new(Nobreaks);

    /// <summary>
    /// Combines an <see cref="Element"/> <see cref="IEnumerable{T}"/> and sends it to
    /// a <see cref="ReferenceHub"/>.
    /// </summary>
    /// <param name="hub">The <see cref="ReferenceHub"/> to combine the <see cref="Element"/>s for and
    /// to send the <see cref="TextHint"/> to.</param>
    /// <param name="elements">The elements to combine.</param>
    internal static void Combine(ReferenceHub hub, IEnumerable<Element> elements)
    {
        CumulativeOffset.Clear();
        Nobreaks.Clear();

        using NetworkWriterPooled totalWriter = NetworkWriterPool.Get();

        totalWriter.WriteUShort(NetworkMessageId<HintMessage>.Id);
        totalWriter.WriteByte(TextHintID);
        totalWriter.WriteFloat(999999); // TODO: calculate length

        // write a constant alpha curve as an attempted fix for a bug
        // where transparency of hints is reduced
        // TODO: remove if this doesn't work
        totalWriter.WriteInt(1);
        totalWriter.WriteHintEffect(AlphaCurveHint);

        ParameterHandler.Setup(totalWriter);

        using NetworkWriterPooled contentWriter = NetworkWriterPool.Get();

        // prevent any overflows from line breaking
        contentWriter.WriteStringNoSize("<line-height=0>");

        CumulativeFloat? lastPosition = null;
        CumulativeFloat? lastOffset = null;

        CombinerContext context = new(hub, contentWriter, Nobreaks, ParameterHandler);

        foreach (Element element in elements)
        {
            if (element.Parameters != null)
            {
                ParameterHandler.SetElementParameters(element.Parameters);
            }

            ParsedData data;
            try
            {
                data = element.GetParsedData(hub);
            }
            catch (Exception e)
            {
                Logger.Error($"Error when trying to get the ParsedData of element, skipping: {e}");

                continue;
            }

            string text = data.ParsedString;

            CumulativeFloat position;

            if (element.AnimatedPosition != null)
            {
                AnimatedParameter parameter = new(element.AnimatedPosition.Value);

                position = new();
                position.Add(PositionUtils.ScaledToBaselineParameter(parameter));
            }
            else
            {
                position = new(PositionUtils.ScaledToBaseline(element.VerticalPosition));
            }

            switch (element.VerticalAlign)
            {
                case Elements.Enums.VerticalAlign.Up:
                    position.Subtract(data.Offset);
                    position.Subtract(data.Offset);
                    break;
                case Elements.Enums.VerticalAlign.Center:
                    position.Subtract(data.Offset);
                    break;
                default:
                    break;
            }

            if (lastPosition == null)
            {
                CumulativeOffset.Add(position);
            }
            else
            {
                CalculateOffset(lastPosition, lastOffset!, position);

                SubOffset.WriteAsLineHeight(contentWriter, ParameterHandler, Nobreaks);

                CumulativeOffset.Add(SubOffset);
            }

            // write text interspersed with modifications
            ReadOnlySpan<char> buffer = text.AsSpan();

            for (int i = 0; i < data.Modifications.Count; i++)
            {
                Modification current = data.Modifications[i];
                int mapped = current.Position - (text.Length - buffer.Length);

                contentWriter.WriteChars(SplitUntil(ref buffer, mapped));

                current.Apply(context, ref buffer);
            }

            // write the remaining characters
            contentWriter.WriteChars(buffer);

            CumulativeOffset.Add(data.Offset);
            lastPosition = position;
            lastOffset = data.Offset;
        } // foreach (Element element in elements)

        // ensure that trailing newlines (e.g. hello\n\n) are triggered by writing
        // a single period
        contentWriter.WriteStringNoSize("<line-height=0>\n<alpha=#00><scale=0>.");

        using NetworkWriterPooled offsetWriter = NetworkWriterPool.Get();

        CumulativeOffset.WriteAsLineHeight(offsetWriter, ParameterHandler, Nobreaks);

        int length = contentWriter.Position + offsetWriter.Position;
        if (contentWriter.Position + offsetWriter.Position > MaxStringLength)
        {
            using NetworkWriterPooled contentWithParameterWriter = NetworkWriterPool.Get();

            contentWithParameterWriter.WriteNetworkWriter(offsetWriter, false);

            int pos = 0;
            int i = 0;

            do
            {
                // TODO: rewrite this to be slightly less dumb
                if (i < Nobreaks.Count)
                {
                    // get how much we can write before we encounter a linebreak
                    NobreakInfo nobreak = Nobreaks[i];
                    int start = nobreak.Start;
                    int maxWritable = start - pos;

                    // if false, the start of the nobreak is greater than what we can write in one pass
                    // if that's the case, we ignore it for now
                    // otherwise, just write it as usual
                    if (maxWritable < MaxStringLength)
                    {
                        int subIndex = ParameterHandler.AddStringParameter(contentWriter.buffer.AsSpan(pos, maxWritable));

                        contentWithParameterWriter.WriteFormatItem(subIndex);

                        // write the text that we can't break during
                        contentWithParameterWriter.WriteBytes(contentWriter.buffer.AsSpan(start, nobreak.Length), false);

                        i++;
                        pos = nobreak.Start + nobreak.Length;

                        continue;
                    }
                }

                int cap = Math.Min(length - pos, MaxStringLength);

                int index = ParameterHandler.AddStringParameter(contentWriter.buffer.AsSpan(pos, cap));
                contentWithParameterWriter.WriteFormatItem(index);

                pos += cap;
            }
            while (length > pos); // write while there is still stuff left

            ParameterHandler.Finish();

            totalWriter.WriteNetworkWriter(contentWithParameterWriter, true); // write WITH size
        }
        else
        {
            ParameterHandler.Finish();

            totalWriter.WriteUShort(checked((ushort)(length + 1))); // write size (of string)
            totalWriter.WriteNetworkWriter(offsetWriter, false);
            totalWriter.WriteNetworkWriter(contentWriter, false);
        }

#if DEBUG
        if (contentWriter.Position < 2000)
        {
            string offset = new(Encoding.UTF8.GetChars(offsetWriter.buffer, 0, offsetWriter.Position));
            string content = new(Encoding.UTF8.GetChars(contentWriter.buffer, 0, contentWriter.Position));

            LabApi.Features.Console.Logger.Debug($"Text: {offset + content}");
        }

#endif

        hub.networkIdentity.connectionToClient.Send(totalWriter.ToArraySegment());
    }

    /// <summary>
    /// Calculates the offset for two elements and places the result in <see cref="SubOffset"/>.
    /// </summary>
    private static void CalculateOffset(CumulativeFloat hintOnePos, CumulativeFloat hintOneTotalLines, CumulativeFloat hintTwoPos)
    {
        SubOffset.Clear();

        SubOffset.Add(hintOneTotalLines);
        SubOffset.Multiply(2f);
        SubOffset.Add(hintOnePos);
        SubOffset.Subtract(hintTwoPos);

        SubOffset.Divide(-2f);
    }

    private static ReadOnlySpan<T> SplitUntil<T>(scoped ref ReadOnlySpan<T> span, int position)
    {
        ReadOnlySpan<T> until = span[..position];

        span = span[position..];

        return until;
    }
}