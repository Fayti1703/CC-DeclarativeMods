using Fayti1703.CommonLib.Cecil.FluentAccess;
using Fayti1703.CommonLib.Cecil.ILEditing;
using HarmonyLib;
using JetBrains.Annotations;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
using Nanoray.PluginManager.Cecil;
using ILInstruction = Mono.Cecil.Cil.Instruction;

namespace Fayti1703.SpriteReplacer;

[HarmonyPatch]
[UsedImplicitly(ImplicitUseKindFlags.Access, ImplicitUseTargetFlags.WithMembers)]
internal static class SpriteLoaderPatch {
	[HarmonyPatch(typeof(SpriteLoader), nameof(SpriteLoader.Get))]
	[HarmonyPrefix]
	[HarmonyBefore(["Nickel"])]
	public static bool GetSpritePrefix(Spr id, out Texture2D? __result) {
		__result = SpriteReplacer.Instance.replacementRegistry.Value.TryGetReplacement(id);
		return __result == null;
	}
}

internal class AtlasBypassEditor : IAssemblyDefinitionEditor {
	public bool WillEditAssembly(string fileBaseName) => fileBaseName == "CobaltCore.dll";

	public void EditAssemblyDefinition(AssemblyDefinition definition) {
		AtlasBypass.PatchDraw(definition.MainModule);
	}
}

internal static class AtlasBypass {
	[UsedImplicitly(ImplicitUseKindFlags.Access)] /* via IL injection */
	public static bool HasAssignedReplacement(Spr id) {
		return SpriteReplacer.Instance.replacementRegistry.Value.TryGetReplacement(id) != null;
	}

	internal static void PatchDraw(ModuleDefinition module) {
		TypeReference Nullable_t = typeof(Nullable<>).RefIn(module);
		TypeReference double_t = typeof(double).RefIn(module);
		TypeReference bool_t = typeof(bool).RefIn(module);
		TypeReference Nullable_Vec_t = Nullable_t.Gmake(module.GetType("Vec"));
		MethodDefinition drawMethod = module.GetType("Draw").Mth(
			"Sprite",
			Nullable_t.Gmake(module.GetType("Spr")),
			double_t,
			double_t,
			bool_t,
			bool_t,
			double_t,
			Nullable_Vec_t,
			Nullable_Vec_t,
			Nullable_Vec_t,
			Nullable_t.Gmake(module.GetType("Rect")),
			Nullable_t.Gmake(module.GetType("Color")),
			typeof(BlendState).RefIn(module),
			typeof(SamplerState).RefIn(module),
			typeof(Effect).RefIn(module)
		);

		Collection<ILInstruction> methodBody = drawMethod.Body.Instructions;
		int match = methodBody.FindMatch(
			x => x.OpCode == OpCodes.Ldarga_S && ( (ParameterReference) x.Operand ).Name == "id",
			x => x.MatchCallIL("instance !0 valuetype [System.Runtime]System.Nullable`1<valuetype [CobaltCore]Spr>::get_Value()"),
			x => x.OpCode == OpCodes.Ldloca_S,
			x =>  x.MatchCallIL("instance bool class [System.Collections]System.Collections.Generic.Dictionary`2<valuetype [CobaltCore]Spr, class [CobaltCore]AtlasItem>::TryGetValue(!0, !1&)"),
			x => x.OpCode == OpCodes.Brfalse || x.OpCode == OpCodes.Brfalse_S
		);
		if(match == -1) throw new InvalidOperationException($"Could not match ILcode for {nameof(PatchDraw)} in {drawMethod.ILSignature()}");
		ILInstruction bypassTarget = methodBody[match + 2];
		ILInstruction trampoline = methodBody[match + 4];
		methodBody.Insert(match + 2, ILInstruction.Create(OpCodes.Dup));
		methodBody.Insert(match + 3, ILInstruction.Create(OpCodes.Call, typeof(AtlasBypass).RefIn(module).Rd().Mth("HasAssignedReplacement").RefIn(module)));
		methodBody.Insert(match + 4, ILInstruction.Create(OpCodes.Brfalse_S, bypassTarget));
		methodBody.Insert(match + 5, ILInstruction.Create(OpCodes.Pop));
		methodBody.Insert(match + 6, ILInstruction.Create(OpCodes.Pop));
		methodBody.Insert(match + 7, ILInstruction.Create(OpCodes.Ldc_I4_0));
		methodBody.Insert(match + 8, ILInstruction.Create(OpCodes.Br_S, trampoline));

#if LOG_METHOD_IL
		SpriteReplacer.Instance.logger.Log(LogLevel.Debug, "Final `{methodSignature}` method IL: \n{methodIL}", drawMethod.ILSignature(), string.Join('\n', methodBody.Select(x => x.ToString())));
#endif
	}

	private static bool MatchCallIL(this ILInstruction inst, string methodSignature) {
		if(inst.OpCode != OpCodes.Call && inst.OpCode != OpCodes.Callvirt) return false;
		return ( (MethodReference) inst.Operand ).ILSignature() == methodSignature;
	}
}
