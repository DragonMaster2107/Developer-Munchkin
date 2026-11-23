using System.Text.Json;
using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Engine.Serialization;
using DeveloperDungeon.Engine.Tests.Support;

namespace DeveloperDungeon.Engine.Tests.Cards;

public class CardCatalogTests
{
    private static string Mon(string id, string extra = "", string deck = "ticket") =>
        $$"""{ "id": "{{id}}", "name": "n", "deck": "{{deck}}", "kind": "monster", "flavor": "x", "rulesText": "x", "level": 2, "rewardLevels": 1, "rewardLoot": 1 {{extra}} }""";

    private static string Equip(string id, string slot, int hands) =>
        $$"""{ "id": "{{id}}", "name": "n", "deck": "loot", "kind": "equipment", "flavor": "x", "rulesText": "x", "slot": "{{slot}}", "hands": {{hands}}, "bonus": 1, "value": 100 }""";

    private static IReadOnlyList<string> ValidateJson(string cardJson) =>
        CardCatalog.FromJson("[" + cardJson + "]").Validate();

    [Fact]
    public void FixtureCatalog_LoadsAndValidatesClean()
    {
        Assert.Empty(TestData.Catalog.Validate());
        var card = TestData.Catalog.Get("monster-l3-legacy");
        Assert.Equal(3, card.Level);
        var bad = Assert.IsType<LoseSlot>(card.BadStuff[0]);
        Assert.Equal(Slot.Head, bad.Slot);
    }

    [Fact]
    public void EffectWithTypeNotFirst_Deserializes()
    {
        var json = Mon("m", """, "effects": [ { "amount": 2, "type": "CombatBonus" } ]""");
        var catalog = CardCatalog.FromJson("[" + json + "]");
        var effect = Assert.IsType<CombatBonus>(catalog.Get("m").Effects[0]);
        Assert.Equal(2, effect.Amount);
    }

    [Fact]
    public void Get_UnknownId_Throws() =>
        Assert.Throws<KeyNotFoundException>(() => TestData.Catalog.Get("nope"));

    [Fact]
    public void Validate_ReportsDuplicateIds()
    {
        var errors = ValidateJson(Mon("dup") + "," + Mon("dup"));
        Assert.Contains(errors, e => e.Contains("dup"));
    }

    [Fact]
    public void Validate_ReportsMonsterWithoutLevel()
    {
        var json = """{ "id": "nolevel", "name": "n", "deck": "ticket", "kind": "monster", "flavor": "x", "rulesText": "x", "rewardLevels": 1, "rewardLoot": 1 }""";
        Assert.Contains(ValidateJson(json), e => e.Contains("nolevel"));
    }

    [Theory]
    [InlineData("hands", 0)]
    [InlineData("hands", 3)]
    [InlineData("head", 1)]
    public void Validate_ReportsHandsMismatch(string slot, int hands) =>
        Assert.Contains(ValidateJson(Equip("hm", slot, hands)), e => e.Contains("hm"));

    [Theory]
    [InlineData("hands", 1)]
    [InlineData("hands", 2)]
    [InlineData("none", 0)]
    [InlineData("head", 0)]
    public void Validate_AcceptsValidHands(string slot, int hands) =>
        Assert.Empty(ValidateJson(Equip("ok", slot, hands)));

    [Fact]
    public void Validate_ReportsCountBelowOne() =>
        Assert.Contains(ValidateJson(Mon("cnt", ", \"count\": 0")), e => e.Contains("cnt"));

    [Fact]
    public void Validate_ReportsDepartmentWithoutKey()
    {
        var json = """{ "id": "dep", "name": "n", "deck": "ticket", "kind": "department", "flavor": "x", "rulesText": "x" }""";
        Assert.Contains(ValidateJson(json), e => e.Contains("dep"));
    }

    [Fact]
    public void Validate_ReportsWrongDeckForKind() =>
        Assert.Contains(ValidateJson(Mon("wd", deck: "loot")), e => e.Contains("wd"));

    [Fact]
    public void UnknownEffectType_ThrowsJsonException()
    {
        var json = Mon("bad", ", \"badStuff\": [{ \"type\": \"Nonsense\" }]");
        Assert.Throws<JsonException>(() => CardCatalog.FromJson("[" + json + "]"));
    }

    [Fact]
    public void FromJson_MergesMultipleArrays()
    {
        var catalog = CardCatalog.FromJson("[" + Mon("a") + "]", "[" + Mon("b") + "]");
        Assert.Equal(2, catalog.All.Count);
        Assert.Equal("b", catalog.Get("b").Id);
    }

    [Fact]
    public void FromJson_AbsentFields_KeepDefaults()
    {
        var card = CardCatalog.FromJson("[" + Mon("d") + "]").Get("d");
        Assert.Equal(1, card.Count);
        Assert.NotNull(card.Effects);
        Assert.NotNull(card.BadStuff);
        Assert.NotNull(card.Tags);
    }

    [Fact]
    public void Serialize_WritesEnumsAsCamelCase_AndRoundTrips()
    {
        var cards = TestData.Catalog.All.ToList();
        var json = JsonSerializer.Serialize(cards, EngineJsonContext.Default.ListCardDefinition);

        Assert.Contains("\"deck\":\"ticket\"", json);
        Assert.Contains("\"kind\":\"monsterEnhancer\"", json);
        Assert.Contains("\"slot\":\"head\"", json);
        Assert.Contains("\"direction\":\"left\"", json);
        Assert.DoesNotContain("\"Head\"", json);
        Assert.DoesNotContain("\"Ticket\"", json);

        var again = JsonSerializer.Serialize(
            JsonSerializer.Deserialize(json, EngineJsonContext.Default.ListCardDefinition)!,
            EngineJsonContext.Default.ListCardDefinition);
        Assert.Equal(json, again);
    }

    private static string Card(string id, string kind, string deck, string effects = "", string extra = "", string name = "n", string rulesText = "x") =>
        $$"""{ "id": "{{id}}", "name": "{{name}}", "deck": "{{deck}}", "kind": "{{kind}}", "flavor": "x", "rulesText": "{{rulesText}}", "effects": [{{effects}}] {{extra}} }""";

    private const string QaDepartment = """{ "id": "dept-qa", "name": "n", "deck": "ticket", "kind": "department", "flavor": "x", "rulesText": "x", "key": "qa" }""";
    private const string PythonLanguage = """{ "id": "lang-python", "name": "n", "deck": "ticket", "kind": "language", "flavor": "x", "rulesText": "x", "key": "python" }""";

    [Theory]
    [InlineData("", "n", "x")]
    [InlineData("noname", "", "x")]
    [InlineData("norules", "n", " ")]
    public void Validate_ReportsEmptyIdNameOrRulesText(string id, string name, string rulesText)
    {
        var errors = ValidateJson(Card(id, "oneShot", "loot", """{ "type": "CombatBonus", "amount": 1 }""", name: name, rulesText: rulesText));
        Assert.NotEmpty(errors);
    }

    [Theory]
    [InlineData("special", "ticket")]
    [InlineData("oneShot", "loot")]
    [InlineData("monsterEnhancer", "ticket")]
    [InlineData("levelUp", "loot")]
    public void Validate_ReportsPlayableKindWithoutUsableEffect(string kind, string deck) =>
        Assert.Contains(ValidateJson(Card("empty", kind, deck, """{ "type": "NextTurnPenalty", "amount": 1 }""")), e => e.Contains("empty"));

    [Theory]
    [InlineData("special", "ticket", """{ "type": "MaxDepartments", "max": 2 }""")]
    [InlineData("special", "ticket", """{ "type": "MaxLanguages", "max": 2 }""")]
    [InlineData("special", "ticket", """{ "type": "AddMonsterToCombat" }""")]
    [InlineData("special", "ticket", """{ "type": "Manual" }""")]
    [InlineData("oneShot", "loot", """{ "type": "CombatBonus", "amount": 1 }""")]
    [InlineData("oneShot", "loot", """{ "type": "AutoEscape" }""")]
    [InlineData("oneShot", "loot", """{ "type": "Manual" }""")]
    [InlineData("monsterEnhancer", "ticket", """{ "type": "MonsterModifier", "levels": 1, "extraLoot": 0 }""")]
    [InlineData("monsterEnhancer", "ticket", """{ "type": "Manual" }""")]
    [InlineData("levelUp", "loot", """{ "type": "GainLevels", "amount": 1 }""")]
    [InlineData("levelUp", "loot", """{ "type": "Manual" }""")]
    public void Validate_AcceptsPlayableKindWithUsableEffect(string kind, string deck, string effect) =>
        Assert.Empty(ValidateJson(Card("ok", kind, deck, effect)));

    [Fact]
    public void Validate_ReportsUnknownRequiredDepartment() =>
        Assert.Contains(ValidateJson(Equip("e", "head", 0).Replace("\"value\": 100", "\"value\": 100, \"requiresDepartment\": \"qa\"")), e => e.Contains("qa"));

    [Fact]
    public void Validate_ReportsUnknownRequiredLanguage() =>
        Assert.Contains(ValidateJson(Equip("e", "head", 0).Replace("\"value\": 100", "\"value\": 100, \"requiresLanguage\": \"python\"")), e => e.Contains("python"));

    [Fact]
    public void Validate_ReportsUnknownConditionalDepartment() =>
        Assert.Contains(ValidateJson(Mon("m", """, "effects": [ { "type": "ConditionalCombatBonus", "amount": 2, "playerDepartment": "qa" } ]""")), e => e.Contains("qa"));

    [Fact]
    public void Validate_ReportsUnknownConditionalLanguage() =>
        Assert.Contains(ValidateJson(Mon("m", """, "effects": [ { "type": "ConditionalCombatBonus", "amount": 2, "playerLanguage": "python" } ]""")), e => e.Contains("python"));

    [Fact]
    public void Validate_DepartmentKeyMustNotBeLanguageKey() =>
        Assert.Contains(ValidateJson(PythonLanguage.Replace("python", "qa") + "," + Equip("e", "head", 0).Replace("\"value\": 100", "\"value\": 100, \"requiresDepartment\": \"qa\"")), e => e.Contains("qa"));

    [Fact]
    public void Validate_AcceptsKnownKeys()
    {
        var equip = Equip("e", "head", 0).Replace("\"value\": 100", "\"value\": 100, \"requiresDepartment\": \"qa\", \"requiresLanguage\": \"python\"");
        var monster = Mon("m", """, "effects": [ { "type": "ConditionalCombatBonus", "amount": 2, "playerDepartment": "qa" }, { "type": "ConditionalCombatBonus", "amount": 1, "playerLanguage": "python" } ]""");
        Assert.Empty(ValidateJson(string.Join(",", QaDepartment, PythonLanguage, equip, monster)));
    }

    [Fact]
    public void Validate_ReportsNullEntry_WithoutCrashing()
    {
        var catalog = CardCatalog.FromJson("[" + Mon("a") + ", null]");
        Assert.NotEmpty(catalog.Validate());
        Assert.Equal("a", catalog.Get("a").Id);
    }

    [Fact]
    public void Validate_ReportsNullId_WithoutCrashing()
    {
        var json = """[{ "id": null, "name": "n", "deck": "ticket", "kind": "monster", "flavor": "x", "rulesText": "x", "level": 1 }]""";
        Assert.NotEmpty(CardCatalog.FromJson(json).Validate());
    }
}
