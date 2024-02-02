using System.Collections.Immutable;
using System.IO;
using System.Text;
using Fayti1703.CommonLib.FluentAccess;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework.Graphics;
using Nanoray.PluginManager;
using Nickel;

namespace Fayti1703.SpriteReplacer;

internal class ReplacementSprite(SpriteReplacement owner, string modUniqueName, string localPath, string errorPath, IFileInfo fsHandle) {
	public readonly SpriteReplacement owner = owner;
	public readonly string modUniqueName = modUniqueName;
	public readonly string localPath = localPath;
	public readonly string errorPath = errorPath;
	public readonly IFileInfo fsHandle = fsHandle;
}

internal class ReplacementRegistry(IModSprites spriteHelper, ILogger modLogger) {
	private static readonly ImmutableHashSet<char> permittedCharacters;
	private bool isLogging = false;

	static ReplacementRegistry() {
		/* `fsSafe` is the 'Portable Filename Character Set', as defined by POSIX.1-2017, plus the '/' character. */
		ImmutableHashSet<char>.Builder fsSafe = ImmutableHashSet<char>.Empty.ToBuilder();
		fsSafe.Add('/'); fsSafe.Add('.'); fsSafe.Add('_'); fsSafe.Add('-');
		for(char c = 'A'; c <= 'Z'; c++) fsSafe.Add(c);
		for(char c = 'a'; c <= 'z'; c++) fsSafe.Add(c);
		for(char c = '0'; c <= '9'; c++) fsSafe.Add(c);
		permittedCharacters = fsSafe.ToImmutable();
	}

	private static string LocalNameToPath(string localName) {
		StringBuilder pathStringBuilder = new StringBuilder(localName).Replace("::", "/");
		for(int i = 0; i < pathStringBuilder.Length; i++) {
			if(!permittedCharacters.Contains(pathStringBuilder[i]))
				pathStringBuilder[i] = '_';
		}

		string pathString = pathStringBuilder.ToString().TrimStart('/');
		if(pathString.EndsWith(".png"))
			pathString = pathString[..^4];
		return pathString;
	}

	internal Texture2D? TryGetReplacement(Spr theSprite) {
		if(this.cache.TryGetValue(theSprite, out Texture2D? texture)) return texture;
		texture = this.TryLoadReplacement(theSprite);
		try {
			this.cache.Add(theSprite, texture);
		} catch(Exception) {
			texture?.Dispose();
			throw;
		}

		return texture;
	}

	private readonly Dictionary<string, Dictionary<string, List<ReplacementSprite>>> replacements = [];
	private readonly Dictionary<Spr, Texture2D?> cache = [];

	private void StartLogging(string whoDidThis) {
		if(this.isLogging) return;
		this.isLogging = true;
		modLogger.Log(LogLevel.Warning, "Mod {uniqueName} activated sprite logging. If you are not actively developing this mod, please tell the author to remove the `LogSprites` property!", whoDidThis);
	}

	private Texture2D? TryLoadReplacement(Spr theSprite) {
		ISpriteEntry? spriteEntry = spriteHelper.LookupBySpr(theSprite);
		if(spriteEntry == null) {
			modLogger.Log(LogLevel.Warning, "Could not find declaration for sprite {theSprite}, skipping lookup.", theSprite);
			return null;
		}

		string modUniqueName = spriteEntry.ModOwner.UniqueName;
		string pathName = LocalNameToPath(spriteEntry.LocalName);
		if(this.isLogging)
			modLogger.Log(LogLevel.Debug, "Looking for a replacement with ('{modUniqueName}', '{pathName}').", modUniqueName, pathName);
		if(!this.replacements.TryGetValue(modUniqueName, out Dictionary<string, List<ReplacementSprite>>? modReplacements)) {
			if(this.isLogging)
				modLogger.Log(LogLevel.Debug, "No replacements for target mod.");
			return null;
		}

		if(!modReplacements.TryGetValue(pathName, out List<ReplacementSprite>? replacements)) {
			if(this.isLogging)
				modLogger.Log(LogLevel.Debug, "No replacements with given path.");
			return null;
		}
		foreach(ReplacementSprite replacement in replacements) {
			try {
				if(this.isLogging)
					modLogger.Log(LogLevel.Debug, "Loading {filePath} from mod {uniqueName}.", replacement.errorPath, replacement.owner.thePackage.Manifest.UniqueName);
				using Stream replacementStream = replacement.fsHandle.OpenRead();
				return Texture2D.FromStream(MG.inst.GraphicsDevice, replacementStream);
			} catch(Exception e) {
				replacement.owner.logger.Log(LogLevel.Error, e, "Could not load texture replacement {errorPath}.", replacement.errorPath);
			}
		}

		if(this.isLogging)
			modLogger.Log(LogLevel.Debug, "All replacements failed to load.");
		return null;
	}

	private void DropCache() {
		foreach(Texture2D? texture in this.cache.Values) texture?.Dispose();
		this.cache.Clear();
	}

	public void AddSpriteReplacement(SpriteReplacement mod) {
		this.DropCache();
		foreach(ReplacementSprite sprite in mod.Sprites) {
			this.replacements
				.GetOrCreate(sprite.modUniqueName, () => [])
				.GetOrCreate(sprite.localPath, () => [])
				.Add(sprite)
			;
		}

		modLogger.Log(LogLevel.Debug, "LogSprites debug data: {data}",  string.Join(", ", mod.thePackage.Manifest.ExtensionData.Keys));
		if(!mod.thePackage.Manifest.ExtensionData.TryGetValue("LogSprites", out object? logSpritesObj)) return;
		if(logSpritesObj is bool logSprites) {
			if(logSprites)
				this.StartLogging(mod.thePackage.Manifest.UniqueName);
			else
				modLogger.Log(LogLevel.Warning, "Mod {uniqueName} attempted to disable sprite logging. Please remove the `LogSprites` property entirely.", mod.thePackage.Manifest.UniqueName);
		} else {
			modLogger.Log(LogLevel.Warning, "Mod {uniqueName} has an invalid `LogSprites` property. Expected `bool`, got `{actualType}`.", mod.thePackage.Manifest.UniqueName, logSpritesObj.GetType().ILName());
		}
	}
}

internal static class DictionaryExtensions {
	public static V GetOrCreate<K, V>(this IDictionary<K, V> dict, K key, Func<V> factory) {
		if(dict.TryGetValue(key, out V? value)) return value;
		dict.Add(key, factory());
		return dict[key];
	}
}
