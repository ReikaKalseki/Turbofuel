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
	
	public class TurbofuelCrafter : FCoreMachine, PowerConsumerInterface
	{
		
		public TurbofuelCrafter(ModCreateSegmentEntityParameters parameters) : base(parameters) {
			this.mMBMState = 0;
			if (parameters.Value == TurbofuelMod.crafterCenterValue) {
				this.mbIsCenter = true;
				this.mMBMState = MBMState.ReacquiringLink;
				this.RequestLowFrequencyUpdates();
				this.mbNeedsUnityUpdate = true;
				this.mrPowerUsePerSecond = TurbofuelMod.getConfig().getFloat(TBConfig.ConfigEntries.CRAFTER_PPS);
				this.mrMaxPower = mrPowerUsePerSecond * 5;
				mrMaxTransferRate = mrMaxPower;
				this.processTime = TurbofuelMod.getConfig().getFloat(TBConfig.ConfigEntries.CRAFTER_TIME);
				missingItems = new int[TurbofuelMod.turbofuelRecipe.Costs.Count];
			}
		}

		private void DeconstructMachineFromCentre(TurbofuelCrafter deletedBlock) {
			FUtil.log("Deconstructing TurbofuelCrafter into placement blocks");
			for (int i = TurbofuelCrafter.MB_MIN_V; i <= TurbofuelCrafter.MB_MAX_V; i++) {
				for (int j = TurbofuelCrafter.MB_MIN_H; j <= TurbofuelCrafter.MB_MAX_H; j++) {
					for (int k = TurbofuelCrafter.MB_MIN_H; k <= TurbofuelCrafter.MB_MAX_H; k++) {
						long num = this.mnX + (long)k;
						long num2 = this.mnY + (long)i;
						long num3 = this.mnZ + (long)j;
						if ((k != 0 || i != 0 || j != 0) && (deletedBlock == null || num != deletedBlock.mnX || num2 != deletedBlock.mnY || num3 != deletedBlock.mnZ)) {
							Segment segment = WorldScript.instance.GetSegment(num, num2, num3);
							if (segment == null || !segment.mbInitialGenerationComplete || segment.mbDestroyed) {
								this.mMBMState = MBMState.Delinking;
								this.RequestLowFrequencyUpdates();
								return;
							}
							ushort cube = segment.GetCube(num, num2, num3);
							if (cube == TurbofuelMod.crafterBlockID) {
								TurbofuelCrafter crafter = segment.FetchEntity(eSegmentEntity.Mod, num, num2, num3) as TurbofuelCrafter;
								if (crafter == null) {
									FUtil.log("Failed to refind a TurbofuelCrafter entity? wut?");
								}
								else {
									crafter.DeconstructSingleBlock();
								}
							}
						}
					}
				}
			}
			if (this != deletedBlock) {
				this.DeconstructSingleBlock();
			}
		}

		private void DeconstructSingleBlock() {
			this.mMBMState = MBMState.Delinked;
			WorldScript.instance.BuildFromEntity(this.mSegment, this.mnX, this.mnY, this.mnZ, 600, TurbofuelMod.crafterPlacementValue);
		}

		private void LinkMultiBlockMachine() {
			for (int j = TurbofuelCrafter.MB_MIN_V; j <= TurbofuelCrafter.MB_MAX_V; j++) {
				for (int i = TurbofuelCrafter.MB_MIN_H; i <= TurbofuelCrafter.MB_MAX_H; i++) {
					for (int k = TurbofuelCrafter.MB_MIN_H; k <= TurbofuelCrafter.MB_MAX_H; k++) {
						long dx = this.mnX + (long)k;
						long dy = this.mnY + (long)j;
						long dz = this.mnZ + (long)i;
						if (k != 0 || i != 0 || j != 0) {
							Segment segment = base.AttemptGetSegment(dx, dy, dz);
							if (segment == null) {
								return;
							}
							ushort cube = segment.GetCube(dx, dy, dz);
							if (cube == TurbofuelMod.crafterBlockID) {
								TurbofuelCrafter crafter = segment.FetchEntity(eSegmentEntity.Mod, dx, dy, dz) as TurbofuelCrafter;
								if (crafter == null) {
									return;
								}
								if (crafter.mMBMState != MachineEntity.MBMState.Linked || crafter.mLinkedCenter != this) {
									if (crafter.mMBMState == MachineEntity.MBMState.ReacquiringLink && crafter.mLinkX == this.mnX && crafter.mLinkY == this.mnY) {
										long num4 = crafter.mLinkZ;
										long mnZ = this.mnZ;
									}
									crafter.mMBMState = MBMState.Linked;
									crafter.AttachToCentreBlock(this);
								}
							}
						}
					}
				}
			}
			this.ContructionFinished();
			base.DropExtraSegments(null);
		}

		private void ContructionFinished() {
			this.FriendlyState = "TurbofuelCrafter Constructed!";
			this.mMBMState = MBMState.Linked;
			this.mSegment.RequestRegenerateGraphics();
			this.MarkDirtyDelayed();
		}

		private void AttachToCentreBlock(TurbofuelCrafter centerBlock) {
			if (centerBlock == null) {
				FUtil.log("Error, can't set side - requested centre is null!");
			}
			this.mMBMState = MBMState.Linked;
			if (this.mLinkX != centerBlock.mnX) {
				this.MarkDirtyDelayed();
				this.mSegment.RequestRegenerateGraphics();
			}
			this.mLinkedCenter = centerBlock;
			this.mLinkX = centerBlock.mnX;
			this.mLinkY = centerBlock.mnY;
			this.mLinkZ = centerBlock.mnZ;
		}

		private static int GetExtents(int x, int y, int z, long lastX, long lastY, long lastZ, WorldFrustrum frustrum) {
			long num = lastX;
			long num2 = lastY;
			long num3 = lastZ;
			int num4 = 0;
			for (int i = 0; i < 100; i++) {
				num += (long)x;
				num2 += (long)y;
				num3 += (long)z;
				if (!TurbofuelCrafter.IsCubeThisMachine(num, num2, num3, frustrum)) {
					break;
				}
				num4++;
			}
			return num4;
		}

		private static bool IsCubeThisMachine(long checkX, long checkY, long checkZ, WorldFrustrum frustrum) {
			Segment segment = frustrum.GetSegment(checkX, checkY, checkZ);
			if (segment == null || !segment.mbInitialGenerationComplete || segment.mbDestroyed) {
				return false;
			}
			ushort cube = segment.GetCube(checkX, checkY, checkZ);
			if (cube != eCubeTypes.MachinePlacementBlock) {
				return false;
			}
			ushort mValue = segment.GetCubeData(checkX, checkY, checkZ).mValue;
			return mValue == TurbofuelMod.crafterPlacementValue;
		}

		public static void CheckForCompletedMachine(WorldFrustrum frustrum, long lastX, long lastY, long lastZ) {
			if (-TurbofuelCrafter.MB_MIN_H + TurbofuelCrafter.MB_MAX_H + 1 != TurbofuelCrafter.WIDTH) {
				FUtil.log("Error, X is configured wrongly");
			}
			if (-TurbofuelCrafter.MB_MIN_V + TurbofuelCrafter.MB_MAX_V + 1 != TurbofuelCrafter.HEIGHT) {
				FUtil.log("Error, Y is configured wrongly");
			}
			if (-TurbofuelCrafter.MB_MIN_H + TurbofuelCrafter.MB_MAX_H + 1 != TurbofuelCrafter.WIDTH) {
				FUtil.log("Error, Z is configured wrongly");
			}
			int num = TurbofuelCrafter.GetExtents(-1, 0, 0, lastX, lastY, lastZ, frustrum);
			num += TurbofuelCrafter.GetExtents(1, 0, 0, lastX, lastY, lastZ, frustrum);
			num++;
			if (TurbofuelCrafter.WIDTH > num) {
				FUtil.log("Crafter isn't big enough along X(" + num + ")");
				return;
			}
			if (TurbofuelCrafter.WIDTH > num) {
				return;
			}
			int num2 = TurbofuelCrafter.GetExtents(0, -1, 0, lastX, lastY, lastZ, frustrum);
			num2 += TurbofuelCrafter.GetExtents(0, 1, 0, lastX, lastY, lastZ, frustrum);
			num2++;
			if (TurbofuelCrafter.HEIGHT > num2) {
				FUtil.log("Crafter isn't big enough along Y(" + num2 + ")");
				return;
			}
			if (TurbofuelCrafter.HEIGHT > num2) {
				return;
			}
			int num3 = TurbofuelCrafter.GetExtents(0, 0, -1, lastX, lastY, lastZ, frustrum);
			num3 += TurbofuelCrafter.GetExtents(0, 0, 1, lastX, lastY, lastZ, frustrum);
			num3++;
			if (TurbofuelCrafter.WIDTH > num3) {
				FUtil.log("Crafter isn't big enough along Z(" + num3 + ")");
				return;
			}
			if (TurbofuelCrafter.WIDTH > num3) {
				return;
			}
			FUtil.log(string.Concat(new object[] {
				"Crafter is detecting test span of ",
				num,
				":",
				num2,
				":",
				num3
			}));
			bool[,,] array = new bool[TurbofuelCrafter.WIDTH, TurbofuelCrafter.HEIGHT, TurbofuelCrafter.WIDTH];
			for (int i = TurbofuelCrafter.MB_MIN_V; i <= TurbofuelCrafter.MB_MAX_V; i++) {
				for (int j = TurbofuelCrafter.MB_MIN_H; j <= TurbofuelCrafter.MB_MAX_H; j++) {
					for (int k = TurbofuelCrafter.MB_MIN_H; k <= TurbofuelCrafter.MB_MAX_H; k++) {
						array[k + TurbofuelCrafter.MB_MAX_H, i + TurbofuelCrafter.MB_MAX_V, j + TurbofuelCrafter.MB_MAX_H] = true;
					}
				}
			}
			for (int l = -TurbofuelCrafter.MB_OUTER_V; l <= TurbofuelCrafter.MB_OUTER_V; l++) {
				for (int m = -TurbofuelCrafter.MB_MAX_H; m <= TurbofuelCrafter.MB_MAX_H; m++) {
					for (int n = -TurbofuelCrafter.MB_OUTER_H; n <= TurbofuelCrafter.MB_OUTER_H; n++) {
						if (n != 0 || l != 0 || m != 0) {
							Segment segment = frustrum.GetSegment(lastX + (long)n, lastY + (long)l, lastZ + (long)m);
							if (segment == null || !segment.mbInitialGenerationComplete || segment.mbDestroyed) {
								return;
							}
							ushort cube = segment.GetCube(lastX + (long)n, lastY + (long)l, lastZ + (long)m);
							bool flag = false;
							if (cube == eCubeTypes.MachinePlacementBlock) {
								ushort mValue = segment.GetCubeData(lastX + (long)n, lastY + (long)l, lastZ + (long)m).mValue;
								if (mValue == TurbofuelMod.crafterPlacementValue) {
									flag = true;
								}
							}
							if (!flag) {
								for (int num4 = TurbofuelCrafter.MB_MIN_V; num4 <= TurbofuelCrafter.MB_MAX_V; num4++) {
									for (int num5 = TurbofuelCrafter.MB_MIN_H; num5 <= TurbofuelCrafter.MB_MAX_H; num5++) {
										for (int num6 = TurbofuelCrafter.MB_MIN_H; num6 <= TurbofuelCrafter.MB_MAX_H; num6++) {
											int num7 = n + num6;
											int num8 = l + num4;
											int num9 = m + num5;
											if (num7 >= TurbofuelCrafter.MB_MIN_H && num7 <= TurbofuelCrafter.MB_MAX_H && num8 >= TurbofuelCrafter.MB_MIN_V && num8 <= TurbofuelCrafter.MB_MAX_V && num9 >= TurbofuelCrafter.MB_MIN_H && num9 <= TurbofuelCrafter.MB_MAX_H) {
												array[num7 + TurbofuelCrafter.MB_MAX_H, num8 + TurbofuelCrafter.MB_MAX_V, num9 + TurbofuelCrafter.MB_MAX_H] = false;
											}
										}
									}
								}
							}
						}
					}
				}
			}
			int num10 = 0;
			for (int num11 = TurbofuelCrafter.MB_MIN_V; num11 <= TurbofuelCrafter.MB_MAX_V; num11++) {
				for (int num12 = TurbofuelCrafter.MB_MIN_H; num12 <= TurbofuelCrafter.MB_MAX_H; num12++) {
					for (int num13 = TurbofuelCrafter.MB_MIN_H; num13 <= TurbofuelCrafter.MB_MAX_H; num13++) {
						if (array[num13 + TurbofuelCrafter.MB_MAX_H, num11 + TurbofuelCrafter.MB_MAX_V, num12 + TurbofuelCrafter.MB_MAX_H]) {
							num10++;
						}
					}
				}
			}
			if (num10 > 1) {
				FUtil.log("Warning, OE has too many valid positions (" + num10 + ")");
				return;
			}
			if (num10 == 0) {
				return;
			}
			for (int num14 = TurbofuelCrafter.MB_MIN_V; num14 <= TurbofuelCrafter.MB_MAX_V; num14++) {
				for (int num15 = TurbofuelCrafter.MB_MIN_H; num15 <= TurbofuelCrafter.MB_MAX_H; num15++) {
					for (int num16 = TurbofuelCrafter.MB_MIN_H; num16 <= TurbofuelCrafter.MB_MAX_H; num16++) {
						if (array[num16 + TurbofuelCrafter.MB_MAX_H, num14 + TurbofuelCrafter.MB_MAX_V, num15 + TurbofuelCrafter.MB_MAX_H]) {
							if (TurbofuelCrafter.BuildMultiBlockMachine(frustrum, lastX + (long)num16, lastY + (long)num14, lastZ + (long)num15)) {
								return;
							}
							FUtil.log("Error, failed to build TurbofuelCrafter due to bad segment?");
						}
					}
				}
			}
			if (num10 != 0) {
				FUtil.log("Error, thought we found a valid position, but failed to build the TurbofuelCrafter?");
			}
		}

		public static bool BuildMultiBlockMachine(WorldFrustrum frustrum, long centerX, long centerY, long centerZ) {
			HashSet<Segment> hashSet = new HashSet<Segment>();
			bool flag = true;
			try {
				WorldScript.mLocalPlayer.mResearch.GiveResearch(TurbofuelMod.crafterBlockID, 0);
				for (int i = TurbofuelCrafter.MB_MIN_V; i <= TurbofuelCrafter.MB_MAX_V; i++) {
					for (int j = TurbofuelCrafter.MB_MIN_H; j <= TurbofuelCrafter.MB_MAX_H; j++) {
						for (int k = TurbofuelCrafter.MB_MIN_H; k <= TurbofuelCrafter.MB_MAX_H; k++) {
							Segment segment = frustrum.GetSegment(centerX + (long)k, centerY + (long)i, centerZ + (long)j);
							if (segment == null || !segment.mbInitialGenerationComplete || segment.mbDestroyed) {
								flag = false;
							}
							else {
								if (!hashSet.Contains(segment)) {
									hashSet.Add(segment);
									segment.BeginProcessing();
								}
								if (k == 0 && i == 0 && j == 0) {
									frustrum.BuildOrientation(segment, centerX + (long)k, centerY + (long)i, centerZ + (long)j, TurbofuelMod.crafterBlockID, TurbofuelMod.crafterCenterValue, 65);
								}
								else {
									frustrum.BuildOrientation(segment, centerX + (long)k, centerY + (long)i, centerZ + (long)j, TurbofuelMod.crafterBlockID, TurbofuelMod.crafterBodyValue, 65);
								}
							}
						}
					}
				}
			}
			finally {
				foreach (Segment segment2 in hashSet) {
					segment2.EndProcessing();
				}
				WorldScript.instance.mNodeWorkerThread.KickNodeWorkerThread();
			}
			if (!flag) {
				FUtil.log("Error, failed to build crafter as one of it's segments wasn't valid!");
			}
			else {
				AudioSpeechManager.PlayStructureCompleteDelayed = true;
			}
			return flag;
		}

		public override void OnDelete() {
			base.OnDelete();
			if (mMBMState != MachineEntity.MBMState.Linked) {
				if (mMBMState != MachineEntity.MBMState.Delinked) {
					FUtil.log("Deleted crafter while in state " + this.mMBMState);
				}
				return;
			}
			if (WorldScript.mbIsServer) {
				ItemManager.DropNewCubeStack(eCubeTypes.MachinePlacementBlock, TurbofuelMod.crafterPlacementValue, 1, this.mnX, this.mnY, this.mnZ, Vector3.zero);
			}
			this.mMBMState = MBMState.Delinking;
			if (this.mbIsCenter) {
				this.DeconstructMachineFromCentre(this);
				return;
			}
			if (this.mLinkedCenter == null) {
				FUtil.log("Error, crafter had no linked centre, so cannot destroy linked centre?");
				return;
			}
			this.mLinkedCenter.DeconstructMachineFromCentre(this);
		}

		public override void SpawnGameObject() {
			this.mObjectType = SpawnableObjectEnum.BlastFurnace;
			if (this.mbIsCenter) {
				base.SpawnGameObject();
			}
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
			if (this.mbIsCenter) {
				if (mMBMState == MachineEntity.MBMState.ReacquiringLink) {
					this.LinkMultiBlockMachine();
				}
				if (mMBMState == MachineEntity.MBMState.Delinking) {
					this.DeconstructMachineFromCentre(null);
				}
				this.mrStateTimer += LowFrequencyThread.mrPreviousUpdateTimeStep;
				if (!WorldScript.mbIsServer) {
					return;
				}
				int num = 1;
				if (this.mAttachedMassStorage.Count == 0 && this.mAttachedHoppers.Count == 0) {
					num = 84;
				}
				for (int i = 0; i < num; i++) {
					this.LookForMachines();
				}
				this.UpdateOperatingState();
			}
		}

		private void SetNewOperatingState(TurbofuelCrafter.OperatingState newState) {
			this.mrStateTimer = 0f;
			this.mOperatingState = newState;
		}

		private void UpdateOperatingState() {
			switch (this.mOperatingState) {
				case TurbofuelCrafter.OperatingState.WaitingOnResources:
					this.UpdateWaitingForResources();
					return;
				case TurbofuelCrafter.OperatingState.OutOfPower:
					this.UpdateOutOfPower();
					return;
				case TurbofuelCrafter.OperatingState.Processing:
					this.UpdateProcessing();
					return;
				case TurbofuelCrafter.OperatingState.OutOfStorage:
					this.UpdateOutOfStorage();
					return;
				default:
					return;
			}
		}

		private void UpdateOutOfPower() {
			if (this.mrCurrentPower >= this.mrPowerUsePerSecond * LowFrequencyThread.mrPreviousUpdateTimeStep) {
				this.SetNewOperatingState(TurbofuelCrafter.OperatingState.Processing);
			}
		}

		private void UpdateWaitingForResources() {
			for (int j = 0; j < missingItems.Length; j++) {
				missingItems[j] = (int)TurbofuelMod.turbofuelRecipe.Costs[j].Amount;
			}
			if (this.mAttachedHoppers.Count > 0) {
				for (int i = mAttachedHoppers.Count-1; i >= 0; i--) {
					StorageMachineInterface storageMachineInterface = this.mAttachedHoppers[i];
					if (storageMachineInterface == null || ((SegmentEntity)storageMachineInterface).mbDelete) {
						this.mAttachedHoppers.RemoveAt(i);
					}
					else {
						eHopperPermissions permissions = storageMachineInterface.GetPermissions();
						if (permissions == eHopperPermissions.RemoveOnly) {
							storageMachineInterface.IterateContents(new IterateItem(this.IterateHopperItem), null);
						}
					}
				}
			}
			bool flag = true;
			for (int k = 0; k < missingItems.Length; k++) {
				if (missingItems[k] > 0) {
					flag = false;
					break;
				}
			}
			if (flag) {
				this.mrSmeltTimer = this.processTime;
				this.SetNewOperatingState(TurbofuelCrafter.OperatingState.Processing);
				this.RemoveIngredients();
			}
		}

		private void RemoveIngredients() {
			for (int i = 0; i < missingItems.Length; i++) {
				missingItems[i] = (int)TurbofuelMod.turbofuelRecipe.Costs[i].Amount;
			}
			if (this.mAttachedHoppers.Count > 0) {
				for (int i = mAttachedHoppers.Count-1; i >= 0; i--) {
					StorageMachineInterface storageMachineInterface = this.mAttachedHoppers[i];
					if (storageMachineInterface == null || ((SegmentEntity)storageMachineInterface).mbDelete) {
						this.mAttachedHoppers.RemoveAt(i);
					}
					else {
						for (int k = 0; k < TurbofuelMod.turbofuelRecipe.Costs.Count; k++) {
							if (missingItems[k] > 0) {
								CraftCost craftCost = TurbofuelMod.turbofuelRecipe.Costs[k];
								if (craftCost.ItemType >= 0) {
									int num;
									if (craftCost.Amount > 1U) {
										num = storageMachineInterface.TryPartialExtractItems(this, craftCost.ItemType, missingItems[k]);
									}
									else {
										num = storageMachineInterface.CountItems(craftCost.ItemType);
										if (num > 0) {
											bool flag = storageMachineInterface.TryExtractItems(this, craftCost.ItemType, 1);
										}
									}
									missingItems[k] -= num;
								}
								else {
									int num2;
									if (craftCost.Amount > 1U) {
										num2 = storageMachineInterface.TryPartialExtractCubes(this, craftCost.CubeType, craftCost.CubeValue, missingItems[k]);
									}
									else {
										num2 = storageMachineInterface.CountCubes(craftCost.CubeType, craftCost.CubeValue);
										if (num2 > 0) {
											bool flag = storageMachineInterface.TryExtractCubes(this, craftCost.CubeType, craftCost.CubeValue, 1);
										}
									}
									missingItems[k] -= num2;
								}
							}
						}
					}
				}
			}
		}

		private bool IterateHopperItem(ItemBase item, object userState) {
			bool flag = false;
			bool flag2 = true;
			for (int j = 0; j < missingItems.Length; j++) {
				CraftCost craftCost = TurbofuelMod.turbofuelRecipe.Costs[j];
				if (!flag) {
					if (craftCost.CubeType != 0 && item.mType == ItemType.ItemCubeStack) {
						ItemCubeStack itemCubeStack = item as ItemCubeStack;
						if (itemCubeStack.mCubeType == craftCost.CubeType && itemCubeStack.mCubeValue == craftCost.CubeValue) {
							ARTHERPetSurvival.instance.GotOre(itemCubeStack.mCubeType);
							missingItems[j] -= itemCubeStack.mnAmount;
							flag = true;
						}
					}
					else if (item.mnItemID == craftCost.ItemType) {
						missingItems[j] -= ItemManager.GetCurrentStackSize(item);
						flag = true;
					}
				}
				if (missingItems[j] > 0) {
					flag2 = false;
				}
			}
			if (flag2) {
				return false;
			}
			return true;
		}

		private bool IterateCrateItem(ItemBase item) {
			bool flag = false;
			bool flag2 = true;
			for (int j = 0; j < missingItems.Length; j++) {
				CraftCost craftCost = TurbofuelMod.turbofuelRecipe.Costs[j];
				if (!flag) {
					if (craftCost.CubeType != 0 && item.mType == ItemType.ItemCubeStack) {
						ItemCubeStack itemCubeStack = item as ItemCubeStack;
						if (itemCubeStack.mCubeType == craftCost.CubeType && itemCubeStack.mCubeValue == craftCost.CubeValue) {
							missingItems[j] -= itemCubeStack.mnAmount;
							flag = true;
						}
					}
					else if (item.mnItemID == craftCost.ItemType) {
						missingItems[j] -= ItemManager.GetCurrentStackSize(item);
						flag = true;
					}
				}
				if (missingItems[j] > 0) {
					flag2 = false;
				}
			}
			if (flag2) {
				return false;
			}
			return true;
		}

		private void UpdateProcessing() {
			for (int i = 0; i < missingItems.Length; i++) {
				if (missingItems[i] > 0) {
					this.SetNewOperatingState(TurbofuelCrafter.OperatingState.WaitingOnResources);
					return;
				}
			}
			this.mrCurrentPower -= this.mrPowerUsePerSecond * LowFrequencyThread.mrPreviousUpdateTimeStep;
			if (this.mrCurrentPower < 0f) {
				this.mrCurrentPower = 0f;
				this.SetNewOperatingState(TurbofuelCrafter.OperatingState.OutOfPower);
				return;
			}
			this.mrSmeltTimer -= LowFrequencyThread.mrPreviousUpdateTimeStep;
			if (this.mrSmeltTimer <= 0f) {
				this.mCreatedItem = ItemManager.SpawnItem(TurbofuelMod.turbofuelRecipe.CraftableItemType);
				if (!this.AttemptToOffload()) {
					this.SetNewOperatingState(TurbofuelCrafter.OperatingState.OutOfStorage);
				}
			}
		}

		private void UpdateOutOfStorage() {
			this.AttemptToOffload();
		}

		public bool AttemptToOffload() {
			if (this.mAttachedHoppers.Count > 0) {
				for (int i = mAttachedHoppers.Count-1; i >= 0; i--) {
					StorageMachineInterface storageMachineInterface = this.mAttachedHoppers[i];
					if (storageMachineInterface == null || ((SegmentEntity)storageMachineInterface).mbDelete) {
						this.mAttachedHoppers.RemoveAt(i);
					}
					else {
						eHopperPermissions permissions = storageMachineInterface.GetPermissions();
						if (permissions == eHopperPermissions.AddOnly && !storageMachineInterface.IsFull() && storageMachineInterface.TryInsert(this, this.mCreatedItem)) {
							this.mCreatedItem = null;
							this.SetNewOperatingState(TurbofuelCrafter.OperatingState.WaitingOnResources);
							return true;
						}
					}
				}
			}
			return false;
		}

		private void RoundRobinSide(out int y, out int x, out int z) {
			int area = 0;
			switch(mnCurrentSide) {
				case 0: {
					y = this.mnCurrentSideIndex / TurbofuelCrafter.WIDTH + TurbofuelCrafter.MB_MIN_V;
					x = TurbofuelCrafter.MB_MIN_H - 1;
					z = this.mnCurrentSideIndex % TurbofuelCrafter.WIDTH + TurbofuelCrafter.MB_MIN_H;
					area = TurbofuelCrafter.HEIGHT * TurbofuelCrafter.WIDTH;
				}
				break;
				case 1: {
					y = this.mnCurrentSideIndex / TurbofuelCrafter.WIDTH + TurbofuelCrafter.MB_MIN_V;
					x = TurbofuelCrafter.MB_MAX_H + 1;
					z = this.mnCurrentSideIndex % TurbofuelCrafter.WIDTH + TurbofuelCrafter.MB_MIN_H;
					area = TurbofuelCrafter.HEIGHT * TurbofuelCrafter.WIDTH;
				}
				break;
				case 2: {
					y = this.mnCurrentSideIndex / TurbofuelCrafter.WIDTH + TurbofuelCrafter.MB_MIN_V;
					x = this.mnCurrentSideIndex % TurbofuelCrafter.WIDTH + TurbofuelCrafter.MB_MIN_H;
					z = TurbofuelCrafter.MB_MAX_H + 1;
					area = TurbofuelCrafter.HEIGHT * TurbofuelCrafter.WIDTH;
				}
				break;
				case 3: {
					y = this.mnCurrentSideIndex / TurbofuelCrafter.WIDTH + TurbofuelCrafter.MB_MIN_V;
					x = this.mnCurrentSideIndex % TurbofuelCrafter.WIDTH + TurbofuelCrafter.MB_MIN_H;
					z = TurbofuelCrafter.MB_MIN_H - 1;
					area = TurbofuelCrafter.HEIGHT * TurbofuelCrafter.WIDTH;
				}
				break;
				case 4: {
					y = TurbofuelCrafter.MB_MIN_V - 1;
					x = this.mnCurrentSideIndex / TurbofuelCrafter.WIDTH + TurbofuelCrafter.MB_MIN_H;
					z = this.mnCurrentSideIndex % TurbofuelCrafter.WIDTH + TurbofuelCrafter.MB_MIN_H;
					area = TurbofuelCrafter.WIDTH * TurbofuelCrafter.WIDTH;
				}
				break;
				case 5: {
					y = TurbofuelCrafter.MB_MAX_V + 1;
					x = this.mnCurrentSideIndex / TurbofuelCrafter.WIDTH + TurbofuelCrafter.MB_MIN_H;
					z = this.mnCurrentSideIndex % TurbofuelCrafter.WIDTH + TurbofuelCrafter.MB_MIN_H;
					area = TurbofuelCrafter.WIDTH * TurbofuelCrafter.WIDTH;
				}
				break;
				default: {
					x = y = z = 0;
					FUtil.log("Error: invalid side index in crafter roundrobin");
				}
				break;
			}
			this.mnCurrentSideIndex++;
			if (this.mnCurrentSideIndex == area) {
				this.mnCurrentSideIndex = 0;
				this.mnCurrentSide = (this.mnCurrentSide + 1) % 6;
			}
		}

		private void LookForMachines() {
			int y;
			int x;
			int z;
			this.RoundRobinSide(out y, out x, out z);
			long dx = (long)x + this.mnX;
			long dy = (long)y + this.mnY;
			long dz = (long)z + this.mnZ;
			Segment segment = base.AttemptGetSegment(dx, dy, dz);
			if (segment == null) {
				return;
			}
			SegmentEntity e = segment.SearchEntity(dx, dy, dz);
			segment.GetCube(dx, dy, dz);
			if (e is StorageMachineInterface) {
				this.AddAttachedHopper((StorageMachineInterface)e);
			}
			else if (e is MassStorageCrate) {
				this.AddAttachedMassStorage((MassStorageCrate)e);
			}
		}

		private void AddAttachedHopper(StorageMachineInterface storageMachine) {
			for (int i = 0; i < this.mAttachedHoppers.Count; i++) {
				StorageMachineInterface storageMachineInterface = this.mAttachedHoppers[i];
				if (storageMachineInterface != null) {
					if ((storageMachineInterface as SegmentEntity).mbDelete) {
						this.mAttachedHoppers.RemoveAt(i);
						i--;
					}
					else if (storageMachineInterface == storageMachine) {
						storageMachine = null;
					}
				}
			}
			if (storageMachine != null) {
				this.mAttachedHoppers.Add(storageMachine);
			}
		}

		private void AddAttachedMassStorage(MassStorageCrate crate) {
			for (int i = 0; i < this.mAttachedMassStorage.Count; i++) {
				MassStorageCrate massStorageCrate = this.mAttachedMassStorage[i];
				if (massStorageCrate != null) {
					if (massStorageCrate.mbDelete) {
						this.mAttachedMassStorage.RemoveAt(i);
						i--;
					}
					else if (massStorageCrate == crate) {
						crate = null;
					}
				}
			}
			if (crate != null) {
				this.mAttachedMassStorage.Add(crate);
				FUtil.log("Crafter now has " + this.mAttachedMassStorage.Count + " attached Crates");
			}
		}

		private void RequestLowFrequencyUpdates() {
			if (!this.mbNeedsLowFrequencyUpdate) {
				this.mbNeedsLowFrequencyUpdate = true;
				this.mSegment.mbNeedsLowFrequencyUpdate = true;
				if (!this.mSegment.mbIsQueuedForUpdate) {
					WorldScript.instance.mSegmentUpdater.AddSegment(this.mSegment);
				}
			}
		}

		public override bool ShouldSave() {
			return true;
		}

		public override void Write(BinaryWriter writer) {
			writer.Write(this.mbIsCenter);
			writer.Write(this.mLinkX);
			writer.Write(this.mLinkY);
			writer.Write(this.mLinkZ);
			if (!this.mbIsCenter) {
				return;
			}
			writer.Write(this.mrCurrentPower);
			writer.Write((byte)this.mOperatingState);
			if (this.mAttachedHoppers != null) {
				writer.Write((byte)this.mAttachedHoppers.Count);
				return;
			}
			writer.Write(0);
		}

		public override void Read(BinaryReader reader, int entityVersion) {
			this.mbIsCenter = reader.ReadBoolean();
			this.mLinkX = reader.ReadInt64();
			this.mLinkY = reader.ReadInt64();
			this.mLinkZ = reader.ReadInt64();
			this.mMBMState = MBMState.ReacquiringLink;
			if (!this.mbIsCenter) {
				return;
			}
			this.mrCurrentPower = reader.ReadSingle();
			this.mOperatingState = (TurbofuelCrafter.OperatingState)reader.ReadByte();
			if (this.mrCurrentPower < 0f) {
				this.mrCurrentPower = 0f;
			}
			if (this.mrCurrentPower > this.mrMaxPower) {
				this.mrCurrentPower = this.mrMaxPower;
			}
			this.RequestLowFrequencyUpdates();
			if (entityVersion >= 1) {
				this.mnAttachedHoppers = reader.ReadByte();
			}
		}

		public override bool ShouldNetworkUpdate() {
			return true;
		}

		public float GetRemainingPowerCapacity() {
			if (this.mLinkedCenter != null) {
				return this.mLinkedCenter.GetRemainingPowerCapacity();
			}
			return this.mrMaxPower - this.mrCurrentPower;
		}

		public float GetMaximumDeliveryRate() {
			return this.mrMaxTransferRate;
		}

		public float GetMaxPower() {
			if (this.mLinkedCenter != null) {
				return this.mLinkedCenter.GetMaxPower();
			}
			return this.mrMaxPower;
		}

		public bool DeliverPower(float amount) {
			if (this.mLinkedCenter != null) {
				return this.mLinkedCenter.DeliverPower(amount);
			}
			if (amount > this.GetRemainingPowerCapacity()) {
				return false;
			}
			this.mrCurrentPower += amount;
			this.MarkDirtyDelayed();
			return true;
		}

		public bool WantsPowerFromEntity(SegmentEntity entity) {
			return this.mLinkedCenter == null || this.mLinkedCenter.WantsPowerFromEntity(entity);
		}

		public override string GetPopupText() {
			if (this.mLinkedCenter != null) {
				return this.mLinkedCenter.GetPopupText();
			}
			string text = "Turbofuel Crafter";
			string text2 = text;
			text = string.Concat(new string[] {
				text2,
				"\nPower: ",
				this.mrCurrentPower.ToString("N0"),
				"/",
				this.mrMaxPower.ToString("N0")
			});
			text = text + "\nDesires " + this.mrPowerUsePerSecond.ToString() + " pps";
			if (WorldScript.mbIsServer) {
				if (this.mAttachedMassStorage.Count == 0 && this.mAttachedHoppers.Count == 0) {
					text += "\nNo Attached Storage Hoppers or Mass Storage found";
				}
				else {
					if (this.mAttachedMassStorage.Count > 0) {
						object obj = text;
						text = string.Concat(new object[] {
							obj,
							"\nAttached to ",
							this.mAttachedMassStorage.Count,
							" Mass Storage units."
						});
					}
					if (this.mAttachedHoppers.Count > 0) {
						object obj2 = text;
						text = string.Concat(new object[] {
							obj2,
							"\nAttached to ",
							this.mAttachedHoppers.Count,
							" Storage Hoppers."
						});
					}
				}
			}
			else if (this.mnAttachedHoppers == 0) {
				text += "\nNo Attached Storage Hoppers found";
			}
			else {
				object obj3 = text;
				text = string.Concat(new object[] {
					obj3,
					"\nAttached to ",
					this.mnAttachedHoppers,
					" Storage Hoppers."
				});
			}
			text = text + "\nState: " + this.mOperatingState;
			if (this.mOperatingState == TurbofuelCrafter.OperatingState.Processing) {
				text = text + "\nProcessing time: " + this.mrSmeltTimer.ToString("N1") + "s";
			}
			if (this.mOperatingState == TurbofuelCrafter.OperatingState.WaitingOnResources) {
				text += "\nMissing ingredients.";
			}
			return text;
		}

		public bool mbIsCenter;

		public TurbofuelCrafter mLinkedCenter;

		public MachineEntity.MBMState mMBMState;

		public long mLinkX;

		public long mLinkY;

		public long mLinkZ;

		public string FriendlyState = "Unknown state!";

		private static readonly int WIDTH = 3;

		private static readonly int HEIGHT = 7;

		private static readonly int MB_MIN_H = -(TurbofuelCrafter.WIDTH / 2);

		private static readonly int MB_MIN_V = -(TurbofuelCrafter.HEIGHT / 2);

		private static readonly int MB_MAX_H = -TurbofuelCrafter.MB_MIN_H;

		private static readonly int MB_MAX_V = -TurbofuelCrafter.MB_MIN_V;

		private static readonly int MB_OUTER_H = TurbofuelCrafter.MB_MAX_H * 2;

		private static readonly int MB_OUTER_V = TurbofuelCrafter.MB_MAX_V * 2;

		public float processTime;

		public float mrCurrentPower;

		public float mrMaxPower;

		public float mrMaxTransferRate;

		public float mrPowerUsePerSecond;

		public RotateConstantlyScript rotatorScript;

		public ItemBase mCreatedItem;

		public List<StorageMachineInterface> mAttachedHoppers = new List<StorageMachineInterface>();

		public List<MassStorageCrate> mAttachedMassStorage = new List<MassStorageCrate>();

		public int mnCurrentSideIndex;

		public int mnCurrentSide;

		public float mrSmeltTimer;

		public TurbofuelCrafter.OperatingState mOperatingState;

		private readonly int[] missingItems;

		private bool mbLinkedToGO;

		private MaterialPropertyBlock mMPB;

		private GameObject GlowObject;

		private Light GlowLight;

		private float mrGlow;

		private float mrStateTimer;

		public bool mbPlacedIllegally;

		private byte mnAttachedHoppers;

		public enum OperatingState
		{
			WaitingOnResources,
			OutOfPower,
			Processing,
			OutOfStorage
		}
	
	}

}