using System.Net;

namespace LiteServerFrame.Core.General.Server
{
    public interface ISession
    {
        uint id { get; }//Session ID
        uint uid { get; }//User ID
        ushort ping { get; set; }
        IPEndPoint remoteEndPoint { get; }//Client IPEndPoint
        void Active(IPEndPoint remotePoint);//激活Session
        bool IsActived();//是否是激活的
        bool IsAuth();//是否授权
        void SetAuth(uint userId);//授权
        bool Send(byte[] bytes, int len);//发送消息
        void Tick(uint currentTimeMS);
        void DoReceiveInGateway(byte[] buffer, int len);
        void Clean();
    }
}