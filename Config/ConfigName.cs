// Vendored verbatim from Ottomation_ModLib, Ottomation.Lib.Config/ConfigName.cs, apart
// from the namespace. OttoAura does not inherit OttoMod, so it cannot take the rule from
// the library and carries its own copy instead. Keep it byte for byte with the original
// so a future diff is a diff and not a reading exercise.
// Source of record: Ottomation_ModLib ADR-0007 and ADR-0008.

using System.Text;
using System.Text.RegularExpressions;

namespace OttoAura.Config;

/// <summary>
/// Turns a human written config section or key name into the PascalCase spelling
/// that the Ottomation series binds against. See ADR-0007 and ADR-0008.
/// </summary>
/// <remarks>
/// The name is split on every run of characters that are neither letters nor digits,
/// each part gets its first letter capitalized while the rest of the part is left
/// exactly as written (so LOD, TriggerL and NoCost survive), and the parts are joined
/// with nothing between them. A leading underscore is kept, so _Author stays _Author.
///
/// A section name also loses a leading ordinal such as "1 - " or "1.5 - ", which only
/// ever existed to order sections in Configuration Manager, and an empty section name
/// becomes <see cref="DefaultSection" />.
/// </remarks>
public static class ConfigName
{
	/// <summary>The section a setting lands in when it was bound under an empty name.</summary>
	public const string DefaultSection = "General";

	// "1 - General" or "1.5 - Fish". The lookahead keeps a bare "1 - " from vanishing.
	private static readonly Regex OrdinalPrefix = new Regex(@"^\s*\d+(?:\.\d+)*\s*-\s*(?=\S)");

	/// <summary>The spelling a section name is bound and migrated under.</summary>
	public static string Section(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return DefaultSection;
		}
		string normalized = Normalize(OrdinalPrefix.Replace(name, string.Empty, 1));
		return (normalized.Length == 0) ? DefaultSection : normalized;
	}

	/// <summary>The spelling a key name is bound and migrated under.</summary>
	public static string Key(string name)
	{
		return Normalize(name);
	}

	internal static string Normalize(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return name;
		}
		StringBuilder builder = new StringBuilder(name.Length);
		if (name[0] == '_')
		{
			builder.Append('_');
		}
		bool atPartStart = true;
		foreach (char c in name)
		{
			if (!char.IsLetterOrDigit(c))
			{
				atPartStart = true;
				continue;
			}
			builder.Append(atPartStart ? char.ToUpperInvariant(c) : c);
			atPartStart = false;
		}
		return builder.ToString();
	}
}
