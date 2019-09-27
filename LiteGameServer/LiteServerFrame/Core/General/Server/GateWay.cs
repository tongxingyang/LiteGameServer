using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using GameFramework.Debug;
using LiteServerFrame.Core.General.Base;
using LiteServerFrame.Utility;

namespace LiteServerFrame.Core.General.Server
{
    public class GateWay
    {
        private Dictionary<uint, ISession> sessions;
        private Socket currentSocket;
        private bool isRunning;
        private Thread threadRecv;
        private byte[] byteBuffer = new byte[4096];
        private ISessionListener sessionListener;
        private readonly NetBufferReader recvBufferTempReader = new NetBufferReader();
        private int port;
        private uint lastClearSessionTime = 0;
        private enProtocolType protocolType;

        public void Init(enProtocolType type, int port, ISessionListener listener)
        {
            this.port = port;
            sessionListener = listener;
            protocolType = type;
            sessions = new Dictionary<uint, ISession>();
            isRunning = true;
            if (protocolType == enProtocolType.KCP)
            {
                currentSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                currentSocket.Bind(new IPEndPoint(IPAddress.Any, this.port));
                threadRecv = new Thread(ThreadRecv) { IsBackground = true };
                threadRecv.Start();
            }
            else if(protocolType == enProtocolType.TCP)
            {
                currentSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);;
                currentSocket.Bind(new IPEndPoint(IPAddress.Any, this.port));
                currentSocket.Listen(10);
                StartAccept(null);
            }
        }

        //---------------------------------------------------------------------
        
        private void StartAccept(SocketAsyncEventArgs e)
        {
            if (e == null)
            {
                e = new SocketAsyncEventArgs();
                e.Completed += AcceptComleted;
            }
            else {
                e.AcceptSocket = null;
            }
            bool result= currentSocket.AcceptAsync(e);
            if (!result) {
                ProcessAccept(e);
            }
        }
        
        private void ProcessAccept(SocketAsyncEventArgs e)
        {
            ISession session = null;
            uint sessionid = SessionIDGenerator.GetNextSessionID();
            session = new TCPSession(sessionid, sessionListener,ClientClose,ProcessSend);
            ((TCPSession) session).receiveSAEA.Completed += IOComleted;
            ((TCPSession) session).sendSAEA.Completed += IOComleted;
            sessions.Add(session.id, session);
            session.SetSocket(e.AcceptSocket);
            sessionListener.OnClientAccept(session,(IPEndPoint)e.AcceptSocket.RemoteEndPoint);
            StartReceive(session);
            StartAccept(e);
        }
        
        public void IOComleted(object sender, SocketAsyncEventArgs e)
        {
            if (e.LastOperation == SocketAsyncOperation.Receive)
            {
                ProcessReceive(e);
            }
            else {
                ProcessSend(e);
            }
        }

        private void ProcessReceive(SocketAsyncEventArgs e)
        {
            TCPSession session= e.UserToken as TCPSession;
            if (session != null && (session.receiveSAEA.BytesTransferred > 0 && session.receiveSAEA.SocketError == SocketError.Success))
            {
                byte[] message = new byte[session.receiveSAEA.BytesTransferred];
                Buffer.BlockCopy(session.receiveSAEA.Buffer, 0, message, 0, session.receiveSAEA.BytesTransferred);
                session.Active((IPEndPoint)e.AcceptSocket.RemoteEndPoint);
                session.DoReceiveInGateway(message,message.Length);
                StartReceive(session);
            }
            else {
                if (session != null && session.receiveSAEA.SocketError != SocketError.Success)
                {
                    ClientClose(session, session.receiveSAEA.SocketError.ToString());
                }
                else {
                    ClientClose(session, "客户端主动断开连接");
                }
            }
        }
        
        private void ProcessSend(SocketAsyncEventArgs e)
        {
            TCPSession session = e.UserToken as TCPSession;
            if (e.SocketError != SocketError.Success)
            {
                ClientClose(session, e.SocketError.ToString());
            }
            else
            {
                session?.Send();
            }
        }
        
        private void ClientClose(ISession session,string error)
        {
            if (session.ConnentSocket != null) {
                lock (session) { 
                    sessionListener.OnClientClose(session, error);
                    session.Clean();
                    if (sessions.ContainsKey(session.id))
                    {
                        sessions.Remove(session.id);
                    }
                }
            }
        }
        
        private void AcceptComleted(object sender, SocketAsyncEventArgs e) 
        {
            ProcessAccept(e);
        }

        public void StartReceive(ISession session)
        {
            try
            {
                bool result = session.ConnentSocket.ReceiveAsync(((TCPSession)session).receiveSAEA);
                if (!result)
                {
                    ProcessReceive(((TCPSession)session).receiveSAEA);
                }
            }
            catch (Exception e) {
                Console.WriteLine(e.Message);
            }
        }
        
        //---------------------------------------------------------------------
        
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
            int cnt = currentSocket.ReceiveFrom(byteBuffer, byteBuffer.Length, SocketFlags.None, ref remotePoint);
            if (cnt > 0)
            {
                recvBufferTempReader.Attach(byteBuffer, cnt);
                byte[] convBytes = new byte[4];
                recvBufferTempReader.ReadBytes(convBytes, 0, 4);
                uint sessionid = BitConverter.ToUInt32(convBytes, 0);

                lock (sessions)
                {
                    ISession session = null;
                    if (sessionid == 0)
                    {
                        sessionid = SessionIDGenerator.GetNextSessionID();
                        session = new KCPSession(sessionid, HandleSessionSend, sessionListener);
                        sessions.Add(session.id, session);
                    }
                    else
                    {
                        if (sessions.ContainsKey(sessionid))
                        {
                            session = sessions[sessionid];
                        }
                    }

                    if (session != null)
                    {
                        session.Active(remotePoint as IPEndPoint);
                        session.DoReceiveInGateway(byteBuffer, cnt);
                    }
                    else
                    {
                        Debuger.LogWarning("无效的包! sessionid:{0}", sessionid);
                    }
                }
            }
        }
        
        private void HandleSessionSend(ISession session, byte[] bytes, int len)
        {
            if (currentSocket != null)
            {
                currentSocket.SendTo(bytes, 0, len, SocketFlags.None, session.remoteEndPoint);
            }
            else
            {
                Debuger.LogError("Socket已经关闭！");
            }
        }

        public void Tick()
        {
            if (isRunning)
            {
                lock (sessions)
                {
                    uint current = (uint)TimeUtility.GetTotalMillisecondsSince1970();
                    if (current - lastClearSessionTime > KCPSession.ActiveTimeout * 1000 / 2)
                    {
                        lastClearSessionTime = current;
                        ClearNoActiveSession();
                    }

                    if (protocolType == enProtocolType.KCP)
                    {
                        foreach (KeyValuePair<uint,ISession> keyValuePair in sessions)
                        {
                            keyValuePair.Value.Tick(current);
                        }
                    }
                }
            }
        }
        
        private void ClearNoActiveSession()
        {
            lock (sessions)
            {
                foreach (KeyValuePair<uint,ISession> keyValuePair in sessions)
                {
                    var session = keyValuePair;
                    if (!session.Value.IsActived())
                    {
                        session.Value.Clean();
                        sessions.Remove(session.Key);
                    }
                }
            }
        }
        
        public void Clean()
        {
            lock (sessions)
            {
                foreach (KeyValuePair<uint,ISession> keyValuePair in sessions)
                {
                    var session = keyValuePair;
                    session.Value.Clean();
                    sessions.Remove(session.Key);
                }
                sessions.Clear();
            }
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

        public ISession GetSession(uint sessionID)
        {
            ISession session = null;
            lock (sessions)
            {
                if (sessions.ContainsKey(sessionID))
                {
                    session = sessions[sessionID];
                }
            }
            return session;
        }
        
        public void Dump()
        {
            StringBuilder sb = new StringBuilder();
            var dic = sessions;
            foreach (var pair in dic)
            {
                ISession session = pair.Value;
                sb.AppendLine("\t" + session);
            }
            Debuger.LogWarning("\nGateway Sessions ({0}):\n{1}", sessions.Count, sb);
        }
    }
}