using System.Drawing;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Timers;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;

namespace SharpHook
{
    public class PlayerGrappleInfo
    {
        public bool IsPlayerGrappling { get; set; }
        public bool GrappleBeamSpawned { get; set; }
        public bool GrappleBeamActive { get; set; }
        public CBeam? GrappleWire { get; set; }
        public Vector newVelocity { get; set; }
        public Vector staticVelocity { get; set; }
    }


    [MinimumApiVersion(125)]
    public partial class SharpHook : BasePlugin
    {
        public override string ModuleName => "SharpHook";
        public override string ModuleVersion => "0.0.1";
        public override string ModuleAuthor => "Zox, (Logic by DEAFPS) BIG THANKS TO DESTOER!)";
        private Dictionary<int, PlayerGrappleInfo> playerGrapples = new Dictionary<int, PlayerGrappleInfo>();
        private Dictionary<int, CCSPlayerController> connectedPlayers = new Dictionary<int, CCSPlayerController>();
        public bool GrappleBeamEnabled { get; set; }
        public bool RoundEnd { get; set; }
        private bool[] use_key = new bool[64];
        public bool ConsoleMessage = true;
        string prefix = $" {ChatColors.Red}[SharpHook] ";
        private static void PrintToCenterAll(string message)
        {
            Utilities.GetPlayers().ForEach(controller =>
            {
                controller.PrintToCenter(message);
            });
        }

        public void InitPlayer(CCSPlayerController player)
        {
            if (player.IsBot || !player.IsValid)
            {
                return;
            }
            else
            {
                connectedPlayers[player.Slot] = player;
                Console.WriteLine($"Player: {player.PlayerName} UserID: {player.UserId} has been added to connected players!");

                // Initialize PlayerGrappleInfo for the player
                playerGrapples[player.Slot] = new PlayerGrappleInfo();
            }
        }

        [ConsoleCommand("css_hookstart")]
        [RequiresPermissions("@css/changemap")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        public void OnHookStartCommand(CCSPlayerController? caller, CommandInfo command)
        {
            if (caller.IsValid)
            {
                if (GrappleBeamEnabled)
                {
                    use_key[caller.Slot] = true;
                }
            }
            else
            {
                caller.PrintToChat($" {prefix}{ChatColors.Gold}Error!");
            }
        }
        [ConsoleCommand("css_hookstop")]
        [RequiresPermissions("@css/changemap")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        public void OnHookStopCommand(CCSPlayerController? caller, CommandInfo command)
        {
            if (caller.IsValid)
            {
                if (GrappleBeamEnabled)
                {

                    use_key[caller.Slot] = false;
                }
            }
            else
            {
                caller.PrintToChat($" {prefix}{ChatColors.Gold}Error!");
            }
        }
        [ConsoleCommand("css_hkdisable")]
        [RequiresPermissions("@css/changemap")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        public void OnHookDisableCommand(CCSPlayerController? caller, CommandInfo command)
        {
            if (!RoundEnd)
            {
                if (GrappleBeamEnabled)
                {
                    Utilities.GetPlayers().ForEach(DetachGrapple);
                    GrappleBeamEnabled = false;
                    Server.PrintToChatAll($" {prefix}{ChatColors.Purple}{caller.PlayerName}{ChatColors.Default} has {ChatColors.Red}disabled hook.");
                }
                else
                {
                    caller.PrintToChat("Hook is already\x02 disabled");
                }
            }
            else
            {
                Server.PrintToChatAll($" {prefix}{ChatColors.Gold}Round ended! You cannot disable or enable hook!");
            }
        }
        [ConsoleCommand("css_hkenable")]
        [RequiresPermissions("@css/changemap")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        public void OnHookEnableCommand(CCSPlayerController? caller, CommandInfo command)
        {
            if (!RoundEnd)
            {
                if (!GrappleBeamEnabled)
                {
                    GrappleBeamEnabled = true;
                    Server.PrintToChatAll($" {prefix}{ChatColors.Purple}{caller.PlayerName}{ChatColors.Default} has {ChatColors.Green}enabled hook!");
                }
                else
                {
                    caller.PrintToChat("Hook is already\x06 enabled!");
                }
            }
            else
            {
                Server.PrintToChatAll($" {prefix}{ChatColors.Gold}Round ended! You cannot disable or enable hook!");
            }
        }
        public override void Load(bool hotReload)
        {
            Console.WriteLine("[SharpHook] Loading...");

            if (hotReload) Utilities.GetPlayers().ForEach(InitPlayer);

            RegisterEventHandler<EventPlayerConnectFull>((@event, info) =>
            {
                InitPlayer(@event.Userid);
                return HookResult.Continue;
            });

            RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
            {
                var player = @event.Userid;

                if (player.IsBot || !player.IsValid)
                {
                    return HookResult.Continue;
                }
                else
                {
                    if (connectedPlayers.TryGetValue(player.Slot, out var connectedPlayer))
                    {
                        connectedPlayers.Remove(player.Slot);
                        Console.WriteLine($"Player: {connectedPlayer.PlayerName} - UserID: {connectedPlayer.UserId} is removed from the connected players.");
                    }

                    playerGrapples.Remove(player.Slot);

                    return HookResult.Continue;
                }
            });

            RegisterEventHandler<EventPlayerDeath>((@event, info) =>
            {
                DetachGrapple(@event.Userid);
                return HookResult.Continue;
            });

            RegisterEventHandler<EventRoundStart>((@event, info) =>
            {
                RoundEnd = false;
                AddTimer(15, () =>
                {
                    if (!RoundEnd)
                    {
                        Console.WriteLine("Round started - HOOK.");
                        GrappleBeamEnabled = true;
                        PrintToCenterAll("Hook is\x06 enabled.");
                    }
                }, CounterStrikeSharp.API.Modules.Timers.TimerFlags.STOP_ON_MAPCHANGE);
                return HookResult.Continue;
            });
            RegisterEventHandler<EventRoundEnd>((@event, info) =>
            {
                GrappleBeamEnabled = false;
                foreach (var playerEntry in connectedPlayers)
                {
                    var player = playerEntry.Value;
                    DetachGrapple(player);
                }
                Console.WriteLine("Round ended - HOOK.");
                RoundEnd = true;

                return HookResult.Continue;
            });

            RegisterListener<Listeners.OnTick>(() =>
            {
                foreach (var playerEntry in connectedPlayers)
                {
                    var player = playerEntry.Value;

                    if (player == null || !player.IsValid || player.IsBot || !player.PawnIsAlive)
                        return;

                    var playerSlot = player.Slot;
                    CCSPlayerController? playerusing = Utilities.GetPlayerFromSlot(player.Slot);
                    //bool use_key = (playerusing.Buttons & PlayerButtons.Use) == PlayerButtons.Use;
                    CCSPlayerPawn? pawn = player.PlayerPawn.Value;

                    if (pawn != null && pawn.AbsOrigin != null)
                    {
                        if (player != null && GrappleBeamEnabled)
                        {
                            bool key_old = playerGrapples[player.Slot].IsPlayerGrappling;

                            playerGrapples[player.Slot].IsPlayerGrappling = use_key[player.Slot];

                            if (!playerGrapples[player.Slot].IsPlayerGrappling)
                            {
                                DetachGrapple(player);
                            }

                            if (!key_old && use_key[player.Slot])
                            {
                                if (ConsoleMessage)
                                {
                                    Console.WriteLine($"{player.PlayerName} has used hook! USERID: {player.UserId} - SteamID: {player?.AuthorizedSteamID?.SteamId64.ToString()}");
                                    ConsoleMessage = false;
                                }

                                float grappleSpeed = 800.0f;

                                QAngle eye_angle = pawn.EyeAngles;

                                // convert angles to rad 
                                double pitch = (Math.PI / 180) * eye_angle.X;
                                double yaw = (Math.PI / 180) * eye_angle.Y;

                                // get direction vector from angles
                                Vector eye_vector = new Vector((float)(Math.Cos(yaw) * Math.Cos(pitch)), (float)(Math.Sin(yaw) * Math.Cos(pitch)), (float)(-Math.Sin(pitch)));
                                var newVelocity = new Vector(
                                    eye_vector.X * grappleSpeed,
                                    eye_vector.Y * grappleSpeed,
                                    eye_vector.Z * grappleSpeed
                                );

                                playerGrapples[player.Slot].staticVelocity = newVelocity;
                            }
                        }
                    }

                    if (playerGrapples.TryGetValue(player.Slot, out var grappleInfo) && grappleInfo.IsPlayerGrappling)
                    {
                        // Ideally we would use Get Client Eye posistion
                        // because this will break when we crouch etc
                        Vector eye = new Vector(pawn.AbsOrigin.X, pawn.AbsOrigin.Y, pawn.AbsOrigin.Z + 61.0f);

                        Vector end = new Vector(eye.X, eye.Y, eye.Z);

                        QAngle eye_angle = pawn.EyeAngles;

                        // convert angles to rad 
                        double pitch = (Math.PI / 180) * eye_angle.X;
                        double yaw = (Math.PI / 180) * eye_angle.Y;

                        // get direction vector from angles
                        Vector eye_vector = new Vector((float)(Math.Cos(yaw) * Math.Cos(pitch)), (float)(Math.Sin(yaw) * Math.Cos(pitch)), (float)(-Math.Sin(pitch)));
                        //Length of beam
                        int t = 3000;
                        end.X += (t * eye_vector.X);
                        end.Y += (t * eye_vector.Y);
                        end.Z += (t * eye_vector.Z);

                        if (player == null || player.PlayerPawn == null || player.PlayerPawn?.Value?.CBodyComponent == null || !player.IsValid || !player.PawnIsAlive)
                            continue;

                        Vector? playerPosition = player.PlayerPawn?.Value.CBodyComponent?.SceneNode?.AbsOrigin;

                        if (playerPosition == null)
                            continue;

                        Vector? grappleTarget = end;
                        if (grappleTarget == null)
                        {
                            Console.WriteLine($"{player.PlayerName} grappleTarget is invalid!");
                            continue;
                        }

                        if (playerGrapples[player.Slot].GrappleWire == null)
                        {
                            playerGrapples[player.Slot].GrappleWire = Utilities.CreateEntityByName<CBeam>("beam");

                            if (playerGrapples[player.Slot].GrappleWire == null)
                            {
                                Console.WriteLine($"Beam could not be created!");
                                return;
                            }

                            var grappleWire = playerGrapples[player.Slot]?.GrappleWire;
                            if (grappleWire != null)
                            {
                                string color = "";
                                //Different colors for CT and T team.
                                if (player.TeamNum == 3)
                                {
                                    color = "Red";
                                }
                                else
                                {
                                    color = "Blue";
                                }
                                // Assuming grappleWire.Render expects a Color object
                                grappleWire.Render = color == "Red" ? Color.Red : Color.Blue;
                                grappleWire.Width = 4f;
                                grappleWire.EndPos.X = grappleTarget.X;
                                grappleWire.EndPos.Y = grappleTarget.Y;
                                grappleWire.EndPos.Z = grappleTarget.Z;
                                grappleWire.DispatchSpawn();
                            }
                        }

                        if (player == null || player.PlayerPawn == null || player.PlayerPawn.Value.CBodyComponent == null || !player.IsValid || !player.PawnIsAlive)
                        {
                            Console.WriteLine($"{player?.PlayerName} is being skipped due to NULL!");
                            continue;
                        }
                        PullPlayer(player, grappleTarget, playerPosition, eye_vector, end);

                    }
                }
            });

            Console.WriteLine("[SharpHook] Plugin is enabled!");
        }
        public void GrappleHandler(CCSPlayerController? player)
        {
            if (player == null) return;

            if (!playerGrapples.ContainsKey(player.Slot))
                playerGrapples[player.Slot] = new PlayerGrappleInfo();


            DetachGrapple(player);
            playerGrapples[player.Slot].IsPlayerGrappling = true;
        }

        private void PullPlayer(CCSPlayerController player, Vector grappleTarget, Vector playerPosition, Vector eye_vector, Vector end)
        {

            if (!player.IsValid || !player.PawnIsAlive)
            {
                Console.WriteLine("Invalid Player!");
                return;
            }

            if (player.PlayerPawn?.Value?.CBodyComponent?.SceneNode == null)
            {
                Console.WriteLine("Error!");
                return;
            }

            if (player.PlayerPawn.Value.AbsVelocity != null)
            {
                player.PlayerPawn.Value.AbsVelocity.X = playerGrapples[player.Slot].staticVelocity.X;
                player.PlayerPawn.Value.AbsVelocity.Y = playerGrapples[player.Slot].staticVelocity.Y;
                player.PlayerPawn.Value.AbsVelocity.Z = playerGrapples[player.Slot].staticVelocity.Z;
            }
            else
            {
                Console.WriteLine("Velocity null!");
                return;
            }

            var grappleWire = playerGrapples[player.Slot].GrappleWire;
            if (grappleWire != null)
            {
                grappleWire.Teleport(playerPosition, new QAngle(0, 0, 0), new Vector(0, 0, 0));
            }
            else
            {
                Console.WriteLine("GrappleWire is null.");
            }
        }

        private void DetachGrapple(CCSPlayerController player)
        {
            ConsoleMessage = true;
            if (playerGrapples.TryGetValue(player.Slot, out var grappleInfo))
            {
                grappleInfo.IsPlayerGrappling = false;

                if (grappleInfo.GrappleWire != null)
                {
                    grappleInfo?.GrappleWire.Remove();
                    grappleInfo!.GrappleWire = null;
                }
            }
        }
    }
}
