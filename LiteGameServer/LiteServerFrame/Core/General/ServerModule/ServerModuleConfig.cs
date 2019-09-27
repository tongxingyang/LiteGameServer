using System;
using System.Collections.Generic;
using System.IO;

namespace LiteServerFrame.Core.General.ServerModule
{
    public class ServerModuleConfig
    {
        private static string ConfigPath = "./ServerModuleConfig.txt";
        private static Dictionary<int,ServerModuleInfo> ServerModuleInfos = new Dictionary<int, ServerModuleInfo>();
        private static bool isInit;

        public static ServerModuleInfo GetServerModuleInfo(int id)
        {
            if (!isInit)
            {
                ReadConfig();
                isInit = true;
            }
            if (ServerModuleInfos.ContainsKey(id))
            {
                return ServerModuleInfos[id];
            }
            return null;
        }
        
        private static void ReadConfig()
        {
            ServerModuleInfos.Clear();
            using (StreamReader stream = new StreamReader(ConfigPath))
            {
                string line;
                while ((line = stream.ReadLine()) != null)
                {
                    ServerModuleInfo info = new ServerModuleInfo();
                    string[] infos = line.Split('|');
                    info.ID = int.Parse(infos[0]);
                    info.Port = int.Parse(infos[1]);
                    info.Name = infos[2];
                    info.AssemblyName = infos[3];
                    ServerModuleInfos.Add(info.ID, info);
                }
            }
        }
    }
}