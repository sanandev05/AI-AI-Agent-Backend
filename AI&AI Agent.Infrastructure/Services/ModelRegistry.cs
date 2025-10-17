using System.Collections.Concurrent;

namespace AI_AI_Agent.Infrastructure.Services
{
    public interface IModelRegistry
    {
        void Add(string provider, string key, string displayName, bool available);
        IReadOnlyList<ModelInfo> List();
    }

    public sealed record ModelInfo(string Provider, string Key, string DisplayName, bool Available);

    public sealed class ModelRegistry : IModelRegistry
    {
        private readonly ConcurrentDictionary<string, ModelInfo> _models = new(StringComparer.OrdinalIgnoreCase);

        public void Add(string provider, string key, string displayName, bool available)
        {
            if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(key)) return;
            var info = new ModelInfo(provider, key, string.IsNullOrWhiteSpace(displayName) ? key : displayName, available);
            _models[key] = info;
        }

        public IReadOnlyList<ModelInfo> List() => _models.Values.OrderBy(m => m.Provider).ThenBy(m => m.DisplayName).ToList();
    }
}
