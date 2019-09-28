using LiteGameServer.Data.Cache;
namespace LiteGameServer.Code
{
    public class CacheHelper
    {
        public static AccountDataCache AccountCache;

        static CacheHelper()
        {
            AccountCache = new AccountDataCache();
        }
    }
}
