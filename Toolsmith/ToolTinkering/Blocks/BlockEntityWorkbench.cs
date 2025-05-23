﻿using SmithingPlus.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Toolsmith.ToolTinkering.Drawbacks;
using Toolsmith.Utils;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.Common.Collectible.Block;
using Vintagestory.GameContent;

namespace Toolsmith.ToolTinkering.Blocks {
    public class BlockEntityWorkbench : BlockEntity {

        protected WorkbenchInventory Inventory { get; private set; }
        protected ICoreClientAPI capi;
        protected Dictionary<string, MeshData> WorkbenchItemMeshCache => ObjectCacheUtil.GetOrCreate(Api, ToolsmithConstants.WorkbenchItemRenderingMeshRefs, () => new Dictionary<string, MeshData>());
        private (float x, float y, float z)[] offsetBySlot = { (0f, 0f, 0f), (0.4f, 1f, 0.3f), (0.6f, 1f, 0.6f), (0.8f, 1f, 0.3f), (1.0f, 1f, 0.6f), (1.2f, 1f, 0.3f), (0f, 0f, 0f), (1.55f, 1f, 0.55f) };

        private int craftingHitsCount = 0;
        private (float xoff, float yoff, float zoff, int rot) craftingSlot1Wiggle = (0f, 0f, 0f, 0);
        private (float xoff, float yoff, float zoff, int rot) craftingSlot2Wiggle = (0f, 0f, 0f, 0);
        private (float xoff, float yoff, float zoff, int rot) craftingSlot3Wiggle = (0f, 0f, 0f, 0);
        private (float xoff, float yoff, float zoff, int rot) craftingSlot4Wiggle = (0f, 0f, 0f, 0);
        private (float xoff, float yoff, float zoff, int rot) craftingSlot5Wiggle = (0f, 0f, 0f, 0);

        public override void Initialize(ICoreAPI api) {
            base.Initialize(api);
            capi = api as ICoreClientAPI;
            Inventory ??= new WorkbenchInventory(Api, Pos);
            if (capi != null ) {
                UpdateMeshes();
            }
        }

        public bool IsSelectSlotEmpty(int slotID) {
            return Inventory.IsSelectSlotEmpty(slotID);
        }

        public (float x, float y, float z) GetOffsetBySlot(int slotID) {
            return offsetBySlot[slotID];
        }

        protected (float xoff, float yoff, float zoff, int rot) GetSlotsCraftingWiggleFactor(int slotID) {
            switch (slotID) {
                case 1:
                    return craftingSlot1Wiggle;
                case 2:
                    return craftingSlot2Wiggle;
                case 3:
                    return craftingSlot3Wiggle;
                case 4:
                    return craftingSlot4Wiggle;
                case 5:
                    return craftingSlot5Wiggle;
                default:
                    return (0f, 0f, 0f, 0);
            }
        }

        protected void SetSlotsCraftingWiggleFactor(int slotID, (float x, float y, float z, int rot) wiggler) {
            switch (slotID) {
                case 1:
                    craftingSlot1Wiggle = wiggler;
                    break;
                case 2:
                    craftingSlot2Wiggle = wiggler;
                    break;
                case 3:
                    craftingSlot3Wiggle = wiggler;
                    break;
                case 4:
                    craftingSlot4Wiggle = wiggler;
                    break;
                case 5:
                    craftingSlot5Wiggle = wiggler;
                    break;
            }
        }

        protected void ResetCraftingAttempt() {
            craftingHitsCount = 0;
            craftingSlot1Wiggle = (0f, 0f, 0f, 0);
            craftingSlot2Wiggle = (0f, 0f, 0f, 0);
            craftingSlot3Wiggle = (0f, 0f, 0f, 0);
            craftingSlot4Wiggle = (0f, 0f, 0f, 0);
            craftingSlot5Wiggle = (0f, 0f, 0f, 0);
        }

        protected void RandomizeWiggles(IWorldAccessor world) {
            var rand = world.Rand;
            for (int i = 1; i < 6; i++) {
                var wiggler = GetSlotsCraftingWiggleFactor(i);
                wiggler.xoff = (float)((rand.NextDouble() * 0.1f) - 0.05);
                wiggler.zoff = (float)((rand.NextDouble() * 0.1f) - 0.05);
                wiggler.rot = rand.Next(-10, 10);
                SetSlotsCraftingWiggleFactor(i, wiggler);
            }
        }

        public bool TryGetOrPutItemOnWorkbench(int slotSelection, ItemSlot mainHandSlot, IPlayer byPlayer, IWorldAccessor world) { //The item is valid for fitting in the slot, see if it is empty and if so, stick one in! Otherwise try and remove the existing item.
            if (slotSelection >= (int)WorkbenchSlots.CraftingSlot1 && slotSelection <= (int)WorkbenchSlots.CraftingSlot5) {
                ResetCraftingAttempt();
            }
            var workbenchSlotSelection = Inventory.GetSlotFromSelectionID(slotSelection);
            if (workbenchSlotSelection != null && !workbenchSlotSelection.Empty) {
                if (!Inventory.AddAdditionalToSlot(slotSelection, mainHandSlot)) {
                    return TryGetItemFromWorkbench(slotSelection, mainHandSlot, byPlayer, world);
                } else {
                    return true;
                }
            }

            return Inventory.AddItemToSlot(slotSelection, mainHandSlot);
        }

        public bool TryGetItemFromWorkbench(int slotSelection, ItemSlot mainHandslot, IPlayer byPlayer, IWorldAccessor world) { //For when we are only going to see if an item can be popped out of the slot.
            if (slotSelection >= (int)WorkbenchSlots.CraftingSlot1 && slotSelection <= (int)WorkbenchSlots.CraftingSlot5) {
                ResetCraftingAttempt();
            }
            var workbenchSlotSelection = Inventory.GetSlotFromSelectionID(slotSelection);
            if (workbenchSlotSelection != null && workbenchSlotSelection.Empty) {
                return false; //There's no item to get, so return false cause it didn't succeed in dropping anything. Just to check if the Rendering needs an update or not!
            }

            ItemStack gotItem = Inventory.GetItemFromSlot(slotSelection);
            if (gotItem == null) {
                return false;
            }

            if (!byPlayer.InventoryManager.TryGiveItemstack(gotItem, slotNotifyEffect: true)) {
                var ent = world.SpawnItemEntity(gotItem, new Vec3d(byPlayer.Entity.Pos.X, byPlayer.Entity.Pos.Y, byPlayer.Entity.Pos.Z));
                if (ent != null) {
                    return true;
                } else {
                    return false;
                }
            } else {
                return true;
            }
        }

        public bool AttemptToCraft(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel) {
            if (craftingHitsCount < ToolsmithConstants.NumHammerStrikesForWorkbenchCraftAction) {
                craftingHitsCount++;
                RandomizeWiggles(world);
                return true;
            } else {
                ResetCraftingAttempt();
                if (world.Side.IsClient()) {
                    return true;
                }

                ItemSlot[] craftingSlots = Inventory.GetFullCraftingSlots();
                if (craftingSlots.Count() > 1) {
                    if (ReforgingUtility.CheckForPossibleMerger(craftingSlots)) {
                        var combinedStack = ReforgingUtility.MergeDupesAndReturn(craftingSlots);
                        var facing = BlockFacing.FromCode(Block.LastCodePart());
                        if (facing == BlockFacing.WEST) {
                            world.SpawnItemEntity(combinedStack, new Vec3d(Pos.X + 0.5, Pos.Y + 1.1, Pos.Z));
                        } else if (facing == BlockFacing.EAST) {
                            world.SpawnItemEntity(combinedStack, new Vec3d(Pos.X + 0.5, Pos.Y + 1.1, Pos.Z + 1.0));
                        } else if (facing == BlockFacing.SOUTH) {
                            world.SpawnItemEntity(combinedStack, new Vec3d(Pos.X, Pos.Y + 1.1, Pos.Z + 0.5));
                        } else {
                            world.SpawnItemEntity(combinedStack, new Vec3d(Pos.X + 1.0, Pos.Y + 1.1, Pos.Z + 0.5));
                        }

                        if (byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Collectible.Tool == EnumTool.Hammer) {
                            byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Collectible.DamageItem(world, byPlayer.Entity, byPlayer.InventoryManager.ActiveHotbarSlot);
                        }
                        MarkDirty(redrawOnClient: true);
                        return true;
                    }

                    var craftedTool = TinkeringUtility.TryCraftToolFromSlots(craftingSlots, world, blockSel);
                    if (craftedTool != null) {
                        var facing = BlockFacing.FromCode(Block.LastCodePart());
                        if (facing == BlockFacing.WEST) {
                            world.SpawnItemEntity(craftedTool, new Vec3d(Pos.X + 0.5, Pos.Y + 1.1, Pos.Z));
                        } else if (facing == BlockFacing.EAST) {
                            world.SpawnItemEntity(craftedTool, new Vec3d(Pos.X + 0.5, Pos.Y + 1.1, Pos.Z + 1.0));
                        } else if (facing == BlockFacing.SOUTH) {
                            world.SpawnItemEntity(craftedTool, new Vec3d(Pos.X, Pos.Y + 1.1, Pos.Z + 0.5));
                        } else {
                            world.SpawnItemEntity(craftedTool, new Vec3d(Pos.X + 1.0, Pos.Y + 1.1, Pos.Z + 0.5));
                        }

                        if (byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Collectible.Tool == EnumTool.Hammer) {
                            byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Collectible.DamageItem(world, byPlayer.Entity, byPlayer.InventoryManager.ActiveHotbarSlot);
                        }
                        MarkDirty(redrawOnClient: true);
                        return true;
                    }
                }

                return false;
            }
        }

        public bool InitiateReforgeAttempt(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel) {
            var reforgingSlot = Inventory.GetSlotFromSelectionID((int)WorkbenchSlots.ReforgeStaging);
            if (reforgingSlot == null || reforgingSlot.Empty) {
                return false;
            }

            var percentDamage = ReforgingUtility.GetReforgablePercentDamage(reforgingSlot.Itemstack);
            if (percentDamage > ToolsmithModSystem.Config.PercentDamageForReforge) {
                return false;
            }

            var recipe = ReforgingUtility.TryGetSmithingRecipeFromCache(reforgingSlot.Itemstack, world.Api);
            if (recipe == null) {
                return false;
            }

            if (recipe.Output.Quantity != reforgingSlot.StackSize) {
                return false;
            }

            //Get the metal type from the item in the slot, then generate a work item for that type of metal
            string metal = reforgingSlot.Itemstack.Collectible.GetMetalItem(world.Api);
            if (metal == null) {
                ToolsmithModSystem.Logger.Warning("Could not get a metal or material variant from " + reforgingSlot.Itemstack.Collectible.Code + ". Cannot start a reforge!");
                return false;
            }
            if (ToolsmithModSystem.Config.DebugMessages) {
                ToolsmithModSystem.Logger.Warning("Here we go! What is the metal? " + metal);
            }
            ItemStack workItem = ReforgingUtility.GetWorkItemFromMetalType(world.Api, metal);
            if (workItem == null) {
                ToolsmithModSystem.Logger.Warning("Could not generate a workitem from this metal - " + metal + " | Unable to start a reforge!");
                return false;
            }
            if (ToolsmithModSystem.Config.DebugMessages) {
                ToolsmithModSystem.Logger.Warning("What is the workitem? " + workItem.Collectible.Code);
            }

            //Generate the complete work item voxel data from the recipe.
            var recipeVoxels = ReforgingUtility.GetVoxelCopyFromRecipe(recipe); //Smithing Plus trims and modifies this array before Serializing it through the Anvil and applying the data to the WorkItem Attributes. It's likely better to follow suit.

            //Calculate the average number of voxels that should be removed from the full piece, then trim off or move around a few voxels to simulate the damage
            var totalVoxels = ReforgingUtility.TotalVoxelsInRecipe(recipe);
            var remainingVoxels = MathUtility.NumberOfVoxelsLeftInReforge(percentDamage, totalVoxels);

            recipeVoxels = ReforgingUtility.DamageWorkpieceForReforge(recipeVoxels, totalVoxels, remainingVoxels, world.Api);

            //Finally, apply any Drawback's additional modifiers to the voxels - But this can come some time after the release, when Drawbacks are more actually implemented.



            //Serialize it and set it to the workitem, and replace the slot with it!
            ReforgingUtility.SerializeVoxelsToWorkPiece(workItem, recipeVoxels);
            ReforgingUtility.SetRecipeIDToWorkPiece(workItem, recipe);
            reforgingSlot.Itemstack = null;
            reforgingSlot.MarkDirty();
            if (byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Collectible.Tool == EnumTool.Hammer) {
                byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Collectible.DamageItem(world, byPlayer.Entity, byPlayer.InventoryManager.ActiveHotbarSlot);
            }

            var facing = BlockFacing.FromCode(Block.LastCodePart());
            if (facing == BlockFacing.WEST) {
                world.SpawnItemEntity(workItem, new Vec3d(Pos.X + 0.5, Pos.Y + 1.1, Pos.Z - 0.5));
            } else if (facing == BlockFacing.EAST) {
                world.SpawnItemEntity(workItem, new Vec3d(Pos.X + 0.5, Pos.Y + 1.1, Pos.Z + 1.5));
            } else if (facing == BlockFacing.SOUTH) {
                world.SpawnItemEntity(workItem, new Vec3d(Pos.X - 0.5, Pos.Y + 1.1, Pos.Z + 0.5));
            } else {
                world.SpawnItemEntity(workItem, new Vec3d(Pos.X + 1.5, Pos.Y + 1.1, Pos.Z + 0.5));
            }

            MarkDirty(redrawOnClient: true);

            return true;
        }

        public override void OnBlockBroken(IPlayer byPlayer = null) {
            base.OnBlockBroken(byPlayer);

            if (Inventory != null && !Inventory.AllSlotsEmpty()) {
                Inventory.DropAll(Pos.ToVec3d());
            }
        }

        public void UpdateMeshes() {
            if (Api == null || Api.Side.IsServer()) {
                return;
            }
            if (Inventory.AllSlotsEmpty()) {
                return;
            }

            for (int i = 1; i <= 7; i++) {
                if (i == 6) {
                    continue;
                }
                UpdateMesh(i);
            }
        }

        protected void UpdateMesh(int i) {
            if (Api == null || Api.Side.IsServer()) {
                return;
            }

            var slot = Inventory.GetSlotFromSelectionID(i);
            if (slot.Empty) {
                return;
            }

            GetOrCreateMesh(slot.Itemstack, i);
        }

        protected string GetCacheKeyForItem(ItemStack stack, int slotIndex) {
            IContainedMeshSource meshSource = stack.Collectible?.GetCollectibleInterface<IContainedMeshSource>();
            var facing = BlockFacing.FromCode(Block.LastCodePart());
            if (meshSource != null) {
                return facing.Code + "-slot-" + slotIndex + "-" + craftingHitsCount + "-" + meshSource.GetMeshCacheKey(stack);
            }

            return facing.Code + "-slot-" + slotIndex + "-" + craftingHitsCount + "-" + stack.Collectible.Code.ToString();
        }

        protected MeshData GetMesh(ItemStack stack, int slotIndex) {
            string key = GetCacheKeyForItem(stack, slotIndex);
            WorkbenchItemMeshCache.TryGetValue(key, out var meshData);
            return meshData;
        }

        protected MeshData GetOrCreateMesh(ItemStack stack, int slotIndex) {
            if (capi == null) {
                return new MeshData();
            }

            MeshData mesh = GetMesh(stack, slotIndex);
            if (mesh != null) {
                return mesh;
            }

            IContainedMeshSource meshSource = stack.Collectible?.GetCollectibleInterface<IContainedMeshSource>();

            if (meshSource != null) {
                mesh = meshSource.GenMesh(stack, capi.ItemTextureAtlas, Pos);
            }

            if (mesh == null) {
                Shape shape;
                if (stack.Class == EnumItemClass.Item) {
                    if ((stack.Item as ItemWorkItem) != null) {
                        return new MeshData();
                    } else {
                        shape = capi.TesselatorManager.GetCachedShape(stack.Item.Shape.Base);
                    }
                } else {
                    shape = capi.TesselatorManager.GetCachedShape(stack.Block.Shape.Base);
                }

                if (shape == null) {
                    return new MeshData();
                }

                ShapeTextureSource texSource = new(capi, shape, "For rendering item on a Workbench");
                texSource.textures.Clear();

                if (shape.Textures == null || shape.Textures.Count > 0) {
                    foreach ((string texCode, AssetLocation assetLoc) in shape.Textures) { //Go through the shape's textures and populate the texSource with any that the shape already has defined
                        if (stack.Class == EnumItemClass.Item && stack.Item.Textures.TryGetValue(texCode, out CompositeTexture texture)) { //Grab the item's own textures to slap on instead of the shape's base, or just run with the base.
                            texSource.textures[texCode] = texture;
                        } else if (stack.Class == EnumItemClass.Block && stack.Block.Textures.TryGetValue(texCode, out CompositeTexture blockTexture)) {
                            texSource.textures[texCode] = blockTexture;
                        } else {
                            texSource.textures[texCode] = new CompositeTexture(assetLoc);
                        }
                    }
                } else if (stack.Item != null && stack.Item.Textures != null && stack.Item.Textures.Count > 0) {
                    foreach ((string texCode, CompositeTexture tex) in stack.Item.Textures) {
                        texSource.textures.Add(texCode, tex);
                    }
                } else if (stack.Block != null && stack.Block.Textures != null && stack.Block.Textures.Count > 0) {
                    foreach ((string texCode, CompositeTexture tex) in stack.Block.Textures) {
                        texSource.textures.Add(texCode, tex);
                    }
                }

                capi.Tesselator.TesselateShape("Part on Workbench rendering", shape, out mesh, texSource);
            }

            string key = GetCacheKeyForItem(stack, slotIndex);
            WorkbenchItemMeshCache[key] = mesh;

            var offset = offsetBySlot[slotIndex];
            var wiggleFactor = GetSlotsCraftingWiggleFactor(slotIndex);
            offset.x += wiggleFactor.xoff;
            offset.z += wiggleFactor.zoff;

            mesh.Scale(new Vec3f(), 0.5f, 0.5f, 0.5f);
            mesh.Translate(offset.x - 0.15f, offset.y, offset.z - 0.15f);
            var facing = BlockFacing.FromCode(Block.LastCodePart());
            if (facing.Equals(BlockFacing.EAST)) {
                mesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, (270 + wiggleFactor.rot) * (MathF.PI / 180), 0);
            } else if (facing.Equals(BlockFacing.WEST)) {
                mesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, (90 + wiggleFactor.rot) * (MathF.PI / 180), 0);
            } else if (facing.Equals(BlockFacing.SOUTH)) {
                mesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, (180 + wiggleFactor.rot) * (MathF.PI / 180), 0);
            } else {
                mesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, wiggleFactor.rot * (MathF.PI / 180), 0);
            }

            return mesh;
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator) {
            if (!Inventory.AllSlotsEmpty()) {
                for (int i = 1; i <= 7; i++) {
                    if (i == 6) {
                        continue;
                    }

                    ItemSlot slot = Inventory.GetSlotFromSelectionID(i);
                    if (slot.Empty) {
                        continue;
                    }

                    MeshData mesh = GetOrCreateMesh(slot.Itemstack, i);
                    mesher.AddMeshData(mesh);
                }
            }

            return base.OnTesselation(mesher, tessThreadTesselator);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve) {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            Inventory = new WorkbenchInventory(worldAccessForResolve.Api, Pos);
            Inventory.FromTreeAttributes(tree);
        }

        public override void ToTreeAttributes(ITreeAttribute tree) {
            base.ToTreeAttributes(tree);
            Inventory?.ToTreeAttributes(tree);
        }
    }
}
