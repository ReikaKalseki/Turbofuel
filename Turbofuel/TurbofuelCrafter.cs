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
	
	public class TurbofuelCrafter : FCoreMBCrafter<TurbofuelCrafter, CraftData> {
	
		private static readonly float PPS_COST = TurbofuelMod.getConfig().getFloat(TBConfig.ConfigEntries.CRAFTER_PPS);

		private bool mbLinkedToGO;

		private Renderer mBaseRend;
	
		private MaterialPropertyBlock mMPB;
	
		private Light mLight;
	
		private float GlowTick;
	
		private Material Gas_Mat;
		
		private float workingColorFade;
		
		public TurbofuelCrafter(ModCreateSegmentEntityParameters parameters) : base(parameters, TurbofuelMod.crafter, PPS_COST, PPS_COST*5, PPS_COST*5, new List<CraftData>{TurbofuelMod.turbofuelRecipe}) {
			
		}

		public override void DropGameObject() {
			base.DropGameObject();
			this.mbLinkedToGO = false;
			this.mMPB = null;
		}

		public override void UnityUpdate() {
			if (!this.mbIsCenter) {
				return;
			}
			if (!this.mbLinkedToGO) {
				if (this.mWrapper == null || !this.mWrapper.mbHasGameObject) {
					return;
				}
				if (this.mWrapper.mGameObjectList == null) {
					Debug.LogError("PSB missing game object #0?");
				}
				if (this.mWrapper.mGameObjectList[0].gameObject == null) {
					Debug.LogError("PSB missing game object #0 (GO)?");
				}
				if (this.Gas_Mat == null) {
					this.Gas_Mat = (Resources.Load("MultiBlockTextures/Gas Storage Diffuse") as Material);
				}
				this.mWrapper.mGameObjectList[0].transform.Search("Gas Storage").GetComponent<Renderer>().material = this.Gas_Mat;
				this.mBaseRend = this.mWrapper.mGameObjectList[0].transform.Search("Gas Storage").GetComponent<Renderer>();
				this.mMPB = new MaterialPropertyBlock();
				this.mLight = this.mWrapper.mGameObjectList[0].transform.Search("Worklight").GetComponent<Light>();
				this.mbLinkedToGO = true;
			}
			this.UpdateGlow();
		}
		
		private void UpdateGlow() {
			this.GlowTick += Time.deltaTime;
			float num = (this.mDistanceToPlayer + 32f - CamDetail.FPS) / 64f;
			if (num < 0f) {
				num = 0f;
			}
			if (num > 5f) {
				num = 5f;
			}
			if (this.GlowTick < num) {
				return;
			}
			if (this.mSegment.mbOutOfView) {
				return;
			}
			this.GlowTick = 0f;
			Color c = Color.white;
			float dT = Time.deltaTime;
			if (currentRecipe == null || state != OperatingState.Processing) {
				workingColorFade -= dT*1.5F;
			}
			else {
				workingColorFade += dT*2.5F;
			}
			workingColorFade = Mathf.Clamp01(workingColorFade);
			float num3 = Mathf.Lerp(Mathf.PingPong(Time.time, 1f), 1, workingColorFade);
			Color c0 = new Color(num3 * 3f, num3 * 0.1f, num3 * 0.1f);
			if (state == OperatingState.OutOfPower)
				c0 = Color.black;
			c = Color.Lerp(c0, new Color(0.1F, 2F, 0.2F), workingColorFade);
			this.mMPB.SetColor("_GlowColor", c);
			this.mBaseRend.SetPropertyBlock(this.mMPB);
			this.mLight.color = c;
		}

		public override void LowFrequencyUpdate() {
			base.LowFrequencyUpdate();
		}
	
	}

}