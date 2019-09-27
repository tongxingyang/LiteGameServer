using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using GameFramework.Debug;
using LiteServerFrame.Core.General.Base;
using LiteServerFrame.Utility;

namespace LiteServerFrame.Core.General.Server
{
    public class TCPSession : ISession
    {
        public static int ActiveTimeout = 30;
        
        public uint id { get; }
        public uint uid { get; set; }
        public Socket ConnentSocket { get; set; }
        public ushort ping { get; set; }
        public IPEndPoint remoteEndPoint { get; set; }
        
        //收到消息后交付处理的对象
        private ISessionListener listener;
        private Action<ISession,string> closeAction;
        private Action<SocketAsyncEventArgs> sendProcessAction;
        private List<byte> readBuffer;
        Queue<byte[]> writeBuffer;
        public SocketAsyncEventArgs receiveSAEA;
        public SocketAsyncEventArgs sendSAEA;
        private bool isReading = false;
        private bool isWriting = false;
        private int lastActiveTime = 0;
        private bool active = false;
        
        
        public TCPSession(uint sid, ISessionListener listener,Action<ISession,string> close, Action<SocketAsyncEventArgs> sendProcess)
        {
            id = sid;
            this.listener = listener;
            closeAction = close;
            sendProcessAction = sendProcess;
            writeBuffer = new Queue<byte[]>();
            readBuffer = new List<byte>();
            receiveSAEA = new SocketAsyncEventArgs();
            sendSAEA = new SocketAsyncEventArgs();
            receiveSAEA.UserToken = this;
            sendSAEA.UserToken = this;
            receiveSAEA.SetBuffer(new byte[4096], 0, 4096);
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

        public void SetSocket(Socket socket)
        {
            ConnentSocket = socket;
        }
        
        public bool IsAuth()
        {
            return uid > 0;
        }

        public void SetAuth(uint userid)
        {
            uid = userid;
        }
        
        public void Tick(uint currentTimeMS)
        {
            
        }
        
        public bool Send(byte[] bytes, int len)
        {
            if (ConnentSocket != null)
            {
                bytes = LengthEncoding.LengthEncode(bytes);
                writeBuffer.Enqueue(bytes);
                if (!isWriting)
                {
                    isWriting = true;
                    SendDataHandle();
                }
            }
            else
            {
                closeAction(this, "socket 不存在");
                return false;
            }
            return true;
        }

        public void Send()
        {
            if (!IsActived())
            {
                Debuger.LogWarning("Session已经不活跃了！");
                return;
            }
            SendDataHandle();
        }

        private void SendDataHandle()
        {
            if (writeBuffer.Count == 0)
            {
                isWriting = false;
                return;
            }
            byte[] date = writeBuffer.Dequeue();
            sendSAEA.SetBuffer(date, 0, date.Length);
            bool result = ConnentSocket.SendAsync(sendSAEA);
            if (!result)
            {
                sendProcessAction(sendSAEA);
            }
        }

        public void DoReceiveInGateway(byte[] buffer, int len)
        {
            readBuffer.AddRange(buffer);
            if (!isReading)
            {
                isReading = true;
                ReceiveDataHandle();
            }
        }

        private void ReceiveDataHandle()
        {
            byte[] data = LengthEncoding.LengthDecode(ref readBuffer);
            if (data == null)
            {
                isReading = false;
                return;
            }
            listener.OnReceive(this, data, data.Length);
            ReceiveDataHandle();
        }
        
        public void Clean()
        {
            listener = null;
            readBuffer.Clear();
            readBuffer = null;
            writeBuffer.Clear();
            writeBuffer = null;
            receiveSAEA.Dispose();
            sendSAEA.Dispose();
            closeAction = null;
            sendProcessAction = null;
            if (ConnentSocket != null)
            {
                try
                {
                    ConnentSocket.Shutdown(SocketShutdown.Both);
                    ConnentSocket.Close();
                    ConnentSocket = null;
                }
                catch (Exception e)
                {
                    Debuger.LogWarning(e);
                }
            }
        }
    }
}