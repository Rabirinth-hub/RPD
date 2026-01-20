/* 废弃
using System;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using RimPersonaDirector;
using Verse;

[HarmonyPatch]
public static class DirectorVariableProvider
{
	private static bool _initialized;

	private static Type _mustacheContextType;

	[HarmonyTargetMethod]
	public static MethodBase TargetMethod()
	{
		Type type = AccessTools.TypeByName("RimTalk.Prompt.MustacheParser");
		if (type == null)
		{
			return null;
		}
		_mustacheContextType = AccessTools.TypeByName("RimTalk.Prompt.MustacheContext");
		if (_mustacheContextType == null)
		{
			return null;
		}
		return AccessTools.Method(type, "EvaluateExpression", new Type[2]
		{
			typeof(string),
			_mustacheContextType
		}, (Type[])null);
	}

	[HarmonyPrefix]
	public static bool InterceptDirectorVariables(string expression, object context, ref string __result)
	{
		if (!_initialized)
		{
			_initialized = true;
		}
		string text = expression.Trim().ToLowerInvariant();
		if (!text.StartsWith("director."))
		{
			return true;
		}
		try
		{
			string expression2 = text.Substring(9);
			__result = ResolveDirectorVariable(expression2, context);
			return false;
		}
		catch (Exception ex)
		{
			__result = "Error parsing '" + expression + "': " + ex.Message;
			return false;
		}
	}

	public static string ResolveDirectorVariable(string expression, object context)
	{
		Pawn pawn = null;
		string text = expression;
		Match match = Regex.Match(expression, "^pawn(\\d+)\\.(.+)");
		if (match.Success)
		{
			int num = int.Parse(match.Groups[1].Value);
			text = match.Groups[2].Value;
			if (AccessTools.Property(_mustacheContextType, "Pawns")?.GetValue(context) is IList list && num >= 1 && num <= list.Count)
			{
				pawn = list[num - 1] as Pawn;
			}
		}
		else
		{
			pawn = AccessTools.Property(_mustacheContextType, "CurrentPawn")?.GetValue(context) as Pawn;
		}
		if (pawn == null)
		{
			return "";
		}
        if (text == "full_profile")
            return DirectorDataEngine.BuildCompleteData(pawn);
        else if (text == "basic.name")
            return pawn.LabelShortCap;
        else if (text == "basic.fullname")
            return pawn.Name?.ToStringFull ?? pawn.LabelShortCap;
        else if (text == "basic.gender")
            return pawn.gender.ToString();
        else if (text == "basic.age")
            return pawn.ageTracker.AgeBiologicalYears.ToString();
        else if (text == "basic.status")
            return DirectorUtils.GetPawnSocialStatus(pawn);
        else if (text == "basic.faction.label")
            return pawn.Faction?.Name ?? "None";
        else if (text == "basic.faction.desc")
            return pawn.Faction?.def?.description?.StripTags() ?? "";
        else if (text == "race.label")
            return pawn.def.label;
        else if (text == "race.desc")
            return pawn.def.description.StripTags();
        else if (text == "race.xenotype.label")
            return pawn.genes?.Xenotype?.label ?? "Baseliner";
        else if (text == "race.xenotype.desc")
            return pawn.genes?.Xenotype?.description.StripTags() ?? "";
        else if (text == "genes.list")
            return DirectorDataEngine.GetGenesInfo(pawn, includeDesc: false);
        else if (text == "genes.list_with_desc")
            return DirectorDataEngine.GetGenesInfo(pawn, includeDesc: true);
        else if (text == "backstory.childhood.title")
            return pawn.story?.Childhood?.TitleCapFor(pawn.gender) ?? "";
        else if (text == "backstory.childhood.desc")
            return pawn.story?.Childhood?.FullDescriptionFor(pawn).Resolve().StripTags() ?? "";
        else if (text == "backstory.adulthood.title")
            return pawn.story?.Adulthood?.TitleCapFor(pawn.gender) ?? "";
        else if (text == "backstory.adulthood.desc")
            return pawn.story?.Adulthood?.FullDescriptionFor(pawn).Resolve().StripTags() ?? "";
        else if (text == "traits.list")
            return DirectorDataEngine.GetTraitsInfo(pawn, includeDesc: false);
        else if (text == "traits.list_with_desc")
            return DirectorDataEngine.GetTraitsInfo(pawn, includeDesc: true);
        else if (text == "ideology.list")
            return DirectorDataEngine.GetIdeologyInfo(pawn, includeDesc: false);
        else if (text == "ideology.list_with_desc")
            return DirectorDataEngine.GetIdeologyInfo(pawn, includeDesc: true);
        else if (text == "skills.list")
            return DirectorDataEngine.GetSkillsInfo(pawn, includeDesc: false);
        else if (text == "skills.list_with_desc")
            return DirectorDataEngine.GetSkillsInfo(pawn, includeDesc: true);
        else if (text == "health.list")
            return DirectorDataEngine.GetHealthInfo(pawn, includeDesc: false);
        else if (text == "health.list_with_desc")
            return DirectorDataEngine.GetHealthInfo(pawn, includeDesc: true);
        else if (text == "relations")
            return DirectorDataEngine.GetRelationsInfo(pawn);
        else if (text == "equipment")
            return DirectorDataEngine.GetEquipmentInfo(pawn);
        else if (text == "inventory")
            return DirectorDataEngine.GetInventoryInfo(pawn);
        else if (text == "rimpsyche")
            return DirectorDataEngine.GetRimPsycheInfo(pawn);
        else if (text == "memories")
            return DirectorDataEngine.GetMemoryInfo(pawn);
        else if (text == "common_knowledge")
            return DirectorDataEngine.GetCommonKnowledgeInfo(pawn, DirectorDataEngine.BuildCompleteData(pawn));
        else
            return "{Unknown Director Var: " + text + "}";
    }
}
*/
