using System.Net;

namespace LiteServerFrame.Core.General.Server
{
    public interface ISessionListener
    {
        void OnReceive(ISession session, byte[] bytes, int len);
        void OnClientAccept(ISession session, IPEndPoint IpEndPoint);
        void OnClientClose(ISession session, string info);
    }
}