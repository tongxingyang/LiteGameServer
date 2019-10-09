using System;
using System.Collections.Generic;
using CommonData.Proto;
using GameFramework.Debug;

namespace LiteServerFrame.Core.General.FSP.Client
{
    public class FPSManager : IDebugLogTag
    {
        public string LOGTAG { get; private set; }
        
        private bool isRunning;
        private FSPClient fspClient;
        private FSPParam fspParam;
        private uint playerID;
        private Action<int, FSPFrameData> frameListener;
        private int clientCurrentFrameIndex;//客户端当前的帧索引
        private int serverLockedFrameIndex; //服务器的帧索引
        private FSPGameState fspGameState = FSPGameState.None;
        private Dictionary<int, FSPFrameData> frameDatas;
        private FSPFrameData nextLocalFrameData;
        private FSPFrameController fspFrameController;

        public FSPFrameController FSPFrameController => fspFrameController;
        public FSPGameState FspGameState => fspGameState;
        public uint PlayerId => playerID;
        public event Action<int> onGameBegin;
        public event Action<int> onRoundBegin;
        public event Action<int> onControlStart;
        public event Action<int> onRoundEnd;
        public event Action<int> onGameEnd;
        public event Action<uint> onGameExit;

        public void Start(FSPParam param, uint playerid)
        {
            fspParam = param;
            playerID = playerid;
            LOGTAG = "FSPManager[" + playerid + "]";

            if (param.useLocal)
            {
                serverLockedFrameIndex = fspParam.maxFrameID;
            }
            else
            {
                fspClient  = new FSPClient();
                fspClient.Init(fspParam.sessionID);
                fspClient.SetFSPAuthID(fspParam.authID);
                fspClient.SetFSPListener(OnFSPListener);
                fspClient.Connect(fspParam.host, fspParam.port);
                fspClient.VerifyAuth();
                serverLockedFrameIndex = fspParam.clientFrameRateMultiple - 1;
            }
            isRunning = true;
            fspGameState = FSPGameState.Create;
            frameDatas = new Dictionary<int, FSPFrameData>();
            clientCurrentFrameIndex = 0;
            fspFrameController = new FSPFrameController();
            fspFrameController.Start(param);
        }

        public void Stop()
        {
            fspGameState = FSPGameState.None;
            fspClient?.Clean();
            fspClient = null;
            frameListener = null;
            frameDatas.Clear();
            frameDatas = null;
            isRunning = false;
            onGameBegin = null;
            onRoundBegin = null;
            onControlStart = null;
            onGameEnd = null;
            onRoundEnd = null;
        }

        public void SetFrameListener(Action<int, FSPFrameData> listener)
        {
            frameListener = listener;
        }
        /// <summary>
        /// 收到一个服务器发送的 FSPFrameData 数据
        /// </summary>
        /// <param name="frame"></param>
        private void OnFSPListener(FSPFrameData frame)
        {
            AddServerFrame(frame);
        }
        
        private void AddServerFrame(FSPFrameData frame)
        {
            if (frame.frameID <= 0)
            {
                ExecuteFrame(frame.frameID, frame);
                return;
            }

            frame.frameID = frame.frameID * fspParam.clientFrameRateMultiple;
            serverLockedFrameIndex = frame.frameID + fspParam.clientFrameRateMultiple - 1;
            frameDatas.Add(frame.frameID, frame);
            fspFrameController.AddFrameID(frame.frameID);
        }

        private void ExecuteFrame(int frameId, FSPFrameData frame)
        {
            if (frame !=null && !frame.IsEmpty())
            {
                foreach (var msg in frame.msgs)
                {
                    switch (msg.cmd)
                    {
                        case FSPProtoCmd.GameBegin: HandleGameBegin(msg.args[0]);break;
                        case FSPProtoCmd.RoundBegin: HandleRoundBegin(msg.args[0]); break;
                        case FSPProtoCmd.ControlStart: HandleControlStart(msg.args[0]); break;
                        case FSPProtoCmd.RoundEnd: HandleRoundEnd(msg.args[0]); break;
                        case FSPProtoCmd.GameEnd: HandleGameEnd(msg.args[0]); break;
                        case FSPProtoCmd.GameExit: HandleGameExit(msg.playerID); break;
                    }
                }
            }
            frameListener?.Invoke(frameId, frame);
        }

        public void SendFSP(int cmd, params int[] args)
        {
            if(!isRunning) return;
            if (fspParam.useLocal)
            {
                SendFSPLocal(cmd, args);
            }
            else
            {
                fspClient.SendFSP(clientCurrentFrameIndex, cmd, args);
            }
        }

        private void SendFSPLocal(int cmd, params int[] args)
        {
            if (nextLocalFrameData == null || nextLocalFrameData.frameID != clientCurrentFrameIndex + 1)
            {
                nextLocalFrameData = new FSPFrameData { frameID = clientCurrentFrameIndex + 1 };
                frameDatas.Add(nextLocalFrameData.frameID,nextLocalFrameData);
            }
            FSPMessage msg = new FSPMessage
            {
                cmd = cmd,
                args = args,
                playerID = PlayerId
            };
            nextLocalFrameData.msgs.Add(msg);
        }

        public void Tick()
        {
            if(!isRunning) return;
            if (fspParam.useLocal)
            {
                if (serverLockedFrameIndex == 0 || serverLockedFrameIndex > clientCurrentFrameIndex)
                {
                    clientCurrentFrameIndex++;
                    ExecuteFrame(clientCurrentFrameIndex,
                        frameDatas.ContainsKey(clientCurrentFrameIndex) ? frameDatas[clientCurrentFrameIndex] : null);
                }
            }
            else
            {
                fspClient.Tick();
                int speed = fspFrameController.GetFrameSpeed(clientCurrentFrameIndex);
                while (speed>0)
                {
                    if (clientCurrentFrameIndex < serverLockedFrameIndex)
                    {
                        clientCurrentFrameIndex++;
                        ExecuteFrame(clientCurrentFrameIndex,
                            frameDatas.ContainsKey(clientCurrentFrameIndex)
                                ? frameDatas[clientCurrentFrameIndex]
                                : null);
                    }
                    speed--;
                }
            }
        }
        
        //-------------------------------------------------------------------------------
        
        public void SendGameBegin()
        {
            SendFSP(FSPProtoCmd.GameBegin, 0);
        }

        private void HandleGameBegin(int arg)
        {
            fspGameState = FSPGameState.GameBegin;
            onGameBegin?.Invoke(arg);
        }

        public void SendRoundBegin()
        {
            SendFSP(FSPProtoCmd.RoundBegin, 0);
        }

        private void HandleRoundBegin(int arg)
        {
            fspGameState = FSPGameState.RoundBegin;
            clientCurrentFrameIndex = 0;

            if (fspParam.useLocal)
            {
                serverLockedFrameIndex = fspParam.maxFrameID;
            }
            else
            {
                serverLockedFrameIndex = fspParam.clientFrameRateMultiple - 1;
            }

            frameDatas.Clear();

            onRoundBegin?.Invoke(arg);
        }

        public void SendControlStart()
        {
            SendFSP(FSPProtoCmd.ControlStart, 0);
        }
        private void HandleControlStart(int arg)
        {
            fspGameState = FSPGameState.ControlStart;
            onControlStart?.Invoke(arg);
        }

        public void SendRoundEnd()
        {
            SendFSP(FSPProtoCmd.RoundEnd, 0);
        }
        private void HandleRoundEnd(int arg)
        {
            fspGameState = FSPGameState.RoundEnd;
            onRoundEnd?.Invoke(arg);
        }

        public void SendGameEnd()
        {
            SendFSP(FSPProtoCmd.GameEnd, 0);
        }
        
        private void HandleGameEnd(int arg)
        {
            fspGameState = FSPGameState.GameEnd;
            onGameEnd?.Invoke(arg);
        }

        public void SendGameExit()
        {
            Debuger.Log();
            SendFSP(FSPProtoCmd.GameExit, 0);
        }

        private void HandleGameExit(uint playerId)
        {
            Debuger.Log(playerId);
            onGameExit?.Invoke(playerId);
        }
        
    }
}