using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace GumshoeTimerSuite
{ 
    [BepInPlugin(GUID, "Gumshoe Timer Suite", "1.1.0")]
    public class GumshoeTimerSuite : BaseUnityPlugin
    {
        private const string GUID = "org.ltmadness.valheim.gumshoetimersuite";
        private const string COLOR_REGEX_PATERN = "#(([0-9a-fA-F]{2}){3,4}|([0-9a-fA-F]){3,4})";

        private static ConfigEntry<ProgressText> progressTextFermenter;
        private static ConfigEntry<ProgressText> progressTextSapCollector;
        private static ConfigEntry<ProgressText> progressTextBeeHive;
        private static ConfigEntry<ProgressText> progressTextSmelter;
        private static ConfigEntry<TextColor> color;
        private static ConfigEntry<string> customColor;
        private static ConfigEntry<string> customBrackets;

        private static string openBr;
        private static string closeBr;

        private static Regex colorRegex;

        public void Awake()
        {
            progressTextFermenter = Config.Bind("General", "Progress Text Fermenter", ProgressText.TIME, "Should the progress text be off/percent/time left for fermenter");
            progressTextSapCollector = Config.Bind("General", "Progress Text Sap Collector", ProgressText.TIME, "Should the progress text be off/percent/time left for sap collector");
            progressTextBeeHive = Config.Bind("General", "Progress Text BeeHive", ProgressText.TIME, "Should the progress text be off/percent/time left for bee hive");
            progressTextSmelter = Config.Bind("General", "Progress Text Smelter", ProgressText.TIME, "Should the progress text be off/percent/time left for objects based on smelter");
            color = Config.Bind("Advanced", "Use color", TextColor.PROGRESSIVE, "Change color of % or time left");
            customColor = Config.Bind("Advanced", "Custom Static Color", "#ff69b4", "If left black uses default, format of color #RRGGBB");
            customBrackets = Config.Bind("Advanced", "Custom Brackets", "()", "What ever is set in this field is split in half and used as opening and closing brackets");

            Config.Save();

            System.Console.WriteLine(
                $"Progress Text Fermenter: {progressTextFermenter.Value}, " +
                $"Progress Text Sap Collector: {progressTextSapCollector.Value}, " +
                $"Progress Text Smelter: {progressTextSmelter.Value}, " +
                $"Use color: {color.Value}, " +
                $"Custom Static Color: {customColor.Value}, " +
                $"Custom brackets: {customBrackets}");

            colorRegex = new Regex(COLOR_REGEX_PATERN);

            SetBrackets();

            Harmony.CreateAndPatchAll(typeof(GumshoeTimerSuite), GUID);
        }

        [HarmonyPatch(typeof(Beehive), "Awake")]
        [HarmonyPrefix]
        public static bool Awake(Beehive __instance)
        {
            ZNetView m_nview = __instance.GetComponent<ZNetView>();
            Collider m_collider = __instance.GetComponentInChildren<Collider>();
            Piece m_piece = __instance.GetComponent<Piece>();
            AccessTools.Field(typeof(Beehive), "m_collider").SetValue(__instance, m_collider);
            AccessTools.Field(typeof(Beehive), "m_piece").SetValue(__instance, m_piece);

            if (m_nview.GetZDO() == null)
            {
                AccessTools.Field(typeof(Beehive), "m_nview").SetValue(__instance, m_nview);
                return false;
            }

            if (m_nview.IsOwner() && m_nview.GetZDO().GetLong("lastTime") == 0L)
            {
                m_nview.GetZDO().Set("lastTime", ZNet.instance.GetTime().Ticks);
            }

            m_nview.Register("RPC_Extract", count => AccessTools.Method(typeof(Beehive), "RPC_Extract")
            .Invoke(__instance, new object[] { count }));

            AccessTools.Field(typeof(Beehive), "m_nview").SetValue(__instance, m_nview);
            __instance.InvokeRepeating("UpdateBees", 0.0f, 1f);

            return false;
        }

        [HarmonyPatch(typeof(Beehive), "GetHoverText")]
        [HarmonyPostfix]
        public static void GetHoverText(ref Beehive __instance, ref string __result)
        {
            if (__result.IsNullOrWhiteSpace())
            {
                return;
            }

            ZNetView m_nview = (ZNetView)AccessTools.Field(typeof(Beehive), "m_nview").GetValue(__instance);

            if (m_nview.GetZDO().GetInt("level") >= __instance.m_maxHoney)
            {
                return;
            }

            DateTime d = new DateTime(m_nview.GetZDO().GetLong("lastTime", ZNet.instance.GetTime().Ticks));
            double num = (ZNet.instance.GetTime() - d).TotalSeconds;
            if (num < 0.0)
            {
                num = 0.0;
            }

            double timePassed = m_nview.GetZDO().GetFloat("product") + num - 1;
            if (timePassed > 0 && timePassed < __instance.m_secPerUnit)
            {
                int perc = (int)Math.Floor((double)(timePassed / __instance.m_secPerUnit * 100));
                string colorHex = GetColor(perc);

                if (ProgressText.PERCENT.Equals(progressTextBeeHive.Value))
                {
                    __result = __result.Replace(")", $") {openBr} <color={colorHex}>{perc}%</color> {closeBr}");
                }
                else if (ProgressText.TIME.Equals(progressTextBeeHive.Value))
                {

                    int timeLeft = (int)Math.Ceiling(__instance.m_secPerUnit - timePassed);
                    __result = __result.Replace(")", GetTimeResult(timeLeft, colorHex));
                }
                return;
            }
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
                            __result = __result.Replace(")", $") {openBr} <color={colorHex}>{perc}%</color> {closeBr}");
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

        [HarmonyPatch(typeof(SapCollector), "GetHoverText")]
        [HarmonyPostfix]
        public static void GetHoverText(ref SapCollector __instance, ref string __result)
        {
            if (__result.IsNullOrWhiteSpace())
            {
                return;
            }

            ZNetView m_nview = (ZNetView)AccessTools.Field(typeof(SapCollector), "m_nview").GetValue(__instance);
            
            if (m_nview.GetZDO().GetInt("level") >= __instance.m_maxLevel)
            {
                return;
            }

            DateTime d = new DateTime(m_nview.GetZDO().GetLong("lastTime", ZNet.instance.GetTime().Ticks));
            double num = (ZNet.instance.GetTime() - d).TotalSeconds;
            if (num < 0.0)
            {
                num = 0.0;
            }

            double timePassed = m_nview.GetZDO().GetFloat("product") + num - 1;
            if (timePassed > 0 && timePassed < __instance.m_secPerUnit)
            {
                int perc = (int)Math.Floor((double)(timePassed / __instance.m_secPerUnit * 100));
                string colorHex = GetColor(perc);

                if (ProgressText.PERCENT.Equals(progressTextSapCollector.Value))
                {
                    __result = __result.Replace(")", $") {openBr} <color={colorHex}>{perc}%</color> {closeBr}");
                }
                else if (ProgressText.TIME.Equals(progressTextSapCollector.Value))
                {
                    
                    int timeLeft = (int)Math.Ceiling(__instance.m_secPerUnit - timePassed);
                    __result = __result.Replace(")", GetTimeResult(timeLeft, colorHex));
                }
                return;
            }
        }

        [HarmonyPatch(typeof(SapCollector), "Awake")]
        [HarmonyPrefix]
        public static bool Awake(SapCollector __instance)
        {
            ZNetView m_nview = __instance.GetComponent<ZNetView>();
            Collider m_collider = __instance.GetComponentInChildren<Collider>();
            Piece m_piece = __instance.GetComponent<Piece>();
            AccessTools.Field(typeof(SapCollector), "m_collider").SetValue(__instance, m_collider);
            AccessTools.Field(typeof(SapCollector), "m_piece").SetValue(__instance, m_piece);

            if (m_nview.GetZDO() == null)
            {
                AccessTools.Field(typeof(SapCollector), "m_nview").SetValue(__instance, m_nview);
                return false;
            }

            if (m_nview.IsOwner() && m_nview.GetZDO().GetLong("lastTime") == 0L)
            {
                m_nview.GetZDO().Set("lastTime", ZNet.instance.GetTime().Ticks);
            }

            m_nview.Register("RPC_Extract", count => AccessTools.Method(typeof(SapCollector), "RPC_Extract")
            .Invoke(__instance, new object[] { count }));
            m_nview.Register("RPC_UpdateEffects", count => AccessTools.Method(typeof(SapCollector), "RPC_UpdateEffects")
            .Invoke(__instance, new object[] { count }));

            AccessTools.Field(typeof(SapCollector), "m_nview").SetValue(__instance, m_nview);
            __instance.InvokeRepeating("UpdateTick", 0.0f, 1f);

            return false;
        }

        [HarmonyPatch(typeof(Smelter), "UpdateHoverTexts")]
        [HarmonyPostfix]
        public static void UpdateHoverText(ref Smelter __instance)
        {
            if (!(bool)__instance.m_addOreSwitch || ProgressText.OFF.Equals(progressTextSmelter.Value))
            {
                return;
            }

            ZNetView m_nview = (ZNetView)AccessTools.Field(typeof(Smelter), "m_nview").GetValue(__instance);
            float progressTime = m_nview.GetZDO().GetFloat("bakeTimer");
            float modifier = (bool)__instance.m_windmill ? __instance.m_windmill.GetPowerOutput() : 1f;
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

        public static void SetBrackets()
        {
            int size = customBrackets.Value.Length;

            if (size % 2 == 0)
            {
                size -= 1;
            }
            int mid = size / 2;
            openBr = customBrackets.Value.Substring(0, mid - 1);
            closeBr = customBrackets.Value.Substring(mid);
        }

        public static string GetTimeResult(Fermenter __instance, double timePassed, string colorHex)
        {
            double left = ((double)__instance.m_fermentationDuration) - timePassed;
            int min = (int)Math.Floor(left / 60);
            int sec = ((int)left) % 60;

            if (colorHex != null)
            {
                return $") {openBr} <color={colorHex}>{min}m {sec}s</color> {closeBr}";
            }

            return String.Format($") {openBr} {min}m {sec}s {closeBr}");
        }

        public static string GetTimeResult(int timeleft, string colorHex)
        {
            if (colorHex != null)
            {
                return $") {openBr} <color={colorHex}>{timeleft}s</color> {closeBr}";
            }

            return String.Format($") {openBr} {timeleft}s {closeBr}");
        }

        public static string GetPercentageResult(int progressPercentage, string colorHex)
        {
            if (colorHex != null)
            {
                return $") {openBr} <color={colorHex}>{progressPercentage}%</color> {closeBr}";
            }

            return String.Format($") {openBr} {progressPercentage}% {closeBr}");
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