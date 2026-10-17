using Content.Shared._RMC14.Weapons.Common;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands;
using Content.Shared.Inventory.Events;
using Content.Shared.Timing;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Weapons.Guns;

public sealed class GunBalanceSystem : EntitySystem
{
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly UseDelaySystem _useDelay = default!;

    public const string GunDrawDelayId = "StoriesGunDrawDelay";
    public const string GunMagazineDelayId = "StoriesGunMagazineDelay";
    public static readonly TimeSpan GunDrawDelay = TimeSpan.FromSeconds(1.1);
    public static readonly TimeSpan GunMagazineDelay = TimeSpan.FromSeconds(1.1);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GunComponent, GotUnequippedEvent>(OnGunUnequipped);
        SubscribeLocalEvent<GunComponent, GotEquippedHandEvent>(OnGunEquippedHand);
        SubscribeLocalEvent<GunComponent, ShotAttemptedEvent>(OnShotAttempted);

        SubscribeLocalEvent<MarineShooterComponent, ShotAttemptedEvent>(OnMarineShotAttempted);

        SubscribeLocalEvent<GunComponent, ItemSlotInsertAttemptEvent>(OnMagazineInsertAttempt);
        SubscribeLocalEvent<GunComponent, ItemSlotEjectAttemptEvent>(OnMagazineEjectAttempt);

        SubscribeLocalEvent<GunComponent, EntInsertedIntoContainerMessage>(OnGunContainerInserted);
        SubscribeLocalEvent<GunComponent, EntRemovedFromContainerMessage>(OnGunContainerRemoved);

        SubscribeLocalEvent<RMCAmmoEjectComponent, RMCTryAmmoEjectEvent>(OnAmmoEjectAttempt, before: [typeof(CMGunSystem), typeof(BreechLoadedSystem)]);
    }

    private void OnGunUnequipped(Entity<GunComponent> ent, ref GotUnequippedEvent args)
    {
        if (Paused(ent))
            return;

        if (_net.IsServer && MetaData(ent).EntityLifeStage < EntityLifeStage.MapInitialized)
            return;

        ApplyDrawDelay(ent);

        var curTime = _timing.CurTime;
        var shooter = EnsureComp<MarineShooterComponent>(args.Equipee);
        if (shooter.NextGlobalFire < curTime + GunDrawDelay)
        {
            shooter.NextGlobalFire = curTime + GunDrawDelay;
            shooter.LastFiredGun = null;
            Dirty(args.Equipee, shooter);
        }
    }

    private void OnGunEquippedHand(Entity<GunComponent> ent, ref GotEquippedHandEvent args)
    {
        if (Paused(ent))
            return;

        if (_net.IsServer && MetaData(ent).EntityLifeStage < EntityLifeStage.MapInitialized)
            return;

        ApplyDrawDelay(ent);

        var curTime = _timing.CurTime;
        var shooter = EnsureComp<MarineShooterComponent>(args.User);
        if (shooter.NextGlobalFire < curTime + GunDrawDelay)
        {
            shooter.NextGlobalFire = curTime + GunDrawDelay;
            shooter.LastFiredGun = null;
            Dirty(args.User, shooter);
        }
    }

    private void OnShotAttempted(Entity<GunComponent> ent, ref ShotAttemptedEvent args)
    {
        if (args.Cancelled)
            return;

        var curTime = _timing.CurTime;
        if (ent.Comp.NextFire > curTime)
        {
            args.Cancel();
            return;
        }

        if (TryComp<UseDelayComponent>(ent, out var useDelay))
        {
            if (_useDelay.IsDelayed((ent.Owner, useDelay), GunDrawDelayId) ||
                _useDelay.IsDelayed((ent.Owner, useDelay), GunMagazineDelayId) ||
                _useDelay.IsDelayed((ent.Owner, useDelay), "StoriesGunCycleModeDelay"))
            {
                args.Cancel();
            }
        }
    }

    private void OnMarineShotAttempted(Entity<MarineShooterComponent> ent, ref ShotAttemptedEvent args)
    {
        if (args.Cancelled)
            return;

        var curTime = _timing.CurTime;
        if (ent.Comp.NextGlobalFire > curTime && ent.Comp.LastFiredGun != args.Used.Owner)
        {
            args.Cancel();
        }
    }

    private void OnGunContainerInserted(Entity<GunComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != SharedGunSystem.MagazineSlot)
            return;

        if (Paused(ent))
            return;

        if (_net.IsServer && MetaData(ent).EntityLifeStage < EntityLifeStage.MapInitialized)
            return;

        ApplyMagazineCooldown(ent);
    }

    private void OnGunContainerRemoved(Entity<GunComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != SharedGunSystem.MagazineSlot)
            return;

        if (Paused(ent))
            return;

        if (_net.IsServer && MetaData(ent).EntityLifeStage < EntityLifeStage.MapInitialized)
            return;

        ApplyMagazineCooldown(ent);
    }

    private void OnMagazineInsertAttempt(Entity<GunComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Slot.ID != SharedGunSystem.MagazineSlot)
            return;

        if (args.User is { } user && !_gun.IsGunInHands(user, ent.Owner))
        {
            args.Cancelled = true;
            return;
        }

        if (TryComp<UseDelayComponent>(ent, out var useDelay) &&
            _useDelay.IsDelayed((ent.Owner, useDelay), GunMagazineDelayId))
        {
            args.Cancelled = true;
        }
    }

    private void OnMagazineEjectAttempt(Entity<GunComponent> ent, ref ItemSlotEjectAttemptEvent args)
    {
        if (args.Slot.ID != SharedGunSystem.MagazineSlot)
            return;

        if (args.User is { } user && !_gun.IsGunInHands(user, ent.Owner))
        {
            args.Cancelled = true;
            return;
        }

        if (TryComp<UseDelayComponent>(ent, out var useDelay) &&
            _useDelay.IsDelayed((ent.Owner, useDelay), GunMagazineDelayId))
        {
            args.Cancelled = true;
        }
    }

    private void OnAmmoEjectAttempt(Entity<RMCAmmoEjectComponent> ent, ref RMCTryAmmoEjectEvent args)
    {
        if (args.Cancelled)
            return;

        if (!HasComp<GunComponent>(ent.Owner))
            return;

        if (!_gun.IsGunInHands(args.User, ent.Owner))
        {
            args.Cancelled = true;
        }
    }

    public void ApplyDrawDelay(Entity<GunComponent> ent)
    {
        var curTime = _timing.CurTime;
        var minNextFire = curTime + GunDrawDelay;

        if (ent.Comp.NextFire < minNextFire)
        {
            ent.Comp.NextFire = minNextFire;
            ent.Comp.LastFire = curTime;
            Dirty(ent);
        }

        var useDelay = EnsureComp<UseDelayComponent>(ent);
        _useDelay.SetLength((ent.Owner, useDelay), GunDrawDelay, GunDrawDelayId);
        _useDelay.TryResetDelay((ent.Owner, useDelay), false, GunDrawDelayId);

        if (TryComp<TransformComponent>(ent, out var xform) &&
            xform.ParentUid.Valid &&
            TryComp<MarineShooterComponent>(xform.ParentUid, out var shooter))
        {
            if (shooter.NextGlobalFire < minNextFire)
            {
                shooter.NextGlobalFire = minNextFire;
                shooter.LastFiredGun = null;
                Dirty(xform.ParentUid, shooter);
            }
        }
    }

    public void ApplyMagazineCooldown(Entity<GunComponent> ent)
    {
        var curTime = _timing.CurTime;
        var minNextFire = curTime + GunMagazineDelay;

        if (ent.Comp.NextFire < minNextFire)
        {
            ent.Comp.NextFire = minNextFire;
            ent.Comp.LastFire = curTime;
            Dirty(ent);
        }

        var useDelay = EnsureComp<UseDelayComponent>(ent);
        _useDelay.SetLength((ent.Owner, useDelay), GunMagazineDelay, GunMagazineDelayId);
        _useDelay.TryResetDelay((ent.Owner, useDelay), false, GunMagazineDelayId);
    }
}
