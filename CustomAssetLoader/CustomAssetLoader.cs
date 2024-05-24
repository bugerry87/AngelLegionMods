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

namespace DressCode
{
	[BepInPlugin("bugerry.CustomAssetLoader", "Custom Asset Loader", "0.3.0")]
	public partial class CustomAssetLoader : BaseUnityPlugin
	{
		public static CustomAssetLoader context;
		public static ManualLogSource debug;
		public static readonly Dictionary<int, XDocument> ConfigRegister = new Dictionary<int, XDocument>();
		public static readonly Dictionary<int, s_t_dress> DressRegister = new Dictionary<int, s_t_dress>();

		public static ConfigEntry<bool> isEnabled;
		public static ConfigEntry<bool> isDebug;
		public static ConfigEntry<bool> unlockAll;
		public static ConfigEntry<string> fashionAssetPath;
		public static ConfigEntry<string> fashionSavePath;
		public static ConfigEntry<string> accessoryAssetPath;
		public static ConfigEntry<string> accessorySavePath;

		public static Dictionary<int, int> FashionMap = new Dictionary<int, int>();
		public static Dictionary<int, int> AccessoryMap = new Dictionary<int, int>();
		public static Dictionary<string, LoadedAssetBundle> AssetBundles;

		public CustomAssetLoader()
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
			accessoryAssetPath = Config.Bind(
				"Accessory",
				"Asset Path",
				"custom/accessory",
				"Relative path to custom accessory assets"
			);
			accessorySavePath = Config.Bind(
				"Accessory",
				"Save File",
				string.Join("/", Application.persistentDataPath, "accessory.sav"),
				"File path to accessory save file"
			);

			if (isEnabled.Value)
			{
				debug = isDebug.Value ? Logger : null;
				Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
			}
		}

		protected void LoadConfigs(string path)
		{
			var assetPath = string.Join("/", Application.streamingAssetsPath, path);
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
								mash_pack?.SetValue(string.Join("/", path, mash_pack?.Value));
							}

							ConfigRegister.Add(id, doc);
							DressRegister.Add(id, new s_t_dress()
							{
								id = int.Parse(part.Attribute("id")?.Value),
								name = icon?.Attribute("name")?.Value ?? part.Attribute("name")?.Value,
								type = part.Attribute("type")?.Value == "glasses" ? 2 : 1,
								res = part.Attribute("name")?.Value,
								desc = part.Element("description")?.Value,
								color = has_color ? color : 2,
								icon = icon_pack != null ? string.Join("/", path, icon_pack) : null,
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

		protected void LoadSaveFile(string path)
		{
			try
			{
				if (File.Exists(path))
				{
					using (var fileStream = new FileStream(path, FileMode.Open))
					{
						FashionMap = new BinaryFormatter().Deserialize(fileStream) as Dictionary<int, int>;
						fileStream.Flush();
						fileStream.Close();
					}
				}
				else
				{
					debug?.LogWarning($"Save file {path} not found!");
				}
			}
			catch (Exception e)
			{
				debug?.LogError($"Error while loading {path}: {e}");
			}
		}

		protected void Awake()
		{
			LoadSaveFile(fashionSavePath.Value);
			LoadSaveFile(accessorySavePath.Value);
			Task.Run(() => LoadConfigs(fashionAssetPath.Value));
			Task.Run(() => LoadConfigs(accessoryAssetPath.Value));
			debug?.LogInfo("Awaken");
		}

		protected void Save(Dictionary<int, int> map, string path)
		{
			try
			{
				using (var fileStream = new FileStream(
					path,
					FileMode.OpenOrCreate
				))
				{
					new BinaryFormatter().Serialize(fileStream, map);
					fileStream.Flush();
					fileStream.Close();
				}
			}
			catch
			{
				debug?.LogError($"Error while saving {path}!");
			}
		}

		protected void OnApplicationQuit()
		{
			debug?.LogInfo("Quit");
			Save(FashionMap, fashionSavePath.Value);
			Save(AccessoryMap, accessorySavePath.Value);
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
						var icon = obj?.transform?.Find(part) ?? obj?.transform;
						var renderer = icon?.GetComponentInChildren<SpriteRenderer>();
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
				debug?.LogInfo("Sys Init Player");
				foreach (var entry in FashionMap)
				{
					var role = __instance.m_self.get_card_id(entry.Key)?.get_role();
					if (role != null && __instance.m_self.has_dress(entry.Value))
					{
						role.dress = entry.Value;
					}
				}

				foreach (var entry in AccessoryMap)
				{
					var role = __instance.m_self.get_card_id(entry.Key)?.get_role();
					if (role != null && __instance.m_self.has_dress(entry.Value))
					{
						role.faces[39] = entry.Value;
					}
				}
			}
		}

		[HarmonyPatch(typeof(ConfigManager), nameof(ConfigManager.get_t_dress))]
		public static class ConfigManager_GetDress_Patch
		{
			public static bool Prefix(ConfigManager __instance, int id, out s_t_dress __result)
			{
				__result = null;
				if (id >= 0)
				{
					return true;
				}
				else if (DressRegister.TryGetValue(id, out __result))
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

		[HarmonyPatch(typeof(dress_gui), nameof(dress_gui.dress_save))]
		public static class DressGui_DressSave_Patch
		{
			public static bool Prefix(ccard ___m_card)
			{
				var id = ___m_card.get_template_id();
				var dress_id = ___m_card.get_role().dress;
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

		[HarmonyPatch(typeof(makeup_gui), "save_data")]
		public static class MakeUpGui_SaveData_Patch
		{
			public static MethodBase TargetMethod()
			{
				return typeof(makeup_gui).GetMethod("save_data");
			}

			public static void Prefix(
				makeup_gui __instance,
				ref int ___m_initial_glass_id,
				int[] ___m_face_color_datas
			) {
				var id = __instance.m_card.get_template_id();
				var accessory_id = ___m_face_color_datas[8];
				if (AccessoryMap != null && (unlockAll.Value || accessory_id < 0))
				{
					___m_initial_glass_id = accessory_id;
					AccessoryMap[id] = accessory_id;
				}
				else
				{
					AccessoryMap?.Remove(id);
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
				foreach (var dress in DressRegister.Values)
				{
					if (dress.type == 1)
					{
						dress.icon = dress.icon ?? template.icon;
						t_dresses.Insert(0, dress);
					}
				}
			}
		}

		[HarmonyPatch(typeof(makeup_gui), "ManageGlasses")]
		public static class MakeUpGui_ManageGlasses_Patch
		{
			public static MethodBase TargetMethod()
			{
				return typeof(makeup_gui).GetMethod("ManageGlasses");
			}

			public static void Prefix(List<s_t_dress> t_dresses)
			{
				var template = t_dresses[0];
				foreach (var dress in DressRegister.Values)
				{
					if (dress.type == 2)
					{
						dress.icon = dress.icon ?? template.icon;
						t_dresses.Insert(0, dress);
					}
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
						foreach (var doc in ConfigRegister.Values)
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
