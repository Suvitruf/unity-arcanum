namespace Arcanum.Formats.Script
{
    /// <summary>Script attachment points — the event that fires a script (engine <c>ScriptAttachmentPoint</c>,
    /// script.h). An object's <c>OBJ_F_SCRIPTS</c> array is indexed by these.</summary>
    public enum Sap
    {
        Examine, Use, Destroy, Unlock, Get, Drop, Throw, Hit, Miss, Dialog,
        FirstHeartbeat, CatchingThiefPc, Dying, EnterCombat, ExitCombat, StartCombat,
        EndCombat, BuyObject, Resurrect, Heartbeat, LeaderKilling, InsertItem, WillKos,
        TakingDamage, WieldOn, WieldOff, CritterHits, NewSector, RemoveItem, LeaderSleeping,
        Bust, DialogOverride, Transfer, CaughtThief, CriticalHit, CriticalMiss,
    }

    /// <summary>How a script operand resolves to a value (engine <c>ScriptValueType</c>).</summary>
    public enum Svt { Counter, GlVar, LcVar, Number, GlFlag, PcVar, PcFlag }

    /// <summary>Which object(s) an object-typed operand resolves to (engine <c>ScriptFocusObject</c>,
    /// script.h). For object operands the operand's <c>op_type</c> is one of these, not a <see cref="Svt"/>.</summary>
    public enum Sfo
    {
        Triggerer, Attachee, EveryFollower, AnyFollower, EveryoneInParty, AnyoneInParty,
        EveryoneInTeam, AnyoneInTeam, EveryoneInVicinity, AnyoneInVicinity, CurrentLoopedObject,
        LocalObject, ExtraObject, EveryoneInGroup, AnyoneInGroup, EverySceneryInVicinity,
        AnySceneryInVicinity, EveryContainerInVicinity, AnyContainerInVicinity, EveryPortalInVicinity,
        AnyPortalInVicinity, Player, EveryItemInVicinity, AnyItemInVicinity,
    }

    /// <summary>Condition opcodes (engine <c>ScriptConditionType</c>). Only <see cref="True"/> is evaluated by
    /// the current VM; the rest are listed so the unimplemented set is explicit (see Docs/Scripting.md).</summary>
    public enum Sct
    {
        True, Daytime, HasGold, LocalFlag, Eq, Le, PcQuestState, GlobalQuestState,
        ObjHasBless, ObjHasCurse, ObjMetPcBefore, ObjHasBadAssociates, ObjIsPolymorphed,
        ObjIsShrunk, ObjHasBodySpell, ObjIsInvisible, ObjHasMirrorImage, ObjHasItemNamed,
        ObjFollowingPc, ObjIsMonsterOfType, ObjIsNamed, ObjIsWieldingItem, ObjIsDead,
        ObjHasMaxFollowers, ObjCanOpenContainer, ObjHasSurrendered, ObjIsInDialog,
        ObjIsSwitchedOff, ObjCanSeeObj, ObjCanHearObj, ObjIsInvulnerable, ObjIsInCombat,
        ObjIsAtLocation, ObjHasReputation, ObjWithinRange, ObjIsInfluencedBySpell, ObjIsOpen,
        ObjIsAnimal, ObjIsUndead, ObjJilted, RumorKnown, RumorQuelled, ObjIsBusted, GlobalFlag,
        CanOpenPortal, SectorIsBlocked, MonstergenDisabled, Identified, KnowsSpell,
        MasteredSpellCollege, ItemsAreBeingRewielded, Prowling, WaitingForLeader,
    }

    /// <summary>Action opcodes (engine <c>ScriptActionType</c>). The VM implements control flow
    /// (<see cref="DoNothing"/>, the two returns, <see cref="Goto"/>) plus <see cref="Teleport"/> /
    /// <see cref="FadeAndTeleport"/>; every other action is a logged no-op. See Docs/Scripting.md.</summary>
    public enum Sat
    {
        DoNothing, ReturnAndSkipDefault, ReturnAndRunDefault, Goto, Dialog, RemoveThisScript,
        ChangeThisScriptToScript, CallScript, SetLocalFlag, ClearLocalFlag, AssignNum, Add,
        Subtract, Multiply, Divide, AssignObj, SetPcQuestState, SetQuestGlobalState, LoopFor,
        LoopEnd, LoopBreak, CritterFollow, CritterDisband, FloatLine, PrintLine, AddBlessing,
        RemoveBlessing, AddCurse, RemoveCurse, GetReaction, SetReaction, AdjustReaction, GetArmor,
        GetStat, GetObjectType, AdjustGold, Attack, Random, GetSocialClass, GetOrigin,
        TransformAttacheeIntoBasicPrototype, TransferItem, GetStoryState, SetStoryState, Teleport,
        SetDayStandpoint, SetNightStandpoint, GetSkill, CastSpell, MarkMapLocation, SetRumor,
        QuellRumor, CreateObject, SetLockState, CallScriptIn, CallScriptAt, ToggleState,
        ToggleInvulnerability, Kill, ChangeArtNum, Damage, CastSpellOn, ActionPerformAnimation,
        GiveQuestXp, WrittenUiStartBook, WrittenUiStartImage, CreateItem, ActionWaitForLeader,
        Destroy, ActionWalkTo, GetWeaponType, DistanceBetween, AddReputation, RemoveReputation,
        ActionRunTo, HealHp, HealFatigue, AddEffect, RemoveEffect, ActionUseItem,
        GetMagictechAdjustment, CallScriptEx, PlaySound, PlaySoundOn, GetArea, QueueNewspaper,
        FloatNewspaperHeadline, PlaySoundScheme, ToggleOpenClosed, GetFaction, GetScrollDistance,
        GetMagictechAdjustmentEx, Rename, ActionBecomeProne, SetWrittenStart, GetLocation,
        GetDaySinceStartup, GetCurrentHour, GetCurrentMinute, ChangeScript, SetGlobalFlag,
        ClearGlobalFlag, FadeAndTeleport, Fade, PlaySpellEyeCandy, GetHoursSinceStartup,
        ToggleSectorBlocked, GetHitPoints, GetFatiguePoints, ActionStopAttacking,
        ToggleMonsterGenerator, GetArmorCoverage, GiveSpellMasteryInCollege, UnfogTownmap,
        StartWrittenUi, ActionTryToSteal100Coins, StopSpellEyeCandy, GrantOneFatePoint,
        CastFreeSpell, SetPcQuestUnbotched, PlayScriptEyeCandy, ActionCastUnresistableSpell,
        ActionCastFreeUnresistableSpell, TouchArt, StopScriptEyeCandy, RemoveScriptCall,
        DestroyItemNamed, ToggleItemInventoryDisplay, HealPoison, StartSchematicUi, StopSpell,
        QueueSlide, EndGameAndPlaySlides, SetRotation, SetFaction, DrainCharges,
        CastUnresistableSpell, AdjustStat, ApplyUnresistableDamage, SetAutolevelScheme,
        SetDayStandpointEx, SetNightStandpointEx,
    }

    /// <summary>One script action: an opcode and 8 typed operands (engine <c>ScriptAction</c>, 0x2C bytes).</summary>
    public sealed class ScriptAction
    {
        public int Type;
        public readonly byte[] OpType = new byte[8]; // each is a Svt
        public readonly int[] OpValue = new int[8];
    }

    /// <summary>One script entry: a condition (opcode + 8 operands) and the action to run when it's true
    /// (<see cref="Action"/>) or false (<see cref="Els"/>). Engine <c>ScriptCondition</c>, 0x84 bytes.</summary>
    public sealed class ScriptCondition
    {
        public int Type;
        public readonly byte[] OpType = new byte[8];
        public readonly int[] OpValue = new int[8];
        public ScriptAction Action;
        public ScriptAction Els;
    }

    /// <summary>A parsed <c>.scr</c> file — a flat list of condition entries executed from a start line
    /// (engine <c>ScriptFile</c>). Execution control flow lives in <c>Arcanum.Script.ScriptVm</c>.</summary>
    public sealed class ScriptFile
    {
        /// <summary><c>ScriptHeader.flags</c> — the <c>.scr</c> template's default script flags (the live per-object
        /// flags are persisted in <c>OBJ_F_SCRIPTS</c>; this is the seed).</summary>
        public uint HeaderFlags;

        /// <summary><c>ScriptHeader.counters</c> — 4 packed per-object counters (engine notes it should be a
        /// <c>uint8_t[4]</c>); template defaults here, seeding the VM's local counters.</summary>
        public readonly byte[] Counters = new byte[4];

        public string Description;
        public uint Flags;
        public System.Collections.Generic.List<ScriptCondition> Entries = new System.Collections.Generic.List<ScriptCondition>();
    }
}
