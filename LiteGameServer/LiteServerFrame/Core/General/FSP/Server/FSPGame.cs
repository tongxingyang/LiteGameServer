using System;
using System.Collections.Generic;
using System.Text;
using CommonData.Proto;
using GameFramework.Debug;

namespace LiteServerFrame.Core.General.FSP.Server
{
    public class FSPGame
    {
        public static int ActiveTimeout = 10;
        public Action<uint> onGameExit;
        public Action<int> onGameEnd;
        private const int MaxPlayerNum = 31;
        
        private FSPGameState state;
        private int stateParam1;
        private int stateParam2;
        public FSPGameState State => state;
        private int authID;
        private uint gameID;
        
        private int gameBeginFlag = 0;
        private int roundBeginFlag = 0;
        private int controlStartFlag = 0;
        private int roundEndFlag = 0;
        private int gameEndFlag = 0;
        
        private int curRoundId = 0;
        public int CurrentRoundId { get { return curRoundId; } }
        private FSPFrameData lockedFrame = new FSPFrameData();
        private int curFrameId = 0;
        public uint GameID => gameID;

        private List<FSPPlayer> listPlayer = new List<FSPPlayer>();
        private List<FSPPlayer> listPlayersExitOnNextFrame = new List<FSPPlayer>();
        
        public void Create(uint gameId, int authId)
        {
            authId = authId;
            gameID = gameId;
            curRoundId = 0;
            SetGameState(FSPGameState.Create);
        }
        
        public void Release()
        {
            SetGameState(FSPGameState.None);
            foreach (FSPPlayer player in listPlayer)
            {
                player.Release();
            }
            listPlayer.Clear();
            listPlayersExitOnNextFrame.Clear();
            onGameExit = null;
            onGameEnd = null;
        }
        
        public FSPPlayer AddPlayer(uint playerId, FSPSession session)
        {
            if (state != FSPGameState.Create)
            {
                Debuger.LogError("当前状态下无法AddPlayer! State = {0}", state);
                return null;
            }

            FSPPlayer player = null;
            for (int i = 0; i < listPlayer.Count; i++)
            {
                player = listPlayer[i];
                if (player.ID == playerId)
                {
                    Debuger.LogWarning("PlayerId已经存在！用新的替代旧的! PlayerId = " + playerId);
                    listPlayer.RemoveAt(i);
                    player.Release();
                    break;
                }
            }

            if (listPlayer.Count >= MaxPlayerNum)
            {
                Debuger.LogError("已经达到最大玩家数了! MaxPlayerNum = {0}", MaxPlayerNum);
                return null;
            }

            player = new FSPPlayer();
            player.Create(playerId, authID, session, OnRecvFromPlayer);
            listPlayer.Add(player);

            return player;
        }
        
        private FSPPlayer GetPlayer(uint playerId)
        {
            foreach (FSPPlayer t in listPlayer)
            {
                var player = t;
                if (player.ID == playerId)
                {
                    return player;
                }
            }
            return null;
        }
        
        public int GetPlayerCount()
        {
            return listPlayer.Count;
        }

        public List<FSPPlayer> GetPlayerList()
        {
            return listPlayer;
        }
        
        private void OnRecvFromPlayer(FSPPlayer player, FSPMessage msg)
        {
            HandleClientCmd(player, msg);
        }
        
        protected virtual void HandleClientCmd(FSPPlayer player, FSPMessage msg)
        {

            uint playerId = player.ID;
            if (!player.HasAuthed)
            {
                if (msg.cmd == FSPProtoCmd.Auth)
                {
                    player.SetAuth(msg.args[0]);
                }
                else
                {
                    Debuger.LogWarning("当前Player未鉴权，无法处理该Cmd：{0}", msg.cmd);
                }
                return;
            }

            switch (msg.cmd)
            {
                case FSPProtoCmd.GameBegin:
                {
                    Debuger.Log("GAME_BEGIN, playerId = {0}, cmd = {1}", playerId, msg);
                    SetFlag(playerId, ref gameBeginFlag, "GameBeginFlag");
                    break;
                }
                case FSPProtoCmd.RoundBegin:
                {
                    Debuger.Log("ROUND_BEGIN, playerId = {0}, cmd = {1}", playerId, msg);
                    SetFlag(playerId, ref roundBeginFlag, "RoundBeginFlag");
                    break;
                }
                case FSPProtoCmd.ControlStart:
                {
                    Debuger.Log("CONTROL_START, playerId = {0}, cmd = {1}", playerId, msg);
                    SetFlag(playerId, ref controlStartFlag, "ControlStartFlag");
                    break;
                }
                case FSPProtoCmd.RoundEnd:
                {
                    Debuger.Log("ROUND_END, playerId = {0}, cmd = {1}", playerId, msg);
                    SetFlag(playerId, ref roundEndFlag, "RoundEndFlag");
                    break;
                }
                case FSPProtoCmd.GameEnd:
                {
                    Debuger.Log("GAME_END, playerId = {0}, cmd = {1}", playerId, msg);
                    SetFlag(playerId, ref gameEndFlag, "GameEndFlag");
                    break;
                }
                case FSPProtoCmd.GameExit:
                {
                    Debuger.Log("GAME_EXIT, playerId = {0}, cmd = {1}", playerId, msg);
                    HandleGameExit(playerId, msg);
                    break;
                }
                default:
                {
                    Debuger.Log("playerId = {0}, cmd = {1}", playerId, msg);
                    AddCmdToCurrentFrame(playerId, msg);
                    break;
                }
            }
        }
        
        private void AddCmdToCurrentFrame(uint playerId, FSPMessage msg)
        {
            msg.playerID = playerId;
            lockedFrame.msgs.Add(msg);
        }
        
        private void AddBasicCmdToCurrentFrame(int cmd, int arg = 0)
        {
            FSPMessage msg = new FSPMessage
            {
                cmd = cmd,
                args = new[] {arg}
            };
            AddCmdToCurrentFrame(0, msg);
        }
        
        private void HandleGameExit(uint playerId, FSPMessage msg)
        {
            AddCmdToCurrentFrame(playerId, msg);
            FSPPlayer player = GetPlayer(playerId);
            if (player != null)
            {
                player.WaitForExit = true;
                onGameExit?.Invoke(player.ID);
            }
        }
        
        public void EnterFrame()
        {
            foreach (var player in listPlayersExitOnNextFrame)
            {
                player.Release();
            }
            listPlayersExitOnNextFrame.Clear();
            HandleGameState();
            if (state == FSPGameState.None)
            {
                return;
            }

            if (lockedFrame.frameID != 0 || !lockedFrame.IsEmpty())
            {
                for (int i = 0; i < listPlayer.Count; i++)
                {
                    FSPPlayer player = listPlayer[i];
                    player.SendToClient(lockedFrame);
                    if (player.WaitForExit)
                    {
                        listPlayersExitOnNextFrame.Add(player);
                        listPlayer.RemoveAt(i);
                        --i;
                    }
                }
            }

            if (lockedFrame.frameID == 0)
            {
                lockedFrame = new FSPFrameData();
            }

            if (state == FSPGameState.RoundBegin || state == FSPGameState.ControlStart)
            {
                curFrameId++;
                lockedFrame = new FSPFrameData {frameID = curFrameId};
            }
        }

        protected void SetGameState(FSPGameState state, int param1 = 0, int param2 = 0)
        {
            Debuger.Log(">>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>");
            Debuger.Log("{0} -> {1}, param1 = {2}, param2 = {3}", state, state, param1, param2);
            Debuger.Log("<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<");
            this.state = state;
            stateParam1 = param1;
            stateParam2 = param2;
        }

        private void HandleGameState()
        {
            switch (state)
            {
                case FSPGameState.None:
                {
                    break;
                }
                case FSPGameState.Create: 
                {
                    OnStateCreate();
                    break;
                }
                case FSPGameState.GameBegin: 
                {
                    OnStateGameBegin();
                    break;
                }
                case FSPGameState.RoundBegin: 
                {
                    OnStateRoundBegin();
                    break;
                }
                case FSPGameState.ControlStart: 
                {
                    OnStateControlStart();
                    break;
                }
                case FSPGameState.RoundEnd: 
                {
                    OnStateRoundEnd();
                    break;
                }
                case FSPGameState.GameEnd:
                {
                    OnStateGameEnd();
                    break;
                }
                default:
                    break;
            }
        }
        
        protected virtual int OnStateCreate()
        {
            if (IsFlagFull(gameBeginFlag))
            {
                SetGameState(FSPGameState.GameBegin);
                AddBasicCmdToCurrentFrame(FSPProtoCmd.GameBegin);
                return 0;
            }
            return 0;
        }
        
        protected virtual int OnStateGameBegin()
        {
            if (CheckGameAbnormalEnd())
            {
                return 0;
            }

            if (IsFlagFull(roundBeginFlag))
            {
                SetGameState(FSPGameState.RoundBegin);
                IncRoundId();
                ClearRound();
                AddBasicCmdToCurrentFrame(FSPProtoCmd.RoundBegin, curRoundId);
                return 0;
            }

            return 0;
        }
        
        protected virtual int OnStateRoundBegin()
        {
            if (CheckGameAbnormalEnd())
            {
                return 0;
            }

            if (IsFlagFull(controlStartFlag))
            {
                ResetRoundFlag();
                SetGameState(FSPGameState.ControlStart);
                AddBasicCmdToCurrentFrame(FSPProtoCmd.ControlStart);
                return 0;
            }

            return 0;
        }
        
        
        protected virtual int OnStateControlStart()
        {
            if (CheckGameAbnormalEnd())
            {
                return 0;
            }

            if (IsFlagFull(roundEndFlag))
            {
                SetGameState(FSPGameState.RoundEnd);
                ClearRound();
                AddBasicCmdToCurrentFrame(FSPProtoCmd.RoundEnd, curRoundId);
                return 0;
            }

            return 0;
        }
        
        protected virtual int OnStateRoundEnd()
        {
            if (CheckGameAbnormalEnd())
            {
                return 0;
            }

            if (IsFlagFull(gameEndFlag))
            {
                SetGameState(FSPGameState.GameEnd, (int)FSPGameEndReason.Normal);
                AddBasicCmdToCurrentFrame(FSPProtoCmd.GameEnd, (int)FSPGameEndReason.Normal);
                return 0;
            }


            if (IsFlagFull(roundBeginFlag))
            {
                SetGameState(FSPGameState.RoundBegin);
                ClearRound();
                IncRoundId();
                AddBasicCmdToCurrentFrame(FSPProtoCmd.RoundBegin, curRoundId);
                return 0;
            }

            return 0;
        }
        
        
        protected virtual int OnStateGameEnd()
        {
            if (onGameEnd != null)
            {
                onGameEnd(stateParam1);
                onGameEnd = null;
            }
            return 0;
        }
        
        public bool IsGameEnd()
        {
            return state == FSPGameState.GameEnd;
        }
        
        private bool CheckGameAbnormalEnd()
        {
            if (listPlayer.Count < 1)
            {
                SetGameState(FSPGameState.GameEnd, (int)FSPGameEndReason.AllOtherExit);
                AddBasicCmdToCurrentFrame(FSPProtoCmd.GameEnd, (int)FSPGameEndReason.AllOtherExit);
                return true;
            }

            for (int i = 0; i < listPlayer.Count; i++)
            {
                FSPPlayer player = listPlayer[i];
                if (player.IsLose())
                {
                    listPlayer.RemoveAt(i);
                    player.Release();
                    --i;
                }
            }

            if (listPlayer.Count < 1)
            {
                SetGameState(FSPGameState.GameEnd, (int)FSPGameEndReason.AllOtherLost);
                AddBasicCmdToCurrentFrame(FSPProtoCmd.GameEnd, (int)FSPGameEndReason.AllOtherLost);
                return true;
            }
            return false;
        }
        
        private void IncRoundId()
        {
            ++curRoundId;
        }

        private void ClearRound()
        {
            lockedFrame = new FSPFrameData();
            curFrameId = 0;
            ResetRoundFlag();
            foreach (FSPPlayer t in listPlayer)
            {
                t?.ClearRound();
            }
        }
        
        private void ResetRoundFlag()
        {
            roundBeginFlag = 0;
            controlStartFlag = 0;
            roundEndFlag = 0;
            gameEndFlag = 0;
        }
        
        private void SetFlag(uint playerId, ref int flag, string flagname)
        {
            flag |= (0x01 << ((int)playerId - 1));
            Debuger.Log("player = {0}, flag = {1}", playerId, flagname);
        }
        
        public bool IsFlagFull(int flag)
        {
            if (listPlayer.Count > 0)
            {
                foreach (FSPPlayer player in listPlayer)
                {
                    int playerId = (int)player.ID;
                    if ((flag & (0x01 << (playerId - 1))) == 0)
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }
        
        public string ToString(string prefix = "")
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("[{0}] AuthId:{1}, State:{2}, CurrentRound:{3}, CurrentFrameId:{4}",gameID, authID, state,
                curRoundId, curFrameId);

            sb.AppendFormat("\n{0}PlayerList:", prefix);
            foreach (FSPPlayer t in listPlayer)
            {
                sb.AppendFormat("\n{0}Player{1}", prefix, t.ToString(prefix + "\t"));
            }

            sb.AppendFormat("\n{0}ListPlayersExitOnNextFrame:", prefix);
            foreach (FSPPlayer t in listPlayersExitOnNextFrame)
            {
                sb.AppendFormat("\n{0}Player{1}", prefix, t.ToString(prefix + "\t"));
            }

            return sb.ToString();
        }

    }
    
    public enum FSPGameEndReason
    {
        Normal = 0, //正常结束
        AllOtherExit = 1, //所有其他人都主动退出了
        AllOtherLost = 2,  //所有其他人都掉线了
    }
}