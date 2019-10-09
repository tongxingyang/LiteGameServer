using System;
using System.Net;
using System.Text;
using CommonData.Proto;
using GameFramework.Debug;
using LiteServerFrame.Core.General.Base.KCP;
using LiteServerFrame.Utility;

namespace LiteServerFrame.Core.General.FSP.Server
{
    public class FSPSession
    {
        public static int ActiveTimeout = 300;
        
        private int lastActiveTime;
        private bool active = false;
        private uint sessionID;
        private ushort ping;
        private Action<IPEndPoint, byte[], int> sender;
        private Action<FSPDataCToS> listener;
        private KCP kcp;
        private SwitchQueue<byte[]> recvBufQueue = new SwitchQueue<byte[]>();
        private uint nextKcpUpdateTime = 0;
        private bool needKcpUpdateFlag = false;
        private byte[] sendBufferTemp = new byte[4096];
        private FSPDataSToC tempSendData = new FSPDataSToC();


        public uint SessionID => sessionID;
        public ushort Ping { get { return ping; } set { ping = value; } }
        public IPEndPoint RemoteEndPoint { get; private set; }
        public bool IsEndPointChanged { get; set; }
        
        public FSPSession(uint sid, Action<IPEndPoint, byte[], int> senderAction)
        {
            sessionID = sid;
            sender = senderAction;

            kcp = new KCP(sid, HandleKcpSend);
            kcp.NoDelay(1, 10, 2, 1);
            kcp.WndSize(128, 128);
            active = true;
        }
        
        public void SetReceiveListener(Action<FSPDataCToS> listenerAction)
        {
            listener = listenerAction;
        }
        
        public void Active(IPEndPoint remoteEndPoint)
        {
            lastActiveTime = (int)TimeUtility.GetTimeSinceStartup();
            active = true;
            if (this.RemoteEndPoint == null || !this.RemoteEndPoint.Equals(remoteEndPoint))
            {
                IsEndPointChanged = true;
                this.RemoteEndPoint = remoteEndPoint;
            }
            
        }

        public void Active(bool value)
        {
            active = value;
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
        
        public bool Send(FSPFrameData frame)
        {
            if (!IsActived())
            {
                Debuger.LogWarning("Session已经不活跃了！");
                return false;
            }

            tempSendData.frame = frame;
            int len = ProtoBuffUtility.Serialize(tempSendData, sendBufferTemp);
            return kcp.Send(sendBufferTemp, len) == 0;
        }

        private void HandleKcpSend(byte[] buffer, int size)
        {
            sender(RemoteEndPoint, buffer, size);
        }

        public void DoReceiveInGateway(byte[] buffer, int size)
        {
            byte[] dst = new byte[size];
            Buffer.BlockCopy(buffer, 0, dst, 0, size);
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
                        if (listener != null)
                        {
                            FSPDataCToS data = ProtoBuffUtility.Deserialize<FSPDataCToS>(recvBuffer);
                            listener(data);
                        }
                        else
                        {
                            Debuger.LogError("找不到接收者！");
                        }
                    }
                }
            }
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
        
        public string ToString(string prefix = "")
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("[{0}] Active:{1}, Ping:{2}, EndPoint:{3}", sessionID, active, ping, RemoteEndPoint);
            return sb.ToString();
        }
        
    }
}