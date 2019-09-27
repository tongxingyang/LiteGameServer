using System.Net;
using System.Net.Sockets;

namespace LiteServerFrame.Core.General.Server
{
    public interface ISession
    {
        uint id { get; }
        uint uid { get; }
        Socket ConnentSocket { get; set; }
        ushort ping { get; set; }
        IPEndPoint remoteEndPoint { get; }
        void Active(IPEndPoint remotePoint);
        bool IsActived();
        bool IsAuth();
        void SetAuth(uint userId);
        bool Send(byte[] bytes, int len);
        void Tick(uint currentTimeMS);
        void DoReceiveInGateway(byte[] buffer, int len);
        void SetSocket(Socket socket);
        void Clean();
    }
}