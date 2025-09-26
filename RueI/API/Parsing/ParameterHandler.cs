namespace RueI.API.Parsing;

using System;
using System.Collections.Generic;

using global::Utils.Networking;
using LabApi.Features.Console;
using Mirror;

using RueI.API.Elements.Parameters;
using RueI.API.Parsing.Structs;
using RueI.Utils.Extensions;

/// <summary>
/// Handles the parameters for <see cref="ElementCombiner"/>.
/// </summary>
internal sealed class ParameterHandler
{
    private readonly List<NobreakInfo> noBreaks;
    private IReadOnlyList<ContentParameter> currentParameters = null!;

    private NetworkWriter writer = null!;
    private int countPosition = 0; // position at which to write the num of parameters
    private int numParams = 0;
    private int elementIndex = 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParameterHandler"/> class.
    /// </summary>
    /// <param name="noBreaks">The <see cref="List{T}"/> to add nobreaks to.</param>
    internal ParameterHandler(List<NobreakInfo> noBreaks)
    {
        this.noBreaks = noBreaks;
    }

    /// <summary>
    /// Sets up the <see cref="ParameterHandler"/> for a new batch.
    /// </summary>
    /// <param name="writer">The <see cref="NetworkWriter"/> to write the parameters to.</param>
    internal void Setup(NetworkWriter writer)
    {
        this.writer = writer;
        this.countPosition = writer.Position;
        this.numParams = 0;

        // write padding for the number of parameters
        writer.WriteInt(0);
    }

    /// <summary>
    /// Finishes the parameter writing.
    /// </summary>
    internal void Finish()
    {
        if (this.numParams == 0)
        {
            // add a string parameter if there's none to prevent
            // errors on the client
            this.AddStringParameter(ReadOnlySpan<byte>.Empty);
        }

        int oldPos = this.writer.Position;

        this.writer.Position = this.countPosition;
        this.writer.WriteInt(this.numParams);

        this.writer.Position = oldPos;
    }

    /// <summary>
    /// Sets the parameters for the current element.
    /// </summary>
    /// <param name="list">A <see cref="IReadOnlyList{T}"/> of <see cref="ContentParameter"/>s.</param>
    internal void SetElementParameters(IReadOnlyList<ContentParameter> list)
    {
        foreach (ContentParameter parameter in list)
        {
            this.writer.WriteByte((byte)parameter.HintParameterType);
            parameter.Write(this.writer);
        }

        this.currentParameters = list;
        this.elementIndex = this.numParams;
        this.numParams += list.Count;
    }

    /// <summary>
    /// Maps a parameter ID relative to the current element's parameter to a total ID.
    /// </summary>
    /// <param name="paramId">The parameter ID to map.</param>
    /// <returns>The mapped parameter.</returns>
    internal int MappedElementParameterId(int paramId) => this.elementIndex + paramId;

    /// <summary>
    /// Adds a simple <see langword="string"/> parameter to the <see cref="ParameterHandler"/>.
    /// </summary>
    /// <param name="bytes">A <see cref="ReadOnlySpan{T}"/> of <see langword="byte"/> that contains the content of the string parameter.</param>
    /// <returns>The ID of the added parameter.</returns>
    internal int AddStringParameter(ReadOnlySpan<byte> bytes)
    {
        this.writer.WriteByte((byte)HintParameterReaderWriter.HintParameterType.Text);
        this.writer.WriteBytes(bytes, writeSize: true);

        return this.numParams++;
    }

    /// <summary>
    /// Adds an <see cref="AnimatableFloat"/> as a parameter to the <see cref="ParameterHandler"/>.
    /// </summary>
    /// <param name="value">The <see cref="AnimatableFloat"/> to add.</param>
    /// <param name="multi">A value to multiply the <see cref="AnimatableFloat"/> by.</param>
    /// <returns>The ID of the added parameter.</returns>
    internal int AddAnimatableFloat(in AnimatableFloat value, float multi = 1)
    {
        this.writer.WriteByte((byte)HintParameterReaderWriter.HintParameterType.AnimationCurve);
        value.Parameter!.WriteTransformed(this.writer, value.Multiplier * multi, value.AddendOrValue);

        return this.numParams++;
    }
}