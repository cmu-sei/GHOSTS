// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Ghosts.Domain.Code.Helpers;
using Newtonsoft.Json;
using MySql.Data.MySqlClient;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Text;
using Ghosts.Client.Infrastructure;


namespace Ghosts.Client.Handlers;

public class Database : BaseHandler
{
    private DatabaseTargets _currentDbTargets;
    private int _jitterFactor;
    private int _insertProbability = 30;
    private int _deleteProbability = 20;
    private int _queryProbability = 50;

    private int _queryLimit = 0;

    private int _maxRows = 0;
    private int _port = 3306;
    private bool _isMsSql = false;

    private DatabaseContentManager contentManager;
    

    public Database(TimelineHandler handler)
    {
        try
        {
            base.Init(handler);
            contentManager = new DatabaseContentManager();
            if (handler.HandlerArgs != null)
            {
                
                if (handler.HandlerArgs.TryGetValue("DatabaseTargets", out var databasetargetsArg))
                {
                    try
                    {
                        _currentDbTargets = JsonConvert.DeserializeObject<DatabaseTargets>(databasetargetsArg.ToString());
                    }
                    catch (Exception e)
                    {
                        Log.Error(e);
                    }
                }
                if (handler.HandlerArgs.TryGetValue("query-limit", out var limitArg))
                {
                    try
                    {
                        _queryLimit = int.Parse(limitArg.ToString());
                        if (_queryLimit < 0) _queryLimit = 0;
                    }
                    catch (Exception e)
                    {
                        Log.Error(e);
                    }
                }

                if (handler.HandlerArgs.TryGetValue("max-rows", out var maxRowsArg))
                {
                    try
                    {
                        _maxRows = int.Parse(maxRowsArg.ToString());
                        if (_maxRows < 0) _maxRows = 0;
                    }
                    catch (Exception e)
                    {
                        Log.Error(e);
                    }
                }

                if (handler.HandlerArgs.ContainsKeyWithOption("ismssql", "true"))
                {
                    _isMsSql = true;
                }

                if (handler.HandlerArgs.TryGetValue("port", out var portArg))
                {
                    try
                    {
                        _port = int.Parse(portArg.ToString());
                        if (_port < 0) _port = 3306;
                    }
                    catch (Exception e)
                    {
                        Log.Error(e);
                    }
                }
                else
                {
                    // port not specified
                    if (_isMsSql)
                    {
                        _port = 1433;  // set default port for Microsoft SQL Server
                    }
                }

                if (handler.HandlerArgs.TryGetValue("insert-probability", out var insertArg))
                {
                    try
                    {
                        _insertProbability = int.Parse(insertArg.ToString());
                        if (! (_insertProbability >= 0 || _insertProbability <= 100)) _insertProbability = 30;
                    }
                    catch (Exception e)
                    {
                        Log.Error(e);
                    }
                }

                if (handler.HandlerArgs.TryGetValue("delete-probability", out var deleteArg))
                {
                    try
                    {
                        _deleteProbability = int.Parse(deleteArg.ToString());
                        if (! (_deleteProbability >= 0 || _deleteProbability <= 100)) _deleteProbability = 20;
                    }
                    catch (Exception e)
                    {
                        Log.Error(e);
                    }
                }

                if (handler.HandlerArgs.TryGetValue("query-probability", out var queryArg))
                {
                    try
                    {
                        _queryProbability = int.Parse(deleteArg.ToString());
                        if (! (_queryProbability >= 0 || _queryProbability <= 100)) _queryProbability = 20;
                    }
                    catch (Exception e)
                    {
                        Log.Error(e);
                    }
                }

                
                if (handler.HandlerArgs.TryGetValue("delay-jitter", out var jitterArg))
                {
                    _jitterFactor = Jitter.JitterFactorParse(jitterArg.ToString());
                }
            }

            if (_currentDbTargets == null)
            {
                Log.Error("Database:: No credentials supplied, either CredentialsFile or Credentials must be supplied in handler args, exiting.");
                return;
            }

            if (handler.Loop)
            {
                while (true)
                {
                    Ex(handler);
                }
            }
            else
            {
                Ex(handler);
            }
        }
        catch (ThreadAbortException)
        {
            Log.Trace("Database closing...");
        }
        catch (Exception e)
        {
            Log.Error(e);
        }

    }

    private void Ex(TimelineHandler handler)
    {
        foreach (var timelineEvent in handler.TimeLineEvents)
        {
            WorkingHours.Is(handler);

            if (timelineEvent.DelayBeforeActual > 0)
                Thread.Sleep(timelineEvent.DelayBeforeActual);

            
            Log.Trace($"Database Command: {timelineEvent.Command} with delay after of {timelineEvent.DelayAfterActual}");

            switch (timelineEvent.Command)
            {
                case "random":
                    var cmd = timelineEvent.CommandArgs[_random.Next(0, timelineEvent.CommandArgs.Count)];
                    if (!string.IsNullOrEmpty(cmd.ToString()))
                    {
                        ExecuteDatabase(handler, timelineEvent, cmd.ToString());
                    }
                    
                    break;
            }
            if (timelineEvent.DelayAfterActual > 0) {
                Thread.Sleep(timelineEvent.DelayAfterActual);
            }
            
        }
    }

    /*
    Operations supported are insert, query, delete. If the database has less than
    10 rows then the first operation will insert 10 new rows to initialize the
    db. A delete operation deletes one row. A query returns at most _queryLimit
    rows from some random offset in the db.

    The first thing done by all operations is to read the number of records in
    the selected database/Table.

    */

    private void ExecuteDatabase(TimelineHandler handler, TimelineEvent timelineEvent, string command)
    {
        var charSeparators = new char[] { '|' };
        var cmdArgs = command.Split(charSeparators, 3, StringSplitOptions.None);
        var hostIp = cmdArgs[0];
        var dbKey = cmdArgs[1];
        var username = _currentDbTargets.GetUsername(dbKey);
        var password = _currentDbTargets.GetPassword(dbKey);
        var databases = _currentDbTargets.GetDatabases(dbKey);

        string dbCmd = "noaction";

        if (username == null || password == null)
        {
            Log.Error($"Database:: Missing username or password for database key '{dbKey}', skipping.");
            return;
        }

        if (databases == null || databases.Count == 0)
        {
            Log.Error($"Database:: No database schema data for database key '{dbKey}', skipping.");
            return;
        }

        var database = databases[_random.Next(0,databases.Count)];
        if (database.Tables == null || database.Tables.Count == 0)
        {
            Log.Error($"Database:: No tables defined for database {database.Name} for database key '{dbKey}', skipping.");
            return;
        }
        var table = database.Tables[_random.Next(0,database.Tables.Count )];
        
        string connstring;

        if (_isMsSql) {
            connstring = $"Server={hostIp},{_port};Database={database.Name};User ID={username};Password={password};TrustServerCertificate=True";
            Log.Trace($"Database:: Beginning MSSQL Database operation to host: {hostIp} with command: {command}");
        } else
        {
            connstring = $"Server={hostIp};Port={_port};Database={database.Name};User ID={username};Password={password}";
            Log.Trace($"Database:: Beginning MySQL Database operation to host: {hostIp} with command: {command}");
        }

         

    
        try
        {

            int rowCount = GetNumberOfRows(connstring, table, hostIp);
            string action = null;
            if (rowCount == 0)
            {
                // DB is empty. Initialize with 10 new records
                if (_isMsSql)
                {
                    for (int i = 0; i < 10; i++)
                    {
                        // For MS SQL insert one row at a time
                        InsertRow(connstring, table, hostIp, 1); 
                    }
                    
                }
                else InsertRow(connstring, table, hostIp, 10);
            } else if (_maxRows > 0 && rowCount > _maxRows)
            {
                // above max rows, do some deletion
                action = "delete";
            } else
            {
                action = GetNextAction();
            }

            if (action == "insert")
            {
                InsertRow(connstring, table, hostIp, 1);
            } else if (action == "query")
            {
                if (_isMsSql) MsSqlQueryTable(connstring, table, hostIp, rowCount);
                else QueryTable(connstring, table, hostIp, rowCount);
            } else if (action == "delete")
            {
                DeleteRow(connstring, table, hostIp);
            }
            if (action != null)
            {
                dbCmd = action;
            }
        }
        catch (ThreadAbortException)
        {
            throw;
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
        

        Report(new ReportItem { Handler = handler.HandlerType.ToString(), Command = hostIp, Arg = dbCmd, Trackable = timelineEvent.TrackableId });
    }

    /// <summary>
    /// Parse range string of format "min, max"
    /// Put  parsed result into column table under min, max
    /// </summary>
    /// <param name="column"></param>
    /// <param name="rangeString"></param>
    private int GetRangeValue(Dictionary<string, string>column, string rangeString)
    {
        var charSeparators = new char[] { ',' };
        var rangeMinMax = rangeString.Split(charSeparators, 2, StringSplitOptions.None);
        int aMin = 0, aMax = 100;
        if (rangeMinMax.Length == 2)
        {
            
            if (int.TryParse(rangeMinMax[0], out int min))
            {
                aMin = min;
            }
            if (int.TryParse(rangeMinMax[1], out int max))
            {
                aMax = max;
            }
        }

        return(_random.Next(aMin, aMax));
    }

    private string GetChoiceValue(Dictionary<string, string>column, string choiceString)
    {
        var charSeparators = new char[] { ',' };
        var choices = choiceString.Split(charSeparators);
        var index = _random.Next(0, choices.Length);
        return choices[index];
    }

    private string GenerateRowContent(DatabaseTable table)
    {
        string rowContent = "";
        for( int i = 0; i < table.Columns.Count; i++)
        {

            string columnValue;
            Dictionary<string, string> column = table.Columns[i];
            if (column.TryGetValue("Range", out var rangeString))
            {
                columnValue = GetRangeValue(column, rangeString).ToString();
            } else if (column.TryGetValue("ContentHint", out var category))
            {
                columnValue = $"'{contentManager.GetCategoryValue(category)}'";
            } else if (column.TryGetValue("Choice", out var choiceString))
            {
                columnValue = $"'{GetChoiceValue(column, choiceString)}'";
            } else
            {
                columnValue = "'noValueSpecified'";
            }
            if (rowContent == "")
            {
                rowContent = columnValue;
            } else
            {
                rowContent = $"{rowContent}, {columnValue}";
            }
        }
        return rowContent;
    }

    private void MsSqlQueryTable(string connstring, DatabaseTable table, string hostIp, int rowCount)
    {

        string query = $"SELECT * FROM {table.Name}";
        if (_queryLimit != 0)
        {
            if (rowCount < 2*_queryLimit)
            {
                query = $"{query} ORDER BY id ASC OFFSET 0 ROWS FETCH NEXT {_queryLimit} ROWS ONLY";
            } else
            {
                // use offset
                int offset = _random.Next(0, rowCount-_queryLimit);
                query = $"{query} ORDER BY id ASC OFFSET {offset} ROWS FETCH NEXT {_queryLimit} ROWS ONLY";
            }
        }
        query = query + ";";
        using (SqlConnection connection = new SqlConnection(connstring))
        {
            connection.Open();
            Log.Trace($"Database:: Successfully opened connection to host: {hostIp}.");
            using (SqlCommand cmd = new SqlCommand(query, connection))
            {
                using (var reader = cmd.ExecuteReader()) {
                    Log.Trace($"Database:: Reading query result");
                    while (reader.Read()) {
                        var rString = "";
                        foreach (Dictionary<string, string> column in table.Columns) {
                            var cName = column["Name"];
                            var value = reader[cName];
                            if (rString == "") rString = $"Database::>  {cName}: {value}";
                                else rString += $"\t{cName}: {value}";
                        }
                        Log.Trace($"{rString}");
                    }
                }
                Log.Trace($"Database:: Successful database operation query to host: {hostIp}.");
            }
        }
    }

    private void QueryTable(string connstring, DatabaseTable table, string hostIp, int rowCount)
    {
        string query = $"SELECT * FROM {table.Name}";
        if (_queryLimit != 0)
        {
            if (rowCount < 2*_queryLimit)
            {
                query = $"{query} LIMIT 0,{_queryLimit}";
            } else
            {
                // use offset
                int offset = _random.Next(0, rowCount-_queryLimit);
                query = $"{query} LIMIT {offset},{_queryLimit}";
            }
        }
        query = query + ";";
        using (MySqlConnection connection = new MySqlConnection(connstring))
        {
            connection.Open();
            Log.Trace($"Database:: Successfully opened connection to host: {hostIp}.");
            using (MySqlCommand cmd = new MySqlCommand(query, connection))
            {
                using (var reader = cmd.ExecuteReader()) {
                    Log.Trace($"Database:: Reading query result");
                    while (reader.Read()) {
                        var rString = "";
                        foreach (Dictionary<string, string> column in table.Columns) {
                            var cName = column["Name"];
                            var value = reader[cName];
                            if (rString == "") rString = $"Database::>  {cName}: {value}";
                                else rString += $"\t{cName}: {value}";
                        }
                        Log.Trace($"{rString}");
                    }
                }
                Log.Trace($"Database:: Successful database operation query to host: {hostIp}.");
            }
        }
    }

    /// <summary>
    /// This always deletes the first row in the table
    /// </summary>
    /// <param name="connstring"></param>
    /// <param name="table"></param>
    /// <param name="hostIp"></param>
    private void DeleteRow(string connstring, DatabaseTable table, string hostIp)
    {
        string query;
        if (_isMsSql)
        {
            query = $"SELECT TOP 1 id FROM {table.Name} ORDER BY id;";
        } else {
            query = $"SELECT id FROM {table.Name} LIMIT 0,1;";
        }
        int idVal = ExecuteScalarQueryInt(connstring, query, hostIp, "idquery");
        query = $"DELETE FROM {table.Name} WHERE id = {idVal};";
        ExecuteScalarQuery(connstring, query, hostIp, "idquery");
    }

    private void InsertRow(string connstring, DatabaseTable table, string hostIp, int numRows)
    {
        
        string columnNames = table.GetColumnList();
        string query = $"INSERT INTO {table.Name} ({columnNames}) VALUES ";
        string values = "";
        for (int i = 0; i < numRows; i++)
        {
            string oneRowValue = GenerateRowContent(table);
            if (values == "")
            {
                values = $"({oneRowValue})";
            } else
            {
                values = $"{values}, ({oneRowValue})";
            }
            if (i == numRows - 1)
            {
                values = values + ";";
            } 
            
        }
        query = $"{query} {values}";
        ExecuteScalarQuery(connstring, query, hostIp, "insert");
    }

    private void ExecuteScalarQuery(string connstring, string query, string hostIp, string operation)
    {
         // any error is caught by the caller
        if (_isMsSql)
        {
            using (SqlConnection connection = new SqlConnection(connstring))
            {
                connection.Open();
                Log.Trace($"Database:: Successfully opened connection to host: {hostIp}.");
                using (SqlCommand cmd = new SqlCommand(query, connection))
                {
                    cmd.ExecuteScalar();
                }
                Log.Trace($"Database:: Successful database operation {operation} to host: {hostIp}.");
            }
        } else {
            using (MySqlConnection connection = new MySqlConnection(connstring))
            {
                connection.Open();
                Log.Trace($"Database:: Successfully opened connection to host: {hostIp}.");
                using (MySqlCommand cmd = new MySqlCommand(query, connection))
                {
                    cmd.ExecuteScalar();
                }
                Log.Trace($"Database:: Successful database operation {operation} to host: {hostIp}.");
            }
        }
    }

    private int ExecuteScalarQueryInt(string connstring, string query, string hostIp, string operation)
    {
        // any error is caught by the caller
        int rval;
        if (_isMsSql)
        {
            using (SqlConnection connection = new SqlConnection(connstring))
            {
                connection.Open();
                Log.Trace($"Database:: Successfully opened connection to host: {hostIp}.");
                using (SqlCommand cmd = new SqlCommand(query, connection))
                {
                    rval = Convert.ToInt32(cmd.ExecuteScalar());
                }
                Log.Trace($"Database:: Successful database operation {operation} to host: {hostIp}.");
            }
            
        } else {
            using (MySqlConnection connection = new MySqlConnection(connstring))
            {
                connection.Open();
                Log.Trace($"Database:: Successfully opened connection to host: {hostIp}.");
                using (MySqlCommand cmd = new MySqlCommand(query, connection))
                {
                    rval = Convert.ToInt32(cmd.ExecuteScalar());
                }
                Log.Trace($"Database:: Successful database operation {operation} to host: {hostIp}.");
            }
        }
        return rval;
    }

    private int GetNumberOfRows(string connstring, DatabaseTable table, string hostIp)
    {
        string query = $"SELECT COUNT(*) FROM {table.Name}";
        return ExecuteScalarQueryInt(connstring, query, hostIp, "rowcount");
    }

    private string GetNextAction()
    {
        var choice = _random.Next(0, 101);
        string action = null;
        int endRange;
        var startRange = 0;

        if (_deleteProbability > 0)
        {
            endRange = _deleteProbability;
            if (choice >= startRange && choice <= endRange) action = "delete";
            else startRange = endRange + 1;
        }

        if (action == null && _insertProbability > 0)
        {
            endRange = startRange + _insertProbability;
            if (choice >= startRange && choice <= endRange) action = "insert";
            else startRange = endRange + 1;
        }

        if (action == null && _queryProbability > 0)
        {
            endRange = startRange + _queryProbability;
            if (choice >= startRange && choice <= endRange) action = "query";
        }
        return action;
    }



    private class DatabaseTable
    {
        public string Name { get; set; }
        public List<Dictionary<string, string>> Columns { get; set; }

        public string allColumns { get; set; } = null;

        /// <summary>
        ///  Return a comma seperated string of all columns suitable for a SQL query.
        /// Column names are sorted alpha order.
        /// Saves values in allColumns since the column names list never changes
        /// </summary>
        /// <returns></returns>
        public string GetColumnList()
        {
            if (allColumns  == null)
            {
                foreach (Dictionary<string, string> column in Columns)
                {
                    
                    if (allColumns == null)
                    {
                        allColumns = column["Name"];
                    } else
                    {
                        allColumns = $"{allColumns}, {column["Name"]}";
                    }
                }
                
            }
            return allColumns;
            
        }
    }
    private class DatabaseSchema
    {
        public string Name { get; set; }
        public List<DatabaseTable> Tables { get; set; }
    }
    
    private class DatabaseHost
    {

        public string Username { get; set; }
        public string Password { get; set; }
        public List<DatabaseSchema> Databases { get; set; }
    }

    
    private class DatabaseTargets
    {

        public string Version { get; set; }
        public Dictionary<string, DatabaseHost> Data { get; set; }

        public string GetUsername(string dbHostKey)
        {

            if (Data != null && Data.TryGetValue(dbHostKey, out DatabaseHost value))
            {
                return value.Username;
            }
            return null;
        }

        
        public string GetPassword(string dbHostKey)
        {
            if (Data != null && Data.TryGetValue(dbHostKey, out DatabaseHost value))
            {
                if (value.Password != null) return Encoding.UTF8.GetString(Convert.FromBase64String(value.Password));
            }
            return null;
        }

        public List<DatabaseSchema> GetDatabases(string dbHostKey)
        {
            if (Data != null && Data.TryGetValue(dbHostKey, out DatabaseHost value))
            {
                return value.Databases;
            }
            return null;
        }
        

    }




}
