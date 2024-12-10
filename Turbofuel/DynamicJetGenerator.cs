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

namespace ReikaKalseki.Turbofuel {
	
	public class DynamicJetGenerator : FCoreMachine {
		
		public DynamicJetGenerator(ModCreateSegmentEntityParameters parameters) : base(parameters) {
			this.mState = GeneratorState.LookingForLink;
			if (parameters.Value == CENTER_VALUE) {
				this.mState = GeneratorState.ReacquiringLink;
				this.mbNeedsLowFrequencyUpdate = true;
				this.mbNeedsUnityUpdate = true;
				Achievements.UnlockAchievementDelayed(Achievements.eAchievements.eTurbinestoSpeed);
				FUtil.log("Building master "+this);
			}
			this.mnOrientation = parameters.Flags >> 6;
			this.mNeighbouringMachines = new List<PowerConsumerInterface>(4);
			this.mNeighbouringHoppers = new List<StorageMachineInterface>(4);
			mObjectType = SpawnableObjectEnum.JetTurbine;
		}
		
		public override void SpawnGameObject() {
			if (this.mValue == CENTER_VALUE)
				base.SpawnGameObject();
		}
	
		public override void LowFrequencyUpdate() {
			if (this.mValue == CENTER_VALUE)
				this.UpdatePlayerDistanceInfo();
			if (this.mState == GeneratorState.ReacquiringLink)
				this.AcquireComponents();
			
			this.LookForMachines();
			for (int i = 0; i < 3; i++) { //why does he do this three times?
				if (this.mNeighbouringMachines.Count <= 6)
					this.LookForMachines();
			}
			
			if (this.mValue == CENTER_VALUE) {
				this.RunGenerator();
			
				int num = 8;
				if (num > this.mNeighbouringMachines.Count)
					num = this.mNeighbouringMachines.Count;
				
				for (int i = 0; i < num; i++) {
					if (this.mrCurrentPower > 0)
						this.AttemptDeliverPower();
				}
				
				if (this.HasSpentFuel)
					this.AttemptToDropOffSpentCanister();
				else if (this.mbNextFuelQueued <= 0)
					this.AttemptResupply();
			}
		}
	
		private void AcquireComponents() {
			for (int i = -1; i < 2; i++) {
				for (int j = -1; j < 2; j++) {
					for (int k = -2; k < 3; k++) {
						if (i != 0 || j != 0 || k != 0) {
							long mnX = this.mnX;
							long mnZ = this.mnZ;
							RotateOrientationCoord(j, k, this.mnOrientation, ref mnX, ref mnZ);
							long y = (long)i + this.mnY;
							Segment segment = base.AttemptGetSegment(mnX, y, mnZ);
							if (segment == null) {
								return;
							}
							ushort cube = segment.GetCube(mnX, y, mnZ);
							if (cube == eCubeTypes.JetTurbineGenerator) {
								DynamicJetGenerator gen = segment.FetchEntity(eSegmentEntity.JetTurbineGenerator, mnX, y, mnZ) as DynamicJetGenerator;
								if (gen == null) {
									return;
								}
								if (gen.mState != GeneratorState.Linked || gen.mLinkedCenter != this) {
									if (gen.mState != GeneratorState.LookingForLink) {
										Debug.Log("Overwriting badly formed dynamic jet generator?");
									}
									gen.mState = GeneratorState.Linked;
									gen.mLinkedCenter = this;
								}
							}
						}
					}
				}
			}
			this.mState = GeneratorState.Linked;
			this.mSegment.RequestRegenerateGraphics();
		}
	/*
		private void SetCenter(DynamicJetGenerator center) {
			this.mLinkedCenter = center;
		}*/
	
		private void RoundRobinSide(out int y, out int u, out int v) {
			if (this.mnCurrentSide == 0) {
				y = this.mnCurrentSideIndex / 5 - 1;
				u = -2;
				v = this.mnCurrentSideIndex % 5 - 2;
			}
			else if (this.mnCurrentSide == 1) {
				y = this.mnCurrentSideIndex / 5 - 1;
				u = 2;
				v = this.mnCurrentSideIndex % 5 - 2;
			}
			else if (this.mnCurrentSide == 2) {
				y = -2;
				u = this.mnCurrentSideIndex / 5 - 1;
				v = this.mnCurrentSideIndex % 5 - 2;
			}
			else {
				y = 2;
				u = this.mnCurrentSideIndex / 5 - 1;
				v = this.mnCurrentSideIndex % 5 - 2;
			}
			this.mnCurrentSideIndex++;
			if (this.mnCurrentSideIndex == 15) {
				this.mnCurrentSideIndex = 0;
				this.mnCurrentSide = (this.mnCurrentSide + 1) % 4;
			}
		}
	
		private void LookForMachines() {
			int y0;
			int u;
			int v;
			this.RoundRobinSide(out y0, out u, out v);
			long dx = this.mnX;
			long dz = this.mnZ;
			RotateOrientationCoord(u, v, this.mnOrientation, ref dx, ref dz);
			long dy = (long)y0 + this.mnY;
			Segment segment = base.AttemptGetSegment(dx, dy, dz);
			if (segment == null) {
				return;
			}
			ushort cube = segment.GetCube(dx, dy, dz);
			if (!CubeHelper.HasEntity((int)cube)) {
				return;
			}
			SegmentEntity e = segment.SearchEntity(dx, dy, dz);
			for (int i = 0; i < this.mNeighbouringMachines.Count; i++) {
				MachineEntity machineEntity = this.mNeighbouringMachines[i] as MachineEntity;
				if (machineEntity != null) {
					if (machineEntity.mbDelete) {
						this.mNeighbouringMachines.RemoveAt(i);
						i--;
					}
					else
					if (machineEntity.mnX == dx && machineEntity.mnY == dy && machineEntity.mnZ == dz) {
						this.mNeighbouringMachines.RemoveAt(i);
						break;
					}
				}
			}
			PowerConsumerInterface pci = e as PowerConsumerInterface;
			if (pci != null) {
				this.mNeighbouringMachines.Add(pci);
			}
			for (int j = 0; j < this.mNeighbouringHoppers.Count; j++) {
				MachineEntity me = this.mNeighbouringHoppers[j] as MachineEntity;
				if (me != null) {
					if (me.mbDelete) {
						this.mNeighbouringHoppers.RemoveAt(j);
						j--;
					}
					else
					if (me.mnX == dx && me.mnY == dy && me.mnZ == dz) {
						this.mNeighbouringHoppers.RemoveAt(j);
						break;
					}
				}
			}
			StorageMachineInterface si = e as StorageMachineInterface;
			if (si != null)
				this.mNeighbouringHoppers.Add(si);
		}
		
		public float getBurnTime(int id) {
			float time = HECF_BURN_TIME;
			if (id == TURBOFUEL_ITEM_ID)
				time *= TurbofuelMod.getConfig().getFloat(TBConfig.ConfigEntries.BURNTIME_FACTOR);
			else if (id == HOF_ITEM_ID)
				time *= TurbofuelMod.getConfig().getFloat(TBConfig.ConfigEntries.BURNTIME_FACTOR_HOF);
			return time;
		}
		
		private void RunGenerator() {
			if (this.mrBurnTime <= 0) {
				if (this.mbNextFuelQueued > 0) {
					mrBurnTime = getBurnTime(mbNextFuelQueued);
					//FUtil.log("Turbine "+this+" consuming queued fuel "+ItemEntry.mEntriesById[mbNextFuelQueued].Name+", burn time = "+mrBurnTime);
					currentFuelType = mbNextFuelQueued;
					this.mbNextFuelQueued = -1;
				}
				else {
					currentFuelType = -1;
					this.mrLastRPM = this.mrRPM;
					this.mrLerpProgress = 0;
					this.mrRPM *= 0.95f;
					if (this.mrRPM < 0.025f) {
						this.mrRPM = 0;
					}
				}
			}
			if (this.mrBurnTime > 0) {
				if (currentFuelType <= 0) { //attempt to compute fuel type
					float tf = mrBurnTime/HECF_BURN_TIME;
					if (tf >= TurbofuelMod.getConfig().getFloat(TBConfig.ConfigEntries.BURNTIME_FACTOR))
						currentFuelType = TURBOFUEL_ITEM_ID;
					else if (tf >= TurbofuelMod.getConfig().getFloat(TBConfig.ConfigEntries.BURNTIME_FACTOR_HOF))
						currentFuelType = HOF_ITEM_ID;
					else
						currentFuelType = HECF_ITEM_ID;
				}
				if (this.mrRPM < MAX_RPM) {
					this.mrLastRPM = this.mrRPM;
					this.mrLerpProgress = 0;
					if (this.mrCurrentPower < this.mrMaxPower) {
						this.mrRPM += 80f * LowFrequencyThread.mrPreviousUpdateTimeStep;
					}
					if (this.mrRPM > MAX_RPM) {
						this.mrRPM = MAX_RPM;
					}
				}
				float f = this.mrRPM / MAX_RPM;
				this.mrBurnTime -= LowFrequencyThread.mrPreviousUpdateTimeStep * f;
				if (this.mrBurnTime < 0)
					this.mrBurnTime = 0;
				
				float pps = HECF_PEAK_POWER_OUTPUT_PER_SEC * f * LowFrequencyThread.mrPreviousUpdateTimeStep;
				if (currentFuelType == TURBOFUEL_ITEM_ID)
					pps *= TurbofuelMod.getConfig().getFloat(TBConfig.ConfigEntries.PPS_FACTOR);
				else if (currentFuelType == HOF_ITEM_ID)
					pps *= TurbofuelMod.getConfig().getFloat(TBConfig.ConfigEntries.PPS_FACTOR_HOF);
				
				if (DifficultySettings.mbEasyPower)
					pps *= 2.5F;
				if (DifficultySettings.mbRushMode)
					pps *= 10F;
				if (DifficultySettings.mbCasualResource) //rapid
					pps *= 4F;
				
				this.mrCurrentPower += pps;
				this.mrCurrentPPS = pps / LowFrequencyThread.mrPreviousUpdateTimeStep;
				GameManager.mrTotalJetPower += pps;
				GameManager.PowerGenerated(pps);
				if (PlayerStats.mbCreated) {
					PlayerStats.instance.AddPowerToStats(pps);
				}
				if (this.mrCurrentPower > this.mrMaxPower) {
					this.mrCurrentPower = this.mrMaxPower;
					this.mrRPM *= 0.925f;
				}
			}
		}
	
		private void AttemptToDropOffSpentCanister() {
			if (this.mNeighbouringHoppers.Count == 0) {
				return;
			}
			if (this.mnHopperRoundRobinPosition >= this.mNeighbouringHoppers.Count) {
				this.mnHopperRoundRobinPosition = 0;
			}
			StorageMachineInterface si = this.mNeighbouringHoppers[this.mnHopperRoundRobinPosition];
			eHopperPermissions permissions = si.GetPermissions();
			if (permissions == eHopperPermissions.AddOnly || permissions == eHopperPermissions.AddAndRemove) {
				ItemBase itemBase = ItemManager.SpawnItem(SPENT_FUEL_ITEM_ID);
				(itemBase as ItemStack).mnAmount = (int)Mathf.Ceil(DifficultySettings.mrResourcesFactor);
				if (!((SegmentEntity)si).mbDelete && si.TryInsert(this, itemBase)) {
					this.HasSpentFuel = false;
					this.RequestImmediateNetworkUpdate();
					if (si is MachineEntity) {
						((MachineEntity)si).RequestImmediateNetworkUpdate();
					}
				}
			}
			if (this.mNeighbouringHoppers.Count > 1) {
				this.mnHopperRoundRobinPosition++;
				this.mnHopperRoundRobinPosition %= this.mNeighbouringHoppers.Count;
			}
		}
	
		private void AttemptResupply() {
			if (this.mNeighbouringHoppers.Count == 0) {
				return;
			}
			if (this.mnHopperRoundRobinPosition >= this.mNeighbouringHoppers.Count) {
				this.mnHopperRoundRobinPosition = 0;
			}
			StorageMachineInterface si = this.mNeighbouringHoppers[this.mnHopperRoundRobinPosition];
			eHopperPermissions permissions = si.GetPermissions();
			if ((permissions == eHopperPermissions.RemoveOnly || permissions == eHopperPermissions.AddAndRemove) && !((SegmentEntity)si).mbDelete) {
				//FUtil.log("Turbine "+this+" seeking fuel");
				int fuel = tryGetFuel(si);
				if (fuel > 0) {
					//FUtil.log("Turbine "+this+" found and queued fuel "+ItemEntry.mEntriesById[fuel].Name);
					this.mbNextFuelQueued = fuel;
					this.HasSpentFuel = true;
					this.RequestImmediateNetworkUpdate();
				}
			}
			if (this.mNeighbouringHoppers.Count > 1) {
				this.mnHopperRoundRobinPosition++;
				this.mnHopperRoundRobinPosition %= this.mNeighbouringHoppers.Count;
			}
		}
		
		private int tryGetFuel(StorageMachineInterface si) {
			//FUtil.log("Trying to pull fuel from "+si);
			if (tryGetFuel(si, TURBOFUEL_ITEM_ID))
				return TURBOFUEL_ITEM_ID;
			else if (tryGetFuel(si, HOF_ITEM_ID))
				return HOF_ITEM_ID;
			else if (tryGetFuel(si, HECF_ITEM_ID))
				return HECF_ITEM_ID;
			else
				return -1;
		}
		
		private bool tryGetFuel(StorageMachineInterface si, int id) {
			return si.TryExtractItems(this, id, 1);
		}
	
		private bool AttemptDeliverPower() {
			bool result = false;
			if (this.mNeighbouringMachines.Count == 0) 
				return result;
			
			if (this.mnMachineRoundRobinPosition >= this.mNeighbouringMachines.Count)
				this.mnMachineRoundRobinPosition = 0;
			
			PowerConsumerInterface pci = this.mNeighbouringMachines[this.mnMachineRoundRobinPosition];
			if (pci.WantsPowerFromEntity(this)) {
				float num = this.mrCurrentPower;
				float maxio = pci.GetMaximumDeliveryRate();
				float space = pci.GetRemainingPowerCapacity();
				
				if (num > maxio)
					num = maxio;				
				if (num > this.mrTransferRate) 
					num = this.mrTransferRate;				
				if (num > space)
					num = space;
				
				if (num > 0 && pci.DeliverPower(num)) {
					this.mrCurrentPower -= num;
					this.MarkDirtyDelayed();
					result = true;
				}
			}
			if (this.mNeighbouringMachines.Count > 1) {
				this.mnMachineRoundRobinPosition++;
				this.mnMachineRoundRobinPosition %= this.mNeighbouringMachines.Count;
			}
			return result;
		}
	
		private bool IsIntake() {
			if (this.mLinkedCenter == null)
				return false;
			
			for (int i = -1; i < 2; i++) {
				for (int j = -1; j < 2; j++) {
					int v = 2;
					long dx = this.mLinkedCenter.mnX;
					long dz = this.mLinkedCenter.mnZ;
					RotateOrientationCoord(j, v, this.mLinkedCenter.mnOrientation, ref dx, ref dz);
					long dy = (long)i + this.mLinkedCenter.mnY;
					if (dx == this.mnX && dy == this.mnY && dz == this.mnZ) {
						return true;
					}
				}
			}
			return false;
		}
	
		public override void OnDelete() {
			base.OnDelete();
			if (this.mState == GeneratorState.Linked) {
				ushort value = HOUSING_PLACEMENT_VALUE;
				if (this.IsIntake()) {
					value = INTAKE_PLACEMENT_VALUE;
				}
				if (WorldScript.mbIsServer) {
					ItemManager.DropNewCubeStack(eCubeTypes.MachinePlacementBlock, value, 1, this.mnX, this.mnY, this.mnZ, Vector3.zero);
				}
				this.mState = GeneratorState.Delinked;
				if (this.mValue == CENTER_VALUE)
					this.DeconstructGenerator(this);
				else
					this.mLinkedCenter.DeconstructGenerator(this);
			}
			else
			if (this.mState != GeneratorState.Delinked) {
				Debug.LogWarning("Deleted vat while in state " + this.mState);
			}
		}
	
		private void DeconstructGenerator(DynamicJetGenerator deletedPart) {
			for (int i = -1; i < 2; i++) {
				for (int j = -1; j < 2; j++) {
					for (int k = -2; k < 3; k++) {
						if (i != 0 || j != 0 || k != 0) {
							long dx = this.mnX;
							long dz = this.mnZ;
							RotateOrientationCoord(j, k, this.mnOrientation, ref dx, ref dz);
							long dy = (long)i + this.mnY;
							Segment segment = null;
							if (this.mFrustrum != null)
								segment = this.mFrustrum.GetSegment(dx, dy, dz);
							if (segment == null)
								WorldScript.instance.GetSegment(dx, dy, dz);
							
							if (segment != null && segment.mbInitialGenerationComplete && !segment.mbDestroyed) {
								ushort cube = segment.GetCube(dx, dy, dz);
								if (cube == eCubeTypes.JetTurbineGenerator) {
									DynamicJetGenerator gen = segment.FetchEntity(eSegmentEntity.JetTurbineGenerator, dx, dy, dz) as DynamicJetGenerator;
									if (gen != null) {
										gen.mState = GeneratorState.Delinked;
										ushort leValue = HOUSING_PLACEMENT_VALUE;
										if (k == 2) {
											leValue = INTAKE_PLACEMENT_VALUE;
										}
										WorldScript.instance.BuildFromEntity(segment, gen.mnX, gen.mnY, gen.mnZ, eCubeTypes.MachinePlacementBlock, leValue);
									}
								}
							}
						}
					}
				}
			}
			if (deletedPart != this) {
				this.mState = GeneratorState.Delinked;
				WorldScript.instance.BuildFromEntity(this.mSegment, this.mnX, this.mnY, this.mnZ, eCubeTypes.MachinePlacementBlock, HOUSING_PLACEMENT_VALUE);
			}
		}
	
		public override void UnitySuspended() {
			this.mFuelSpentLight = null;
			this.mIntakeParticles = null;
			this.mExhaustParticles = null;
			this.mExhaustMesh = null;
			this.mBlurMesh = null;
			this.mBlurMPB = null;
			this.mHurtBox = null;
			this.Blades = null;
			this.TurbineBase = null;
			this.JTGScript = null;
			this.LinkedToGO = false;
		}
	
		private void LinkToGO() {
			if (mWrapper == null) {
				FUtil.log("Error: turbine has no GO wrapper");
				return;
			}
			if (mWrapper.mGameObjectList == null) {
				FUtil.log("Error: turbine GO wrapper has null GOs");
				return;
			}
			if (mWrapper.mGameObjectList.Count == 0) {
				FUtil.log("Error: turbine GO wrapper has no GOs");
				return;
			}
			if (this.mFuelSpentLight == null) {
				this.mFuelSpentLight = findObject("SpentFuelLight").GetComponent<Light>();
			}
			if (this.mIntakeParticles == null) {
				this.mIntakeParticles = findObject("Intake Particles").GetComponent<ParticleSystem>();
				this.mIntakeParticles.SetEmissionRate(0f);
			}
			if (this.mHeatShimmerParticles == null) {
				this.mHeatShimmerParticles = findObject("Heat Shimmer").GetComponent<ParticleSystem>();
				this.mHeatShimmerParticles.SetEmissionRate(0f);
			}
			if (this.mExhaustParticles == null) {
				this.mExhaustParticles = findObject("Exhaust Particles").GetComponent<ParticleSystem>();
				this.mExhaustParticles.SetEmissionRate(0f);
			}
			if (this.mExhaustMesh == null) {
				this.mExhaustMesh = findObject("_ExhaustMesh").gameObject;
			}
			if (this.mHurtBox == null) {
				this.mHurtBox = findObject("Engine Exhaust Hurt").GetComponent<HurtPlayerOnStay>();
			}
			if (this.mBlurMesh == null) {
				this.mBlurMesh = findObject("Blurred").gameObject;
				this.mBlurMPB = new MaterialPropertyBlock();
			}
			if (this.TurbineBase == null) {
				this.TurbineBase = findObject("Jet Turbine Base").gameObject;
			}
			if (this.Blades == null) {
				this.Blades = findObject("Jet Turbine Blades").gameObject;
			}
			this.JTGScript = this.mWrapper.mGameObjectList[0].GetComponent<JetTurbineGeneratorScript>();
			this.LinkedToGO = true;
		}
		
		private Transform findObject(string name) {
			Transform ret = this.mWrapper.mGameObjectList[0].transform.Search(name);
			if (ret == null) {
				FUtil.log("Error: Could not find GO component: "+name+" in GO hierarchy: ");
				for (int i = 0; i < mWrapper.mGameObjectList.Count; i++) {
					FUtil.log("Wrapper object "+i+":");
					mWrapper.mGameObjectList[i].dumpObjectData();
				}
			}
			return ret;
		}
	
		public override void UnityUpdate() {
			if (this.mWrapper == null || !this.mWrapper.mbHasGameObject) {
				return;
			}
			if (!this.LinkedToGO) {
				this.LinkToGO();
			}
			if (TurbineMaterial == null) {
				TurbineMaterial = (Resources.Load("MultiBlockTextures/Jet Turbine Cover Diffuse") as Material);
			}
			if (TurbineMaterial == null) {
				Debug.LogError("Really bad error - Jet Turbine has failed to load in texture!");
			}
			else {
				this.mWrapper.mGameObjectList[0].transform.Search("Jet Turbine Base").GetComponent<Renderer>().material = TurbineMaterial;
				this.mWrapper.mGameObjectList[0].transform.Search("Jet Turbine Blades").GetComponent<Renderer>().material = TurbineMaterial;
			}
			if (this.mrLerpProgress < 1f) {
				this.mrLerpProgress += Time.deltaTime / LowFrequencyThread.mrPreviousUpdateTimeStep;
				if (this.mrLerpProgress >= 1f) {
					this.mrLerpProgress = 1f;
					this.mrLastRPM = this.mrRPM;
				}
			}
			float num = Mathf.Lerp(this.mrLastRPM, this.mrRPM, this.mrLerpProgress);
			float num2 = num / MAX_RPM;
			if (this.TurbineBase.activeSelf != base.AmInSameRoom()) {
				this.Blades.SetActive(!this.TurbineBase.activeSelf);
				this.TurbineBase.SetActive(!this.TurbineBase.activeSelf);
			}
			if (!this.mbWellBehindPlayer && this.mDistanceToPlayer < 64f && !this.mSegment.mbOutOfView && base.AmInSameRoom()) {
				this.JTGScript.Blades.Rotate(0f, 0, num2 * Time.deltaTime * 4096f);
				float num3 = num2 * 4f;
				if (num3 > 2f) {
					num3 = 2f;
				}
				num3 -= 1f;
				if (num3 < 0) {
					num3 = 0;
				}
				if (num3 > 1f) {
					num3 = 1f;
				}
				this.mBlurMPB.Clear();
				this.mBlurMPB.SetColor("_Color", new Color(1f, 1f, 1f, num3));
				this.mBlurMesh.GetComponent<Renderer>().SetPropertyBlock(this.mBlurMPB);
				this.mBlurMesh.SetActive(true);
			}
			else {
				this.mBlurMesh.SetActive(false);
			}
			if (this.mrRPM > 0) {
				if (this.JTGScript.JetSound.isActiveAndEnabled) {
					if (!this.JTGScript.TurbineSound.isPlaying) {
						this.JTGScript.TurbineSound.Play();
						this.JTGScript.JetSound.Play();
						this.JTGScript.TurbineSound.volume = 0;
						this.JTGScript.JetSound.volume = 0;
					}
					this.JTGScript.TurbineSound.volume = Mathf.Lerp(0.3f, 1f, num2);
					this.JTGScript.TurbineSound.pitch = Mathf.Lerp(0.05f, 2f, num2);
					this.JTGScript.JetSound.volume = Mathf.Lerp(0.3f, 1f, num2);
					this.JTGScript.JetSound.pitch = Mathf.Lerp(0.05f, 2f, num2);
				}
			}
			else
			if (this.JTGScript.TurbineSound.isPlaying) {
				this.JTGScript.TurbineSound.Stop();
				this.JTGScript.JetSound.Stop();
			}
			if (this.HasSpentFuel) {
				if (this.mFuelSpentLight.intensity < 8f) {
					this.mFuelSpentLight.enabled = true;
					this.mFuelSpentLight.intensity += Time.deltaTime * 4f;
					if (Mathf.Approximately(mFuelSpentLight.intensity, 8f)) {
						this.mFuelSpentLight.intensity = 2f;
					}
				}
			}
			else
			if (this.mFuelSpentLight.intensity > 0.1f) {
				this.mFuelSpentLight.intensity *= 0.95f;
				if (this.mFuelSpentLight.intensity <= 0.1f) {
					this.mFuelSpentLight.enabled = false;
				}
			}
			if (base.AmInSameRoom() && this.mDistanceToPlayer < 128f) {
				this.ParticlesActive = true;
				this.ParticleUpdateDelay -= Time.deltaTime;
				float num4 = this.mrRPM / MAX_RPM;
				if (this.mrCurrentPower > this.mrMaxPower) {
					num4 = 0;
				}
				float num5 = 250f * num4;
				float num6 = num5 / 250f * 7f;
				Vector3 localScale = new Vector3(num6 / 4f, num6 / 4f, num6);
				this.mExhaustMesh.transform.localScale = localScale;
				num5 -= this.mDistanceToPlayer * 3f;
				if (num5 < 0) {
					num5 = 0;
				}
				if (this.ParticleUpdateDelay <= 0) {
					this.ParticleUpdateDelay = this.mDistanceToPlayer / 128f;
					float num7 = this.mIntakeParticles.GetEmissionRate();
					float num8 = this.mExhaustParticles.GetEmissionRate();
					num7 += (num5 - num7) * Time.deltaTime;
					this.mIntakeParticles.SetEmissionRate(num7);
					float num9 = 0;
					if (this.mrBurnTime > 0 && this.mrCurrentPower < this.mrMaxPower) {
						num9 = 125f;
					}
					num9 -= this.mDistanceToPlayer * 2f;
					if (num9 < 0) {
						num9 = 0;
						num8 *= 0.5f;
					}
					num8 += (num9 - num8) * Time.deltaTime;
					this.mExhaustParticles.SetEmissionRate(num8);
					float emissionRate = this.mHeatShimmerParticles.GetEmissionRate();
					if (num9 * 2f > this.mrMaxHeatShimmer) {
						this.mrMaxHeatShimmer = num9 * 2f;
					}
					if (num9 <= 4f) {
						this.mrMaxHeatShimmer -= Time.deltaTime;
						num9 = this.mrMaxHeatShimmer - this.mDistanceToPlayer * 8f;
						if (num9 < 0) {
							num9 = 0;
						}
						this.mHeatShimmerParticles.SetEmissionRate(num9);
					}
					else {
						this.mHeatShimmerParticles.SetEmissionRate(emissionRate * 0.75f);
					}
					this.mHurtBox.DamagePerSecond = (int)(num8 / 1f);
				}
			}
			else
			if (this.ParticlesActive) {
				this.ParticlesActive = false;
				this.mIntakeParticles.SetEmissionRate(0f);
				this.mExhaustParticles.SetEmissionRate(0f);
				this.mHeatShimmerParticles.SetEmissionRate(0f);
			}
		}
	
		static void RotateOrientationCoord(int u, int v, int orientation, ref long x, ref long z) {
			switch (orientation) {
				case 0:
					x -= (long)u;
					z += (long)v;
					break;
				case 1:
					x -= (long)v;
					z -= (long)u;
					break;
				case 2:
					x += (long)u;
					z -= (long)v;
					break;
				case 3:
					x += (long)v;
					z += (long)u;
					break;
			}
		}
		/*
	
		public void CheckHousing(WorldFrustrum frustrum, long lastX, long lastY, long lastZ) {
			if (frustrum == null) {
				Debug.LogWarning("Cannot check jet turbine intake, frustrum is null");
				return;
			}
			int[] array = new int[4];
			for (int i = 1; i <= 4; i++) {
				ushort id;
				ushort meta;
				GetCubePair(frustrum, lastX + (long)i, lastY, lastZ, out id, out meta);
				if (id != eCubeTypes.MachinePlacementBlock)
					break;
				if (meta == 3) {
					array[3] = i;
					break;
				}
			}
			for (int j = -1; j >= -4; j--) {
				ushort id;
				ushort meta;
				GetCubePair(frustrum, lastX + (long)j, lastY, lastZ, out id, out meta);
				if (id != eCubeTypes.MachinePlacementBlock)
					break;
				if (meta == 3) {
					array[1] = Mathf.Abs(j);
					break;
				}
			}
			for (int k = 1; k <= 4; k++) {
				ushort id;
				ushort meta;
				GetCubePair(frustrum, lastX, lastY, lastZ + (long)k, out id, out meta);
				if (id != eCubeTypes.MachinePlacementBlock)
					break;
				if (meta == 3) {
					array[0] = k;
					break;
				}
			}
			for (int l = -1; l >= -4; l--) {
				ushort id;
				ushort meta;
				GetCubePair(frustrum, lastX, lastY, lastZ + (long)l, out id, out meta);
				if (id != eCubeTypes.MachinePlacementBlock)
					break;
				if (meta == 3) {
					array[2] = Mathf.Abs(l);
					break;
				}
			}
			for (int m = 0; m < 4; m++) {
				if (array[m] > 0 && CheckGeneratorConfig(frustrum, lastX, lastY, lastZ, m, array[m])) {
					break;
				}
			}
		}
	
		public void CheckIntake(WorldFrustrum frustrum, long lastX, long lastY, long lastZ) {
			bool[] array = new bool[4];
			for (int i = 0; i < 4; i++) {
				array[i] = true;
			}
			for (int j = 1; j <= 4; j++) {
				ushort id;
				ushort meta;
				GetCubePair(frustrum, lastX + (long)j, lastY, lastZ, out id, out meta);
				if (id != eCubeTypes.MachinePlacementBlock || meta != HOUSING_PLACEMENT_VALUE) {
					array[1] = false;
					break;
				}
			}
			for (int k = -1; k >= -4; k--) {
				ushort id;
				ushort meta;
				GetCubePair(frustrum, lastX + (long)k, lastY, lastZ, out id, out meta);
				if (id != eCubeTypes.MachinePlacementBlock || meta != HOUSING_PLACEMENT_VALUE) {
					array[3] = false;
					break;
				}
			}
			for (int l = 1; l <= 4; l++) {
				ushort id;
				ushort meta;
				GetCubePair(frustrum, lastX, lastY, lastZ + (long)l, out id, out meta);
				if (id != eCubeTypes.MachinePlacementBlock || meta != HOUSING_PLACEMENT_VALUE) {
					array[2] = false;
					break;
				}
			}
			for (int m = -1; m >= -4; m--) {
				ushort id;
				ushort meta;
				GetCubePair(frustrum, lastX, lastY, lastZ + (long)m, out id, out meta);
				if (id != eCubeTypes.MachinePlacementBlock || meta != HOUSING_PLACEMENT_VALUE) {
					array[0] = false;
					break;
				}
			}
			for (int n = 0; n < 4; n++) {
				if (array[n] && CheckGeneratorConfig(frustrum, lastX, lastY, lastZ, n, 0)) {
					break;
				}
			}
		}
	
		private void GetCubePair(WorldFrustrum frustrum, long x, long y, long z, out ushort type, out ushort value) {
			Segment segment = frustrum.GetSegment(x, y, z);
			if (segment == null || !segment.mbInitialGenerationComplete || segment.mbDestroyed) {
				type = 0;
				value = 0;
				return;
			}
			type = segment.GetCube(x, y, z);
			value = segment.GetCubeData(x, y, z).mValue;
		}
	
		private void RotateOrientationCoord(int u, int v, int orientation, ref long x, ref long z) {
			switch (orientation) {
				case 0:
					x -= (long)u;
					z += (long)v;
					break;
				case 1:
					x -= (long)v;
					z -= (long)u;
					break;
				case 2:
					x += (long)u;
					z -= (long)v;
					break;
				case 3:
					x += (long)v;
					z += (long)u;
					break;
			}
		}
	
		private bool CheckGeneratorConfig(WorldFrustrum frustrum, long lastX, long lastY, long lastZ, int orientation, int intakeDistance) {
			bool[,] array = new bool[3, 3];
			Debug.Log("prepping possibleConfigs defaults");
			for (int i = -1; i <= 1; i++) {
				for (int j = -1; j <= 1; j++) {
					array[j + 1, i + 1] = true;
				}
			}
			Debug.Log("Checking valid potential turbine rows");
			int num = 0;
			for (int k = -2; k < 3; k++) {
				for (int l = -2; l < 3; l++) {
					for (int m = -4 + intakeDistance; m <= intakeDistance; m++) {
						long x = lastX;
						long z = lastZ;
						RotateOrientationCoord(l, m, orientation, ref x, ref z);
						ushort id;
						ushort meta;
						try {
							GetCubePair(frustrum, x, lastY + (long)k, z, out id, out meta);
						}
						catch (Exception ex) {
							Debug.Log("Exception GetCubePair");
							Debug.LogException(ex);
							throw ex;
						}
						bool flag = true;
						if (id != eCubeTypes.MachinePlacementBlock) {
							flag = false;
						}
						if (m < intakeDistance && meta != HOUSING_PLACEMENT_VALUE) {
							flag = false;
						}
						if (m == intakeDistance && meta != INTAKE_PLACEMENT_VALUE) {
							flag = false;
						}
						if (!flag) {
							for (int n = -1; n <= 1; n++) {
								for (int num4 = -1; num4 <= 1; num4++) {
									int tu = l + num4;
									int ty = k + n;
									if (tu >= -1 && tu <= 1 && ty >= -1 && ty <= 1) {
										try {
											array[tu + 1, ty + 1] = false;
										}
										catch (Exception ex2) {
											Debug.Log(string.Concat(new object[] {
												"Possible Config is ",
												array.GetLength(0),
												":",
												array.GetLength(1)
											}));
											Debug.Log(string.Concat(new object[] {
												"Exception possibleConfigs[]TU was ",
												tu,
												" and TY was",
												ty
											}));
											Debug.LogException(ex2);
											throw ex2;
										}
									}
								}
							}
							break;
						}
						num++;
					}
				}
			}
			Debug.Log("Found " + num + " valid generator blocks along search space");
			int amt = 0;
			for (int num8 = -1; num8 <= 1; num8++) {
				for (int num9 = -1; num9 <= 1; num9++) {
					if (array[num9 + 1, num8 + 1]) {
						amt++;
					}
				}
			}
			Debug.Log(string.Concat(new object[] {
				"Found ",
				amt,
				" valid generator positions along orientation ",
				orientation
			}));
			for (int h = -1; h <= 1; h++) {
				for (int u = -1; u <= 1; u++) {
					if (array[u + 1, h + 1]) {
						int v = intakeDistance - 2;
						long centerX = lastX;
						long centerZ = lastZ;
						RotateOrientationCoord(u, v, orientation, ref centerX, ref centerZ);
						BuildGenerator(frustrum, centerX, lastY + (long)h, centerZ, orientation);
						return true;
					}
				}
			}
			return false;
		}
	
		private void BuildGenerator(WorldFrustrum frustrum, long centerX, long centerY, long centerZ, int orientation) {
			if (frustrum == null) {
				Debug.LogWarning("Cannot do DynamicJetGenerator::BuildGenerator, frustrum is null");
				return;
			}
			HashSet<Segment> hashSet = new HashSet<Segment>();
			try {
				if (WorldScript.mbHasPlayer) {
					WorldScript.mLocalPlayer.mResearch.GiveResearch(eCubeTypes.JetTurbineGenerator, 0);
				}
				byte leFlags = (byte)(1 + (orientation << 6));
				for (int i = -1; i < 2; i++) {
					for (int j = -1; j < 2; j++) {
						for (int k = -2; k < 3; k++) {
							long x = centerX;
							long z = centerZ;
							RotateOrientationCoord(j, k, orientation, ref x, ref z);
							long y = (long)i + centerY;
							Segment segment = frustrum.GetSegment(x, y, z);
							if (segment == null || !segment.mbInitialGenerationComplete || segment.mbDestroyed) {
								return;
							}
							if (!hashSet.Contains(segment)) {
								hashSet.Add(segment);
								segment.BeginProcessing();
							}
							ushort leValue = COMPONENT_VALUE;
							if (i == 0 && j == 0 && k == 0) {
								leValue = CENTER_VALUE;
							}
							frustrum.BuildOrientation(segment, x, y, z, eCubeTypes.JetTurbineGenerator, leValue, leFlags);
						}
					}
				}
				Debug.Log("Generator built!");
			}
			finally {
				foreach (Segment segment2 in hashSet) {
					segment2.EndProcessing();
				}
				WorldScript.instance.mNodeWorkerThread.KickNodeWorkerThread();
			}
		}*/
	
		public override bool ShouldSave() {
			return this.mValue == CENTER_VALUE;
		}
	
		public override int GetVersion() {
			return 1;
		}
	
		public override void Write(BinaryWriter writer) {
			writer.Write(this.mrCurrentPower);
			writer.Write(this.mrBurnTime);
			writer.Write(this.mrRPM);
			writer.Write(this.mbNextFuelQueued);
			writer.Write(this.currentFuelType);
			writer.Write(this.HasSpentFuel);
		}
	
		public override void Read(BinaryReader reader, int entityVersion) {
			this.mrCurrentPower = reader.ReadSingle();
			this.mrBurnTime = reader.ReadSingle();
			this.mrRPM = reader.ReadSingle();
			this.mbNextFuelQueued = reader.ReadInt32();
			this.currentFuelType = reader.ReadInt32();
			this.HasSpentFuel = reader.ReadBoolean();
		}
	
		public override bool ShouldNetworkUpdate() {
			return this.mValue == CENTER_VALUE;
		}
	
		protected override bool setupHolobaseVisuals(Holobase hb, out GameObject model, out Vector3 size, out Color color) {
			if (!base.setupHolobaseVisuals(hb, out model, out size, out color))
				return false;
			if (this.mValue == CENTER_VALUE)
				return false;
			size = new Vector3(3f, 3f, 5f);
			color = Color.gray;
			return true;
		}
	
		public const ushort HOUSING_PLACEMENT_VALUE = 2;
	
		public const ushort INTAKE_PLACEMENT_VALUE = 3;
	
		public const ushort COMPONENT_VALUE = 0;
	
		public const ushort CENTER_VALUE = 1;
	
		public const float HECF_BURN_TIME = 30F;
	
		public const float HECF_PEAK_POWER_OUTPUT_PER_SEC = 182.26666F;
	
		public const float MAX_RPM = 8000;
	
		public const float VOLUME_MIN = 0.3F;
	
		public const float VOLUME_MAX = 1F;
	
		public const float PITCH_MIN = 0.05F;
	
		public const float PITCH_MAX = 2F;
	
		public readonly int HECF_ITEM_ID = ItemEntry.mEntriesByKey["HighEnergyCompositeFuel"].ItemID;
		public readonly int HOF_ITEM_ID = ItemEntry.mEntriesByKey["HighOctaneFuel"].ItemID;
		public readonly int TURBOFUEL_ITEM_ID = TurbofuelMod.turbofuelRecipe.CraftableItemType;
	
		public readonly int SPENT_FUEL_ITEM_ID = ItemEntry.mEntriesByKey["EmptyFuelCanister"].ItemID;
		
		public int currentFuelType;
	
		public int mnCurrentSide;
	
		public int mnCurrentSideIndex;
	
		public GeneratorState mState;
	
		public int mnOrientation;
	
		public DynamicJetGenerator mLinkedCenter;
	
		public float mrMaxPower = 15000f;
	
		public float mrCurrentPower;
	
		public float mrTransferRate = 320f;
	
		public float mrCurrentPPS;
	
		public List<PowerConsumerInterface> mNeighbouringMachines;
	
		private int mnMachineRoundRobinPosition;
	
		public List<StorageMachineInterface> mNeighbouringHoppers;
	
		private int mnHopperRoundRobinPosition;
	
		public float mrRPM;
	
		public float mrBurnTime;
	
		public int mbNextFuelQueued;
	
		private float mrLastRPM;
	
		public float mrLerpProgress;
	
		public bool HasSpentFuel;
	
		private Light mFuelSpentLight;
	
		private ParticleSystem mIntakeParticles;
	
		private ParticleSystem mExhaustParticles;
	
		private GameObject mExhaustMesh;
	
		private GameObject mBlurMesh;
	
		private MaterialPropertyBlock mBlurMPB;
	
		private HurtPlayerOnStay mHurtBox;
	
		private ParticleSystem mHeatShimmerParticles;
	
		private float mrMaxHeatShimmer;
	
		public static Material TurbineMaterial;
	
		private GameObject Blades;
	
		private GameObject TurbineBase;
	
		private bool LinkedToGO;
	
		private JetTurbineGeneratorScript JTGScript;
	
		private float ParticleUpdateDelay;
	
		private bool ParticlesActive = true;
	
		public enum GeneratorState {
			LookingForLink,
			ReacquiringLink,
			Linked,
			Delinked
		}
	
	}

}