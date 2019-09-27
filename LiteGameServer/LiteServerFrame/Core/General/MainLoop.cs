using System.Threading;
using LiteServerFrame.Core.General.ServerModule;

namespace LiteServerFrame.Core.General
{
    public class MainLoop
    {
        public static void Run()
        {
            while (true)
            {
                ServerModuleManager.Instance.Tick();
                Thread.Sleep(1);
            }
        }
    }
}