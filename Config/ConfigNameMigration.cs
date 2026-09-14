// Vendored verbatim from Ottomation_ModLib, Ottomation.Lib.Config/ConfigNameMigration.cs,
// apart from the namespace. OttoAura does not inherit OttoMod, so it calls Apply itself
// from Plugin.Awake before the first bind and routes its config() helper through
// ConfigName. The doc comments still name the library's Config class, which OttoAura does
// not have; they are left as written so a future diff is a diff and not a reading exercise.
// Source of record: Ottomation_ModLib ADR-0007 and ADR-0008.

using System;
using System.Collections.Generic;
using System.Text;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace OttoAura.Config;

/// <summary>
/// One time rewrite of a plugin's existing .cfg so the section headers and keys carry
/// the <see cref="ConfigName" /> spelling before anything binds against them.
/// </summary>
/// <remarks>
/// Run this after <see cref="Config.File" /> is set and before the first
/// <see cref="Config.Define{T}(bool, string, string, T)" />. Values, comments and any
/// line that does not parse are copied through untouched, and a file that already reads
/// correctly is not rewritten at all.
///
/// A plugin that does not inherit OttoMod calls this itself, before its first bind, and
/// binds through <see cref="ConfigName.Section" /> and <see cref="ConfigName.Key" />.
/// </remarks>
public static class ConfigNameMigration
{
	public static void Apply(ConfigFile file, ManualLogSource? logger)
	{
		try
		{
			string path = file.ConfigFilePath;
			if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
			{
				// A first run has nothing to migrate.
				return;
			}
			string[] lines = System.IO.File.ReadAllLines(path);
			// A downgrade to a pre-migration build re-adds the old spelling on save, so a
			// file can hold both. Renaming the old line would leave two identical keys and
			// let load order decide the value, so the already correct name wins and the
			// stale line is dropped instead.
			HashSet<string> present = CollectNormalizedNames(lines);
			HashSet<string> seen = new HashSet<string>();
			List<string> kept = new List<string>();
			string currentSection = string.Empty;
			bool changed = false;
			for (int i = 0; i < lines.Length; i++)
			{
				string? sectionName = SectionName(lines[i]);
				if (sectionName != null)
				{
					currentSection = ConfigName.Section(sectionName);
				}
				string? duplicateKey = DuplicateKey(lines[i], currentSection, present, seen);
				if (duplicateKey != null)
				{
					logger?.LogWarning("Dropping the stale duplicate " + duplicateKey);
					changed = true;
					continue;
				}
				string? rewritten = RewriteLine(lines[i]);
				kept.Add(rewritten ?? lines[i]);
				if (rewritten != null)
				{
					changed = true;
				}
			}
			lines = kept.ToArray();
			if (!changed)
			{
				return;
			}
			// The whole text is built first so a failure can not truncate the config.
			StringBuilder builder = new StringBuilder();
			foreach (string line in lines)
			{
				builder.Append(line).Append(Environment.NewLine);
			}
			// The backup is the only copy of the player's values once the write lands,
			// so it is written before the rename and removed only on success.
			string backup = path + ".premigration.bak";
			System.IO.File.Copy(path, backup, overwrite: true);
			System.IO.File.WriteAllText(path, builder.ToString());
			logger?.LogInfo("Migrated config names to PascalCase: " + path);
			try
			{
				// Rebind against the new names so the old values are picked up.
				file.Reload();
			}
			catch (Exception reloadFailure)
			{
				// The file on disk is already renamed. Leaving it there would let the next
				// Bind save an indeterminate table over it, so put the original back.
				System.IO.File.Copy(backup, path, overwrite: true);
				logger?.LogWarning($"Config name migration rolled back: {reloadFailure}");
				return;
			}
			finally
			{
				TryDelete(backup, logger);
			}
		}
		catch (Exception ex)
		{
			// A failed migration must never stop the plugin from loading.
			logger?.LogWarning($"Config name migration skipped: {ex}");
		}
	}

	/// <summary>
	/// Returns the section name when the line is a section header, otherwise null.
	/// </summary>
	private static string? SectionName(string line)
	{
		string trimmed = line.Trim();
		if (trimmed.Length >= 2 && trimmed[0] == '[' && trimmed[trimmed.Length - 1] == ']')
		{
			return trimmed.Substring(1, trimmed.Length - 2);
		}
		return null;
	}

	/// <summary>
	/// Returns the key name of a "Section/Key" entry line, or null when the line is not one.
	/// </summary>
	private static string? KeyName(string line)
	{
		string trimmed = line.Trim();
		if (trimmed.Length == 0 || trimmed[0] == '#' || SectionName(line) != null)
		{
			return null;
		}
		int equals = line.IndexOf('=');
		if (equals < 0)
		{
			return null;
		}
		string key = line.Substring(0, equals).Trim();
		return (key.Length == 0) ? null : key;
	}

	/// <summary>
	/// Every "Section/Key" pair the file already spells correctly. A line that would be
	/// renamed onto one of these is a stale duplicate rather than a value to keep.
	/// </summary>
	private static HashSet<string> CollectNormalizedNames(string[] lines)
	{
		HashSet<string> names = new HashSet<string>();
		string section = string.Empty;
		foreach (string line in lines)
		{
			string? sectionName = SectionName(line);
			if (sectionName != null)
			{
				section = ConfigName.Section(sectionName);
				continue;
			}
			string? key = KeyName(line);
			if (key != null && ConfigName.Key(key) == key)
			{
				names.Add(section + "/" + key);
			}
		}
		return names;
	}

	/// <summary>
	/// Returns the "Section/Key" pair to report when this line must be dropped, or null
	/// when the line is kept. A line is dropped when it carries an old spelling whose
	/// correct spelling already exists, or when its correct spelling was already seen.
	/// </summary>
	private static string? DuplicateKey(string line, string section, HashSet<string> present, HashSet<string> seen)
	{
		string? key = KeyName(line);
		if (key == null)
		{
			return null;
		}
		string normalized = ConfigName.Key(key);
		if (normalized.Length == 0)
		{
			return null;
		}
		string pair = section + "/" + normalized;
		if (normalized != key && present.Contains(pair))
		{
			return section + "/" + key;
		}
		if (!seen.Add(pair))
		{
			return section + "/" + key;
		}
		return null;
	}

	/// <summary>
	/// Removes the backup once the migration has taken. A backup left behind is harmless,
	/// so a failure here is logged and nothing more.
	/// </summary>
	private static void TryDelete(string path, ManualLogSource? logger)
	{
		try
		{
			System.IO.File.Delete(path);
		}
		catch (Exception ex)
		{
			logger?.LogWarning($"Could not remove the migration backup {path}: {ex.Message}");
		}
	}

	/// <summary>
	/// Returns the rewritten line, or null when the line must be copied through as is.
	/// </summary>
	private static string? RewriteLine(string line)
	{
		string trimmed = line.Trim();
		if (trimmed.Length == 0 || trimmed[0] == '#')
		{
			// Comments, both # and ##, are regenerated by BepInEx from the descriptions.
			return null;
		}
		if (trimmed.Length >= 2 && trimmed[0] == '[' && trimmed[trimmed.Length - 1] == ']')
		{
			string section = trimmed.Substring(1, trimmed.Length - 2);
			string normalized = ConfigName.Section(section);
			if (normalized.Length == 0 || normalized == section)
			{
				return null;
			}
			return Indent(line) + "[" + normalized + "]" + TrailingSpace(line);
		}
		int equals = line.IndexOf('=');
		if (equals < 0)
		{
			return null;
		}
		string left = line.Substring(0, equals);
		string key = left.Trim();
		string normalizedKey = ConfigName.Key(key);
		if (key.Length == 0 || normalizedKey.Length == 0 || normalizedKey == key)
		{
			return null;
		}
		// Only the left side moves. Everything from the '=' onward survives byte for byte.
		return Indent(left) + normalizedKey + TrailingSpace(left) + line.Substring(equals);
	}

	private static string Indent(string line)
	{
		int i = 0;
		while (i < line.Length && char.IsWhiteSpace(line[i]))
		{
			i++;
		}
		return line.Substring(0, i);
	}

	private static string TrailingSpace(string line)
	{
		int i = line.Length;
		while (i > 0 && char.IsWhiteSpace(line[i - 1]))
		{
			i--;
		}
		return line.Substring(i);
	}
}
