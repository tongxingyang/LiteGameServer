using System;
using System.Collections.Generic;
using System.Text;
using CommonData.Proto;
using GameFramework.Debug;
using LiteServerFrame.Utility;

namespace LiteServerFrame.Core.General.FSP.Server
{
    public class FSPManager
    {
        private long lastTicks = 0;
        private bool useCustomEnterFrame;
        private FSPParam param = new FSPParam();
        private FSPGateWay gateway;
        private Dictionary<uint, FSPGame> mapGame;
        private uint lastClearGameTime = 0;
        
        public void Init(int port)
        {
            Debuger.Log("port:{0}", port);
            gateway = new FSPGateWay();
            gateway.Init(port);
            param.port = gateway.Port;
            param.host = gateway.Host;
            mapGame = new Dictionary<uint, FSPGame>();
        }

        public void Clean()
        {
            mapGame.Clear();
        }
        
        public void SetFrameInterval(int serverFrameInterval, int clientFrameRateMultiple) //MS
        {
            Debuger.Log("serverFrameInterval:{0}, clientFrameRateMultiple:{1}", serverFrameInterval, clientFrameRateMultiple);
            param.serverFrameInterval = serverFrameInterval;
            param.clientFrameRateMultiple = clientFrameRateMultiple;
        }
        
        public void SetServerTimeout(int serverTimeout)
        {
            param.serverTimeout = serverTimeout;
        }

        public int GetFrameInterval()
        {
            return param.serverFrameInterval;
        }

        public FSPGame CreateGame(uint gameId, int authId)
        {
            Debuger.Log("gameId:{0}, auth:{1}", gameId, authId);
            FSPGame game = new FSPGame();
            game.Create(gameId, authId);
            mapGame.Add(gameId, game);
            return game;
        }
        
        public void ReleaseGame(uint gameId)
        {
            Debuger.Log("gameId:{0}", gameId);
            if (mapGame.ContainsKey(gameId))
            {
                FSPGame game = mapGame[gameId];
                if (game != null)
                {
                    game.Release();
                    mapGame.Remove(gameId);
                }
            }
        }

        public uint AddPlayer(uint gameId, uint playerId)
        {
            var game = mapGame[gameId];
            var session = gateway.CreateSession();
            game.AddPlayer(playerId, session);
            return session.SessionID;
        }
        
        public List<uint> AddPlayer(uint gameId, List<uint> listPlayerId)
        {
            var game = mapGame[gameId];
            List<uint> listSid = new List<uint>();
            foreach (uint player in listPlayerId)
            {
                var session = gateway.CreateSession();
                game.AddPlayer(player, session);
                listSid.Add(session.SessionID);
            }
            return listSid;
        }
        
        public void Tick()
        {
            gateway.Tick();
            uint current = (uint)TimeUtility.GetTotalMillisecondsSince1970();

            if (current - lastClearGameTime > FSPGame.ActiveTimeout * 1000 / 2)
            {
                lastClearGameTime = current;
                ClearNoActiveGame();
            }

            long nowticks = DateTime.Now.Ticks;
            long interval = nowticks - lastTicks;

            long frameIntervalTicks = param.serverFrameInterval * 10000;
            if (interval > frameIntervalTicks)
            {
                lastTicks = nowticks - (nowticks % (frameIntervalTicks));
                if (!useCustomEnterFrame)
                {
                    EnterFrame();
                }
            }
        }

        public void EnterFrame()
        {
            if (gateway.IsRunning)
            {
                foreach (KeyValuePair<uint,FSPGame> keyValuePair in mapGame)
                {
                    keyValuePair.Value.EnterFrame();
                }
            }
        }
        
        private void ClearNoActiveGame()
        {
            foreach (KeyValuePair<uint,FSPGame> keyValuePair in mapGame)
            {
                if (keyValuePair.Value.IsGameEnd())
                {
                    mapGame.Remove(keyValuePair.Key);
                }
            }
        }
        
        public void Dump()
        {
            gateway.Dump();
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("\nFSPParam:{0}", param.ToString("\t"));
            sb.AppendLine("\nGameList:");
            foreach (var game in mapGame)
            {
                sb.AppendFormat("\n\tGame {0}", game.Value.ToString("\t\t"));
            }
            Debuger.LogWarning(sb.ToString());
        }
    }
}