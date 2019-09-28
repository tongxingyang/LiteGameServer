using System.Collections.Generic;
using CommonData.Data;
using LiteGameServer.Data.Handle;

namespace LiteGameServer.Data.Cache
{
    public class AccountDataCache
    {
        private Dictionary<string, AccountData> AccountDataDic = new Dictionary<string, AccountData>();

        public AccountDataHandle AccountDataHandle = new AccountDataHandle();

        public AccountData GetAccountData(string name)
        {
            AccountData accountModel = null;

            if (AccountDataDic.ContainsKey(name))
            {
                accountModel = AccountDataDic[name];
                return accountModel;
            }

            accountModel = AccountDataHandle.GetByUsername(name);
            if (accountModel != null)
            {
                AccountDataDic[name] = accountModel;
            }
            return accountModel;
        }

        public bool HasAccountData(string name)
        {
            bool ret = AccountDataDic.ContainsKey(name);
            if (ret == false)
            {
                AccountData accountModel = AccountDataHandle.GetByUsername(name);
                if (accountModel != null)
                {
                    AccountDataDic[name] = accountModel;
                    ret = true;
                }
                else
                {
                    ret = false;
                }
            }
            return ret;
        }

        public bool AccountMatch(string name, string passworld)
        {
            AccountData accountModel = GetAccountData(name);
            if (accountModel.Passworld == passworld)
            {
                return true;
            }
            return false;
        }

        public void UpdateAccount(AccountData accountModel)
        {
            AccountDataHandle.Update(accountModel);
            AccountDataDic[accountModel.AccountName] = accountModel;
        }

        public void RemoveAccountCache(string name)
        {
            if (AccountDataDic.ContainsKey(name))
            {
                AccountDataDic.Remove(name);
            }
        }
    }
}
