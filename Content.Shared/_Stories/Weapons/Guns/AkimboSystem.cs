using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Shared._Stories.Weapons.Guns;

public sealed class AkimboSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AkimboComponent, GetWeaponAccuracyEvent>(OnAkimboGetWeaponAccuracy);
        SubscribeLocalEvent<AkimboComponent, GunRefreshModifiersEvent>(OnAkimboRefreshModifiers);
    }

    private void OnAkimboGetWeaponAccuracy(Entity<AkimboComponent> gun, ref GetWeaponAccuracyEvent args)
    {
        if (!TryGetOtherAkimboGun(gun, out _))
            return;

        args.AccuracyMultiplier *= gun.Comp.AccuracyMultiplier;
    }

    private void OnAkimboRefreshModifiers(Entity<AkimboComponent> gun, ref GunRefreshModifiersEvent args)
    {
        if (!TryGetOtherAkimboGun(gun, out _))
            return;

        args.CameraRecoilScalar += gun.Comp.RecoilModifier;
    }

    public bool TryGetOtherAkimboGun(Entity<AkimboComponent> gun, out Entity<AkimboComponent> other)
    {
        other = default;
        var parent = Transform(gun.Owner).ParentUid;
        if (!parent.IsValid() || !TryComp<HandsComponent>(parent, out var handsComp))
            return false;

        foreach (var hand in handsComp.Hands.Keys)
        {
            if (_hands.GetHeldItem(parent, hand) is { } held &&
                held != gun.Owner &&
                TryComp<AkimboComponent>(held, out var otherComp))
            {
                other = (held, otherComp);
                return true;
            }
        }

        return false;
    }
}
