using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;

namespace FlyToPlugin;

public class FlyToPlugin : BasePlugin
{
    public override string ModuleName => "FlyToPlugin";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "Kianya";
    public override string ModuleDescription => "Command teleport to teammate within in specific time";

    public override void Load(bool hotReload)
    {

        Logger.LogInformation("FlyToPlugin loaded");

    }

    [ConsoleCommand("css_flyto", "Teleport to a teammate after a new round starts")]
    [CommandHelper(minArgs: 1, usage: "Expect name of player", whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void FlyToCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        // Check if the player is valid
        if (player == null || !player.IsValid || !player.PlayerPawn.IsValid ||
            player.Connected != PlayerConnectedState.PlayerConnected)
        {
            return;
        }

        // Check if the player is on the human side
        if (player.Team != CsTeam.CounterTerrorist)
        {
            player.PrintToChat($" {ChatColors.Red}[FlyTo] {ChatColors.Default}Only Human side can use this command.");
            return;
        }

        // Check time available for human can use this command
        if (CanUseFlyTo() == false)
        {
            player.PrintToChat($" {ChatColors.Red}[FlyTo] {ChatColors.Default}Can not use 'FlyTo' after zombie spawned");
            return;
        }

        // Check if the player has a pawn
        var playerPawn = player.PlayerPawn.Value;
        var teamMateName = commandInfo.GetArg(1);
        bool playerFound = false;

        foreach (var eachPlayer in Utilities.GetPlayers())
        {
            if (eachPlayer.IsValid && eachPlayer.PlayerPawn.IsValid &&
                eachPlayer.PlayerName == teamMateName && eachPlayer.Team == CsTeam.CounterTerrorist)
            {
                var targetPlayerPawn = eachPlayer.PlayerPawn.Value;
                var targetPlayerPosition = targetPlayerPawn?.AbsOrigin ?? new Vector(0);
                var targetPlayerAngles = targetPlayerPawn?.AbsRotation ?? new QAngle(0);

                var newPosition = new Vector(targetPlayerPosition.X, targetPlayerPosition.Y, targetPlayerPosition.Z + 20);
                var newAngles = new QAngle(targetPlayerAngles.X, targetPlayerAngles.Y, targetPlayerAngles.Z);

                playerPawn?.Teleport(newPosition, newAngles);
                player.PrintToChat($" {ChatColors.Green}[FlyTo] {ChatColors.Default}Teleported you to {ChatColors.Yellow}{teamMateName}");
                playerFound = true;
                break;
            }
        }

        if (!playerFound)
        {
            player.PrintToChat($" {ChatColors.Red}[FlyTo] {ChatColors.Default}Can not find '{ChatColors.Yellow}{teamMateName}{ChatColors.Default}'");
        }
    }

    private static bool CanUseFlyTo()
    {
        var mapName = Server.MapName;
        var countDownZombieSpawn = 15;

        if (mapName == "ze_immortal_flame")
        {
            countDownZombieSpawn = 30;
        }

        // Get game rules safely
        var gameRulesProxy = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault();
        if (gameRulesProxy?.GameRules == null)
        {
            Server.PrintToConsole("[FlyTo] Error to get 'gameRulesProxy'");
            return false;
        }

        var gameRules = gameRulesProxy.GameRules;

        // Get time since round start
        var timeSinceRoundStart = (int)(gameRules.LastThinkTime - gameRules.RoundStartTime);

        if (timeSinceRoundStart < countDownZombieSpawn)
        {
            return true;
        }

        if (timeSinceRoundStart > countDownZombieSpawn)
        {
            return false;
        }

        return false;
    }

    // TODO : 1 - Maybe Create the config file for each map zombie spawn timmer
    // TODO : 2 - Add feature detact string expression for player name like Orin, Orian, if match more than 2 people
    // TODO : 3 - Add feature to teleport random @ct player using !flyto @randomct
    // TODO : 4 - Add feature to teleport by using middle mouse button

}

