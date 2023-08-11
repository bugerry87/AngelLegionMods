using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using protocol.game;
using System.Drawing;
using Steamworks;

namespace CustomAssetLoader
{
	[BepInPlugin("bugerry.CustomAssetLoader", "Custom Asset Loader", "0.0.1")]
	public partial class BepInExPlugin : BaseUnityPlugin
	{
		public static BepInExPlugin context;
		public static ManualLogSource debug;
		public static readonly Dictionary<int, XDocument> FashionConfigRegister = new Dictionary<int, XDocument>();
		public static readonly Dictionary<int, s_t_dress> FashionDressRegister = new Dictionary<int, s_t_dress>();

		public static ConfigEntry<bool> isEnabled;
		public static ConfigEntry<bool> isDebug;
		public static ConfigEntry<string> fashionAssetPath;
		public static ConfigEntry<string> fashionSavePath;

		public static Dictionary<int, int> FashionMap = new Dictionary<int, int>();

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
			fashionAssetPath = Config.Bind(
				"Fashion",
				"Asset Path",
				"custom/fashion",
				"Relative path to custom fashion assets"
			);
			fashionSavePath = Config.Bind(
				"Fashion",
				"Save File",
				string.Join("/", Application.persistentDataPath, "fashion.sav"),
				"File path to fashion save file"
			);

			debug = isDebug.Value ? Logger : null;

			if (isEnabled.Value)
			{
				try
				{
					Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
					debug?.LogInfo("CustomAssetLoader Patched");
				}
				catch
				{
					debug?.LogError("CustomAssetLoader Patch Failed!!!");
				}
			}
		}

		protected void LoadFashionConfigs()
		{
			var assetPath = string.Join("/", Application.streamingAssetsPath, fashionAssetPath.Value);
			foreach (var file in Directory.EnumerateFiles(assetPath, "*.xml", SearchOption.TopDirectoryOnly))
			{
				try
				{
					debug?.LogInfo($"Loading config: {file}");
					var doc = XDocument.Load(file);
					foreach (var part in doc.Elements("part"))
					{
						var icon = part.Element("icon");
						var has_color = int.TryParse(icon?.Attribute("color")?.Value, out int color);
						var attr_id = part.Attribute("id");
						if (attr_id != null && int.TryParse(attr_id.Value, out int id))
						{
							foreach (var mesh in part.Elements("mesh"))
							{
								var attr_pack = mesh.Attribute("pack");
								attr_pack?.SetValue(string.Join("/", fashionAssetPath.Value, attr_pack?.Value));
							}

							FashionConfigRegister.Add(id, doc);
							FashionDressRegister.Add(id, new s_t_dress()
							{
								id = int.Parse(part.Attribute("id")?.Value),
								name = part.Attribute("display_name")?.Value ?? part.Attribute("name")?.Value,
								type = 1,
								res = part.Attribute("name")?.Value,
								desc = part.Element("description")?.Value,
								color = has_color ? color : 2,
								icon = icon?.Attribute("texture")?.Value,
								access = part.Element("access")?.Value,
								dlcId = "",
								is_show = 1,
								suipian_id = 0,
								suipian_num = 0,
								target_id = 0,
								tz_id = 0,
								exclusive_role_id = 0,
								extra_dispose = 200,
								bind_hair = 0,
							});
						}
						else
						{
							debug?.LogWarning($"Missing id in {file}");
						}
					}
				}
				catch (Exception e)
				{
					debug?.LogError($"Error while loading {file}: {e}");
				}
			}
		}

		protected void LoadFashionSaveFile()
		{
			try
			{
				if (File.Exists(fashionSavePath.Value))
				{
					using (var fileStream = new FileStream(fashionSavePath.Value, FileMode.Open))
					{
						FashionMap = new BinaryFormatter().Deserialize(fileStream) as Dictionary<int, int>;
						fileStream.Flush();
						fileStream.Close();
					}
				}
				else
				{
					debug?.LogWarning($"Save file {fashionSavePath.Value} not found!");
				}
			}
			catch (Exception e)
			{
				debug?.LogError($"Error while loading {fashionSavePath.Value}: {e}");
			}
		}

		protected void Awake()
		{
			LoadFashionSaveFile();
			Task.Run(LoadFashionConfigs);
			debug.LogInfo("CustomAssetLoader Awaken");
		}

		protected void OnDestroy()
		{
			try
			{
				using (var fileStream = new FileStream(
					fashionSavePath.Value,
					FileMode.OpenOrCreate
				)) {
					new BinaryFormatter().Serialize(fileStream, FashionMap);
					fileStream.Flush();
					fileStream.Close();
				}
			}
			catch
			{
				debug?.LogError($"Error while saving {fashionSavePath.Value}!");
			}
		}

		[HarmonyPatch(typeof(sys), "init_player")]
		public static class Sys_InitPlayer_Patch
		{
			public static MethodBase TargetMethod()
			{
				return typeof(sys).GetMethod("init_player");
			}

			public static void Postfix(sys __instance)
			{
				debug.LogInfo("Sys Init Player");
				foreach (var entry in FashionMap)
				{
					var role = __instance.m_self.get_card_id(entry.Key)?.get_role();
					if (role != null)
					{
						role.dress = entry.Value;
					}
				}
			}
		}

		[HarmonyPatch(typeof(ConfigManager), nameof(ConfigManager.get_t_dress))]
		public static class ConfigManager_GetDress_Patch
		{
			public static bool Prefix(int id, out s_t_dress __result)
			{
				__result = null;
				if (id >= 0)
				{
					return true;
				}
				else if (FashionDressRegister.TryGetValue(id, out __result))
				{
					return false;
				}
				return true;
			}
		}

		[HarmonyPatch(typeof(player), nameof(player.has_dress))]
		public static class Player_HasDress_Patch
		{
			public static bool Prefix(int dress_id, out bool __result)
			{
				__result = true;
				return dress_id >= 0;
			}
		}

		[HarmonyPatch(typeof(player), nameof(player.has_dress_num))]
		public static class Player_HasDressNum_Patch
		{
			public static bool Prefix(int dress_id, out int __result)
			{
				__result = 1;
				return dress_id >= 0;
			}
		}

		[HarmonyPatch(typeof(player), nameof(player.the_dress_max_num))]
		public static class Player_DressMaxNum_Patch
		{
			public static bool Prefix(s_t_dress t_dress, out int __result)
			{
				__result = 2;
				return t_dress.id >= 0;
			}
		}

		[HarmonyPatch(typeof(dress_gui), nameof(dress_gui.reset))]
		public static class DressGui_Reset_Patch
		{
			public static ccard card;
			public static void Prefix(ccard _card)
			{
				card = _card;
			}
		}

		[HarmonyPatch(typeof(dress_gui), nameof(dress_gui.dress_save))]
		public static class DressGui_DressSave_Patch
		{
			public static bool Prefix()
			{
				var id = DressGui_Reset_Patch.card.get_template_id();
				var dress_id = DressGui_Reset_Patch.card.get_role().dress;
				if (FashionMap != null && dress_id < 0)
				{
					var card = sys._instance.m_self.get_card_id(id);
					card.get_role().dress = dress_id;
					FashionMap[id] = dress_id;
					return false;
				}
				else
				{
					FashionMap?.Remove(id);
					return true;
				}
			}
		}

		[HarmonyPatch(typeof(dress_gui), "list_item")]
		public static class DressGui_ListItem_Patch
		{
			public static s_t_dress dress;

			public static MethodBase TargetMethod()
			{
				return typeof(dress_gui).GetMethod("list_item");
			}

			public static void Prefix(List<s_t_dress> t_dresses)
			{
				var template = t_dresses[0];
				foreach (var dress in FashionDressRegister.Values)
				{
					dress.icon = dress.icon ?? template.icon;
					t_dresses.Insert(0, dress);
				}
			}
		}

		[HarmonyPatch(typeof(battle), nameof(battle.add_unit))]
		public static class Battle_AddUnit_Patch
		{
			public static void Prefix(battle __instance, msg_fight_role _role, Vector3 pos)
			{
				if (_role.site < 6 && FashionMap.TryGetValue(_role.id, out int dress_id))
				{
					_role.dress_id = dress_id;
				}
			}
		}

		[HarmonyPatch(typeof(unit_manager), nameof(unit_manager.load_part_xml))]
		public static class UnitManager_LoadPartXml_Patch
		{
			public static void Postfix(string name, unit_config __result)
			{
				if (name.EndsWith("part_ex"))
				{
					var unit = __result.m_xml.Element("unit");
					if (unit != null)
					{
						foreach (var doc in FashionConfigRegister.Values)
						{
							unit.Add(doc.Elements("part"));
						}
					}
					__result.m_part = unit.Elements("part");
				}
			}
		}
	}
}
