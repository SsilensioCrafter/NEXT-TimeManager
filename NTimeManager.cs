using System;
using System.Globalization;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("NTimeManager", "NEXT Rust", "1.0.0")]
    [Description("Time manager with day/night durations and time commands.")]
    public class NTimeManager : RustPlugin
    {
        private const string PermissionAdmin = "ntimemanager.admin";
        private const string PermissionTime = "ntimemanager.time";

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
            permission.RegisterPermission(PermissionTime, this);
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

            var dayApplied = TrySetCycleValue(sky.Cycle,
                new[] { "DayLengthInMinutes", "DayLength", "dayLengthInMinutes" },
                _config.DayDurationMinutes);
            var nightApplied = TrySetCycleValue(sky.Cycle,
                new[] { "NightLengthInMinutes", "NightLength", "nightLengthInMinutes" },
                _config.NightDurationMinutes);

            if (dayApplied || nightApplied)
            {
                TryRefreshCycle(sky.Cycle);
            }

            var envDayApplied = TrySetEnvLength("env.daylength", _config.DayDurationMinutes);
            var envNightApplied = TrySetEnvLength("env.nightlength", _config.NightDurationMinutes);

            if ((!dayApplied || !nightApplied) && (!envDayApplied || !envNightApplied))
            {
                PrintWarning("Could not apply day/night lengths via TOD cycle parameters.");
            }

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
            if (!HasTimePermission(player))
            {
                Reply(player, "У вас нет прав для этой команды.");
                return;
            }

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

        private bool HasTimePermission(BasePlayer player)
        {
            if (player == null)
            {
                return true;
            }

            return permission.UserHasPermission(player.UserIDString, PermissionTime);
        }

        private void Reply(BasePlayer player, string message)
        {
            if (player != null)
            {
                SendReply(player, message);
            }
        }

        private static bool TrySetCycleValue(object cycle, string[] memberNames, float value)
        {
            if (cycle == null)
            {
                return false;
            }

            var cycleType = cycle.GetType();
            foreach (var memberName in memberNames)
            {
                var property = cycleType.GetProperty(memberName);
                if (property != null && property.CanWrite && property.PropertyType == typeof(float))
                {
                    property.SetValue(cycle, value);
                    return true;
                }

                var field = cycleType.GetField(memberName);
                if (field != null && field.FieldType == typeof(float))
                {
                    field.SetValue(cycle, value);
                    return true;
                }
            }

            return false;
        }

        private static void TryRefreshCycle(object cycle)
        {
            if (cycle == null)
            {
                return;
            }

            var cycleType = cycle.GetType();
            var refreshMethod = cycleType.GetMethod("Refresh");
            if (refreshMethod != null)
            {
                refreshMethod.Invoke(cycle, null);
                return;
            }

            var updateMethod = cycleType.GetMethod("Update");
            if (updateMethod != null)
            {
                updateMethod.Invoke(cycle, null);
            }
        }

        private static bool TrySetEnvLength(string command, float value)
        {
            if (!HasConsoleCommand(command))
            {
                return false;
            }

            try
            {
                ConsoleSystem.Run(ConsoleSystem.Option.Server, command,
                    value.ToString(CultureInfo.InvariantCulture));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool HasConsoleCommand(string command)
        {
            var indexProperty = typeof(ConsoleSystem).GetProperty("Index");
            if (indexProperty == null)
            {
                return false;
            }

            var index = indexProperty.GetValue(null);
            if (index == null)
            {
                return false;
            }

            var indexType = index.GetType();
            var findMethod = indexType.GetMethod("FindCommand", new[] { typeof(string) })
                             ?? indexType.GetMethod("GetCommand", new[] { typeof(string) });
            if (findMethod == null)
            {
                return false;
            }

            return findMethod.Invoke(index, new object[] { command }) != null;
        }
    }
}
