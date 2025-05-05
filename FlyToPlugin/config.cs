using CounterStrikeSharp.API.Core;


public class Config : BasePluginConfig
{
    public int _defaultZombieSpawnTime { get; set; } = 15; // Default zombie spawn time in seconds
    public int _makoZombieSpawnTime { get; set; } = 30; // Default zombie spawn time in seconds (Map Mako)
    public int _warmUpTime { get; set; } = 120; // Warmup time in seconds

}