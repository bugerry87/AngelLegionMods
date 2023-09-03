using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections;
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

namespace DressCode
{
	[BepInPlugin("bugerry.CustomAssetLoader", "Custom Asset Loader", "0.2.0")]
	public partial class BepInExPlugin : BaseUnityPlugin
	{
		public static BepInExPlugin context;
		public static ManualLogSource debug;
		public static readonly Dictionary<int, XDocument> FashionConfigRegister = new Dictionary<int, XDocument>();
		public static readonly Dictionary<int, s_t_dress> FashionDressRegister = new Dictionary<int, s_t_dress>();

		public static ConfigEntry<bool> isEnabled;
		public static ConfigEntry<bool> isDebug;
		public static ConfigEntry<bool> unlockAll;
		public static ConfigEntry<string> fashionAssetPath;
		public static ConfigEntry<string> fashionSavePath;

		public static Dictionary<int, int> FashionMap = new Dictionary<int, int>();
		public static Dictionary<string, LoadedAssetBundle> AssetBundles;

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
			unlockAll = Config.Bind(
				"Fashion",
				"Unlock All",
				false,
				"Unlocks all sort of fashion"
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

			if (isEnabled.Value)
			{
				debug = isDebug.Value ? Logger : null;
				Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
			}
		}

		protected void LoadFashionConfigs()
		{
			var assetPath = string.Join("/", Application.streamingAssetsPath, fashionAssetPath.Value);
			foreach (var file in Directory.EnumerateFiles(assetPath, "*.xml", SearchOption.AllDirectories))
			{
				try
				{
					debug?.LogInfo($"Loading config: {file}");
					var doc = XDocument.Load(file);
					foreach (var part in doc.Elements("part"))
					{
						var icon = part.Element("icon");
						var icon_pack = icon?.Attribute("pack")?.Value;
						var has_color = int.TryParse(icon?.Attribute("color")?.Value, out int color);
						var attr_id = part.Attribute("id");
						if (attr_id != null && int.TryParse(attr_id.Value, out int id))
						{
							foreach (var mesh in part.Elements("mesh"))
							{
								var mash_pack = mesh.Attribute("pack");
								mash_pack?.SetValue(string.Join("/", fashionAssetPath.Value, mash_pack?.Value));
							}

							FashionConfigRegister.Add(id, doc);
							FashionDressRegister.Add(id, new s_t_dress()
							{
								id = int.Parse(part.Attribute("id")?.Value),
								name = icon?.Attribute("name")?.Value ?? part.Attribute("name")?.Value,
								type = 1,
								res = part.Attribute("name")?.Value,
								desc = part.Element("description")?.Value,
								color = has_color ? color : 2,
								icon = icon_pack != null ? string.Join("/", fashionAssetPath.Value, icon_pack) : null,
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

		[HarmonyPatch(typeof(ResourceManager), nameof(ResourceManager.Init))]
		public static class ResoucreManager_LoadObjectAsync_Patch
		{
			public static void Postfix(Dictionary<string, LoadedAssetBundle> ___loadedAssetbundles)
			{
				AssetBundles = ___loadedAssetbundles;
			}
		}

		[HarmonyPatch(typeof(sys), nameof(sys.get_texture_2D_async))]
		public static class Sys_GetTexture2DAsync_Patch
		{
			public static bool Prefix(string name, out Task<Texture2D> __result)
			{
				__result = null;
				if (name != null && name.StartsWith(fashionAssetPath.Value))
				{
					try
					{
						var paths = name.Split(':');
						var pack = paths[0];
						var part = paths.Length > 1 ? paths[1] : Path.GetFileName(pack);
						var obj = Utility.ResManager.LoadObject(pack) as GameObject;
						var icon = obj?.transform?.Find(part);
						var renderer = icon?.GetComponent<SpriteRenderer>();
						__result = Task.Run(() => renderer?.sprite?.texture);
					}
					catch (Exception e)
					{
						debug?.LogError(e);
					}
					return false;
				}
				return true;
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
				//QualitySettings.antiAliasing = 12;
				debug.LogInfo("Sys Init Player");
				foreach (var entry in FashionMap)
				{
					var role = __instance.m_self.get_card_id(entry.Key)?.get_role();
					if (role != null && __instance.m_self.has_dress(entry.Value))
					{
						role.dress = entry.Value;
					}
				}
			}
		}

		[HarmonyPatch(typeof(ConfigManager), nameof(ConfigManager.get_t_dress))]
		public static class ConfigManager_GetDress_Patch
		{
			public static ConfigManager instance;

			public static bool Prefix(ConfigManager __instance, int id, out s_t_dress __result)
			{
				instance = __instance;
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
				return !unlockAll.Value && dress_id >= 0;
			}
		}

		[HarmonyPatch(typeof(player), nameof(player.has_dress_num))]
		public static class Player_HasDressNum_Patch
		{
			public static bool Prefix(int dress_id, out int __result)
			{
				__result = 1;
				return !unlockAll.Value && dress_id >= 0;
			}
		}

		[HarmonyPatch(typeof(player), nameof(player.the_dress_max_num))]
		public static class Player_DressMaxNum_Patch
		{
			public static bool Prefix(s_t_dress t_dress, out int __result)
			{
				__result = 2;
				return !unlockAll.Value && t_dress.id >= 0;
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
				if (FashionMap != null && (unlockAll.Value || dress_id < 0))
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
			public static void Prefix(msg_fight_role _role, Vector3 pos)
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
					if (unit != null && unit.Attribute("modded") == null)
					{
						foreach (var doc in FashionConfigRegister.Values)
						{
							unit.Add(doc.Elements("part"));
						}
						unit.Add(new XAttribute("modded", true));
						__result.m_part = unit.Elements("part");
					}
				}
			}
		}
	}
}
