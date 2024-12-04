using UnityEngine;  //Needed for most Unity Enginer manipulations: Vectors, GameObjects, Audio, etc.
using System.IO;    //For data read/write methods
using System;    //For data read/write methods
using System.Collections.Generic;   //Working with Lists and Collections
using System.Linq;   //More advanced manipulation of lists/collections
using System.Threading;
using Harmony;
using ReikaKalseki;
using ReikaKalseki.FortressCore;

namespace ReikaKalseki.Turbofuel
{
  public class TurbofuelMod : FCoreMod
  {
    public const string MOD_KEY = "ReikaKalseki.Turbofuel";
    public const string CUBE_KEY = "ReikaKalseki.Turbofuel_Key";
    
    private static Config<TBConfig.ConfigEntries> config;

    public static MultiblockData crafter;
	
	public static readonly CraftData turbofuelRecipe = RecipeUtil.createNewRecipe("Turbofuel");
    
    public TurbofuelMod() : base("Turbofuel") {
    	config = new Config<TBConfig.ConfigEntries>(this);
    }
	
	public static Config<TBConfig.ConfigEntries> getConfig() {
		return config;
	}

    protected override void loadMod(ModRegistrationData registrationData) {
        config.load();
        
        runHarmony();
		
		registrationData.RegisterEntityHandler(eSegmentEntity.JetTurbineGenerator);
		
		crafter = FUtil.registerMultiblock(registrationData, "TurbofuelCrafter", MultiblockData.GASSTORAGE);
		
		turbofuelRecipe.CraftedKey = "ReikaKalseki.Turbofuel";
		turbofuelRecipe.addIngredient("CompressedSulphur", (uint)config.getInt(TBConfig.ConfigEntries.SULFUR_COST));
		if (config.getBoolean(TBConfig.ConfigEntries.USE_HOF)) {
			turbofuelRecipe.addIngredient("HighOctaneFuel", 1);
		}
		else {
			turbofuelRecipe.addIngredient("CoalOre", (uint)config.getInt(TBConfig.ConfigEntries.COAL_COST));
			turbofuelRecipe.addIngredient("HighEnergyCompositeFuel", 1);
		}
		int resin = config.getInt(TBConfig.ConfigEntries.RESIN_COST);
		if (resin > 0)
			turbofuelRecipe.addIngredient("RefinedLiquidResin", (uint)resin);
		CraftData.LinkEntries(new List<CraftData>{turbofuelRecipe}, null);
    }
    
    public override void CheckForCompletedMachine(ModCheckForCompletedMachineParameters parameters) {	 
    	if (parameters.CubeValue == crafter.placerMeta)
			crafter.checkForCompletedMachine(parameters);
	}
    
	public override ModCreateSegmentEntityResults CreateSegmentEntity(ModCreateSegmentEntityParameters parameters) {
		ModCreateSegmentEntityResults modCreateSegmentEntityResults = new ModCreateSegmentEntityResults();
		try {
			if (parameters.Cube == crafter.blockID) {
				modCreateSegmentEntityResults.Entity = new TurbofuelCrafter(parameters);
				parameters.ObjectType = crafter.prefab.model;
			}
			else if (parameters.Type == eSegmentEntity.JetTurbineGenerator) {
				parameters.ObjectType = SpawnableObjectEnum.JetTurbine;
				modCreateSegmentEntityResults.Entity = new DynamicJetGenerator(parameters);
			}
		}
		catch (Exception e) {
			FUtil.log(e.ToString());
		}
		return modCreateSegmentEntityResults;
	}
    
    public static void getJetWaila(UIManager ui) {
		if (WorldScript.instance.localPlayerInstance.mPlayerBlockPicker.selectBlockType == eCubeTypes.JetTurbineGenerator) {
			DynamicJetGenerator gen = WorldScript.instance.localPlayerInstance.mPlayerBlockPicker.selectedEntity as DynamicJetGenerator;
			string text = PersistentSettings.GetString("Jet Turbine Generator");
			if (gen != null && gen.mLinkedCenter != null) {
				gen = gen.mLinkedCenter;
				text = text + "\nRPM: " + (gen.mrRPM * 25f).ToString("N0");
				float pct = gen.mrBurnTime;
				if (gen.mbNextFuelQueued > 0)
					pct += gen.getBurnTime(gen.mbNextFuelQueued);
				pct /= DynamicJetGenerator.HECF_BURN_TIME;
				
				text = text + "\n" + string.Format(PersistentSettings.GetString("UI_Fuel_level_X"), pct.ToString("P2"));
				text = text + "\n" + string.Format(PersistentSettings.GetString("UI_Current_PPS_X"), gen.mrCurrentPPS.ToString("F2"));
				text = text + "\n" + string.Format(PersistentSettings.GetString("UI_Internal_Power_X_X"), gen.mrCurrentPower.ToString("F0"), gen.mrMaxPower.ToString("F0"));
				
				if (gen.currentFuelType > 0)
					text = text + "\nBurning "+FUtil.getItemName(gen.currentFuelType)+", " + gen.mrBurnTime.ToString("F2")+"s remaining";
				else
					text = text + "\nNo fuel";
				
				if (gen.mbNextFuelQueued > 0)
					text = text + "\nHas "+FUtil.getItemName(gen.mbNextFuelQueued)+" in input slot";
				else
					text = text + "\nNo fuel in input";
			}
			else {
				text = text + "\nMultiblock is nonfunctional, rebuild it";
			}
			ui.SetInfoText(text, 0.75f, false);
		}
    }
  }
}
