using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace LibraryOfAiLexandria
{
    public class BotManager
    {
        private readonly Dictionary<int, DiscordBotInstance> _activeBots = new();
        private readonly Action<string> _logCallback;

        public BotManager(Action<string> logCallback)
        {
            _logCallback = logCallback;
        }

        public async Task StartBotAsync(int index, BotConfig config)
        {
            if (_activeBots.ContainsKey(index))
            {
                await StopBotAsync(index);
            }

            var instance = new DiscordBotInstance(config, _logCallback);
            _activeBots[index] = instance;
            await instance.StartAsync();
        }

        public async Task StopBotAsync(int index)
        {
            if (_activeBots.TryGetValue(index, out var instance))
            {
                await instance.StopAsync();
                _activeBots.Remove(index);
            }
        }

        public async Task StopAllAsync()
        {
            var tasks = _activeBots.Keys.ToList().Select(StopBotAsync);
            await Task.WhenAll(tasks);
        }

        public bool IsBotRunning(int index)
        {
            return _activeBots.TryGetValue(index, out var instance) && instance.IsConnected;
        }
    }
}
