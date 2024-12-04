/*
 * Created by SharpDevelop.
 * User: Reika
 * Date: 04/11/2019
 * Time: 11:28 AM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.IO;
//For data read/write methods
using System.Collections;
//Working with Lists and Collections
using System.Collections.Generic;
//Working with Lists and Collections
using System.Linq;
//More advanced manipulation of lists/collections
using System.Reflection;
using System.Reflection.Emit;
using Harmony;
using UnityEngine;
//Needed for most Unity Enginer manipulations: Vectors, GameObjects, Audio, etc.
using ReikaKalseki.FortressCore;

namespace ReikaKalseki.Turbofuel
{
	
	public class TurbofuelCrafter : FCoreMBCrafter<TurbofuelCrafter> {
	
		private static readonly float PPS_COST = TurbofuelMod.getConfig().getFloat(TBConfig.ConfigEntries.CRAFTER_PPS);
		
		public TurbofuelCrafter(ModCreateSegmentEntityParameters parameters) : base(parameters, TurbofuelMod.crafter, PPS_COST, PPS_COST*5, PPS_COST*5, new List<CraftData>{TurbofuelMod.turbofuelRecipe}) {
			
		}

		public override void DropGameObject() {
			base.DropGameObject();
			this.mbLinkedToGO = false;
			this.mMPB = null;
			this.GlowObject = null;
		}

		public override void UnityUpdate() {
			if (!this.mbLinkedToGO) {
				if (this.mWrapper == null || !this.mWrapper.mbHasGameObject) {
					return;
				}
				if (this.mMPB == null) {
					this.mMPB = new MaterialPropertyBlock();
				}
				this.GlowObject = Extensions.Search(this.mWrapper.mGameObjectList[0].transform, "Glow").gameObject;
				this.GlowLight = Extensions.Search(this.mWrapper.mGameObjectList[0].transform, "Worklight").GetComponent<Light>();
				this.rotatorScript = Extensions.Search(this.mWrapper.mGameObjectList[0].transform, "Rotator").GetComponent<RotateConstantlyScript>();
				this.mbLinkedToGO = true;
				this.rotatorScript.YRot = 0f;
				this.mrGlow = 0f;
			}
			if (this.mOperatingState == TurbofuelCrafter.OperatingState.Processing) {
				this.rotatorScript.YRot += Time.deltaTime * 2f;
				if ((double)this.rotatorScript.YRot > 10.0) {
					this.rotatorScript.YRot = 10f;
				}
				this.mrGlow += Time.deltaTime * 2f;
				if (this.mrGlow > 4f) {
					this.mrGlow = 4f;
				}
			}
			else if (this.mrStateTimer > 0.5f) {
				this.mrGlow *= 0.8f;
				this.rotatorScript.YRot -= Time.deltaTime * 2f;
			}
			if (this.mrGlow > 0f) {
				this.GlowObject.SetActive(true);
				this.GlowLight.enabled = true;
			}
			this.mMPB.SetFloat("_Overbright", this.mrGlow);
			this.GlowObject.GetComponent<Renderer>().SetPropertyBlock(this.mMPB);
			this.GlowLight.intensity = this.mrGlow;
			if (this.mrGlow <= 0.01f) {
				this.GlowObject.SetActive(false);
				this.GlowLight.enabled = false;
				this.mrGlow = 0f;
			}
			if (this.rotatorScript.YRot <= 0.01f) {
				this.rotatorScript.YRot = 0f;
			}
		}

		public override void LowFrequencyUpdate() {
			base.LowFrequencyUpdate();
		}

		public RotateConstantlyScript rotatorScript;

		private bool mbLinkedToGO;

		private MaterialPropertyBlock mMPB;

		private GameObject GlowObject;

		private Light GlowLight;

		private float mrGlow;
	
	}

}