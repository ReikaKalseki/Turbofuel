using System;

using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Xml;
using ReikaKalseki.FortressCore;

namespace ReikaKalseki.Turbofuel
{
	public class TBConfig
	{		
		public enum ConfigEntries {
			[ConfigEntry("Turbofuel PPS Relative To HECF", typeof(float), 3, 2, 10, 0)]PPS_FACTOR,
			[ConfigEntry("Turbofuel Burn Time Relative To HECF", typeof(float), 4, 1, 100, 0)]BURNTIME_FACTOR,
			[ConfigEntry("HOF PPS Relative To HECF", typeof(float), 2, 1, 5, 0)]PPS_FACTOR_HOF,
			[ConfigEntry("HOF Burn Time Relative To HECF", typeof(float), 1.5F, 1, 5, 0)]BURNTIME_FACTOR_HOF,
			[ConfigEntry("Turbofuel Blender PPS Cost", typeof(float), 6000, 500, 30000, 0)]CRAFTER_PPS,
			[ConfigEntry("Turbofuel Blender Crafting Time", typeof(float), 10, 1, 120, 0)]CRAFTER_TIME,
			[ConfigEntry("Turbofuel Sulfur Cost", typeof(int), 20, 1, 100, 0)]SULFUR_COST,
			[ConfigEntry("Turbofuel Coal Cost", typeof(int), 50, 1, 100, 0)]COAL_COST,
			[ConfigEntry("Turbofuel Uses HOF+Sulfur instead of HECF+Coal+Sulfur", false)]USE_HOF,
		}
	}
}
