namespace RueI.Patches;

using HarmonyLib;
using RueI.API;

/// <summary>
/// Patches <see cref="AspectRatioSync.UserCode_CmdSetAspectRatio__Single(float)"/>
/// to update <see cref="RueDisplay"/>s when the aspect ratio is updated.
/// </summary>
[HarmonyPatch(typeof(AspectRatioSync), "UserCode_CmdSetAspectRatio__Single")]

internal static class AspectRatioPatch
{
#pragma warning disable SA1313 // Parameter names should begin with lower-case letter
#pragma warning disable SA1600 // Elements should be documented
    internal static void Postfix(AspectRatioSync __instance)
#pragma warning restore SA1600 // Elements should be documented
#pragma warning restore SA1313 // Parameter names should begin with lower-case letter
    {
        if (ReferenceHub.TryGetHub(__instance.netIdentity?.connectionToClient, out ReferenceHub hub))
        {
            RueDisplay display = RueDisplay.Get(hub);

            display.Update();
        }
    }
}