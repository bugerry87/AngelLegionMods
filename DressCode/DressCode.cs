using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace DressCode
{
	[BepInPlugin("bugerry.DressCode", "Dress Code", "0.0.1")]
	public partial class BepInExPlugin : BaseUnityPlugin
	{
		public static BepInExPlugin context;
		public static ManualLogSource debug;

		public static ConfigEntry<bool> isEnabled;
		public static ConfigEntry<bool> isDebug;

		public static ConfigEntry<bool> uncencor;
		public static ConfigEntry<bool> fixHomeFashion;

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
			uncencor = Config.Bind(
				"Dress Code",
				"Disable Censorship",
				true,
				"Disables and removes censor mosaic from Miya"
			);
			fixHomeFashion = Config.Bind(
				"Dress Code",
				"Fix Home Fashion",
				true,
				"Forces Miya to where selected Fashion at home"
			);

			if (isEnabled.Value)
			{
				debug = isDebug.Value ? Logger : null;
				Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
			}
		}

		/*
		[HarmonyPatch(typeof(unit), "message")]
		public static class Unit_Message_Patch
		{
			public static MethodBase TargetMethod()
			{
				return typeof(IMessage).GetMethod("message");
			}

			public static void Prefix(s_message message)
			{
				switch (message.m_type)
				{
					case "unit_part":
					case "unit_part2":
					case "unit_part_random":
					case "unit_part_random2":
						message.m_type = "ignore";
						break;
					default:
						break;
				}
			}
		}
		*/

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
						return uncencor.Value;
					default:
						return false;
				}
			}

			public static void Postfix(ref s_director_ex ___m_director)
			{
				debug?.LogInfo("Director Play");
				___m_director.time_lists?.RemoveAll(Predicate);
				___m_director.lists?.RemoveAll(Predicate);
			}
		}
	}
}
