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
        public override string ModuleVersion => "v3.0";

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

        [ConsoleCommand("css_cointhrow", "Throw a coin with text-based roulette")]
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

            bool isHeads = Random.Next(2) == 0;

            ShowTextRoulette(player, isHeads, () =>
            {
                string resultString = isHeads ? "Heads" : "Tails";

                Server.PrintToChatAll(
                    $"Player {ChatColors.Green}{player.PlayerName}{ChatColors.Default} threw the coin and the result is {ChatColors.Green}{resultString}{ChatColors.Default}."
                );

                int totalThrows = _database?.IncrementPlayerThrows(steamId, player.PlayerName) ?? 0;

                Server.PrintToChatAll(
                    $"His total number of throws is {ChatColors.Green}{totalThrows}x{ChatColors.Default}."
                );
            });

            UpdateLastCoinThrowTime(steamId);
        }

        private void ShowTextRoulette(CCSPlayerController player, bool isHeads, Action onComplete)
        {
            string[] options = { "Heads", "Tails" };
            int currentIndex = 0;
            float delay = 0.1f;       // start fast
            float slowdownStep = 0.05f;
            int spins = 10;           // fast spins before slowing down

            void SpinStep()
            {
                string prev = options[(currentIndex - 1 + options.Length) % options.Length];
                string curr = options[currentIndex];
                string next = options[(currentIndex + 1) % options.Length];

                string html =
                    $"<font color='#FFFFFF' size='20'>{prev}</font><br>" +
                    $"<font color='#FF0000' size='25'><b>{curr}</b></font><br>" +
                    $"<font color='#FFFFFF' size='20'>{next}</font>";

                player.PrintToCenterHtml(html);

                currentIndex = (currentIndex + 1) % options.Length;

                if (spins > 0)
                {
                    spins--;
                    AddTimer(delay, SpinStep);
                }
                else
                {
                    delay += slowdownStep;

                    // When slow enough, stop at the target
                    if (delay > 0.5f)
                    {
                        currentIndex = isHeads ? 0 : 1;
                        prev = options[(currentIndex - 1 + options.Length) % options.Length];
                        curr = options[currentIndex];
                        next = options[(currentIndex + 1) % options.Length];

                        html =
                            $"<font color='#FFFFFF' size='20'>{prev}</font><br>" +
                            $"<font color='#FF0000' size='25'><b>{curr}</b></font><br>" +
                            $"<font color='#FFFFFF' size='20'>{next}</font>";

                        player.PrintToCenterHtml(html);
                        onComplete();
                        return;
                    }
                    AddTimer(delay, SpinStep);
                }
            }

            SpinStep();
        }
    }
}
