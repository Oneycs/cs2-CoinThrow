using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using System;
using System.Collections.Generic;

namespace CoinThrow
{
    public class CoinThrow : BasePlugin, IPluginConfig<Config>
    {
        private readonly Dictionary<string, DateTime> _lastCoinThrowTimes = new();
        private static readonly Random Random = new();
        private const int CooldownSeconds = 10;

        private Database? _database;

        public override string ModuleAuthor => "TICHOJEBEC";
        public override string ModuleName => "CoinThrow";
        public override string ModuleVersion => "v1.2";

        public Config Config { get; set; } = new();
        public void OnConfigParsed(Config config) => Config = config;

        public override void Load(bool hotReload)
        {
            _database = new Database(Config.DbHost, Config.DbPort, Config.DbDatabase, Config.DbUser, Config.DbPassword);
            _database.Initialize();

            Console.WriteLine("[CoinThrow] Plugin loaded successfully.");
        }

        private bool IsOnCooldown(string steamId, out double remainingSeconds)
        {
            if (_lastCoinThrowTimes.TryGetValue(steamId, out var lastThrow))
            {
                var elapsed = DateTime.Now - lastThrow;
                if (elapsed.TotalSeconds < CooldownSeconds)
                {
                    remainingSeconds = CooldownSeconds - elapsed.TotalSeconds;
                    return true;
                }
            }

            remainingSeconds = 0;
            return false;
        }

        private void UpdateLastCoinThrowTime(string steamId) =>
            _lastCoinThrowTimes[steamId] = DateTime.Now;

        [ConsoleCommand("css_cointhrow", "Throw a coin with a rolling menu effect")]
        public void OnCoinThrowCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null || player.PlayerPawn.Value == null || !player.IsValid)
                return;

            string steamId = player.SteamID.ToString();

            if (IsOnCooldown(steamId, out var remaining))
            {
                player.PrintToChat($"You can throw the coin only once every {ChatColors.DarkRed}{CooldownSeconds}{ChatColors.Default} seconds! ({remaining:F0}s left)");
                return;
            }

            // Randomize final result
            string[] options = { "Heads", "Tails" };
            int finalIndex = Random.Next(options.Length);
            string finalResult = options[finalIndex];

            int totalRolls = 12; // how many steps in the animation
            int currentRoll = 0;

            // Timer rolls through Heads/Tails quickly
            AddTimer(0.15f, () =>
            {
                if (!player.IsValid)
                    return;

                int index = currentRoll % options.Length;
                string display = BuildRollingHtml(options, index, false);
                player.PrintToCenterHtml(display);

                currentRoll++;

                // Continue rolling until we hit totalRolls
                if (currentRoll < totalRolls)
                {
                    AddTimer(0.15f, () => OnRollStep(player, options, finalIndex, finalResult, steamId, ref currentRoll, totalRolls));
                }
                else
                {
                    // Show final result
                    string finalDisplay = BuildRollingHtml(options, finalIndex, true);
                    player.PrintToCenterHtml(finalDisplay);

                    int totalThrows = _database?.IncrementPlayerThrows(steamId, player.PlayerName) ?? 0;

                    Server.PrintToChatAll(
                        $"Player {ChatColors.Green}{player.PlayerName}{ChatColors.Default} threw the coin and the result is {ChatColors.Green}{finalResult}{ChatColors.Default}."
                    );

                    Server.PrintToChatAll(
                        $"His total number of throws is {ChatColors.Green}{totalThrows}x{ChatColors.Default}."
                    );

                    UpdateLastCoinThrowTime(steamId);
                }
            });
        }

        private void OnRollStep(CCSPlayerController player, string[] options, int finalIndex, string finalResult, string steamId, ref int currentRoll, int totalRolls)
        {
            if (!player.IsValid)
                return;

            int index = currentRoll % options.Length;
            string display = BuildRollingHtml(options, index, false);
            player.PrintToCenterHtml(display);

            currentRoll++;

            if (currentRoll < totalRolls)
            {
                AddTimer(0.15f, () => OnRollStep(player, options, finalIndex, finalResult, steamId, ref currentRoll, totalRolls));
            }
            else
            {
                string finalDisplay = BuildRollingHtml(options, finalIndex, true);
                player.PrintToCenterHtml(finalDisplay);

                int totalThrows = _database?.IncrementPlayerThrows(steamId, player.PlayerName) ?? 0;

                Server.PrintToChatAll(
                    $"Player {ChatColors.Green}{player.PlayerName}{ChatColors.Default} threw the coin and the result is {ChatColors.Green}{finalResult}{ChatColors.Default}."
                );

                Server.PrintToChatAll(
                    $"His total number of throws is {ChatColors.Green}{totalThrows}x{ChatColors.Default}."
                );

                UpdateLastCoinThrowTime(steamId);
            }
        }

        private string BuildRollingHtml(string[] options, int currentIndex, bool isFinal)
        {
            string top = "<div style='text-align:center;color:white;font-size:20px;'>Rolling your Coin...</div>";
            string list = "";

            for (int i = 0; i < options.Length; i++)
            {
                string color = (i == currentIndex) ? "yellow" : "white";
                string fontWeight = (i == currentIndex) ? "bold" : "normal";
                list += $"<div style='text-align:center;color:{color};font-size:18px;font-weight:{fontWeight};'>{options[i]}</div>";
            }

            string footer = "<div style='text-align:center;color:orange;font-size:16px;'>CSKO.NET</div>";
            return $"{top}{list}{footer}";
        }
    }
}
