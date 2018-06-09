using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Harmony;
using System.Reflection;
using Verse;
using RimWorld;
using UnityEngine;
using Verse.Sound;
using Pharmacist;

/* notes
 * Medical Defaults vertical padding: 34
 * Medical Overview tab vertical padding: ~32
 * ITab pawn visitor (?) 44?
 * Medical Tab vertical padding: 30 minimum
 */

namespace PharmMedicinePatch
{
	[StaticConstructorOnStartup]
	class Main
	{
		static List<int> localMedsList;
		static Main()
		{
			Log.Message("Patching Pharmacist medcare list");
			
			//get medicines from ModMedicinePatch
			localMedsList = new List<int>(Traverse.Create(typeof(ModMedicinePatch.ModMedicalCareUtility)).Field("medsListOrder").GetValue<List<int>>());

			//Add No Care and No Meds
			localMedsList.Insert(0, -1);
			localMedsList.Insert(0, -2);

			//Manually convert to MedicalCareCategory Type
			MedicalCareCategory[] medCareReplacement = new MedicalCareCategory[localMedsList.Count];
			for (int i = 0; i < localMedsList.Count; i++)
			{
				Log.Message(i.ToString() + " | " + (localMedsList[i]));
				medCareReplacement[i] = (MedicalCareCategory)(localMedsList[i]+2);

				//Add keys to language database for translations in the Float Menu
				if (!LanguageDatabase.activeLanguage.HaveTextForKey($"MedicalCareCategory_{i}"))
				{
					LanguageDatabase.activeLanguage.keyedReplacements.Add($"MedicalCareCategory_{i}", MedicalCareUtility.GetLabel((MedicalCareCategory)i));
				}
			}

			//set Pharmacist's medcares array
			Traverse.Create<MainTabWindow_Pharmacist>().Field("medcares").SetValue(medCareReplacement);

			//add modded meds to Pharmacists texture library
			Texture2D[] tex = Traverse.Create(typeof(ModMedicinePatch.ModMedicalCareUtility)).Field("careTextures").GetValue<Texture2D[]>();

			Traverse.Create(typeof(Pharmacist.Resources)).Field("medcareGraphics").SetValue(tex);
			
			Log.Message("Done Patching Pharmacist medcare list");

			Log.Message("Patching Pharmacist comparison function..");
			var harmony = HarmonyInstance.Create("Antaios.Rimworld.PharmMedicinePatch");
			harmony.PatchAll(Assembly.GetExecutingAssembly());
			Log.Message("Done patching Pharmacist comparison function..");
		}

		[HarmonyPatch(typeof(PharmacistUtility),"TendAdvice", new Type[] { typeof(Pawn), typeof(InjurySeverity) })]
		public static class TendAdvice
		{
			[HarmonyPostfix]
			public static void _Postfix(ref MedicalCareCategory __result, Pawn patient, InjurySeverity severity)
			{
				//left original function running for logs

				Population population = patient.GetPopulation();
				var pharmacist = PharmacistSettings.medicalCare[population][severity];
				var playerSetting = patient?.playerSettings?.medCare ?? MedicalCareCategory.Best;

				//get values which indicate relative medical potency
				int ph = localMedsList.IndexOf((int)pharmacist-2);
				int ps = localMedsList.IndexOf((int)playerSetting-2);

				MedicalCareCategory r = playerSetting;

				if (ph < ps)
					r =  pharmacist;

				__result = r;
			}
		}
	}
}
