using System.Collections.Generic;
using CommonData.Data;
using LiteGameServer.Code;
using NHibernate;
using NHibernate.Criterion;

namespace LiteGameServer.Data.Handle
{
    public class AccountDataHandle
    {
        public void AddAccount(AccountData user)
        {
            using (ISession session = DataHelper.OpenSession())
            {
                using (ITransaction transaction = session.BeginTransaction())
                {
                    session.Save(user);
                    transaction.Commit();
                }
            }
        }

        public void Update(AccountData user)
        {
            using (ISession session = DataHelper.OpenSession())
            {
                using (ITransaction transaction = session.BeginTransaction())
                {
                    session.Update(user);
                    transaction.Commit();
                }
            }
        }

        public void Delete(AccountData user)
        {
            using (ISession session = DataHelper.OpenSession())
            {
                using (ITransaction transaction = session.BeginTransaction())
                {
                    session.Delete(user);
                    transaction.Commit();
                }
            }
        }

        public AccountData GetById(int id)
        {
            using (ISession session = DataHelper.OpenSession())
            {
                using (ITransaction transaction = session.BeginTransaction())
                {
                    AccountData user = session.Get<AccountData>(id);
                    transaction.Commit();
                    return user;
                }
            }
        }

        public AccountData GetByUsername(string username)
        {
            using (ISession session = DataHelper.OpenSession())
            {
                AccountData user = session.CreateCriteria(typeof(AccountData)).Add(Restrictions.Eq("AccountName", username)).UniqueResult<AccountData>();
                return user;
            }
        }

        public ICollection<AccountData> GetAllUsers()
        {
            using (ISession session = DataHelper.OpenSession())
            {
                IList<AccountData> users = session.CreateCriteria(typeof(AccountData)).List<AccountData>();
                return users;
            }
        }

        public bool VerifyUser(string username, string password)
        {
            using (ISession session = DataHelper.OpenSession())
            {
                AccountData user = session
                    .CreateCriteria(typeof(AccountData))
                    .Add(Restrictions.Eq("AccountName", username))
                    .Add(Restrictions.Eq("Passworld", password))
                    .UniqueResult<AccountData>();
                if (user == null) return false;
                return true;
            }
        }
    }
}
