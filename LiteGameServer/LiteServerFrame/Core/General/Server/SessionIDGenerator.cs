namespace LiteServerFrame.Core.General.Server
{
    public class SessionIDGenerator
    {
        private static uint lastID;

        public static uint GetNextSessionID()
        {
            return ++lastID;
        }
    }
}