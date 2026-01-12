using System;
using System.Globalization;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("NEXT_TimeManager", "NEXT Rust", "1.0.0")]
    [Description("Time manager with day/night durations and time commands.")]
    public class NTimeManager : RustPlugin
    {
        private const string PermissionAdmin = "nexttimemanager.admin";

        private PluginConfig _config;

        private class PluginConfig
        {
            public float DayDurationMinutes = 45f;
            public float NightDurationMinutes = 15f;
        }

        protected override void LoadDefaultConfig()
        {
            _config = new PluginConfig();
            SaveConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<PluginConfig>();
            if (_config == null)
            {
                PrintWarning("Config file is invalid, using defaults.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config, true);

        private void Init()
        {
            permission.RegisterPermission(PermissionAdmin, this);
        }

        private void OnServerInitialized()
        {
            ApplyDurations();
        }

        private void ApplyDurations()
        {
            var sky = TOD_Sky.Instance;
            if (sky == null)
            {
                PrintWarning("TOD_Sky instance not available.");
                return;
            }

            ConsoleSystem.Run(ConsoleSystem.Option.Server, "env.daylength",
                _config.DayDurationMinutes.ToString(CultureInfo.InvariantCulture));
            ConsoleSystem.Run(ConsoleSystem.Option.Server, "env.nightlength",
                _config.NightDurationMinutes.ToString(CultureInfo.InvariantCulture));
        }

        [ChatCommand("timeset")]
        private void TimeSetCommand(BasePlayer player, string command, string[] args)
        {
            if (!HasAdminPermission(player))
            {
                Reply(player, "У вас нет прав для этой команды.");
                return;
            }

            if (args.Length == 0)
            {
                Reply(player, "Использование: /timeset day|night");
                return;
            }

            var sky = TOD_Sky.Instance;
            if (sky == null)
            {
                Reply(player, "Система времени недоступна.");
                return;
            }

            var target = args[0].ToLowerInvariant();
            if (target == "day")
            {
                sky.Cycle.Hour = 12f;
                Reply(player, "Время установлено на день.");
                return;
            }

            if (target == "night")
            {
                sky.Cycle.Hour = 0f;
                Reply(player, "Время установлено на ночь.");
                return;
            }

            Reply(player, "Использование: /timeset day|night");
        }

        [ChatCommand("timeduration")]
        private void TimeDurationCommand(BasePlayer player, string command, string[] args)
        {
            if (!HasAdminPermission(player))
            {
                Reply(player, "У вас нет прав для этой команды.");
                return;
            }

            Reply(player,
                $"День: {_config.DayDurationMinutes} мин., Ночь: {_config.NightDurationMinutes} мин.");
        }

        [ChatCommand("time")]
        private void TimeCommand(BasePlayer player, string command, string[] args)
        {
            var sky = TOD_Sky.Instance;
            if (sky == null)
            {
                Reply(player, "Система времени недоступна.");
                return;
            }

            var hour = sky.Cycle.Hour;
            var h = (int)Math.Floor(hour);
            var m = (int)Math.Floor((hour - h) * 60);
            var phase = sky.IsDay ? "день" : "ночь";

            Reply(player, $"Сейчас {phase}. Время: {h:D2}:{m:D2}.");
        }

        private bool HasAdminPermission(BasePlayer player)
        {
            if (player == null)
            {
                return true;
            }

            return player.IsAdmin || permission.UserHasPermission(player.UserIDString, PermissionAdmin);
        }

        private void Reply(BasePlayer player, string message)
        {
            if (player != null)
            {
                SendReply(player, message);
            }
        }
    }
}
