using System.Collections.Immutable;
using System.IO;
using System.Runtime.Loader;
using HarmonyLib;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Nanoray.PluginManager;
using Nanoray.PluginManager.Cecil;
using Nickel;
using OneOf;
using OneOf.Types;

namespace Fayti1703.SpriteReplacer;

[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
public class SpriteReplacer : Mod {
	internal static SpriteReplacer Instance { get; private set; } = null!;
	internal readonly ILogger logger;
	internal readonly Lazy<ReplacementRegistry> replacementRegistry;

	public SpriteReplacer(
		ILogger modLogger,
		ExtendablePluginLoader<IModManifest, Mod> modLoader,
		Func<IPluginPackage<IModManifest>, ILogger> modLoggerProvider,
		IModHelper modHelper,
		ExtendableAssemblyDefinitionEditor assemblyEditor
	) {
		Instance = this;
		this.logger = modLogger;
		this.replacementRegistry = new Lazy<ReplacementRegistry>(() => new ReplacementRegistry(modHelper.Content.Sprites, modLogger));
		modLoader.RegisterPluginLoader(new SpriteReplacementLoader(
			this.replacementRegistry,
			modLoggerProvider
		));
		assemblyEditor.RegisterDefinitionEditor(new AtlasBypassEditor());
		modHelper.Events.OnModLoadPhaseFinished += (_, phase) => {
			if(phase != ModLoadPhase.AfterGameAssembly) return;
			Harmony harmony = new("Fayti1703.SpriteReplacer");
			harmony.PatchAll(typeof(SpriteReplacer).Assembly);
		};
		AssemblyLoadContext.Default.Resolving += (_, name) => name.FullName == typeof(SpriteReplacer).Assembly.GetName().FullName ? typeof(SpriteReplacer).Assembly : null;
	}
}

internal class SpriteReplacementLoader(
	Lazy<ReplacementRegistry> registry,
	Func<IPluginPackage<IModManifest>, ILogger> modLoggerProvider
) : IPluginLoader<IModManifest, Mod> {
	public OneOf<Yes, No, Error<string>> CanLoadPlugin(IPluginPackage<IModManifest> package) {
		if(package.Manifest.ModType != "SpriteReplacement") return new No();
		if(package.Manifest.LoadPhase < ModLoadPhase.AfterGameAssembly)
			return new Error<string>("SpriteReplacement mods must load in or after phase `AfterGameAssembly`.");
		if(!package.PackageRoot.GetRelativeDirectory("sprites").Exists)
			return new Error<string>("SpriteReplacement mod is missing critical `sprites` directory.");

		return new Yes();
	}

	public PluginLoadResult<Mod> LoadPlugin(IPluginPackage<IModManifest> package) {
		SpriteReplacement mod = new(package, modLoggerProvider(package));

		ImmutableList<ReplacementSprite>.Builder sprites = ImmutableList<ReplacementSprite>.Empty.ToBuilder();
		List<string> warnings = [];

		foreach(IDirectoryInfo modDirectory in package.PackageRoot.GetRelativeDirectory("sprites").Directories) {
			Queue<IFileSystemInfo> toWalk = new(modDirectory.Children);
			while(toWalk.Count > 0) {
				IFileSystemInfo entry = toWalk.Dequeue();
				if(entry.IsDirectory) {
					foreach(IFileSystemInfo child in entry.AsDirectory!.Children) {
						toWalk.Enqueue(child);
					}
				} else if(entry.IsFile) {
					string localPath = Path.GetFileNameWithoutExtension(modDirectory.GetRelativePathTo(entry));
					string errorPath = package.PackageRoot.GetRelativePathTo(entry);
					sprites.Add(new ReplacementSprite(
						mod,
						modDirectory.Name,
						localPath,
						errorPath,
						entry.AsFile!
					));
				} else {
					warnings.Add($"Unhandled file system entry at {package.PackageRoot.GetRelativePathTo(entry)}.");
				}
			}
		}

		mod.AssignSprites(sprites.ToImmutable());

		registry.Value.AddSpriteReplacement(mod);

		return new PluginLoadResult<Mod>.Success { Plugin = mod, Warnings = warnings };
	}
}
