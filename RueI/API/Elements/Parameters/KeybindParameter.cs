namespace RueI.API.Elements.Parameters;

using global::Utils.Networking;
using Mirror;

/// <summary>
/// Represents a parameter that is replaced with a keybind.
/// </summary>
/// <remarks>
/// This is the RueI equivalent of the base-game <see cref="Hints.SSKeybindHintParameter"/>.
/// </remarks>
public class KeybindParameter : FormattableParameter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KeybindParameter"/> class
    /// from an SS setting ID.
    /// </summary>
    /// <param name="id">The ID of the server-setting parameter.</param>
    /// <param name="format">The format to use.</param>
    public KeybindParameter(int id, string format = "[{0}]")
        : base(format)
    {
        this.Id = id;
    }

    /// <summary>
    /// Gets the ID of the SS keybind setting.
    /// </summary>
    public int Id { get; }

    /// <inheritdoc/>
    internal override HintParameterReaderWriter.HintParameterType HintParameterType => HintParameterReaderWriter.HintParameterType.SSKeybind;

    /// <inheritdoc/>
    internal override void Write(NetworkWriter writer)
    {
        writer.WriteInt(this.Id);
        writer.WriteString(this.Format);
    }
}