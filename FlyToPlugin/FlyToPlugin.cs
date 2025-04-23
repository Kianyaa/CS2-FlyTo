using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;
using QAngle = CounterStrikeSharp.API.Modules.Utils.QAngle;
using System.Runtime.InteropServices;

namespace FlyToPlugin;

public class FlyToPlugin : BasePlugin
{
    public override string ModuleName => "FlyToPlugin";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "Kianya";
    public override string ModuleDescription => "Command teleport to teammate within in specific time";

    private const int _defaultZombieSpawnTime = 15; // default map zombie spawn after 15 seconds
    private const int _makoZombieSpawnTime = 30; // mako map zombie spawn after 30 seconds
    private const int _warmUpTime = 120; // warmup round is 2 minutes
    private bool _isWarmUp = false;
    private string _mapName = string.Empty;

    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventPlayerPing>(OnPlayerPing);
        RegisterEventHandler<EventRoundAnnounceWarmup>(OnEventWarmupRound);
        RegisterEventHandler<EventWarmupEnd>(OnEventWarmUpEnd);
    }

    public override void Unload(bool hotReload)
    {
        DeregisterEventHandler<EventPlayerPing>(OnPlayerPing);
        DeregisterEventHandler<EventRoundAnnounceWarmup>(OnEventWarmupRound);
        DeregisterEventHandler<EventWarmupEnd>(OnEventWarmUpEnd);
    }


    [ConsoleCommand("css_flyto", "Teleport to a teammate after a new round starts")]
    [CommandHelper(minArgs: 1, usage: "Expect player name in alive human-side", whoCanExecute: CommandUsage.CLIENT_ONLY)]
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
            player.PrintToChat($" {ChatColors.Red}[FlyTo] {ChatColors.Default}Only Human side can use this command");
            return;
        }

        // Check if the player is a alive human
        if (player.PawnIsAlive == false)
        {
            player.PrintToChat($" {ChatColors.Red}[FlyTo] {ChatColors.Default}You are not alive to use this command");
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
                player.PrintToChat($" {ChatColors.Red}[FlyTo] {ChatColors.Default}No Human players available to Teleport to");
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
                player.PrintToChat($" {ChatColors.Red}[FlyTo] {ChatColors.Default}No Human players available to Teleport to");
                return;
            }
        }

        // Find a valid teammate in the same team
        int matchCount = 0;
        CCSPlayerController? matchedPlayer = null;

        // Loop through all players and check if the name contains the input substring (case-insensitive)
        foreach (var p in Utilities.GetPlayers()) // 64*64 loops if server is full 
        {

            if (p is { IsValid: true, PlayerPawn.IsValid: true, Team: CsTeam.CounterTerrorist, PawnIsAlive: true })
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

    private bool CanUseFlyTo()
    {

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

        if (_mapName == "ze_ffvii_mako_reactor_v5_3")
        {
            // Check Warmup Round
            if (_isWarmUp == true)
            {
                if (timeSinceRoundStart < _warmUpTime)
                {
                    return true;
                }

                if (timeSinceRoundStart > _warmUpTime)
                {
                    return false;
                }

            }

            if (timeSinceRoundStart < _makoZombieSpawnTime)
            {
                return true;
            }

            if (timeSinceRoundStart > _makoZombieSpawnTime)
            {
                return false;
            }
        }

        else
        {
            // Check Warmup Round
            if (_isWarmUp == true)
            {
                if (timeSinceRoundStart < _warmUpTime)
                {
                    return true;
                }

                if (timeSinceRoundStart > _warmUpTime)
                {
                    return false;
                }

            }

            if (timeSinceRoundStart < _defaultZombieSpawnTime)
            {
                return true;
            }

            if (timeSinceRoundStart > _defaultZombieSpawnTime)
            {
                return false;
            }
        }

        return false;

    }

    public HookResult OnPlayerPing(EventPlayerPing? @eventPlayerPing, GameEventInfo info)
    {

        // Get the player who triggered the ping

        if (@eventPlayerPing?.Userid == null || !@eventPlayerPing.Userid.IsValid || @eventPlayerPing.Userid.Team != CsTeam.CounterTerrorist)
        {
            return HookResult.Continue;
        }

        var playerping = @eventPlayerPing.Userid;

        if (CanUseFlyTo() == false)
        {
            return HookResult.Continue;
        }

        if (playerping != null)
        {
            var entity = GetClientAimTarget(playerping);

            if (entity != null && entity.DesignerName.ToLower().Contains("player"))
            {
                var newPosition = entity.AbsOrigin;
                var newAngles = entity.AbsRotation;

                playerping.PlayerPawn.Value?.Teleport(newPosition, newAngles);
                playerping.PrintToChat($" {ChatColors.Green}[FlyTo] {ChatColors.Default}Teleported you to your aimed teammate");
            }
        }


        return HookResult.Continue;
    }

    public HookResult OnEventWarmupRound(EventRoundAnnounceWarmup @event, GameEventInfo info)
    {
        _isWarmUp = true; // if warmup round 
        _mapName = Server.MapName;

        return HookResult.Continue;
    }

    public HookResult OnEventWarmUpEnd(EventWarmupEnd @event, GameEventInfo info)
    {
        _isWarmUp = false; 

        return HookResult.Continue;
    }



    public static CBaseEntity? GetClientAimTarget(CCSPlayerController player)
    {
        var GameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;

        if (GameRules is null)
            return null;

        VirtualFunctionWithReturn<IntPtr, IntPtr, IntPtr> findPickerEntity = new(GameRules.Handle, 27);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) findPickerEntity = new(GameRules.Handle, 28);

        var target = new CBaseEntity(findPickerEntity.Invoke(GameRules.Handle, player.Handle));
        if (target != null && target.IsValid) return target;

        return null;
    }

    // TODO : 1 - Maybe Create the config file for each map zombie spawn timmer
    // TODO : 2 - Fix if there are Kianya and Karin if I type Ka only its gonna show 'More than one player matches' instead go for Karin line 118

}

