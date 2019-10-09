namespace LiteServerFrame.Core.General.FSP.Server
{
    public class FSPSessionIDGenerator
    {
        private static uint lastID;

        public static uint GetNextSessionID()
        {
            return ++lastID;
        }
    }
}