using System.Collections.Generic;
using System.Linq;

namespace Neo.Plugins
{
    public class RpcIteratorPlugin : Plugin
    {
        public override string Name => "RpcIterator";
        public override string Description => "Enables Iterator for the node";

        private Settings settings;
        private static readonly Dictionary<uint, IteratorServer> servers = new();
        private static readonly Dictionary<uint, List<object>> handlers = new();

        protected override void Configure()
        {
            settings = new Settings(GetConfiguration());
            foreach (RpcIteratorSettings s in settings.Servers)
                if (servers.TryGetValue(s.Network, out IteratorServer server))
                    server.UpdateSettings(s);
        }

        public override void Dispose()
        {
            foreach (var (_, server) in servers)
                server.Dispose();
            base.Dispose();
        }

        protected override void OnSystemLoaded(NeoSystem system)
        {
            RpcIteratorSettings s = settings.Servers.FirstOrDefault(p => p.Network == system.Settings.Network);
            if (s is null) return;

            IteratorServer server = new(system, s);

            if (handlers.Remove(s.Network, out var list))
            {
                foreach (var handler in list)
                {
                    server.RegisterMethods(handler);
                }
            }

            server.StartRpcServer();
            servers.TryAdd(s.Network, server);
        }

        public static void RegisterMethods(object handler, uint network)
        {
            if (servers.TryGetValue(network, out IteratorServer server))
            {
                server.RegisterMethods(handler);
                return;
            }
            if (!handlers.TryGetValue(network, out var list))
            {
                list = new List<object>();
                handlers.Add(network, list);
            }
            list.Add(handler);
        }
    }
}
