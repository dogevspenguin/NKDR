using System;
using System.Collections.Generic;
using System.Reflection;
using UniLinq;
using UnityEngine;

using BDArmory.UI;
using BDArmory.Utils;

namespace BDArmory.Settings
{
	[AttributeUsage(AttributeTargets.Field)]
	public class SettingsDataField : Attribute
	{
		public SettingsDataField()
		{
		}
		public static void Save()
		{
			ConfigNode fileNode = ConfigNode.Load(BDTISettings.settingsConfigURL);

			if (!fileNode.HasNode("IconSettings"))
			{
				fileNode.AddNode("IconSettings");
			}

			ConfigNode settings = fileNode.GetNode("IconSettings");
			using (IEnumerator<FieldInfo> field = typeof(BDTISettings).GetFields().AsEnumerable().GetEnumerator())
				while (field.MoveNext())
				{
					if (field.Current == null) continue;
					if (!field.Current.IsDefined(typeof(SettingsDataField), false)) continue;

					settings.SetValue(field.Current.Name, field.Current.GetValue(null).ToString(), true);
				}
			if (!fileNode.HasNode("TeamColors"))
			{
				fileNode.AddNode("TeamColors");
			}

			ConfigNode colors = fileNode.GetNode("TeamColors");

			foreach (var keyValuePair in BDTISetup.Instance.ColorAssignments)
			{
				Debug.Log(keyValuePair.ToString());
				string color = $"{Mathf.RoundToInt(keyValuePair.Value.r * 255)},{Mathf.RoundToInt(keyValuePair.Value.g * 255)},{Mathf.RoundToInt(keyValuePair.Value.b * 255)},{Mathf.RoundToInt(keyValuePair.Value.a * 255)}";
				colors.SetValue(keyValuePair.Key.ToString(), color, true);
			}

			fileNode.Save(BDTISettings.settingsConfigURL);
		}
		public static void Load()
		{
			ConfigNode fileNode = ConfigNode.Load(BDTISettings.settingsConfigURL);
			if (!fileNode.HasNode("IconSettings")) return;

			ConfigNode settings = fileNode.GetNode("IconSettings");

			using (IEnumerator<FieldInfo> field = typeof(BDTISettings).GetFields().AsEnumerable().GetEnumerator())
				while (field.MoveNext())
				{
					if (field.Current == null) continue;
					if (!field.Current.IsDefined(typeof(SettingsDataField), false)) continue;

					if (!settings.HasValue(field.Current.Name)) continue;
					object parsedValue = ParseValue(field.Current.FieldType, settings.GetValue(field.Current.Name));
					if (parsedValue != null)
					{
						field.Current.SetValue(null, parsedValue);
					}
				}
			if (!fileNode.HasNode("TeamColors")) return;
			ConfigNode colors = fileNode.GetNode("TeamColors");
			for (int i = 0; i < colors.CountValues; i++)
			{
				Debug.Log("[BDArmory.BDTISettingsField]: loading team " + colors.values[i].name + "; color: " + GUIUtils.ParseColor255(colors.values[i].value));
				if (BDTISetup.Instance.ColorAssignments.ContainsKey(colors.values[i].name))
				{
					BDTISetup.Instance.ColorAssignments[colors.values[i].name] = GUIUtils.ParseColor255(colors.values[i].value);
				}
				else
				{
					BDTISetup.Instance.ColorAssignments.Add(colors.values[i].name, GUIUtils.ParseColor255(colors.values[i].value));
				}
			}
		}
		public static object ParseValue(Type type, string value)
		{
			if (type == typeof(bool))
			{
				return bool.Parse(value);
			}
			else if (type == typeof(float))
			{
				return float.Parse(value);
			}
			else if (type == typeof(string))
			{
				return value;
			}
			Debug.LogError("[BDArmory.BDTISettingsField]: BDAPersistentSettingsField to parse settings field of type " + type +
						   " and value " + value);

			return null;
		}
	}
}