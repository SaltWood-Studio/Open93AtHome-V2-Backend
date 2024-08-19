using SQLite;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Open93AtHome.Modules.Database
{
    public class DatabaseHandler
    {

        private SQLiteConnection _db;

        public DatabaseHandler()
        {
            _db = new SQLiteConnection("./database.sqlite");
        }

        public void CreateTable<T>() => _db.CreateTable<T>();

        public void AddEntity<T>(T instance) => _db.Insert(instance);

        public T? GetEntity<T>(object pk) where T : class, new() // 避免插进去拔不出来
        {
            try
            {
                return _db.Get<T>(pk);
            }
            catch
            {
                return null;
            }
        }

        public IEnumerable<T> GetEntities<T>() where T : new()
        {
            var items = _db.Query<T>($"SELECT * FROM {typeof(T).Name}");
            
            foreach (var item in items)
            {
                yield return item;
            }
        }

        public int RemoveEntity<T>(object primaryKey) => this._db.Delete<T>(primaryKey);

        public int RemoveAll<T>() => this._db.DeleteAll<T>();
        public int Update(object? obj) => this._db.Update(obj);
    }
}
