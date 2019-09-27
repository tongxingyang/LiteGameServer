using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using GameFramework.Debug;
using LiteServerFrame.Core.General.Base;
using LiteServerFrame.Utility;

namespace LiteServerFrame.Core.General.IPC
{
    public class IPCManager
    {
        private IPCInfo ipcInfo;
        private Socket currentSocket;
        private Thread threadRecv;
        private byte[] byteBuffer = new byte[4096];
        private Queue<byte[]> recvBufferQueue;
        private bool isRunning;
        private RPCManager rpcManager;
        private string currInvokingName;
        private int currInvokingSrc;

        public void Init(int id)
        {
            recvBufferQueue = new Queue<byte[]>();
            ipcInfo = IPCConfig.GetIPCInfo(id);
            rpcManager = new RPCManager();
            rpcManager.Init();
        }

        public void Clean()
        {
            Stop();
            ipcInfo = null;
            lock (recvBufferQueue)
            {
                recvBufferQueue.Clear();
                recvBufferQueue = null;
            }
            byteBuffer = null;
            rpcManager.Clean();
            rpcManager = null;
        }

        public void Stop()
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

        public void Start()
        {
            try
            {
                currentSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                currentSocket.Bind(IPUtility.GetIPEndPointAny(AddressFamily.InterNetwork, ipcInfo.Port));
                isRunning = true;
                threadRecv = new Thread(ThreadRecv) { IsBackground = true };
                threadRecv.Start();
            }
            catch (Exception e)
            {
                Debuger.LogError(e.Message + e.StackTrace);
                Stop();
            }
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
            int cnt = currentSocket.ReceiveFrom(byteBuffer, byteBuffer.Length, SocketFlags.None, ref remotePoint);
            if (cnt > 0)
            {
                byte[] dst = new byte[cnt];
                Buffer.BlockCopy(byteBuffer, 0, dst, 0, cnt);

                lock (recvBufferQueue)
                {
                    recvBufferQueue.Enqueue(dst);
                }                
            }
        }
        
        private void DoReceiveInMain()
        {
            lock (recvBufferQueue)
            {
                if (recvBufferQueue.Count > 0)
                {
                    byte[] buffer = recvBufferQueue.Dequeue();

                    IPCMessage msg = ProtoBuffUtility.Deserialize<IPCMessage>(buffer);
                    HandleMessage(msg);
                }
            }
        }

        public void Tick()
        {
            DoReceiveInMain();
        }

        private void SendMessage(int dstID, byte[] bytes, int len)
        {
            int dstPort = IPCConfig.GetIPCInfo(dstID).Port;
            IPEndPoint ep = IPUtility.GetHostEndPoint("127.0.0.1", dstPort);
            currentSocket.SendTo(bytes, 0, len, SocketFlags.None, ep);
        }
        
        public void AddRPCListener(object listener)
        {
            rpcManager.RegisterListener(listener);
        }

        public void RemoveRPCListener(object listener)
        {
            rpcManager.UnRegisterListener(listener);
        }

        private void HandleMessage(IPCMessage msg)
        {
            RPCMessage rpcmsg = msg.rpcMessage;
            var helper = rpcManager.GetMethodHelper(rpcmsg.name);
            if (helper != null)
            {
                object[] args  = new object[rpcmsg.args.Length +1];
                List<RPCArg> rawargs = rpcmsg.rawargs;
                ParameterInfo[] paramInfo = helper.method.GetParameters();
                if (args.Length == paramInfo.Length)
                {
                    for (int i = 0; i < rawargs.Count; i++)
                    {
                        if (rawargs[i].type == enRPCArgType.PBObject)
                        {
                            args[i + 1] = ProtoBuffUtility.Deserialize(paramInfo[i + 1].ParameterType, rawargs[i].rawValue);
                        }
                        else
                        {
                            args[i + 1] = rawargs[i].value;
                        }
                    }
                    args[0] = msg.srcID;
                    currInvokingName = rpcmsg.name;
                    currInvokingSrc = msg.srcID;
                    try
                    {
                        helper.method.Invoke(helper.listener, BindingFlags.NonPublic, null, args, null);
                    }
                    catch (Exception e)
                    {
                        Debuger.LogError("RPC调用出错：{0}\n{1}", e.Message, e.StackTrace);
                    }
                    currInvokingName = "";
                    currInvokingSrc = 0;
                }
                else
                {
                    Debuger.LogWarning("参数数量不一致！");
                }
            }
            else
            {
                Debuger.LogWarning("RPC不存在！");
            }
        }
        
        public void Return(params object[] args)
        {
            var name = "On" + currInvokingName;
            RPCMessage rpcmsg = new RPCMessage
            {
                name = name,
                args = args
            };

            IPCMessage msg = new IPCMessage
            {
                srcID = ipcInfo.ID,
                rpcMessage = rpcmsg
            };

            byte[] temp = ProtoBuffUtility.Serialize(msg);
            SendMessage(currInvokingSrc, temp, temp.Length);
        }
        
        public void ReturnError(string errinfo, int errcode = 1)
        {
            var name = "On" + currInvokingName + "Error";
            RPCMessage rpcmsg = new RPCMessage
            {
                name = name,
                args = new object[] {errinfo, errcode}
            };

            IPCMessage msg = new IPCMessage
            {
                srcID = ipcInfo.ID,
                rpcMessage = rpcmsg
            };
            byte[] temp = ProtoBuffUtility.Serialize(msg);
            SendMessage(currInvokingSrc, temp, temp.Length);
        }
        
        public void Invoke(int dstID, string name, params object[] args)
        {
            RPCMessage rpcmsg = new RPCMessage
            {
                name = name,
                args = args
            };

            IPCMessage msg = new IPCMessage
            {
                srcID = ipcInfo.ID,
                rpcMessage = rpcmsg
            };

            byte[] temp = ProtoBuffUtility.Serialize(msg);
            SendMessage(dstID, temp, temp.Length);
        }

        
    }
}