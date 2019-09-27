using System;
using System.Net.Sockets;

namespace LiteServerFrame.Core.General.Client
{
    public interface IConnection
    {
        Action<byte[], int> onReceive { get; set; }
        bool Connected { get; }
        int ID { get; }
        int Port { get; }
        string IP { get; }
        void Init(int connId, int bindPort);
        void Clean();
        void Connect(string ip, int port);
        void Close();
        bool Send(byte[] bytes, int len);
        void Tick();
    }
}