using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using Nanoray.PluginManager;
using Nickel;

namespace Fayti1703.SpriteReplacer;

internal sealed class SpriteReplacement(IPluginPackage<IModManifest> thePackage, ILogger logger) : Mod {
	public readonly IPluginPackage<IModManifest> thePackage = thePackage;
	public readonly ILogger logger = logger;
	public ImmutableList<ReplacementSprite> Sprites { get; private set; } = null!;

	internal void AssignSprites(ImmutableList<ReplacementSprite> sprites) {
		if(this.Sprites != null)
			throw new InvalidOperationException("Cannot late init: already init.");
		this.Sprites = sprites;
	}
}
