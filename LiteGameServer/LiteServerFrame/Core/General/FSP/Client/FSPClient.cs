using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using CommonData.Proto;
using GameFramework.Debug;
using LiteServerFrame.Core.General.Base.KCP;
using LiteServerFrame.Utility;

namespace LiteServerFrame.Core.General.FSP.Client
{
    public class FSPClient
    {
        private static int TimeOut = 5000;

        private bool isRunning;
        private string ip;
        private int port;
        private uint sessionID;
        private Socket currentSocket;
        private IPEndPoint remoteEndPoint;
        private bool isWaitReconnect;
        private uint lastRecvTimestamp;
        private KCP kcp;
        private Thread threadRecv;
        private SwitchQueue<byte[]> recvBuffQueue;
        private bool needKcpUpdateFlag;
        private uint nextKcpUpdateTime;
        private int authID;
        private Action<FSPFrameData> recvListener;
        private byte[] recvBufferTemp;
        private byte[] sendBufferTemp;
        private FSPDataCToS tempSendData;
        public bool IsRunning => isRunning;

        public void Init(uint sessionid)
        {
            sessionID = sessionid;
            sendBufferTemp = new byte[4096];
            recvBufferTemp = new byte[4096];
            tempSendData = new FSPDataCToS();
            recvBuffQueue = new SwitchQueue<byte[]>();
            tempSendData.sessionID = sessionid;
            tempSendData.msgs.Add(new FSPMessage());
            kcp = new KCP(sessionid, HandleKcpSend);
            kcp.NoDelay(1, 10, 2, 1);
            kcp.WndSize(128, 128);
        }
        
        private void HandleKcpSend(byte[] buffer, int size)
        {
            currentSocket.SendTo(buffer, 0, size, SocketFlags.None, remoteEndPoint);
        }

        public void Clean()
        {
            kcp?.Dispose();
            kcp = null;
            recvListener = null;
            sendBufferTemp = null;
            recvBufferTemp = null;
            tempSendData = null;
            recvBuffQueue.Clear();
            recvBuffQueue = null;
            Close();
        }

        private void Close()
        {
            isRunning = false;
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

        public void SetFSPAuthID(int authid)
        {
            authID = authid;
        }

        public void SetFSPListener(Action<FSPFrameData> listener)
        {
            recvListener = listener;
        }

        public bool Connect(string ipAddress, int listenPort)
        {
            if (currentSocket != null)
            {
                Debuger.LogError("无法建立连接，需要先关闭上一次连接！");
                return false;
            }
            ip = ipAddress;
            port = listenPort;
            lastRecvTimestamp = (uint) TimeUtility.GetTotalMillisecondsSince1970();
            try
            {
                remoteEndPoint = IPUtility.GetHostEndPoint(ip, port);
                if (remoteEndPoint == null)
                {
                    Debuger.LogError("无法将Host解析为IP！");
                    Close();
                    return false;
                }
                currentSocket = new Socket(remoteEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
                currentSocket.Bind(IPUtility.GetIPEndPointAny(AddressFamily.InterNetwork, 0));
                isRunning = true;
                threadRecv = new Thread(ThreadRecv) { IsBackground = true };
                threadRecv.Start();
            }
            catch (Exception e)
            {
                Debuger.LogError(e.Message + e.StackTrace);
                Close();
                return false;
            }
            return true;
        }

        public void Reconnect()
        {
            isWaitReconnect = false;
            Close();
            Connect(ip, port);
            VerifyAuth();
        }
        
        private void ThreadRecv()
        {
            while (isRunning)
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
            int cnt = currentSocket.ReceiveFrom(recvBufferTemp, recvBufferTemp.Length, SocketFlags.None, ref remotePoint);

            if (cnt > 0)
            {
                if (!remoteEndPoint.Equals(remotePoint))
                {
                    Debuger.LogError("收到非目标服务器的数据！");
                    return;
                }
                byte[] dst = new byte[cnt];
                Buffer.BlockCopy(recvBufferTemp, 0, dst, 0, cnt);
                recvBuffQueue.Push(dst);
            }
        }
        
        private void DoReceiveInMain()
        {
            recvBuffQueue.Switch();
            while (!recvBuffQueue.Empty())
            {
                var recvBufferRaw = recvBuffQueue.Pop();
                int ret = kcp.Input(recvBufferRaw, recvBufferRaw.Length);

                if (ret < 0)
                {
                    Debuger.LogError("收到不正确的KCP包, Ret:{0}", ret);
                    return;
                }

                needKcpUpdateFlag = true;
                for (int size = kcp.PeekSize(); size > 0; size = kcp.PeekSize())
                {
                    var recvBuffer = new byte[size];
                    if (kcp.Recv(recvBuffer) > 0)
                    {
                        lastRecvTimestamp = (uint)TimeUtility.GetTotalMillisecondsSince1970();
                        var data = ProtoBuffUtility.Deserialize<FSPDataSToC>(recvBuffer);
                        if (recvListener != null)
                        {
                            foreach (FSPFrameData frame in data.frames)
                            {
                                recvListener(frame);
                            }
                        }
                    }
                }
            }
        }

        public void VerifyAuth()
        {
            SendFSP(0, FSPProtoCmd.Auth, authID);
        }

        public bool SendFSP(int clientFrameID, int cmd, int arg)
        {
            return SendFSP(clientFrameID, cmd, new [] {arg});
        }

        public bool SendFSP(int clientFrameID, int cmd, int[] args)
        {
            if (isRunning)
            {
                FSPMessage msg = tempSendData.msgs[0];
                msg.cmd = cmd;
                msg.clientFrameID = clientFrameID;
                msg.args = args;
                int length = ProtoBuffUtility.Serialize(tempSendData, sendBufferTemp);
                kcp.Send(sendBufferTemp, length);
                return length > 0;
            }
            return false;
        }

        private void CheckTimeOut()
        {
            uint current = (uint)TimeUtility.GetTotalMillisecondsSince1970();
            if ((current - lastRecvTimestamp) > TimeOut)
            {
                isWaitReconnect = true;
            }
        }

        public void Tick()
        {
            if (!isRunning)
            {
                return;
            }
            DoReceiveInMain();
            uint current = (uint)TimeUtility.GetTotalMillisecondsSince1970();
            if (needKcpUpdateFlag || current >= nextKcpUpdateTime)
            {
                if (kcp != null)
                {
                    kcp.Update(current);
                    nextKcpUpdateTime = kcp.Check(current);
                    needKcpUpdateFlag = false;
                }
            }
            if (isWaitReconnect)
            {
                if (NetworkInterface.GetIsNetworkAvailable())
                {
                    Reconnect();
                }
                else
                {
                    Debuger.Log("等待重连，但是网络不可用！");
                }
            }
            CheckTimeOut();
        }
        
        public string ToDebugString()
        {
            return $"ip:{ip}, port:{port}";
        }
    }
}