﻿namespace LiteServerFrame.Core.General.Server
{
    public interface ISessionListener
    {
        void OnReceive(ISession session, byte[] bytes, int len);
    }
}