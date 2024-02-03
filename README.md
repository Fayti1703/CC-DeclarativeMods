# Declarative Mod Frameworks

Framework mods that load other non-code mods.

### Dependencies
The majority of the required dependencies are declared in the `.csproj`. Perform a restore operation to fetch them.

These additional dependencies are required:

* The [Nickel Modloader](https://github.com/Shockah/Nickel)
  * If your Cobalt Core or Nickel installation path are not in one of the auto-detected directories,
    create a `Path.props.user` with contents like:
    ```xml
    <Project>
    	<PropertyGroup>
    		<ModLoaderPath>/path/to/Nickel</ModLoaderPath>
    		<GameExePath>/path/to/CobaltCore/</GameExePath>
    		<ModDeployModsPath>/path/to/ModLibrary</ModDeployModsPath>
    	</PropertyGroup>
    </Project>
    ```

## SpriteReplacer

Loads mods of type `"SpriteReplacement"`. Your `nickel.json` should look similar to this:
```json
{
	"UniqueName": "YourName.SampleSpriteReplacement",
	"Version": "1.0.0",
	"RequiredApiVersion": "0.1.0",
	"DisplayName": "Sample Sprite Replacement",
	"Author": "YourName",
	"ModType": "SpriteReplacement",
	"Dependencies": [{
		"UniqueName": "Fayti1703.SpriteReplacer",
		"Version": "1.0.0"
	}]
}
```

Replacement sprites are loaded from the mods `sprites` directory.
Create a folder for each mod (use `CobaltCore` for vanilla) whose sprites you wish to replace; then place
a file at the appropriate path within that folder.  
The file path must match the sprite's declared name; where `::` indicates a new directory and unsafe characters are replaced with `_`.

For example, a replacement for the sprite `map_shop` from `CobaltCore` should be placed at `sprites/CobaltCore/map_shop.png`.  
A replacement for the sprite `assets/Cards/Default.png` from `Shockah.Dracula` should be placed at `sprites/Shockah.Dracula/assets/Cards/Default.png`.

### Sprite Logging

If a replacement doesn't seem to work, or you need help figuring out the path for a given sprite, add the following to your `nickel.json`, just before the final `}` (make sure to add a `,` on the previous line):
```json
	"LogSprites": true
```
This will cause SpriteReplacer to log any replacement sprite lookups into the Nickel log, at `DEBUG` level. You will find messages like this:
```
[xxxx-xx-xx xx:xx:xx][Debug][Fayti1703.SpriteReplacer] Looking for a replacement with ('Shockah.Dracula', 'assets/Ship/Chassis').
[xxxx-xx-xx xx:xx:xx][Debug][Fayti1703.SpriteReplacer] No replacements for target mod.
[xxxx-xx-xx xx:xx:xx][Debug][Fayti1703.SpriteReplacer] Looking for a replacement with ('CobaltCore', 'map_shop').
[xxxx-xx-xx xx:xx:xx][Debug][Fayti1703.SpriteReplacer] Loading sprites\CobaltCore\map_shop.png from mod Fayti1703.SampleReplace.
[xxxx-xx-xx xx:xx:xx][Debug][Fayti1703.SpriteReplacer] Looking for a replacement with ('CobaltCore', 'map_question').
[xxxx-xx-xx xx:xx:xx][Debug][Fayti1703.SpriteReplacer] No replacements with given path.
```

Note that the "local name" (the second value in parentheses) is always in path-name form -- 
so if you wished to replace `('Shockah.Dracula', 'assets/Ship/Chassis')`,  you would simply place a file at `sprites/Shockah.Dracula/assets/Ship/Chassis.png`.

**After you are done, please make sure to remove the `"LogSprites": true` line from your mod's `nickel.json`.** Warnings are printed in the Nickel log to remind you.
