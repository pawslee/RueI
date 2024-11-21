namespace RueI;

/*********\
*  /\_/\  *
* ( o.o ) *
*  > ^ <  *
\*********/

using static UnityAlternative;

/// <summary>
/// Represents the main class for RueI.
/// </summary>
/// <remarks>
/// The <see cref="RueIMain"/> class is responsible for properly initializing all of RueI.
/// </remarks>
public static class RueIMain
{
    /// <summary>
    /// Gets the <see cref="HarmonyLib.Harmony"/> id for RueI.
    /// </summary>
    public const string HARMONYID = "RueI_Hint_Dependency";

    /// <summary>
    /// Gets the current version of RueI.
    /// </summary>
    public static readonly Version Version = typeof(RueIMain).Assembly.GetName().Version;

    private static bool isInit = false;

    private static bool isPatchesRegistered = false;

    /// <summary>
    /// Ensures that RueI is properly initialized.
    /// </summary>
    /// <param name="registerPatches">Register harmony patches or not.</param>
    public static void EnsureInit(bool registerPatches = true)
    {
        if (isInit)
        {
            return;
        }

        isInit = true;

        if (!StartupArgs.Args.Contains("-noRMsg", StringComparison.OrdinalIgnoreCase)) // TODO: make this work
        {
            Provider.Log($"[Info] [RueI] Thank you for using RueI! Running v{Version.ToString(3)}");
        }

        if (registerPatches && !isPatchesRegistered)
        {
            isPatchesRegistered = true;

            HarmonyLib.Harmony harmony = new(HARMONYID);
            Provider.PatchAll(harmony);
        }

        _ = Parsing.CharacterLengths.Lengths.Count; // force static initializer
    }
}
