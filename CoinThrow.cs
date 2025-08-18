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
        public override string ModuleVersion => "1.2";

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
            int flips = 5;         // how many times to flip before result
            float delay = 0.3f;    // time between flips
        
            void SpinStep(int remainingFlips)
            {
                if (remainingFlips > 0)
                {
                    string curr = options[remainingFlips % options.Length];
                    string html =
                        $"<br><font size='20' color='#FFFFFF'>Rolling your coin...</font><br><br>" +
                        $"<font size='28' color='#FF0000'><b>&gt; {curr.ToUpper()} &lt;</b></font><br><br>" +
                        $"<font size='15' color='#AAAAAA'>{Config.ServerBrand}</font>";
        
                    player.PrintToCenterHtml(html);
        
                    AddTimer(delay, () => SpinStep(remainingFlips - 1));
                }
                else
                {
                    // Final result
                    string result = isHeads ? "Heads" : "Tails";
                    string finalHtml =
                        $"<br><font size='20' color='#FFFFFF'>Result:</font><br><br>" +
                        $"<font size='28' color='#FF0000'><b>&gt; {result.ToUpper()} &lt;</b></font><br><br>" +
                        $"<font size='15' color='#AAAAAA'>{Config.ServerBrand}</font>";
        
                    // ✅ Send chat immediately
                    onComplete();
        
                    // ✅ Keep final HUD for 2s by refreshing it every 0.2s
                    int repeats = 10; // 10 × 0.2s = 2s
                    void HoldResult(int count)
                    {
                        if (count <= 0)
                        {
                            player.PrintToCenterHtml(""); // clear after hold
                            return;
                        }
        
                        player.PrintToCenterHtml(finalHtml);
                        AddTimer(0.2f, () => HoldResult(count - 1));
                    }
        
                    HoldResult(repeats);
                }
            }
        
            SpinStep(flips);
        }
    }
}
