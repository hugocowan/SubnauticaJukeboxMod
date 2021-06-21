using HarmonyLib;
using QModManager.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

namespace JukeboxSpotify
{
    [HarmonyPatch(typeof(JukeboxInstance))]
    class JukeboxInstancePatcher
    {

        [HarmonyPrefix]
        [HarmonyPatch("SetLabel")]
        static void SetLabelPrefix(ref string text)
        {
            text = Spotify._currentTrackTitle;
        }

        [HarmonyPrefix]
        [HarmonyPatch("SetLength")]
        static void SetLengthPrefix(ref uint length)
        {
            length = Spotify._currentTrackLength;
        }

        //[HarmonyPrefix]
        //[HarmonyPatch("OnButtonPlayPause")]
        //static void OnButtonPlayPause(ref Action<string> ___SetLabel)
        //{
        //    Logger.Log(Logger.Level.Info, "We pressed play pause button", null, true);
        //    ___SetLabel(Spotify._currentTrackTitle);

        //}

        //[HarmonyTranspiler]
        //[HarmonyPatch("UpdateUI")]
        //static IEnumerable<CodeInstruction> OnUpdateUITranspiler(IEnumerable<CodeInstruction> instructions)
        //{
        //    Logger.Log(Logger.Level.Info, "Method running", null, true);
        //    var startIndex = -1;
        //    var endIndex = -1;

        //    var codes = new List<CodeInstruction>(instructions);

        //    Logger.Log(Logger.Level.Info, "codes.Count: " + codes.Count, null, true);

        //    for (var i = 0; i < codes.Count; i++)
        //    {
        //        Logger.Log(Logger.Level.Info, "Before - [" + i + "] " + codes[i].opcode.Name + "    " + codes[i].operand, null, true);
                
        //        if (codes[i].operand as string == "UInt32 get_length()")
        //        {
        //            startIndex = i - 1;
        //        }
        //    }

        //    if (startIndex > -1)
        //    {
                
        //    }

        //    //List<CodeInstruction> FirstLines = codes.Take(4).ToList();
        //    //codes.RemoveRange(0, FirstLines.Count);


        //    Logger.Log(Logger.Level.Error, "Inserting code at index " + startIndex, null, true);
        //    var thisCode = new CodeInstruction(OpCodes.Ldarg_0);
        //    var stringCode = new CodeInstruction(OpCodes.Ldstr, "pls");
        //    CodeInstruction methodCode = new CodeInstruction(OpCodes.Call, "instance void JukeboxInstance::SetLabel(string)"));

        //    codes = codes.Prepend(methodCode).Prepend(stringCode).Prepend(thisCode).ToList();

        //    for (var i = 0; i < codes.Count; i++)
        //    {
        //        Logger.Log(Logger.Level.Info, "After - [" + i + "] " + codes[i].opcode.Name + "    " + codes[i].operand, null, true);
        //    }

        //    return codes.AsEnumerable();
        //}


    }
}
