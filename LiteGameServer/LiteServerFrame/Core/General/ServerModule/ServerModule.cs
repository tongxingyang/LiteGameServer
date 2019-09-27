using GameFramework.Debug;

namespace LiteServerFrame.Core.General.ServerModule
{
    public class ServerModule : IDebugLogTag
    {
        public string LOGTAG { get; private set; }
        public int ID{get { return serverModuleInfo.ID; }}
        public int Port{get { return serverModuleInfo.Port; }}
        private ServerModuleInfo serverModuleInfo;

        public virtual void Create(ServerModuleInfo info)
        {
            serverModuleInfo = info;
            LOGTAG = this.GetType().Name + "[" + info.ID + "," + info.Port + "]";
        }

        public virtual void Release()
        {
            
        }

        public virtual void Start()
        {
            
        }

        public virtual void Stop()
        {
            
        }

        public virtual void Tick()
        {
            
        }
    }
}