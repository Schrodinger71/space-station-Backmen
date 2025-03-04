﻿using System.Linq;
using Content.Corvax.Interfaces.Server;
using Content.Server.GameTicking;
using Content.Server.Hands.Systems;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Clothing.Components;
using Content.Shared.Inventory;
using Robust.Shared.Prototypes;

namespace Content.Server.Corvax.Loadout;

// NOTE: Full implementation will be in future, now just sponsor items
public sealed class LoadoutSystem : EntitySystem
{
    private const string BackpackSlotId = "back";

    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly HandsSystem _handsSystem = default!;
    [Dependency] private readonly StorageSystem _storageSystem = default!;
    private IServerSponsorsManager? _sponsorsManager;

    public override void Initialize()
    {
        IoCManager.Instance!.TryResolveType(out _sponsorsManager); // Corvax-Sponsors
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        if (_sponsorsManager == null)
            return;

        if (_sponsorsManager.TryGetPrototypes(ev.Player.UserId, out var prototypes))
        {
            foreach (var loadoutId in prototypes)
            {
                // NOTE: Now is easy to not extract method because event give all info we need
                if (_prototypeManager.TryIndex<LoadoutItemPrototype>(loadoutId, out var loadout))
                {
                    var isSponsorOnly = loadout.SponsorOnly &&
                                        !prototypes.Contains(loadoutId);
                    var isWhitelisted = ev.JobId != null &&
                                        loadout.WhitelistJobs != null &&
                                        !loadout.WhitelistJobs.Contains(ev.JobId);
                    var isBlacklisted = ev.JobId != null &&
                                        loadout.BlacklistJobs != null &&
                                        loadout.BlacklistJobs.Contains(ev.JobId);
                    var isSpeciesRestricted = loadout.SpeciesRestrictions != null &&
                                              loadout.SpeciesRestrictions.Contains(ev.Profile.Species);

                    if (isSponsorOnly || isWhitelisted || isBlacklisted || isSpeciesRestricted)
                        continue;

                    var entity = Spawn(loadout.EntityId, Transform(ev.Mob).Coordinates);

                    // Take in hand if not clothes
                    if (!TryComp<ClothingComponent>(entity, out var clothing))
                    {
                        _handsSystem.TryPickup(ev.Mob, entity);
                        continue;
                    }

                    // Automatically search empty slot for clothes to equip
                    string? firstSlotName = null;
                    var isEquiped = false;

                    if (_inventorySystem.TryGetSlots(ev.Mob, out var slots))
                    {
                        foreach (var slot in slots)
                        {
                            if (!clothing.Slots.HasFlag(slot.SlotFlags))
                                continue;

                            if (firstSlotName == null)
                                firstSlotName = slot.Name;

                            if (_inventorySystem.TryGetSlotEntity(ev.Mob, slot.Name, out var _))
                                continue;

                            if (_inventorySystem.TryEquip(ev.Mob, entity, slot.Name, true))
                            {
                                isEquiped = true;
                                break;
                            }
                        }
                    }


                    if (isEquiped || firstSlotName == null)
                        continue;

                    // Force equip to first valid clothes slot
                    // Get occupied entity -> Insert to backpack -> Equip loadout entity
                    if (_inventorySystem.TryGetSlotEntity(ev.Mob, firstSlotName, out var slotEntity) &&
                        _inventorySystem.TryGetSlotEntity(ev.Mob, BackpackSlotId, out var backEntity) &&
                        _storageSystem.CanInsert(backEntity.Value, slotEntity.Value, out _))
                    {
                        _storageSystem.Insert(backEntity.Value, slotEntity.Value, out _, playSound: false);
                    }
                    _inventorySystem.TryEquip(ev.Mob, entity, firstSlotName, true);
                }
            }
        }
    }
}
