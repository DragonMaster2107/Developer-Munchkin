using System.Text.Json.Serialization;

namespace DeveloperDungeon.Engine.Cards;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(CombatBonus), nameof(CombatBonus))]
[JsonDerivedType(typeof(ConditionalCombatBonus), nameof(ConditionalCombatBonus))]
[JsonDerivedType(typeof(LoseLevels), nameof(LoseLevels))]
[JsonDerivedType(typeof(GainLevels), nameof(GainLevels))]
[JsonDerivedType(typeof(LoseSlot), nameof(LoseSlot))]
[JsonDerivedType(typeof(LoseSmallItem), nameof(LoseSmallItem))]
[JsonDerivedType(typeof(DiscardHand), nameof(DiscardHand))]
[JsonDerivedType(typeof(GiveEquippedItemToNeighbor), nameof(GiveEquippedItemToNeighbor))]
[JsonDerivedType(typeof(DrawCards), nameof(DrawCards))]
[JsonDerivedType(typeof(EscapeBonus), nameof(EscapeBonus))]
[JsonDerivedType(typeof(AutoEscape), nameof(AutoEscape))]
[JsonDerivedType(typeof(TiesWin), nameof(TiesWin))]
[JsonDerivedType(typeof(HelpingGrantsLevel), nameof(HelpingGrantsLevel))]
[JsonDerivedType(typeof(MaxDepartments), nameof(MaxDepartments))]
[JsonDerivedType(typeof(MaxLanguages), nameof(MaxLanguages))]
[JsonDerivedType(typeof(NextTurnPenalty), nameof(NextTurnPenalty))]
[JsonDerivedType(typeof(MonsterModifier), nameof(MonsterModifier))]
[JsonDerivedType(typeof(CurseImmunity), nameof(CurseImmunity))]
[JsonDerivedType(typeof(AddMonsterToCombat), nameof(AddMonsterToCombat))]
[JsonDerivedType(typeof(Manual), nameof(Manual))]
public abstract record Effect;

public sealed record CombatBonus(int Amount) : Effect;

public sealed record ConditionalCombatBonus(int Amount, string? MonsterTag = null, string? PlayerDepartment = null, string? PlayerLanguage = null) : Effect;

public sealed record LoseLevels(int Amount) : Effect;

public sealed record GainLevels(int Amount) : Effect;

public sealed record LoseSlot(Slot Slot) : Effect;

public sealed record LoseSmallItem() : Effect;

/// <summary>Count null = alle Handkarten.</summary>
public sealed record DiscardHand(int? Count = null) : Effect;

public sealed record GiveEquippedItemToNeighbor(Direction Direction) : Effect;

public sealed record DrawCards(Deck Deck, int Count) : Effect;

public sealed record EscapeBonus(int Amount) : Effect;

public sealed record AutoEscape() : Effect;

public sealed record TiesWin() : Effect;

public sealed record HelpingGrantsLevel() : Effect;

public sealed record MaxDepartments(int Max) : Effect;

public sealed record MaxLanguages(int Max) : Effect;

public sealed record NextTurnPenalty(int Amount) : Effect;

public sealed record MonsterModifier(int Levels, int ExtraLoot) : Effect;

public sealed record CurseImmunity(string CurseTag) : Effect;

public sealed record AddMonsterToCombat() : Effect;

public sealed record Manual() : Effect;
