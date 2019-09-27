using System;
using System.Collections.Generic;
using System.IO;

namespace LiteServerFrame.Core.General.IPC
{
    public class IPCConfig
    {
        private static string ConfigPath = "./IPCConfig.txt";
        private static Dictionary<int, IPCInfo> IPCInfos = new Dictionary<int, IPCInfo>();
        private static bool isInit = false;
        
        public static IPCInfo GetIPCInfo(int id)
        {
            if (!isInit)
            {
                ReadConfig();
                isInit = true;
            }
            if (IPCInfos.ContainsKey(id))
            {
                return IPCInfos[id];
            }
            return null;
        }

        private static void ReadConfig()
        {
            IPCInfos.Clear();
            using (StreamReader stream = new StreamReader(ConfigPath))
            {
                string line;
                while ((line = stream.ReadLine()) != null)
                {
                    IPCInfo info = new IPCInfo();
                    string[] infos = line.Split('|');
                    info.ID = int.Parse(infos[0]);
                    info.Port = int.Parse(infos[1]);
                    IPCInfos.Add(info.ID, info);
                }
            }
        }
    }
}