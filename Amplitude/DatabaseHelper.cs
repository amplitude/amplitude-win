using SQLite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Amplitude
{
    class Event
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Text { get; set; }
    }

    class DatabaseHelper
    {
        static string DB_PATH = Path.Combine(Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "amplitude.sqlite"));

        public DatabaseHelper()
        {
            Debug.WriteLine(GetConnectionString());
            using (var db = new SQLiteConnection(GetConnectionString()))
            {
                db.CreateTable<Event>();
            }
        }

        public string GetConnectionString()
        {
            // This doesn't work because sqlite-net doesn't seem to support the string
            // return "Data Source=" + DB_PATH + "; Version=3; Pooling=True; Max Pool Size=100;";
            return DB_PATH;
        }

        public SQLiteConnection GetConnection()
        {
            return new SQLiteConnection(GetConnectionString());
        }

        public int AddEvent(string evt)
        {
            using (var db = GetConnection())
            {
                try
                {
                    Event e = new Event()
                    {
                        Text = evt
                    };
                    int result = db.Insert(e);
                    if (result == 0)
                    {
                        Debug.WriteLine("Insert failed");
                        return -1;
                    }
                    return e.Id;
                }
                catch (SQLiteException e)
                {
                    Debug.WriteLine("AddEvent failed");
                    Debug.WriteLine(e);
                }
            }
            return -1;
        }

        public int GetEventCount()
        {
            using (var db = GetConnection())
            {
                try
                {
                    return db.Table<Event>().Count();
                }
                catch (SQLiteException e)
                {
                    Debug.WriteLine("GetNumberRows failed");
                    Debug.WriteLine(e);
                }
            }
            return 0;
        }

        public Tuple<int, IEnumerable<Event>> GetEvents(int lessThanId, int limit)
        {
            using (var db = GetConnection())
            {
                try
                {
                    var query = db.Table<Event>();
                    if (lessThanId >= 0)
                    {
                        query = query.Where(e => e.Id < lessThanId);
                    }
                    query = query.OrderBy(e => e.Id);
                    if (limit >= 0)
                    {
                        query = query.Take(limit);
                    }
                    var results = query.ToList();
                    if (results.Count() > 0)
                    {
                        return new Tuple<int, IEnumerable<Event>>(results.Last().Id, results);
                    }
                }
                catch (SQLiteException e)
                {
                    Debug.WriteLine("GetEvents failed", e);
                    Debug.WriteLine(e);
                }
            }
            return new Tuple<int, IEnumerable<Event>>(-1, new List<Event>());
        }

        public int GetNthEventId(int n)
        {
            using (var db = GetConnection())
            {
                try
                {
                    Event e = db.Table<Event>().OrderBy(i => i.Id).Skip(n - 1).Take(1).First();
                    return e.Id;
                }
                catch (SQLiteException e)
                {
                    Debug.WriteLine("GetNthEventId failed");
                    Debug.WriteLine(e);
                }
            }
            return -1;
        }

        public void RemoveEvents(int maxId)
        {
            using (var db = GetConnection())
            {
                try
                {
                    db.Execute("DELETE FROM Event WHERE Id <= ?", maxId);
                }
                catch (SQLiteException e)
                {
                    Debug.WriteLine("RemoveEvemts failed");
                    Debug.WriteLine(e);
                }
            }
        }

        public void RemoveEvent(int id)
        {
            using (var db = GetConnection())
            {
                try
                {
                    db.Execute("DELETE FROM Event WHERE Id = ?", id);
                }
                catch (SQLiteException e)
                {
                    Debug.WriteLine("RemoveEvent failed");
                    Debug.WriteLine(e);
                }
            }
        }
    }
}
