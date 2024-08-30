using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace Camera
{
	[BepInPlugin("bugerry.Camera", "Camera", "0.0.2")]
	public partial class BepInExPlugin : BaseUnityPlugin
	{
		public static BepInExPlugin context;
		public static ManualLogSource debug;

		public static ConfigEntry<bool> isEnabled;
		public static ConfigEntry<bool> isDebug;

		public static ConfigEntry<KeyCode> crouchButton;
		public static ConfigEntry<Vector3> crouchOffset;

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

			crouchButton = Config.Bind(
				"Camera",
				"Crouch Button",
				KeyCode.LeftShift,
				"Key for toggling crouch"
			);

			crouchOffset = Config.Bind(
				"Camera",
				"Crouch Offset",
				Vector3.zero,
				"Crouching offset from player center"
			);

			if (isEnabled.Value)
			{
				debug = isDebug.Value ? Logger : null;
				Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
			}
		}

		[HarmonyPatch(typeof(cam_move), "Update")]
		public static class CamMove_Update_Patch
		{
			public static MethodBase TargetMethod()
			{
				return typeof(cam_move).GetMethod("Update");
			}

			public static void Prefix(cam_move __instance)
			{
				if (Input.GetKeyDown(crouchButton.Value))
				{
					if (__instance.transform.localPosition == crouchOffset.Value)
					{
						__instance.transform.localPosition = new Vector3(0f, 0.5f, 0f);
					}
					else
					{
						__instance.transform.localPosition = crouchOffset.Value;
					}
				}
			}
		}
	}
}
