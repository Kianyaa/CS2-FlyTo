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
    [CommandHelper(minArgs: 1, usage: "Expect name of alive human player", whoCanExecute: CommandUsage.CLIENT_ONLY)]
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

        // Check if the player is a alive human
        if (player.PawnIsAlive == false)
        {
            player.PrintToChat($" {ChatColors.Red}[FlyTo] {ChatColors.Default}You are not alive.");
            return;
        }

        // Check time available for human can use this command
        if (CanUseFlyTo() == false)
        {
            player.PrintToChat($" {ChatColors.Red}[FlyTo] {ChatColors.Default}Can not use 'FlyTo' after zombie spawned");
            return;
        }


        // Get teammate name from command argument
        var teamMateName = commandInfo.GetArg(1);
        var isRandom = false;


        // Check is teleport to yourself matched all character
        if (player.PlayerName.ToLower() == teamMateName.ToLower())
        {
            player.PrintToChat($" {ChatColors.Red}[FlyTo] {ChatColors.Default}You can not teleport to yourself");
            return;
        }

        // ------- If User Enter @randomct, then get random ct player-------
        if (teamMateName.ToLower() == "@randomct")
        {

            var ctPlayers = Utilities.GetPlayers().Where(p => p.Team == CsTeam.CounterTerrorist && p.PawnIsAlive).ToList();

            // you already alone in the server
            if (ctPlayers.Count == 1)
            {
                player.PrintToChat($" {ChatColors.Red}[FlyTo] {ChatColors.Default}No Human players available to Teleport to.");
                return;
            }

            if (ctPlayers.Count > 0 && ctPlayers.Count != 1)
            {
                var random = new Random();
                var randomPlayer = ctPlayers[random.Next(ctPlayers.Count)];
                teamMateName = randomPlayer.PlayerName;
                isRandom = true;

                // Careful to Kaboom the server from here
                while (teamMateName.ToLower() == player.PlayerName.ToLower())
                {
                    randomPlayer = ctPlayers[random.Next(ctPlayers.Count)];
                    teamMateName = randomPlayer.PlayerName;
                }

            }

            else
            {
                player.PrintToChat($" {ChatColors.Red}[FlyTo] {ChatColors.Default}No Human players available to Teleport to.");
                return;
            }
        }

        // Find a valid teammate in the same team
        int matchCount = 0;
        CCSPlayerController? matchedPlayer = null;

        // Loop through all players and check if the name contains the input substring (case-insensitive)
        foreach (var p in Utilities.GetPlayers())
        {

            if (p is { IsValid: true, PlayerPawn.IsValid: true, Team: CsTeam.CounterTerrorist, PawnIsAlive: true})
            {
                var playerName = p.PlayerName;

                // Check if the player's name contains the input string (case-insensitive)
                if (playerName.Contains(teamMateName, StringComparison.OrdinalIgnoreCase)) // 
                {
                    matchCount++;

                    // If more than 1 match is found and randomct is false, stop and notify the user
                    if (matchCount > 1 && isRandom == false)
                    {
                        player.PrintToChat($" {ChatColors.Red}[FlyTo] {ChatColors.Default}More than one player matches '{ChatColors.Yellow}{teamMateName}{ChatColors.Default}'. Please specify further.");
                        return;
                    }

                    matchedPlayer = p;

                }
            }

        }


        if (matchCount == 1 && matchedPlayer != null && isRandom == false && matchedPlayer.PlayerName != player.PlayerName)
        {
            var targetPlayerPawn = matchedPlayer.PlayerPawn.Value;
            var newPosition = new Vector(targetPlayerPawn?.AbsOrigin?.X, targetPlayerPawn?.AbsOrigin?.Y, targetPlayerPawn?.AbsOrigin?.Z + 20);
            var newAngles = new QAngle(targetPlayerPawn?.AbsRotation?.X, targetPlayerPawn?.AbsRotation?.Y, targetPlayerPawn?.AbsRotation?.Z);

            player.PlayerPawn.Value?.Teleport(newPosition, newAngles);
            player.PrintToChat($" {ChatColors.Green}[FlyTo] {ChatColors.Default}Teleported you to {ChatColors.Yellow}{matchedPlayer.PlayerName}");
        }

        else if (matchedPlayer?.PlayerName == player.PlayerName)
        {
            player.PrintToChat($" {ChatColors.Red}[FlyTo] {ChatColors.Default}You can not teleport to yourself");
        }

        else if (matchCount == 0)
        {
            player.PrintToChat($" {ChatColors.Red}[FlyTo] {ChatColors.Default}Cannot find '{ChatColors.Yellow}{commandInfo.GetArg(1)}{ChatColors.Default}'");
        }

        else if ((isRandom == true) && (teamMateName != player.PlayerName))
        {
            var targetPlayerPawn = matchedPlayer.PlayerPawn.Value;
            var newPosition = new Vector(targetPlayerPawn?.AbsOrigin?.X, targetPlayerPawn?.AbsOrigin?.Y, targetPlayerPawn?.AbsOrigin?.Z + 20);
            var newAngles = new QAngle(targetPlayerPawn?.AbsRotation?.X, targetPlayerPawn?.AbsRotation?.Y, targetPlayerPawn?.AbsRotation?.Z);

            player.PlayerPawn.Value?.Teleport(newPosition, newAngles);
            player.PrintToChat($" {ChatColors.Green}[FlyTo] {ChatColors.Default}Teleported you to {ChatColors.Yellow}{matchedPlayer.PlayerName}");
        }

        else
        {
            player.PrintToChat($" {ChatColors.Red}[FlyTo] {ChatColors.Default}You can not teleport to yourself");
        }


    }

    private static bool CanUseFlyTo()
    {
        var mapName = Server.MapName;
        var countDownZombieSpawn = 15;

        if (mapName == "ze_immortal_flame")
        {
            countDownZombieSpawn = 500; // Time to wait for zombie spawn
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
    // TODO : 2 - Fix if there are Kianya and Karin if I type Ka only its gonna show 'More than one player matches' line 118
    // TODO : 3 - Teleport to zombie massage
    // TODO : 4 - Add feature to teleport by using middle mouse button
}

