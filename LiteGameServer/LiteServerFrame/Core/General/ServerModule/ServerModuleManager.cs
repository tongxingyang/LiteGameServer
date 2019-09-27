using System;
using System.Collections.Generic;
using GameFramework.Debug;
using LiteServerFrame.Utility;

namespace LiteServerFrame.Core.General.ServerModule
{
    public class ServerModuleManager : Singleton<ServerModuleManager>
    {
        private Dictionary<int, ServerModule> serverModules;
        private string nameSpace;

        public void Init(string space)
        {
            nameSpace = space;
            serverModules = new Dictionary<int, ServerModule>();
        }

        public void StartServerModule(int id)
        {
            ServerModuleInfo info = ServerModuleConfig.GetServerModuleInfo(id);
            string fullName = nameSpace + "." + info.Name + "." + info.Name;
            try
            {
                Type type = Type.GetType(fullName + "," + info.AssemblyName);
                if (type != null)
                {
                    var module = Activator.CreateInstance(type) as ServerModule;
                    if (module != null)
                    {
                        module.Create(info);
                        serverModules.Add(module.ID, module);
                        module.Start();
                    }
                }
            }
            catch (Exception e)
            {
                Debuger.LogError(e.Message);
            }
        }

        public void StopServerModule(int id)
        {
            if (serverModules.ContainsKey(id))
            {
                var module = serverModules[id];
                if (module != null)
                {
                    module.Stop();
                    module.Release();
                    serverModules.Remove(id);
                }
            }
        }

        public void StopAllServerModule()
        {
            foreach (KeyValuePair<int,ServerModule> serverModule in serverModules)
            {
                var module = serverModule.Value;
                if (module != null)
                {
                    module.Stop();
                    module.Release();
                }
            }
            serverModules.Clear();
        }

        public void Tick()
        {
            foreach (KeyValuePair<int,ServerModule> serverModule in serverModules)
            {
                var module = serverModule.Value;
                module?.Tick();
            }
        }
    }
}