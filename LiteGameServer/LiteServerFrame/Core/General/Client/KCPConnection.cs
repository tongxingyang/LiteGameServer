using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using DebugerTool;
using LiteServerFrame.Core.General.Base.KCP;
using LiteServerFrame.Utility;

namespace LiteServerFrame.Core.General.Client
{
    public class KCPConnection : IConnection
    {
        public Action<byte[], int> onReceive { get; set; }
        public bool Connected { get; private set; }
        public int ID { get; private set; }
        public string IP { get; private set; }
        public int Port { get; private set; }
        private int remotePort;
        private string remoteIp;
        private IPEndPoint remotEndPoint;
        private Socket currentSocket;
        private Thread threadRecv;
        private byte[] byteBuffer = new byte[4096];
        private KCP kcp;
        private SwitchQueue<byte[]> recvBufQueue = new SwitchQueue<byte[]>();
        private uint nextKcpUpdateTime = 0;
        private bool needKcpUpdateFlag = false;
        
        public void Init(int connId, int bindPort)
        {
            ID = connId;
            Port = bindPort;
        }

        public void Clean()
        {
            onReceive = null;
            Close();
        }

        public void Connect(string ip, int port)
        {
            Debuger.Log("连接服务端 IP {0} Port {1} ", ip, port);
            IP = ip;
            remoteIp = ip;
            remotePort = port;
            remotEndPoint = IPUtility.GetHostEndPoint(ip, port);
            currentSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            currentSocket.Bind(new IPEndPoint(IPAddress.Any, Port));
            kcp = new KCP(0 ,HandleKcpSend);
            kcp.NoDelay(1, 10, 2, 1);
            kcp.WndSize(128, 128);
            Connected = true;
            threadRecv = new Thread(ThreadRecv){IsBackground = true};
            threadRecv.Start();
        }

        private void HandleKcpSend(byte[] bytes, int len)
        {
            currentSocket.SendTo(bytes, 0, len, SocketFlags.None, remotEndPoint);
        }
        
        public void Close()
        {
            Connected = false;
            kcp?.Dispose();
            kcp = null;
            threadRecv?.Interrupt();
            threadRecv = null;
            if (currentSocket != null)
            {
                try
                {
                    currentSocket.Shutdown(SocketShutdown.Both);
                }
                catch (Exception e)
                {
                    Debuger.LogWarning(e.Message + e.StackTrace);
                }
                currentSocket.Close();
                currentSocket = null;
            }
        }

        private void ThreadRecv()
        {
            while (Connected)
            {
                try
                {
                    DoReceiveInThread();
                }
                catch (Exception e)
                {
                    Debuger.LogWarning(e.Message + "\n" + e.StackTrace);
                    Thread.Sleep(1);
                }
                
            }
        }
        
        private void DoReceiveInThread()
        {
            EndPoint remotePoint = IPUtility.GetIPEndPointAny(AddressFamily.InterNetwork, 0);
            int cnt = currentSocket.ReceiveFrom(byteBuffer, byteBuffer.Length, SocketFlags.None, ref remotePoint);

            if (cnt > 0)
            {
                if (!remotEndPoint.Equals(remotePoint))
                {
                    Debuger.LogError("收到非目标服务器的数据！");
                    return;
                }

                byte[] dst = new byte[cnt];
                Buffer.BlockCopy(byteBuffer, 0, dst, 0, cnt);
                recvBufQueue.Push(dst);
            }
        }
        
        private void DoReceiveInMain()
        {
            recvBufQueue.Switch();

            while (!recvBufQueue.Empty())
            {
                var recvBufferRaw = recvBufQueue.Pop();
                int ret = kcp.Input(recvBufferRaw, recvBufferRaw.Length);
                if (ret < 0)
                {
                    Debuger.LogError("收到不正确的KCP包!Ret:{0}", ret);
                    return;
                }
                needKcpUpdateFlag = true;
                for (int size = kcp.PeekSize();size > 0; size = kcp.PeekSize())
                {
                    var recvBuffer = new byte[size];
                    if (kcp.Recv(recvBuffer) > 0)
                    {
                        onReceive.Invoke(recvBuffer, size);
                    }
                }
            }
        }
        
        public bool Send(byte[] bytes, int len)
        {
            if (!Connected)
            {
                return false;
            }
            return kcp.Send(bytes, len) > 0;
        }

        public void Tick()
        {
            if (Connected)
            {
                DoReceiveInMain();
                uint current = (uint) TimeUtility.GetTotalMillisecondsSince1970();
                if (needKcpUpdateFlag || current >= nextKcpUpdateTime)
                {
                    kcp.Update(current);
                    nextKcpUpdateTime = kcp.Check(current);
                    needKcpUpdateFlag = false;
                }
            }
        }
    }
}