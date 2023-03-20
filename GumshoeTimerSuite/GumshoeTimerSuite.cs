using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Text.RegularExpressions;

namespace GumshoeTimerSuite
{ 
    [BepInPlugin(GUID, "Gumshoe Timer Suite", "1.0.0")]
    public class GumshoeTimerSuite : BaseUnityPlugin
    {
        private const string GUID = "org.ltmadness.valheim.gumshoetimersuite";
        private const string colorRegexPatern = "#(([0-9a-fA-F]{2}){3,4}|([0-9a-fA-F]){3,4})";

        private static ConfigEntry<ProgressText> progressTextFermenter;
        private static ConfigEntry<ProgressText> progressTextSmelter;
        private static ConfigEntry<TextColor> color;
        private static ConfigEntry<string> customColor;

        private static Regex colorRegex;

        public void Awake()
        {
            progressTextFermenter = Config.Bind("General", "Progress Text Fermenter", ProgressText.TIME, "Should the progress text be off/percent/time left for fermenter");
            progressTextSmelter = Config.Bind("General", "Progress Text Smelter", ProgressText.TIME, "Should the progress text be off/percent/time left for objects based on smelter");
            color = Config.Bind("Advanced", "Use color", TextColor.PROGRESSIVE, "Change color of % or time left");
            customColor = Config.Bind("Advanced", "Custom Static Color", "#ff69b4", "If left black uses default, format of color #RRGGBB");

            Config.Save();

            System.Console.WriteLine(
                $"Progress Text Fermenter: {progressTextFermenter.Value}, " +
                $"Progress Text Smelter: {progressTextSmelter.Value}, " +
                $"Use color: {color.Value}, " +
                $"Custom Static Color: {customColor.Value}");

            colorRegex = new Regex(colorRegexPatern);

            Harmony.CreateAndPatchAll(typeof(GumshoeTimerSuite), GUID);
        }

        [HarmonyPatch(typeof(Fermenter), "GetHoverText")]
        [HarmonyPostfix]
        public static void GetHoverText(ref Fermenter __instance, ref string __result)
        {
            if (__result.IsNullOrWhiteSpace())
            {
                return;
            }

            ZNetView m_nview = (ZNetView)AccessTools.Field(typeof(Fermenter), "m_nview").GetValue(__instance);
            if (!m_nview.GetZDO().GetString("Content", "").IsNullOrWhiteSpace())
            {
                DateTime d = new DateTime(m_nview.GetZDO().GetLong("StartTime", 0L));
                if (d.Ticks != 0L)
                {
                    double timePassed = (ZNet.instance.GetTime() - d).TotalSeconds;

                    if (!timePassed.Equals(-1) && timePassed < __instance.m_fermentationDuration)
                    {
                        int perc = (int)(timePassed / __instance.m_fermentationDuration * 100);
                        string colorHex = GetColor(perc);

                        if (ProgressText.PERCENT.Equals(progressTextFermenter.Value))
                        {
                            __result = __result.Replace(")", $"<color={colorHex}>{perc}%</color> )");
                        }
                        else if (ProgressText.TIME.Equals(progressTextFermenter.Value))
                        {
                            __result = __result.Replace(")", GetTimeResult(__instance, timePassed, colorHex));
                        }
                        return;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Smelter), "UpdateHoverTexts")]
        [HarmonyPostfix]
        public static void UpdateHoverText(ref Smelter __instance)
        {
            if (!(bool)(UnityEngine.Object)__instance.m_addOreSwitch || ProgressText.OFF.Equals(progressTextSmelter.Value))
            {
                return;
            }

            ZNetView m_nview = (ZNetView)AccessTools.Field(typeof(Smelter), "m_nview").GetValue(__instance);
            float progressTime = m_nview.GetZDO().GetFloat("bakeTimer");
            float modifier = (bool)(UnityEngine.Object)__instance.m_windmill ? __instance.m_windmill.GetPowerOutput() : 1f;
            float timePerItem = __instance.m_secPerProduct;
            float progressUnmoddified = timePerItem - progressTime;

            int timeLeft = (int)(progressUnmoddified / modifier);

            if (progressTime > 0.0)
            {
                int progressPercentage = (int)(progressTime / timePerItem * 100);
                string colorHex = GetColor(progressPercentage);

                if (ProgressText.TIME.Equals(progressTextSmelter.Value))
                {
                    __instance.m_addOreSwitch.m_hoverText = __instance.m_addOreSwitch.m_hoverText.Replace(")", GetTimeResult(timeLeft, colorHex));
                }
                else
                {
                   __instance.m_addOreSwitch.m_hoverText = __instance.m_addOreSwitch.m_hoverText.Replace(")", GetPercentageResult(progressPercentage, colorHex));
                }

               return;
            }
        }

        public static string GetTimeResult(Fermenter __instance, double timePassed, string colorHex)
        {
            double left = ((double)__instance.m_fermentationDuration) - timePassed;
            int min = (int)Math.Floor(left / 60);
            int sec = ((int)left) % 60;

            if (colorHex != null)
            {
                return $"<color={colorHex}>{min}m {sec}s</color> )";
            }

            return String.Format($"{min}m {sec}s )");
        }

        public static string GetTimeResult(int timeleft, string colorHex)
        {
            if (colorHex != null)
            {
                return $")( <color={colorHex}>{timeleft}s</color> )";
            }

            return String.Format($")( {timeleft}s )");
        }

        public static string GetPercentageResult(int progressPercentage, string colorHex)
        {
            if (colorHex != null)
            {
                return $")( <color={colorHex}>{progressPercentage}%</color> )";
            }

            return String.Format($")( {progressPercentage}% )");
        }

        public static string GetColor(int percentage)
        {
            var csc = customColor.Value.Trim();
            if (TextColor.PROGRESSIVE.Equals(color.Value))
            {
                return $"#{255 / 100 * (100 - percentage):X2}{255 / 100 * percentage:X2}{0:X2}";
            }
            else if (TextColor.STATIC.Equals(color.Value) && !csc.IsNullOrWhiteSpace() && colorRegex.IsMatch(csc))
            {
                return csc;
            }

            return null;
        }

        enum ProgressText
        {
            OFF,
            PERCENT,
            TIME
        }

        enum TextColor
        {
            WHITE,
            PROGRESSIVE,
            STATIC
        }
    }
}