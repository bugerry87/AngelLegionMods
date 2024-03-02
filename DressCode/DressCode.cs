using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace DressCode
{
	[BepInPlugin("bugerry.DressCode", "Dress Code", "0.1.0")]
	public partial class BepInExPlugin : BaseUnityPlugin
	{
		public static BepInExPlugin context;
		public static ManualLogSource debug;

		public static ConfigEntry<bool> isEnabled;
		public static ConfigEntry<bool> isDebug;

		public static ConfigEntry<bool> uncensor;
		public static ConfigEntry<bool> fixHomeFashion;
		public static ConfigEntry<bool> fixHomeHair;

		public BepInExPlugin()
		{
			context = this;
			isEnabled = Config.Bind(
				"General",
				"Enable",
				true,
				"Enables or disables this mod"
			);
			isDebug = Config.Bind(
				"General",
				"Debug",
				true,
				"Enables or disabled debug mode"
			);
			uncensor = Config.Bind(
				"Dress Code",
				"Disable Censorship",
				true,
				"Disables and removes censor mosaic from Miya"
			);
			fixHomeFashion = Config.Bind(
				"Dress Code",
				"Fix Home Fashion",
				true,
				"Forces Miya to where selected fashion at home"
			);
			fixHomeHair = Config.Bind(
				"Dress Code",
				"Fix Home Hair",
				true,
				"Forces Miya to where selected hair at home"
			);

			if (isEnabled.Value)
			{
				debug = isDebug.Value ? Logger : null;
				Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
			}
		}

		[HarmonyPatch(typeof(ConfigManager), nameof(ConfigManager.get_t_dress))]
		public static class ConfigManager_Patch
		{
			public static ConfigManager instance;

			public static void Prefix(ConfigManager __instance, int id)
			{
				instance = __instance;
			}
		}

		[HarmonyPatch(typeof(director_ex), "msg")]
		[HarmonyPatch(new Type[] { typeof(s_message) })]
		public static class Director_Msg_Patch
		{
			public static MethodBase TargetMethod()
			{
				return typeof(director_ex).GetMethod("msg", new Type[] { typeof(s_message) });
			}

			public static void Postfix(director_ex __instance, s_message _msg)
			{
				if (!fixHomeHair.Value || _msg.m_type != "unit_hair")
				{
					return;
				}

				var miya = __instance.m_unit_root.Find("miya")?.GetComponentInChildren<unit>(true);
				var face = sys._instance.m_self.m_cards[0].get_role().faces;
				var cm = ConfigManager_Patch.instance;

				if (miya && face?.Count > 38)
				{
					var id = face[37];
					s_t_role_makeup s_t_role_makeup = cm.get_t_role_makeup(id);
					if (s_t_role_makeup != null && miya.m_t_class == null)
					{
						var num = (float)face[38];
						float time = 0.001f * (num / 1000f);
						float time2 = 0.001f * (num % 1000f);
						var color = sys._instance.m_hair_color.Evaluate(time) + (sys._instance.m_hair_bright.Evaluate(time2) - Color.gray) * 2f;
						var hash = "#" + ColorUtility.ToHtmlStringRGB(color);

						miya.m_t_class = new s_t_class();
						miya.change_part(s_t_role_makeup.skin_resource, hash);
						miya.init_part();
					}
				}
			}
		}

		[HarmonyPatch(typeof(director_ex), nameof(director_ex.play_director))]
		[HarmonyPatch(new Type[] { typeof(string), typeof(string), typeof(bool), typeof(bool) })]
		public static class Director_Play_Patch
		{
			private static bool Predicate(s_message msg)
			{
				switch (msg.m_type)
				{
					case "unit_part":
					case "unit_part2":
					case "unit_part_random":
					case "unit_part_random2":
						return fixHomeFashion.Value;
					case "cam_mask_on":
						return uncensor.Value;
					default:
						return false;
				}
			}

			public static void Postfix(ref s_director_ex ___m_director)
			{
				debug?.LogInfo("Director Play");
				___m_director.time_lists?.RemoveAll(Predicate);
				___m_director.lists?.RemoveAll(Predicate);
				___m_director.lists?.Add(new s_message("unit_hair"));
			}
		}
	}
}
