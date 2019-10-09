using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using GameFramework.Debug;
using LiteServerFrame.Core.General.Base;
using LiteServerFrame.Utility;

namespace LiteServerFrame.Core.General.FSP.Server
{
    public class FSPGateWay
    {
        private bool isRunning = false;
        private Thread threadRecv;
        private Socket systemSocket;
        private byte[] recvBufferTemp = new byte[4096];
        private NetBufferReader recvBufferTempReader = new NetBufferReader();
        private int port;
        private Dictionary<uint, FSPSession> mapSession;
        
        public bool IsRunning => isRunning;
        public int Port
        {
            get
            {
                var ipEndPoint = systemSocket.LocalEndPoint as IPEndPoint;
                if (ipEndPoint != null)
                    return ipEndPoint.Port;
                return 0;
            }
        }

        public string Host =>  IPUtility.SelfIP;
        
        public void Init(int port)
        {
            Debuger.Log("port:{0}", port);
            this.port = port;
            mapSession = new Dictionary<uint, FSPSession>();
            Start();
        }

        public void Clean()
        {
            mapSession.Clear();
            Close();
        }
        
        public void Start()
        {
            isRunning = true;
            systemSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            systemSocket.Bind(IPUtility.GetIPEndPointAny(AddressFamily.InterNetwork, port));
            threadRecv = new Thread(ThreadRecv) { IsBackground = true };
            threadRecv.Start();
        }

        public void Close()
        {
            isRunning = false;
            if (threadRecv != null)
            {
                threadRecv.Interrupt();
                threadRecv = null;
            }

            if (systemSocket != null)
            {
                try
                {
                    systemSocket.Shutdown(SocketShutdown.Both);

                }
                catch (Exception e)
                {
                    Debuger.LogWarning(e.Message + e.StackTrace);
                }
                systemSocket.Close();
                systemSocket = null;
            }
        }
        
        public FSPSession CreateSession()
        {
            Debuger.Log();
            uint sid = FSPSessionIDGenerator.GetNextSessionID();
            FSPSession session = new FSPSession(sid, HandleSessionSend);
            mapSession.Add(sid, session);
            return session;
        }
        
        public FSPSession GetSession(uint sid)
        {
            FSPSession session = null;
            lock (mapSession)
            {
                if (mapSession.ContainsKey(sid))
                {
                    session = mapSession[sid];
                }
            }
            return session;
        }
        
        
        private void HandleSessionSend(IPEndPoint remoteEndPoint, byte[] buffer, int size)
        {
            if (systemSocket != null)
            {
                int cnt = systemSocket.SendTo(buffer, 0, size, SocketFlags.None, remoteEndPoint);
            }
            else
            {
                Debuger.LogError("Socket已经关闭！");
            }
        }
        
        private void ThreadRecv()
        {
            while (IsRunning)
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
            int cnt = systemSocket.ReceiveFrom(recvBufferTemp, recvBufferTemp.Length, SocketFlags.None, ref remotePoint);

            if (cnt > 0)
            {
                
                recvBufferTempReader.Attach(recvBufferTemp, cnt);
                byte[] m_32b = new byte[4];
                recvBufferTempReader.ReadBytes(m_32b, 0, 4);
                uint sid = BitConverter.ToUInt32(m_32b, 0);

                lock (mapSession)
                {
                    FSPSession session = null;
                    if (sid == 0)
                    {
                        Debuger.LogError("基于KCP的Sid为0，该包需要被丢掉");
                    }
                    else
                    {
                        if(mapSession.ContainsKey(sid))
                            session = mapSession[sid];
                    }

                    if (session != null)
                    {
                        session.Active(remotePoint as IPEndPoint);
                        session.DoReceiveInGateway(recvBufferTemp, cnt);
                    }
                    else
                    {
                        Debuger.LogWarning("无效的包! sid:{0}", sid);
                    }
                }

            }
        }

        private uint lastClearSessionTime = 0;
        
        public void Tick()
        {
            if (IsRunning)
            {
                lock (mapSession)
                {
                    uint current = (uint)TimeUtility.GetTotalMillisecondsSince1970();

                    if (current - lastClearSessionTime > FSPSession.ActiveTimeout * 1000 / 2)
                    {
                        lastClearSessionTime = current;
                        ClearNoActiveSession();
                    }

                    foreach (KeyValuePair<uint,FSPSession> keyValuePair in mapSession)
                    {
                        keyValuePair.Value.Tick(current);
                    }
                }
            }
        }
        
        private void ClearNoActiveSession()
        {
            lock (mapSession)
            {
                foreach (KeyValuePair<uint,FSPSession> keyValuePair in mapSession)
                {
                    var session = keyValuePair;
                    if (!session.Value.IsActived())
                    {
                        mapSession.Remove(session.Key);
                    }
                }
            }
        }

        public void Dump()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var pair in mapSession)
            {
                var session = pair.Value;
                sb.AppendLine("\t" + session.ToString());
            }
            Debuger.LogWarning("\nFSPGateway Sessions ({0}):\n{1}", mapSession.Count, sb);

        }
    }
}