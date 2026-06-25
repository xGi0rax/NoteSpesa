using Ergon.Models;
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Ergon.Services
{
    public class ErgDatabase
    {
        private readonly SQLiteConnection Database;
        private static readonly object _locker = new object();

        public ErgDatabase()
        {
            string db_path = Path.Combine(FileSystem.AppDataDirectory, Constants.DB_NAME);
            Database = new SQLiteConnection(db_path);
        }
        public void BeginTrans()
        {
            lock (_locker)
            {
                if (!Database.IsInTransaction)
                {
                    Database.BeginTransaction();
                }
            }
        }
        public void CommitTrans()
        {
            lock (_locker)
            {
                if (Database.IsInTransaction)
                {
                    Database.Commit();
                }
            }
        }
        public void RollbackTrans()
        {
            lock (_locker)
            {
                if (Database.IsInTransaction)
                {
                    Database.Rollback();
                }
            }
        }

        private void CreateTableIfNotExists<T>() where T : class, new()
        {
            lock (_locker) { Database.CreateTable<T>(); }
        }
        public bool Insert<T>(T entity) where T : class, new()
        {
            lock (_locker)
            {
                try
                {
                    CreateTableIfNotExists<T>();
                    Database.Insert(entity);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }
        public int InsertAll<T>(IEnumerable<T> items) where T : class, new()
        {
            lock (_locker)
            {
                CreateTableIfNotExists<T>();
                return Database.InsertAll(items);
            }
        }
        public int Delete<T>(Expression<Func<T, bool>> predicate) where T : class, new()
        {
            lock (_locker)
            {
                CreateTableIfNotExists<T>();
                var table = GetTable<T>();
                return table.Delete(predicate);
            }
        }
        public void DeleteAll<T>() where T : class, new()
        {
            lock (_locker)
            {
                CreateTableIfNotExists<T>();
                Database.DeleteAll<T>();
            }
        }
        public int Update<T>(T item) where T : new()
        {
            lock (_locker) { return Database.Update(item); }
        }
        public int UpdateAll<T>(IEnumerable<T> items) where T : class, new()
        {
            lock (_locker) { return Database.UpdateAll(items); }
        }
        private TableQuery<T> GetTable<T>() where T : class, new()
        {
            lock (_locker) { 
                CreateTableIfNotExists<T>();
                return Database.Table<T>();
            }
        }
        public List<T> GetAll<T>() where T : class, new()
        {
            lock (_locker)
            {
                var table = GetTable<T>();
                return [.. table];
            }
        }

        public List<T> GetFiltered<T>(Expression<Func<T, bool>> predicate) where T : class, new()
        {
            lock (_locker)
            {
                var table = GetTable<T>();
                return [.. table.Where(predicate)];
            }
        }
        public void DropTable<T>() where T : class, new()
        {
            lock (_locker) { Database.DropTable<T>(); }
        }
        public void DropAllTables()
        {
            lock (_locker)
            {
                Database.DropTable<Timbratura>();
                Database.DropTable<Planning>();
                Database.DropTable<Presenza>();
                Database.DropTable<Anavoci>();
                Database.DropTable<Dipendente>();
                Database.DropTable<Tabgen>();
                Database.DropTable<Cliente>();
                Database.DropTable<Prenotazione>();
                Database.DropTable<SpesaDettaglio>();
            }
        }
    }
}
