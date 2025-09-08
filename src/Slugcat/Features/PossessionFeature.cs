using System;
using Martyr.Possession;
using Martyr.Utils;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using SlugBase.Features;
using static SlugBase.Features.FeatureTypes;

namespace Martyr.Slugcat.Features;

/// <summary>
/// A collection of hooks for updating creatures' possession states.
/// </summary>
public class PossessionFeature : IFeature
{
    public static readonly PlayerFeature<bool> AllowPossession = PlayerBool($"{MartyrMain.MOD_GUID}/allow_possession");

    /// <summary>
    /// Applies the Possession module's hooks to the game.
    /// </summary>
    public void ApplyHooks()
    {
        IL.Creature.Update += UpdatePossessedCreatureILHook;

        On.Creature.Die += RemovePossessionHook;
        On.Player.AddFood += AddPossessionTimeHook;
        On.Player.Update += UpdatePlayerPossessionHook;
    }

    /// <summary>
    /// Removes the Possession module's hooks from the game.
    /// </summary>
    public void RemoveHooks()
    {
        IL.Creature.Update -= UpdatePossessedCreatureILHook;

        On.Creature.Die -= RemovePossessionHook;
        On.Player.AddFood -= AddPossessionTimeHook;
        On.Player.Update -= UpdatePlayerPossessionHook;
    }

    public static bool HasPossessionAbility(Player player) =>
        AllowPossession.TryGet(player, out bool canPossess) && canPossess;

    private static void AddPossessionTimeHook(On.Player.orig_AddFood orig, Player self, int add)
    {
        orig.Invoke(self, add);

        if (self.TryGetPossessionManager(out PossessionManager manager))
        {
            manager.PossessionTime += add * 40;
        }
    }

    /// <summary>
    /// Removes any possession this creature had before death.
    /// </summary>
    private static void RemovePossessionHook(On.Creature.orig_Die orig, Creature self)
    {
        orig.Invoke(self);

        if (CompatibilityManager.IsRainMeadowEnabled() && !MeadowUtils.IsMine(self)) return;

        if (self.TryGetPossession(out Player player)
            && player.TryGetPossessionManager(out PossessionManager manager))
        {
            manager.StopPossession(self);
        }
    }

    /// <summary>
    /// Updates the player's possession manager. If none is found, a new one is created, then updated as well.
    /// </summary>
    private static void UpdatePlayerPossessionHook(On.Player.orig_Update orig, Player self, bool eu)
    {
        orig.Invoke(self, eu);

        if (CompatibilityManager.IsRainMeadowEnabled() && MeadowUtils.IsOnline)
        {
            if (MeadowUtils.IsGameMode(MeadowUtils.MeadowGameModes.Meadow)) return;

            Possession.Meadow.RPCManager.UpdateRPCs();

            if (!MeadowUtils.IsMine(self)) return;
        }

        if (!self.dead && HasPossessionAbility(self))
        {
            PossessionManager manager = self.GetPossessionManager();

            manager.Update();
        }
    }

    /// <summary>
    /// Conditionally overrides the game's default behavior for taking control of creatures in Safari Mode.
    /// Also adds basic behaviors for validating a creature's possession state.
    /// </summary>
    private static void UpdatePossessedCreatureILHook(ILContext context)
    {
        try
        {
            ILCursor c = new(context);
            ILLabel? target = null;

            c.GotoNext(
                MoveType.After,
                x => x.MatchLdsfld(typeof(ModManager).GetField(nameof(ModManager.MSC))),
                x => x.MatchBrfalse(out target)
            ).MoveAfterLabels();

            c.Emit(OpCodes.Ldarg_0).EmitDelegate(UpdateCreaturePossession);
            c.Emit(OpCodes.Brtrue, target);
        }
        catch (Exception ex)
        {
            MyLogger.LogError($"Failed to apply hook: {nameof(UpdatePossessedCreatureILHook)}", ex);
        }
    }

    /// <summary>
    /// Updates the creature's possession state. If the possession is no longer valid, it is removed instead.
    /// </summary>
    /// <param name="self">The creature itself.</param>
    /// <returns><c>true</c> if the game's default behavior was overriden, <c>false</c> otherwise.</returns>
    private static bool UpdateCreaturePossession(Creature self)
    {
        if ((CompatibilityManager.IsRainMeadowEnabled() && !MeadowUtils.IsMine(self))
            || !self.TryGetPossession(out Player player)
            || !player.TryGetPossessionManager(out PossessionManager manager))
        {
            return false;
        }

        if (!manager.HasPossession(self) || !manager.IsPossessionValid(self))
        {
            manager.StopPossession(self);

            return false;
        }

        self.SafariControlInputUpdate(player.playerState.playerNumber);

        return true;
    }
}