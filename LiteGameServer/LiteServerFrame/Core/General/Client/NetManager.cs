using System;
using System.Collections.Generic;
using System.Reflection;
using CommonData.Code;
using CommonData.Data;
using GameFramework.Debug;
using LiteServerFrame.Core.General.Base;
using LiteServerFrame.Utility;

namespace LiteServerFrame.Core.General.Client
{
    public class NetManager
    {
        class ListenerHelper
        {
            public uint cmd;
            public Type typeMsg;
            public Delegate successHandle;
            public Delegate errorHandle;
            public float timeout;
            public float timestamp;
        }

        private IConnection connection;
        private uint uid;
        private RPCManager rpcManager;
        private string currInvokingName;
        private Dictionary<uint, ListenerHelper> ntfHelpers; //服务器推送的监听
        private Dictionary<uint, ListenerHelper> rspHelpers; //一问一答监听
        private float lastCheckTimeoutStamp = 0;

        public void Init(enProtocolType protocolType, int connID, int bindPort)
        {
            Debuger.Log("connId:{0}, bindPort:{1}", connID, bindPort);
            ntfHelpers = new Dictionary<uint, ListenerHelper>();
            rspHelpers = new Dictionary<uint, ListenerHelper>();
            if (protocolType == enProtocolType.KCP)
            {
                connection = new KCPConnection();
            }
            else if (protocolType == enProtocolType.TCP)
            {
                connection = new TCPConnection();
            }
            connection.Init(connID, bindPort);
            rpcManager = new RPCManager();
            rpcManager.Init();
        }

        public void Clean()
        {
            ntfHelpers.Clear();
            rspHelpers.Clear();
            rpcManager.Clean();
            connection?.Clean();
            connection = null;
        }

        public void SetUserID(uint id)
        {
            this.uid = id;
        }

        public void Close()
        {
            connection?.Close();
        }

        public void Connect(string ip, int port)
        {
            if (connection.Connected)
            {
                Debuger.Log("当前已有连接，先关闭旧的连接");
                return;
            }
            connection.Connect(ip, port);
            connection.onReceive = OnReceive;
        }

        public void Tick()
        {
            connection?.Tick();
            CheckTimeOut();
        }

        private void CheckTimeOut()
        {
            float currentTime = TimeUtility.GetTimeSinceStartup();
            if (currentTime - lastCheckTimeoutStamp >= 5)
            {
                lastCheckTimeoutStamp = currentTime;
                foreach (KeyValuePair<uint, ListenerHelper> listenerHelper in rspHelpers)
                {
                    var currentHelper = listenerHelper.Value;
                    float dt = currentTime - currentHelper.timestamp;
                    if (dt >= currentHelper.timeout)
                    {
                        rspHelpers.Remove(listenerHelper.Key);
                        currentHelper.errorHandle?.DynamicInvoke(enNetErrorCode.Timeout);
                    }
                    Debuger.LogWarning("cmd:{0} Is Timeout!", currentHelper.cmd);
                }
            }
        }

        private void OnReceive(byte[] bytes, int len)
        {
            NetMessage msg = new NetMessage();
            msg.Deserialize(bytes, len);
            if (msg.Head.cmd == 0)
            {
                RPCMessage rpcmsg = ProtoBuffUtility.Deserialize<RPCMessage>(msg.content);
                HandleRPCMessage(rpcmsg);
            }
            else
            {
                HandlePrptoMessage(msg);
            }
        }

        //--------------------------------------------------------------------------

        public void RegisterRPCListener(object listener)
        {
            rpcManager.RegisterListener(listener);
        }

        public void UnRegisterRPCListener(object listener)
        {
            rpcManager.UnRegisterListener(listener);
        }

        private void HandleRPCMessage(RPCMessage rpcmsg)
        {
            Debuger.Log("Connection[{0}]-> {1}({2})", connection.ID, rpcmsg.name, rpcmsg.args);
            var helper = rpcManager.GetMethodHelper(rpcmsg.name);
            if (helper != null)
            {
                object[] args = rpcmsg.args;
                var rawargs = rpcmsg.rawargs;
                var paramInfo = helper.method.GetParameters();

                if (rawargs.Count == paramInfo.Length)
                {
                    for (int i = 0; i < rawargs.Count; i++)
                    {
                        if (rawargs[i].type == enRPCArgType.PBObject)
                        {
                            var type = paramInfo[i].ParameterType;
                            object arg = ProtoBuffUtility.Deserialize(type, rawargs[i].rawValue);
                            args[i] = arg;
                        }
                    }
                    currInvokingName = rpcmsg.name;
                    try
                    {
                        helper.method.Invoke(helper.listener, BindingFlags.NonPublic, null, args, null);
                    }
                    catch (Exception e)
                    {
                        Debuger.LogError("RPC调用出错：{0}: {1}\n{2}", rpcmsg.name, e.Message, e.StackTrace);
                    }
                    currInvokingName = null;
                }
                else
                {
                    Debuger.LogWarning("参数数量不一致！{0}", rpcmsg.name);
                }
            }
            else
            {
                Debuger.LogWarning("RPC不存在！{0} ", rpcmsg.name);
            }
        }

        public void Invoke(string name, params object[] args)
        {
            RPCMessage rpcmsg = new RPCMessage
            {
                name = name,
                args = args
            };
            byte[] buffer = ProtoBuffUtility.Serialize(rpcmsg);

            NetMessage msg = new NetMessage
            {
                Head = new ProtocolHead
                {
                    uid = uid,
                    dataSize = (ushort) buffer.Length
                },
                content = buffer
            };
            byte[] tmp = null;
            int len = msg.Serialize(out tmp);
            connection.Send(tmp, len);
        }

        public void Return(params object[] args)
        {
            if (connection != null)
            {
                var name = "On" + currInvokingName;
                RPCMessage rpcmsg = new RPCMessage
                {
                    name = name,
                    args = args
                };
                byte[] buffer = ProtoBuffUtility.Serialize(rpcmsg);

                NetMessage msg = new NetMessage
                {
                    Head = new ProtocolHead
                    {
                        uid = uid,
                        dataSize = (ushort) buffer.Length
                    },
                    content = buffer
                };

                byte[] tmp = null;
                int len = msg.Serialize(out tmp);
                connection.Send(tmp, len);
            }
        }

        public void ReturnError(string errinfo, int errcode = 1)
        {
            var name = "On" + currInvokingName + "Error";
            RPCMessage rpcmsg = new RPCMessage
            {
                name = name,
                args = new object[] {errinfo, errcode}
            };
            byte[] buffer = ProtoBuffUtility.Serialize(rpcmsg);

            NetMessage msg = new NetMessage
            {
                Head = new ProtocolHead {dataSize = (ushort) buffer.Length},
                content = buffer
            };

            byte[] tmp = null;
            int len = msg.Serialize(out tmp);
            connection.Send(tmp, len);
        }

        //--------------------------------------------------------------------------


        //--------------------------------------------------------------------------

        private void HandlePrptoMessage(NetMessage message)
        {
            if (message.Head.index == 0) //监听命令
            {
                if (ntfHelpers.ContainsKey(message.Head.cmd))
                {
                    var helper = ntfHelpers[message.Head.cmd];
                    if (helper != null)
                    {
                        object obj = ProtoBuffUtility.Deserialize(helper.typeMsg, message.content);
                        if (obj != null)
                        {
                            helper.successHandle.DynamicInvoke(obj);
                        }
                        else
                        {
                            Debuger.LogError("反序列化失败！ cmd:{0}", message.Head.cmd);
                        }
                    }
                    else
                    {
                        Debuger.LogError("未找到对应的监听者! cmd:{0}", message.Head.cmd);
                    }
                }
                else
                {
                    Debuger.LogError("未找到对应的监听者! cmd:{0}", message.Head.cmd);
                }
            }
            else //一问一答
            {
                if (rspHelpers.ContainsKey(message.Head.index))
                {
                    var helper = rspHelpers[message.Head.index];
                    if (helper != null)
                    {
                        rspHelpers.Remove(message.Head.index);
                        object obj = ProtoBuffUtility.Deserialize(helper.typeMsg, message.content);
                        if (obj != null)
                        {
                            helper.successHandle.DynamicInvoke(obj);
                        }
                        else
                        {
                            Debuger.LogError("反序列化失败！ cmd:{0}", message.Head.cmd);
                        }
                    }
                    else
                    {
                        Debuger.LogError("未找到对应的监听者! cmd:{0}", message.Head.cmd);
                    }
                }
                else
                {
                    Debuger.LogError("未找到对应的监听者! cmd:{0}", message.Head.cmd);
                }
            }
        }

        //一问一答方式
        public void Send<TReq, TRsp>(uint cmd, TReq req, Action<TRsp> successHandle, float timeOut = 30,
            Action<enNetErrorCode> errorHandle = null)
        {
            NetMessage message = new NetMessage
            {
                Head =
                {
                    index = MessageIndexGenerator.GetNextMessageIndex(),
                    cmd = cmd,
                    uid = uid
                },
                content = ProtoBuffUtility.Serialize(req)
            };
            message.Head.dataSize = (ushort) message.content.Length;
            byte[] temp;
            int len = message.Serialize(out temp);
            connection.Send(temp, len);
            AddListener(cmd, successHandle, message.Head.index, timeOut, errorHandle);
        }

        private void AddListener<TRsp>(uint cmd, Action<TRsp> successHandle, uint index, float timeout, Action<enNetErrorCode> errorHandle)
        {
            ListenerHelper helper = new ListenerHelper()
            {
                cmd = cmd,
                typeMsg = typeof(TRsp),
                successHandle = successHandle,
                errorHandle = errorHandle,
                timeout = timeout,
                timestamp = TimeUtility.GetTimeSinceStartup()
            };
            rspHelpers.Add(index, helper);
        }

        public void Send<TReq>(uint cmd, TReq req)
        {
            NetMessage meaasge = new NetMessage
            {
                Head =
                {
                    index = 0,
                    cmd = cmd,
                    uid = uid
                },
                content = ProtoBuffUtility.Serialize(req)
            };
            meaasge.Head.dataSize = (ushort) meaasge.content.Length;
            byte[] temp;
            int len = meaasge.Serialize(out temp);
            connection.Send(temp, len);
        }

        public void AddListener<TNtf>(uint cmd, Action<TNtf> callBack)
        {
            ListenerHelper helper = new ListenerHelper()
            {
                typeMsg = typeof(TNtf),
                successHandle = callBack
            };
            ntfHelpers.Add(cmd, helper);
        }

        //--------------------------------------------------------------------------
    }
}