﻿using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace Graphics
{
	[BepInPlugin("bugerry.Graphics", "Graphics", "0.1.0")]
	public partial class BepInExPlugin : BaseUnityPlugin
	{
		public static BepInExPlugin context;
		public static ManualLogSource debug;

		public static ConfigEntry<bool> isEnabled;
		public static ConfigEntry<bool> isDebug;

		public static ConfigEntry<int> antiAliasing;
		public static ConfigEntry<AnisotropicFiltering> anisotropicFiltering;
		public static ConfigEntry<bool> softParticles;
		public static ConfigEntry<ShadowQuality> shadows;
		public static ConfigEntry<int> pixelLightCount;
		public static ConfigEntry<bool> allowHDR;

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

			antiAliasing = Config.Bind(
				"Graphics",
				"Anti Aliasing",
				QualitySettings.antiAliasing,
				new ConfigDescription(
					"Anti Aliasing Level from 0 to 8",
					new AcceptableValueList<int>(0, 4, 8)
				)
			);

			anisotropicFiltering = Config.Bind(
				"Graphics",
				"Anisotropic Filtering",
				QualitySettings.anisotropicFiltering,
				"AnisotropicFiltering Level"
			);

			shadows = Config.Bind(
				"Graphics",
				"Shadows",
				QualitySettings.shadows,
				"Shadows quality"
			);

			softParticles = Config.Bind(
				"Graphics",
				"Soft Particles",
				QualitySettings.softParticles,
				"Soft Particles for better object intersection"
			);

			pixelLightCount = Config.Bind(
				"Graphics",
				"Pixel Light Count",
				QualitySettings.pixelLightCount,
				"Max number of lights considered per pixel"
			);

			allowHDR = Config.Bind(
				"Graphics",
				"Allow HDR",
				true,
				"Please disable HDR while using Anti Aliasing!"
			);

			if (isEnabled.Value)
			{
				debug = isDebug.Value ? Logger : null;
			}
		}

		protected void Awake()
		{
			if (!isEnabled.Value) return;
			
			antiAliasing.SettingChanged += (object source, EventArgs args) =>
			{
				QualitySettings.antiAliasing = antiAliasing.Value;
			};

			anisotropicFiltering.SettingChanged += (object source, EventArgs args) =>
			{
				QualitySettings.anisotropicFiltering = anisotropicFiltering.Value;
			};

			softParticles.SettingChanged += (object source, EventArgs args) =>
			{
				QualitySettings.softParticles = softParticles.Value;
			};

			shadows.SettingChanged += (object source, EventArgs args) =>
			{
				QualitySettings.shadows = shadows.Value;
			};

			pixelLightCount.SettingChanged += (object source, EventArgs args) =>
			{
				QualitySettings.pixelLightCount = pixelLightCount.Value;
			};
		}

		protected void Start()
		{
			QualitySettings.antiAliasing = antiAliasing.Value;
			QualitySettings.anisotropicFiltering = anisotropicFiltering.Value;
			QualitySettings.softParticles = softParticles.Value;
			QualitySettings.shadows = shadows.Value;
			QualitySettings.pixelLightCount = pixelLightCount.Value;
		}

		protected void FixedUpdate()
		{
			if (Camera.main == null) return;
			Camera.main.allowHDR = allowHDR.Value;
		}
	}
}
