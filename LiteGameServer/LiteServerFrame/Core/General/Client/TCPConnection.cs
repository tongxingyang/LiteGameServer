using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using GameFramework.Debug;
using LiteServerFrame.Core.General.Base;
using LiteServerFrame.Utility;

namespace LiteServerFrame.Core.General.Client
{
    public class TCPConnection : IConnection
    {
        public Action<byte[], int> onReceive { get; set; }
        public bool Connected { get; set; }
        public int ID { get; set; }
        public int Port { get; set; }
        public string IP { get; set; }
        private int remotePort;
        private string remoteIp;
        private IPEndPoint remotEndPoint;
        private Socket currentSocket;
        private byte[] cacheBuffer;
        private List<byte> receiveBuffer;
        private bool isReading = false;
        
        public void Init(int connId, int bindPort)
        {
            cacheBuffer = new byte[4096];
            receiveBuffer = new List<byte>();
            ID = connId;
            Port = bindPort;
        }

        public void Clean()
        {
            receiveBuffer.Clear();
            receiveBuffer = null;
            cacheBuffer = null;
            onReceive = null;
            Close();
        }

        public void Connect(string ip, int port)
        {
            IP = ip;
            remoteIp = ip;
            remotePort = port;
            Connected = true;
            remotEndPoint = IPUtility.GetHostEndPoint(ip, port);
            try
            {
                currentSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                currentSocket.Bind(new IPEndPoint(IPAddress.Any, Port));
                currentSocket.Connect(ip, port);
                currentSocket.BeginReceive(cacheBuffer, 0, cacheBuffer.Length, SocketFlags.None, ReceiveCallBack,
                    cacheBuffer);
            }
            catch (Exception e)
            {
                Debuger.LogError(e);
            }
           
        }

        private void ReceiveCallBack(IAsyncResult ar)
        {
            try
            {
                int length = currentSocket.EndReceive(ar);
                byte[] message = new byte[length];
                Buffer.BlockCopy(cacheBuffer, 0, message, 0, length);
                receiveBuffer.AddRange(message);
                if (!isReading)
                {
                    isReading = true;
                    ReceiveDataHandle();
                }
                currentSocket.BeginReceive(cacheBuffer, 0, cacheBuffer.Length, SocketFlags.None, ReceiveCallBack, cacheBuffer);
            }
            catch (Exception e)
            {
                Debuger.LogError("断开连接........."+e.Message);
                currentSocket.Shutdown(SocketShutdown.Both);
                currentSocket.Close();
                currentSocket = null;
            }
        }

        private void ReceiveDataHandle()
        {
            byte[] data = LengthEncoding.LengthDecode(ref receiveBuffer);
            if (data == null)
            {
                isReading = false;
                return;
            }
            onReceive(data, data.Length);
            ReceiveDataHandle();
        }

        public void Close()
        {
            Connected = false;
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

        public bool Send(byte[] bytes, int len)
        {
            if (!Connected || currentSocket == null)
            {
                return false;
            }
            bytes = LengthEncoding.LengthEncode(bytes);
            currentSocket.Send(bytes);
            return true;
        }

        public void Tick()
        {
            
        }
    }
}