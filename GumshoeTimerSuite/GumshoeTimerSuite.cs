﻿using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace GumshoeTimerSuite
{ 
    [BepInPlugin(GUID, "Gumshoe Timer Suite", "1.1.0")]
    public class GumshoeTimerSuite : BaseUnityPlugin
    {
        private const string GUID = "org.ltmadness.valheim.gumshoetimersuite";
        private const string COLOR_REGEX_PATERN = "#(([0-9a-fA-F]{2}){3,4}|([0-9a-fA-F]){3,4})";
        private const bool TEST = false;

        private static ConfigEntry<ProgressText> progressTextFermenter;
        private static ConfigEntry<ProgressText> progressTextSapCollector;
        private static ConfigEntry<ProgressText> progressTextBeeHive;
        private static ConfigEntry<ProgressText> progressTextSmelter;
        private static ConfigEntry<ProgressText> progressTextCookingStation;
        private static ConfigEntry<TextColor> color;
        private static ConfigEntry<string> customColor;

        private static Regex colorRegex;

        public void Awake()
        {
            progressTextFermenter = 
                Config.Bind("General", "Progress Text Fermenter", ProgressText.TIME, "Should the progress text be off/percent/time left for fermenter");
            progressTextSapCollector = 
                Config.Bind("General", "Progress Text Sap Collector", ProgressText.TIME, "Should the progress text be off/percent/time left for sap collector");
            progressTextBeeHive = 
                Config.Bind("General", "Progress Text Bee Hive", ProgressText.TIME, "Should the progress text be off/percent/time left for bee hive");
            progressTextSmelter = 
                Config.Bind("General", "Progress Text Smelter", ProgressText.TIME, "Should the progress text be off/percent/time left for objects based on smelter");
            progressTextCookingStation = 
                Config.Bind("General", "Progress Text Cooking Station", ProgressText.TIME, "Should the progress text be off/percent/time left for objects based on cooking station");
            color = 
                Config.Bind("Advanced", "Use color", TextColor.PROGRESSIVE, "Change color of % or time left");
            customColor = 
                Config.Bind("Advanced", "Custom Static Color", "#ff69b4", "If left black uses default, format of color #RRGGBB");

            Config.Save();

            System.Console.WriteLine(
                $"Progress Text Fermenter: {progressTextFermenter.Value}, " +
                $"Progress Text Sap Collector: {progressTextSapCollector.Value}, " +
                $"Progress Text Smelter: {progressTextSmelter.Value}, " +
                $"Use color: {color.Value}, " +
                $"Custom Static Color: {customColor.Value}, ");

            colorRegex = new Regex(COLOR_REGEX_PATERN);

            Harmony.CreateAndPatchAll(typeof(GumshoeTimerSuite), GUID);
        }

        [HarmonyPatch(typeof(CookingStation), "GetHoverText")]
        [HarmonyPostfix]
        public static void GetHoverText(CookingStation __instance, ref string __result)
        {
            GetHoverTextCookingStation(__instance, ref __result);
        }

        [HarmonyPatch(typeof(CookingStation), "OnHoverFuelSwitch")]
        [HarmonyPostfix]
        public static void OnHoverFuelSwitch(CookingStation __instance, ref string __result)
        {
            GetHoverTextCookingStation(__instance, ref __result);
        }

        public static void GetHoverTextCookingStation(CookingStation __instance, ref string __result)
        {
            if (ProgressText.OFF.Equals(progressTextCookingStation.Value) || __instance.m_addFoodSwitch == null)
            {
                return;
            }

            ZNetView m_nview = (ZNetView)AccessTools.Field(typeof(CookingStation), "m_nview").GetValue(__instance);
            StringBuilder builder = new StringBuilder();

            for (int slot = 0; slot < __instance.m_slots.Length; ++slot)
            {
                string itemName = m_nview.GetZDO().GetString(nameof(slot) + slot.ToString());
                float cookedTime = m_nview.GetZDO().GetFloat(nameof(slot) + slot.ToString());

                if (itemName != "")
                {
                    CookingStation.ItemConversion itemConversion = 
                        (CookingStation.ItemConversion)AccessTools
                                                .Method(typeof(CookingStation), "GetItemConversion")
                                                .Invoke(__instance, new object[] { itemName });
                    if (itemConversion != null)
                    {
                        float cookTime = itemConversion.m_cookTime + 1;
                        float burnTime = itemConversion.m_cookTime * 2;
                        int perc = (int)Math.Floor((double)(cookedTime / cookTime * 100));
                        string colorHex = GetColor(perc);
                        int burnTimeLeft = (int)Math.Ceiling(burnTime - cookedTime);
                        if (ProgressText.PERCENT.Equals(progressTextCookingStation.Value))
                        {
                            if (perc <= 100)
                            {
                                if (!string.IsNullOrEmpty(colorHex))
                                {
                                    builder.Insert(0, $"<color={colorHex}>{perc}%</color> \n");
                                }
                                else
                                {
                                    builder.Insert(0, $"{perc}% \n");
                                }

                                builder.Insert(0, $"{GetFromItemName(itemConversion)} -> {GetToItemName(itemConversion)} ");
                            }
                            else
                            {
                                perc = (int)Math.Floor(burnTimeLeft / itemConversion.m_cookTime * 100);
                                colorHex = GetColor(perc);
                                if (!string.IsNullOrEmpty(colorHex))
                                {
                                    builder.Insert(0, $"<color={colorHex}>{perc}%</color> \n");
                                }
                                else
                                {
                                    builder.Insert(0, $"{perc}% \n");
                                }

                                builder.Insert(0, $"{GetToItemName(itemConversion)} -> Burned ");
                            }
                        }
                        else if (ProgressText.TIME.Equals(progressTextCookingStation.Value))
                        {
                            int timeLeft = (int)Math.Ceiling(cookTime - cookedTime);

                            if (timeLeft > 0)
                            {
                                if (!string.IsNullOrEmpty(colorHex))
                                {
                                    builder.Insert(0, $"<color={colorHex}>{timeLeft}s</color>\n");
                                }
                                else
                                {
                                    builder.Insert(0, $"{timeLeft}s\n");
                                }

                                builder.Insert(0, $"{GetFromItemName(itemConversion)} -> {GetToItemName(itemConversion)} ");
                            }
                            else
                            {
                                if (!string.IsNullOrEmpty(colorHex))
                                {
                                    builder.Insert(0, $"<color={colorHex}>{burnTimeLeft}s</color>\n");
                                }
                                else
                                {
                                    builder.Insert(0, $"{burnTimeLeft}s\n");
                                }

                                builder.Insert(0, $"{GetToItemName(itemConversion)} -> Burned ");
                            }
                        }
                    }
                }
            }

            __result = builder.ToString();

            if (TEST)
            {
                __result += "\n Cooking Station";
            }

            return;
        }
             

        [HarmonyPatch(typeof(Beehive), "Awake")]
        [HarmonyPrefix]
        public static bool Awake(Beehive __instance)
        {
            ZNetView m_nview = __instance.GetComponent<ZNetView>();
            Collider m_collider = __instance.GetComponentInChildren<Collider>();
            Piece m_piece = __instance.GetComponent<Piece>();
            AccessTools.Field(typeof(Beehive), "m_collider")
                       .SetValue(__instance, m_collider);
            AccessTools.Field(typeof(Beehive), "m_piece")
                       .SetValue(__instance, m_piece);

            if (m_nview.GetZDO() == null)
            {
                AccessTools.Field(typeof(Beehive), "m_nview")
                           .SetValue(__instance, m_nview);
                return false;
            }

            if (m_nview.IsOwner() && m_nview.GetZDO().GetLong("lastTime") == 0L)
            {
                m_nview.GetZDO()
                       .Set("lastTime", ZNet.instance.GetTime().Ticks);
            }

            m_nview.Register("RPC_Extract",
                caller => AccessTools.Method(typeof(Beehive), "RPC_Extract")
                                     .Invoke(__instance, new object[] { caller }));

            AccessTools.Field(typeof(Beehive), "m_nview")
                       .SetValue(__instance, m_nview);
            __instance.InvokeRepeating("UpdateBees", 0.0f, 1f);

            return false;
        }

        [HarmonyPatch(typeof(Beehive), "GetHoverText")]
        [HarmonyPostfix]
        public static void GetHoverText(Beehive __instance, ref string __result)
        {
            if (__result.IsNullOrWhiteSpace() || ProgressText.OFF.Equals(progressTextBeeHive.Value))
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
                    __result = $"Progress: <color={colorHex}>{perc}%</color>\n" + __result;
                } 
                else if (ProgressText.TIME.Equals(progressTextBeeHive.Value))
                {
                    int timeLeft = (int)Math.Ceiling(__instance.m_secPerUnit - timePassed);
                    __result = GetTimeResult(timeLeft, colorHex) + __result;
                }

                if (TEST)
                {
                    __result += "\n BeeHive";
                }
                return;
            }
        }

        [HarmonyPatch(typeof(Fermenter), "GetHoverText")]
        [HarmonyPostfix]
        public static void GetHoverText(Fermenter __instance, ref string __result)
        {
            if (__result.IsNullOrWhiteSpace() || ProgressText.OFF.Equals(progressTextFermenter.Value))
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
                            __result = $"Progress: <color={colorHex}>{perc}s</color>\n" + __result;
                        }
                        else if (ProgressText.TIME.Equals(progressTextFermenter.Value))
                        {
                            __result = GetTimeResult(__instance, timePassed, colorHex) + __result;
                        }

                        if (TEST)
                        {
                            __result += "\n Fermenter";
                        }
                        return;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(SapCollector), "GetHoverText")]
        [HarmonyPostfix]
        public static void GetHoverText(SapCollector __instance, ref string __result)
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
                    __result = GetPercentageResult(perc, colorHex) + __result;
                }
                else if (ProgressText.TIME.Equals(progressTextSapCollector.Value))
                {
                    
                    int timeLeft = (int)Math.Ceiling(__instance.m_secPerUnit - timePassed);
                    __result = GetTimeResult(timeLeft, colorHex) + __result;
                }

                if (TEST)
                {
                    __result += "\n Sap Collector";
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

            var m_nviewField = AccessTools.Field(typeof(SapCollector), "m_nview");

            if (m_nview.GetZDO() == null)
            {
                m_nviewField.SetValue(__instance, m_nview);
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

            m_nviewField.SetValue(__instance, m_nview);
            __instance.InvokeRepeating("UpdateTick", 0.0f, 1f);

            return false;
        }

        [HarmonyPatch(typeof(Smelter), "UpdateHoverTexts")]
        [HarmonyPostfix]
        public static void UpdateHoverText(Smelter __instance)
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
                    __instance.m_addOreSwitch.m_hoverText = GetTimeResult(timeLeft, colorHex) + __instance.m_addOreSwitch.m_hoverText;
                }
                else
                {
                   __instance.m_addOreSwitch.m_hoverText = GetPercentageResult(progressPercentage, colorHex) + __instance.m_addOreSwitch.m_hoverText;
                }

                __instance.m_addOreSwitch.m_hoverText += "\n Smelter";
                return;
            }
        }

        public static string GetFromItemName(CookingStation.ItemConversion itemConversion)
        {
            return Localization.instance.Localize(itemConversion.m_from.m_itemData.m_shared.m_name);
        }

        public static string GetToItemName(CookingStation.ItemConversion itemConversion)
        {
            return Localization.instance.Localize(itemConversion.m_to.m_itemData.m_shared.m_name);
        }

        public static string GetTimeResult(Fermenter __instance, double timePassed, string colorHex)
        {
            double left = ((double)__instance.m_fermentationDuration) - timePassed;
            int min = (int)Math.Floor(left / 60);
            int sec = ((int)left) % 60;

            if (!string.IsNullOrEmpty(colorHex))
            {
                return $"Progress: <color={colorHex}>{min}m {sec}s</color>\n";
            }

            return $"Progress: {min}m {sec}s";
        }

        public static string GetTimeResult(int timeleft, string colorHex)
        {
            if (!string.IsNullOrEmpty(colorHex))
            {
                return $"Progress: <color={colorHex}>{timeleft}s</color>\n";
            }

            return $"Progress: {timeleft}s\n";
        }

        public static string GetPercentageResult(int progressPercentage, string colorHex)
        {
            if (!string.IsNullOrEmpty(colorHex))
            {
                return $"Progress: <color={colorHex}>{progressPercentage}%</color> \n";
            }

            return $"Progress: {progressPercentage}% \n";
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

        enum Status
        {
            NotDone,
            Done,
            Burnt
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