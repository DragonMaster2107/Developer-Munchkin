namespace DeveloperDungeon.Engine.State;

public sealed class PlayerState
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Color { get; set; } = "";
    public int Level { get; set; } = 1;
    public List<CardInstance> Hand { get; set; } = [];
    public List<CardInstance> Equipped { get; set; } = [];
    public List<CardInstance> Backpack { get; set; } = [];
    public List<CardInstance> Departments { get; set; } = [];
    public List<CardInstance> Languages { get; set; } = [];
}
