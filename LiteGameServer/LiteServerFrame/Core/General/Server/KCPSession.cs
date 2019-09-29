using System;
using System.Net;
using System.Net.Sockets;
using GameFramework.Debug;
using LiteServerFrame.Core.General.Base.KCP;
using LiteServerFrame.Utility;

namespace LiteServerFrame.Core.General.Server
{
    public class KCPSession : ISession
    {
        public static int ActiveTimeout = 60;
        
        private uint sessionId;
        private uint userId;
        public uint id {get { return sessionId; }}
        public uint uid {get { return userId; }}
        public ushort ping { get; set; }
        public Socket ConnentSocket { get; set; }
        public IPEndPoint remoteEndPoint { get; private set; }

        //发送消息的Action
        private Action<ISession, byte[], int> senderAction;
        //收到消息后交付处理的对象
        private ISessionListener listener;
        private KCP kcp;
        private SwitchQueue<byte[]> recvBufQueue = new SwitchQueue<byte[]>();
        private int lastActiveTime = 0;
        private bool active = false;
        private uint nextKcpUpdateTime = 0;
        private bool needKcpUpdateFlag = false;
        
        public KCPSession(uint sid, Action<ISession, byte[], int> sender, ISessionListener listener)
        {
            sessionId = sid;
            senderAction = sender;
            this.listener = listener;

            kcp = new KCP(sid, HandleKcpSend);
            kcp.NoDelay(1, 10, 2, 1);
            kcp.WndSize(128, 128);
        }
        
        private void HandleKcpSend(byte[] bytes, int len)
        {
            senderAction(this, bytes, len);
        }
        
        public void Active(IPEndPoint remotePoint)
        {
            lastActiveTime = (int)TimeUtility.GetTimeSinceStartup();
            active = true;
            remoteEndPoint = remotePoint;
        }

        public bool IsActived()
        {
            if (!active)
            {
                return false;
            }
            int dt = (int)TimeUtility.GetTimeSinceStartup() - lastActiveTime;
            if (dt > ActiveTimeout)
            {
                active = false;
            }
            return active;
        }

        public bool IsAuth()
        {
            return userId > 0;
        }

        public void SetAuth(uint userid)
        {
            userId = userid;
        }

        public bool Send(byte[] bytes, int len)
        {
            if (!IsActived())
            {
                Debuger.LogWarning("Session已经不活跃了！");
                return false;
            }
            return kcp.Send(bytes, len) > 0;
        }

        public void Tick(uint currentTimeMS)
        {
            DoReceiveInMain();
            uint current = currentTimeMS;
            if (needKcpUpdateFlag || current >= nextKcpUpdateTime)
            {
                kcp.Update(current);
                nextKcpUpdateTime = kcp.Check(current);
                needKcpUpdateFlag = false;
            }
        }

        public void DoReceiveInGateway(byte[] buffer, int len)
        {
            byte[] dst = new byte[len];
            Buffer.BlockCopy(buffer, 0, dst, 0, len);
            recvBufQueue.Push(dst);
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

                for (int size = kcp.PeekSize(); size > 0; size = kcp.PeekSize())
                {
                    var recvBuffer = new byte[size];
                    if (kcp.Recv(recvBuffer) > 0)
                    {
                        listener.OnReceive(this, recvBuffer, size);
                    }
                }
            }
        }

        public void SetSocket(Socket socket)
        {
            
        }
        
        public void Clean()
        {
            kcp?.Dispose();
            kcp = null;
        }
    }
}