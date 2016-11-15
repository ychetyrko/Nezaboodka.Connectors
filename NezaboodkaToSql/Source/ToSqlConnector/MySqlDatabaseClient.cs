using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using MySql.Data.MySqlClient;

namespace Nezaboodka.ToSqlConnector
{
    public class MySqlDatabaseClient
    {
        private static readonly Random RandomAddressGenerator = new Random();
        private static readonly Random RandomDelayGenerator = new Random();

        private static Dictionary<System.Type, Func<DatabaseRequest, MySqlCommand, DatabaseResponse>> _requestExec =
            new Dictionary<System.Type, Func<DatabaseRequest, MySqlCommand, DatabaseResponse>>();

        // Public

        public FileContentHandler FileContentHandler { get; set; }
        public ReadOnlyCollection<string> ServerAddresses { get; set; }
        public int CurrentServerAddressNumber { get; set; }

        public string CurrentServerAddress
        {
            get { return ServerAddresses[CurrentServerAddressNumber]; }
        }

        public ServerAddressSelectionMode ServerAddressSelectionMode { get; set; }
        public string DatabaseName { get; set; }

        public ClientTypeBinder TypeBinder { get; private set; }

        public int TimeoutInMilliseconds { get; set; } // Timeout.Infinite (-1) is set by default
        public int RetryLimit { get; set; } // number of attempts, 0 is unlimited by default
        public int MaxDelayBeforeRetryInMilliseconds { get; set; }

        public MySqlDatabaseClient(string serverAddress, string databaseName, ClientTypeBinder typeBinder)
            : this(new string[] {serverAddress}, ServerAddressSelectionMode.FirstAvailable, databaseName, typeBinder)
        {
        }

        public MySqlDatabaseClient(IList<string> serverAddresses, ServerAddressSelectionMode serverAddressSelectionMode,
            string databaseName, ClientTypeBinder typeBinder)
            : this(serverAddresses, serverAddressSelectionMode, databaseName, typeBinder, Timeout.Infinite, 0, 5000)
        {
        }

        public MySqlDatabaseClient(IList<string> serverAddresses, ServerAddressSelectionMode serverAddressSelectionMode,
            string databaseName, ClientTypeBinder typeBinder, int timeoutInMilliseconds, int retryLimit,
            int maxDelayBeforeRetryInMilliseconds)
        {
            RegisterRequests();

            FileContentHandler = new FileContentHandler();
            ServerAddresses = new ReadOnlyCollection<string>(serverAddresses);
            ServerAddressSelectionMode = serverAddressSelectionMode;
            if (serverAddressSelectionMode != ServerAddressSelectionMode.FirstAvailable)
            {
                lock (RandomAddressGenerator)
                {
                    CurrentServerAddressNumber = RandomAddressGenerator.Next(serverAddresses.Count);
                }
            }
            DatabaseName = databaseName;
            TypeBinder = typeBinder;
            TimeoutInMilliseconds = timeoutInMilliseconds;
            RetryLimit = retryLimit;
            MaxDelayBeforeRetryInMilliseconds = maxDelayBeforeRetryInMilliseconds;
            //fContext = new DatabaseClientContext();
        }

        public MySqlDatabaseClient(MySqlDatabaseClient existing)
            : this(existing, existing.TypeBinder)
        {
        }

        public MySqlDatabaseClient(MySqlDatabaseClient existing, ClientTypeBinder typeBinder)
        {
            RegisterRequests();

            FileContentHandler = existing.FileContentHandler;
            ServerAddresses = existing.ServerAddresses;
            ServerAddressSelectionMode = existing.ServerAddressSelectionMode;
            CurrentServerAddressNumber = existing.CurrentServerAddressNumber;
            if (ServerAddressSelectionMode != ServerAddressSelectionMode.FirstAvailable)
            {
                lock (RandomAddressGenerator)
                {
                    CurrentServerAddressNumber = RandomAddressGenerator.Next(ServerAddresses.Count);
                }
            }
            DatabaseName = existing.DatabaseName;
            TypeBinder = typeBinder;
            TimeoutInMilliseconds = existing.TimeoutInMilliseconds;
            RetryLimit = existing.RetryLimit;
            MaxDelayBeforeRetryInMilliseconds = existing.MaxDelayBeforeRetryInMilliseconds;
            //fContext = new DatabaseClientContext(existing.fContext);
        }

        // Administration

        public EnvironmentConfiguration GetEnvironmentConfiguration()
        {
            return null; // null <= not an N*.Server
        }

        public IList<string> GetDatabaseList()
        {
            var request = new GetDatabaseListRequest();
            var response = (GetDatabaseListResponse) ExecuteRequest(request);
            IList<string> result = response.DatabaseNames;
            if (result == null)
                result = new List<string>();
            return result;
        }

        public IList<string> AlterDatabaseList(IList<string> databaseNamesToAdd,
            IList<string> databaseNamesToRemove)
        {
            var request = new AlterDatabaseListRequest();
            request.DatabaseNamesToAdd = databaseNamesToAdd;
            request.DatabaseNamesToRemove = databaseNamesToRemove;
            var response = (AlterDatabaseListResponse) ExecuteRequest(request);
            IList<string> result = response.DatabaseNames;
            if (result == null)
                result = new List<string>();
            return result;
        }

        public DatabaseConfiguration GetDatabaseConfiguration()
        {
            throw new NotImplementedException();
        }

        public DatabaseConfiguration AlterDatabaseConfiguration(DatabaseConfiguration databaseConfiguration)
        {
            throw new NotImplementedException();    // TODO: <!> translate DatabaseConfiguration
        }

        public DatabaseAccessMode GetDatabaseAccessMode()
        {
            var request = new GetDatabaseAccessModeRequest();
            var response = (GetDatabaseAccessModeResponse)ExecuteRequest(request);
            return response.DatabaseAccessMode;
        }

        public DatabaseAccessMode SetDatabaseAccessMode(DatabaseAccessMode databaseAccessMode,
            bool createDatabaseSnapshot)
        {
            throw new NotImplementedException();    // TODO: implement SetDatabaseAccessMode
        }

        public void UnloadDatabase()
        {
            throw new NotImplementedException();
        }

        public void LoadDatabase()
        {
            throw new NotImplementedException();
        }

        public void RefreshDatabaseCryptoKey(int timeoutForPreviousKeyInMilliseconds)
        {
            throw new NotImplementedException();
        }

        public void RefreshEnvironmentCryptoKey(int timeoutForPreviousKeyInMilliseconds)
        {
            throw new NotImplementedException();
        }

        public void CleanupRemovedDatabases()
        {
            // TODO: remove databases only from db_list, move them to db_cleanup_list, then cleanup by request
        }

        // Database

        public DbObject SaveObject(DbObject anObject)
        {
            return (DbObject) SaveObjects(new SaveQuery(new DbObject[] {anObject}, null, null, false))?[0];
        }

        public DbObject SaveObject(DbObject anObject, bool errorOnRevisionMismatch)
        {
            return (DbObject) SaveObjects(new SaveQuery(new DbObject[] {anObject}, null, null,
                errorOnRevisionMismatch))?[0];
        }

        public DbObject SaveObject(DbObject anObject, TypeAndFields typeAndFieldsToSave)
        {
            return (DbObject) SaveObjects(new SaveQuery(new DbObject[] {anObject},
                new TypeAndFields[] {typeAndFieldsToSave}, null, false))?[0];
        }

        public DbObject SaveObject(DbObject anObject, TypeAndFields typeAndFieldsToSave, bool errorOnRevisionMismatch)
        {
            return (DbObject) SaveObjects(new SaveQuery(new DbObject[] {anObject},
                new TypeAndFields[] {typeAndFieldsToSave}, null, errorOnRevisionMismatch))?[0];
        }

        public DbObject SaveObject(DbObject anObject, TypeAndFields typeAndFieldsToSave,
            TypeAndFields typeAndFieldsToReturn)
        {
            return (DbObject) SaveObjects(new SaveQuery(new DbObject[] {anObject},
                new TypeAndFields[] {typeAndFieldsToSave}, new TypeAndFields[] {typeAndFieldsToReturn},
                false))?[0];
        }

        public DbObject SaveObject(DbObject anObject, TypeAndFields typeAndFieldsToSave,
            TypeAndFields typeAndFieldsToReturn, bool errorOnRevisionMismatch)
        {
            return (DbObject) SaveObjects(new SaveQuery(new DbObject[] {anObject},
                new TypeAndFields[] {typeAndFieldsToSave}, new TypeAndFields[] {typeAndFieldsToReturn},
                errorOnRevisionMismatch))?[0];
        }

        public IList SaveObjects(IList objects)
        {
            return SaveObjects(new SaveQuery(objects, null, null, false));
        }

        public IList SaveObjects(IList objects, bool errorOnRevisionMismatch)
        {
            return SaveObjects(new SaveQuery(objects, null, null, errorOnRevisionMismatch));
        }

        public IList SaveObjects(TypeAndFields typeAndFieldsToSave, IList objects)
        {
            return SaveObjects(new SaveQuery(objects, new TypeAndFields[] {typeAndFieldsToSave}, null, false));
        }

        public IList SaveObjects(TypeAndFields typeAndFieldsToSave, IList objects, bool errorOnRevisionMismatch)
        {
            return SaveObjects(new SaveQuery(objects, new TypeAndFields[] {typeAndFieldsToSave},
                null, errorOnRevisionMismatch));
        }

        public IList SaveObjects(TypeAndFields typeAndFieldsToSave, TypeAndFields typeAndFieldsToReturn, IList objects)
        {
            return SaveObjects(new SaveQuery(objects, new TypeAndFields[] {typeAndFieldsToSave},
                new TypeAndFields[] {typeAndFieldsToReturn}, false));
        }

        public IList SaveObjects(TypeAndFields typeAndFieldsToSave, TypeAndFields typeAndFieldsToReturn, IList objects,
            bool errorOnRevisionMismatch)
        {
            return SaveObjects(new SaveQuery(objects, new TypeAndFields[] {typeAndFieldsToSave},
                new TypeAndFields[] {typeAndFieldsToReturn}, errorOnRevisionMismatch));
        }

        public IList SaveObjects(IList<TypeAndFields> typesAndFieldsToSave, IList objects)
        {
            return SaveObjects(new SaveQuery(objects, typesAndFieldsToSave, null, false));
        }

        public IList SaveObjects(IList<TypeAndFields> typesAndFieldsToSave, IList objects, bool errorOnRevisionMismatch)
        {
            return SaveObjects(new SaveQuery(objects, typesAndFieldsToSave, null, errorOnRevisionMismatch));
        }

        public IList SaveObjects(IList<TypeAndFields> typesAndFieldsToSave, IList<TypeAndFields> typesAndFieldsToReturn,
            IList objects)
        {
            return SaveObjects(new SaveQuery(objects, typesAndFieldsToSave, typesAndFieldsToReturn, false));
        }

        public IList SaveObjects(IList<TypeAndFields> typesAndFieldsToSave, IList<TypeAndFields> typesAndFieldsToReturn,
            IList objects, bool errorOnRevisionMismatch)
        {
            return SaveObjects(new SaveQuery(objects, typesAndFieldsToSave, typesAndFieldsToReturn,
                errorOnRevisionMismatch));
        }

        public IList SaveObjects(SaveQuery query)
        {
            return SaveObjectsInQueries(new SaveQuery[] {query})[0].Objects;
        }

        public IList<QueryResult> SaveObjectsInQueries(IList<SaveQuery> queries)
        {
            throw new NotImplementedException();
            //var request = new SaveObjectsRequest(queries);
            //foreach (SaveQuery query in queries)
            //    for (int i = 0; i < query.InObjects.Count; i++)
            //    {
            //        FileObject fileObject = query.InObjects[i] as FileObject;
            //        if (fileObject != null)
            //        {
            //            if (request.FileObjects == null)
            //                request.FileObjects = new List<FileObject>();
            //            request.FileObjects.Add(fileObject);
            //        }
            //    }
            //var response = (SaveObjectsResponse)ExecuteRequest(request);
            //return response.Results;
        }

        public DeleteResult DeleteObject(DbKey objectKey)
        {
            return DeleteObjects(new DeleteQuery(new DbKey[] {objectKey}, false));
        }

        public DeleteResult DeleteObject(DbKey objectKey, bool errorOnObjectNotFound)
        {
            return DeleteObjects(new DeleteQuery(new DbKey[] {objectKey}, errorOnObjectNotFound));
        }

        public DeleteResult DeleteObject(DbKey objectKey, TypeAndFields typeAndFieldsWithObjectsToDelete,
            bool errorOnObjectNotFound)
        {
            return DeleteObjects(new DeleteQuery(new DbKey[] {objectKey},
                new List<TypeAndFields>() {typeAndFieldsWithObjectsToDelete}, errorOnObjectNotFound));
        }

        public DeleteResult DeleteObjects(List<DbKey> objectKeys)
        {
            return DeleteObjects(new DeleteQuery(objectKeys, false));
        }

        public DeleteResult DeleteObjects(DbKey[] objectKeys)
        {
            return DeleteObjects(new DeleteQuery(objectKeys, false));
        }

        public DeleteResult DeleteObjects(List<DbKey> objectKeys,
            IList<TypeAndFields> typesAndFieldsWithDetailObjectsToDelete)
        {
            return DeleteObjects(new DeleteQuery(objectKeys, typesAndFieldsWithDetailObjectsToDelete, false));
        }

        public DeleteResult DeleteObjects(DbKey[] objectKeys,
            IList<TypeAndFields> typesAndFieldsWithDetailObjectsToDelete)
        {
            return DeleteObjects(new DeleteQuery(objectKeys, typesAndFieldsWithDetailObjectsToDelete, false));
        }

        public DeleteResult DeleteObjects(List<DbKey> objectKeys,
            IList<TypeAndFields> typesAndFieldsWithDetailObjectsToDelete, bool errorOnObjectNotFound)
        {
            return DeleteObjects(new DeleteQuery(objectKeys, typesAndFieldsWithDetailObjectsToDelete,
                errorOnObjectNotFound));
        }

        public DeleteResult DeleteObjects(DbKey[] objectKeys,
            IList<TypeAndFields> typesAndFieldsWithDetailObjectsToDelete, bool errorOnObjectNotFound)
        {
            return DeleteObjects(new DeleteQuery(objectKeys, typesAndFieldsWithDetailObjectsToDelete,
                errorOnObjectNotFound));
        }

        public DeleteResult DeleteObjects(DeleteQuery query)
        {
            return DeleteObjectsInQueries(new DeleteQuery[] {query})[0];
        }

        public IList<DeleteResult> DeleteObjectsInQueries(IList<DeleteQuery> queries)
        {
            throw new NotImplementedException();
            //var request = new DeleteObjectsRequest(queries);
            //var response = (DeleteObjectsResponse)ExecuteRequest(request);
            //return response.Results;
        }

        public object GetObject(DbKey objectKey)
        {
            return GetObjects(new GetQuery(null, new DbKey[] {objectKey}, null, null, false, null))[0];
        }

        public object GetObject(DbKey objectKey, bool errorOnObjectNotFound)
        {
            return GetObjects(new GetQuery(null, new DbKey[] {objectKey}, null, null, errorOnObjectNotFound, null))[0];
        }

        public object GetObject(DbKey objectKey, TypeAndFields typeAndFieldsToReturn)
        {
            return GetObjects(new GetQuery(null, new DbKey[] {objectKey}, new TypeAndFields[] {typeAndFieldsToReturn},
                null, false, null))[0];
        }

        public object GetObject(DbKey objectKey, TypeAndFields typeAndFieldsToReturn, bool errorOnObjectNotFound)
        {
            return GetObjects(new GetQuery(null, new DbKey[] {objectKey}, new TypeAndFields[] {typeAndFieldsToReturn},
                null, errorOnObjectNotFound, null))[0];
        }

        public IList GetObjects(IList<DbKey> objectKeys)
        {
            return GetObjects(new GetQuery(null, objectKeys, null, null, false, null));
        }

        public IList GetObjects(IList<DbKey> objectKeys, bool errorOnObjectNotFound)
        {
            return GetObjects(new GetQuery(null, objectKeys, null, null, errorOnObjectNotFound, null));
        }

        public IList GetObjects(IList<DbKey> objectKeys, IList<TypeAndFields> typesAndFieldsToReturn)
        {
            return GetObjects(new GetQuery(null, objectKeys, typesAndFieldsToReturn, null, false, null));
        }

        public IList GetObjects(IList<DbKey> objectKeys, IList<TypeAndFields> typesAndFieldsToReturn,
            bool errorOnObjectNotFound)
        {
            return GetObjects(new GetQuery(null, objectKeys, typesAndFieldsToReturn, null, errorOnObjectNotFound, null));
        }

        public IList GetObjects(GetQuery query)
        {
            return GetObjectsInQueries(new GetQuery[] {query})[0].Objects;
        }

        public IList<QueryResult> GetObjectsInQueries(IList<GetQuery> queries)
        {
            throw new NotImplementedException();
            //var request = new GetObjectsRequest(queries);
            //var response = (GetObjectsResponse)ExecuteRequest(request);
            //return response.Results;
        }

        public DbObject LookupObject(DbObject anObject, string indexToUse)
        {
            return (DbObject) LookupObjects(new LookupQuery(new DbObject[] {anObject}, indexToUse, null, false))[0];
        }

        public DbObject LookupObject(DbObject anObject, string indexToUse, bool errorOnObjectNotFound)
        {
            return (DbObject) LookupObjects(new LookupQuery(new DbObject[] {anObject}, indexToUse, null,
                errorOnObjectNotFound))[0];
        }

        public DbObject LookupObject(DbObject anObject, string indexToUse, TypeAndFields typeAndFieldsToReturn)
        {
            return (DbObject) LookupObjects(new LookupQuery(new DbObject[] {anObject}, indexToUse,
                new TypeAndFields[] {typeAndFieldsToReturn}, false))[0];
        }

        public DbObject LookupObject(DbObject anObject, string indexToUse, TypeAndFields typeAndFieldsToReturn,
            bool errorOnObjectNotFound)
        {
            return (DbObject) LookupObjects(new LookupQuery(new DbObject[] {anObject}, indexToUse,
                new TypeAndFields[] {typeAndFieldsToReturn}, errorOnObjectNotFound))[0];
        }

        public IList LookupObjects(IList objectsToLookup, string indexToUse)
        {
            return LookupObjects(new LookupQuery(objectsToLookup, indexToUse, null, false));
        }

        public IList LookupObjects(IList objectsToLookup, string indexToUse, bool errorOnObjectNotFound)
        {
            return LookupObjects(new LookupQuery(objectsToLookup, indexToUse, null, errorOnObjectNotFound));
        }

        public IList LookupObjects(IList objectsToLookup, string indexToUse, IList<TypeAndFields> typesAndFieldsToReturn)
        {
            return LookupObjects(new LookupQuery(objectsToLookup, indexToUse, typesAndFieldsToReturn, false));
        }

        public IList LookupObjects(IList objectsToLookup, string indexToUse, IList<TypeAndFields> typesAndFieldsToReturn,
            bool errorOnObjectNotFound)
        {
            return LookupObjects(new LookupQuery(objectsToLookup, indexToUse, typesAndFieldsToReturn,
                errorOnObjectNotFound));
        }

        public IList LookupObjects(LookupQuery query)
        {
            return LookupObjectsInQueries(new LookupQuery[] {query})[0].Objects;
        }

        public IList<QueryResult> LookupObjectsInQueries(IList<LookupQuery> queries)
        {
            throw new NotImplementedException();
            //var request = new LookupObjectsRequest(queries);
            //var response = (LookupObjectsResponse)ExecuteRequest(request);
            //return response.Results;
        }

        public QueryResult SearchObjects(SearchQuery query)
        {
            return SearchObjects(new SearchQuery[] {query})[0];
        }

        public IList<QueryResult> SearchObjects(IList<SearchQuery> queries)
        {
            throw new NotImplementedException();
            //var request = new SearchObjectsRequest(queries);
            //var response = (SearchObjectsResponse)ExecuteRequest(request);
            //return response.Results;
        }

        // Files

        public FileObject GetFile(string fileName)
        {
            FileObject result = null;
            IList results = SearchFiles(fileName, null, 1, null, null, null, null, null, null);
            if (results != null && results.Count == 1)
                result = results[0] as FileObject;
            return result;
        }

        public IList SearchFiles(string fileMaskToMatch, int searchLimit, FileObject startAfter,
            TypeAndFields typeAndFieldsToReturn)
        {
            return SearchFiles(fileMaskToMatch, null, searchLimit, null, startAfter, null, null, null,
                new TypeAndFields[] {typeAndFieldsToReturn});
        }

        public IList SearchFiles(string fileMaskToMatch, string fileMaskToNotMatch, int searchLimit,
            string forVar, FileObject after, string where, string having,
            IList<Parameter> parameters, IList<TypeAndFields> typesAndFieldsToReturn)
        {
            var fileRange = new FileRange();
            SearchQuery query = CreateSearchFilesQuery(fileMaskToMatch, fileMaskToNotMatch, searchLimit,
                forVar, after, where, having, parameters, typesAndFieldsToReturn, fileRange);
            var queries = new SearchQuery[] {query};
            var request = new SearchObjectsRequest(queries);
            var response = (SearchObjectsResponse) ExecuteRequest(request);
            return response.Results[0].Objects;
        }

        public SearchQuery CreateSearchFilesQuery(string fileMaskToMatch, string fileMaskToNotMatch,
            int searchLimit, string forVar, FileObject after, string where, string having,
            IList<Parameter> parameters, IList<TypeAndFields> typesAndFieldsToReturn, FileRange fileRange)
        {
            throw new NotImplementedException();
        }

        // ExecuteRequest

        public DatabaseResponse ExecuteRequest(DatabaseRequest request)
        {
            DatabaseResponse result = null;
            string serverAddress = CurrentServerAddress;
            if (ServerAddressSelectionMode == ServerAddressSelectionMode.RandomPerCall)
                CurrentServerAddressNumber = (CurrentServerAddressNumber + 1)%ServerAddresses.Count;
            Stopwatch stopwatch = null;
            int totalElapsedTimeInMilliseconds = 0;
            int retryCount = 0;
            int basePartOfDelayInMilliseconds = 0;
            bool success = false;
            while (!success)
            {
                stopwatch = Stopwatch.StartNew();
                result = ExecuteRequestNoRetry(serverAddress, request);
                int elapsedMilliseconds = (int) stopwatch.ElapsedMilliseconds;
                if (result is ErrorResponse)
                {
                    ErrorResponse error = result as ErrorResponse;
                    totalElapsedTimeInMilliseconds += elapsedMilliseconds;
                    if ((error.ErrorStatus == ErrorStatus.Retry) &&
                        (RetryLimit <= 0 || retryCount < RetryLimit) &&
                        (TimeoutInMilliseconds == Timeout.Infinite ||
                         totalElapsedTimeInMilliseconds < TimeoutInMilliseconds))
                    {
                        if (basePartOfDelayInMilliseconds < elapsedMilliseconds)
                            basePartOfDelayInMilliseconds = elapsedMilliseconds;
                        if (basePartOfDelayInMilliseconds < MaxDelayBeforeRetryInMilliseconds/2)
                            basePartOfDelayInMilliseconds = basePartOfDelayInMilliseconds*2;
                        else
                            basePartOfDelayInMilliseconds = MaxDelayBeforeRetryInMilliseconds/2;
                        int randomPartOfDelayInMilliseconds;
                        lock (RandomDelayGenerator)
                            randomPartOfDelayInMilliseconds = RandomDelayGenerator.Next(basePartOfDelayInMilliseconds);
                        Thread.Sleep(basePartOfDelayInMilliseconds + randomPartOfDelayInMilliseconds);
                        retryCount++;
                    }
                    else
                    {
                        if (error.ErrorStatus == ErrorStatus.SecurityError)
                        {
                            //var newContext = new DatabaseClientContext(fContext);
                            //newContext.DatabaseChangeNumbers = null;
                            //Interlocked.Exchange(ref fContext, null);
                            throw new NezaboodkaSecurityException(error.ErrorMessage);
                        }
                        else if (error.ErrorStatus == ErrorStatus.AvailabilityError)
                            throw new NezaboodkaAvailabilityException(error.ErrorMessage);
                        else
                            throw new NezaboodkaException(error.ErrorMessage);
                    }
                }
                else
                    success = true;
            }
            return result;
        }

        // Internal

        private DatabaseResponse ExecuteRequestNoRetry(string serverAddress, DatabaseRequest request)
        {
            DatabaseResponse result = null;
            var dbName = Consts.AdministrationDatabaseName;
            if (!(request is AdministrationRequest) || request is RefreshDatabaseCryptoKeyRequest ||
                request is GetDatabaseConfigurationRequest || request is AlterDatabaseConfigurationRequest ||
                request is UnloadDatabaseRequest || request is LoadDatabaseRequest)
            {
                dbName = DatabaseName;
            }

            MySqlConnectionStringBuilder conStringBuilder = new MySqlConnectionStringBuilder
            {
                Server = serverAddress,
                UserID = Consts.MySqlUserId,
                Password = Consts.MySqlPass,
                Database = dbName,
                PersistSecurityInfo = false
            };

            MySqlConnection con =
                new MySqlConnection(conStringBuilder.GetConnectionString(true));

            MySqlCommand cmd = con.CreateCommand();
            // TODO: make test for TimeOut
            cmd.CommandTimeout = (TimeoutInMilliseconds == -1) ? 0 : TimeoutInMilliseconds;

            try
            {
                con.Open();
                result = _requestExec[request.GetType()].Invoke(request, cmd);
            }
            catch (KeyNotFoundException ex)
            {
                throw new NezaboodkaException(ex.Message);
            }
            catch (MySqlException ex)
            {
                // TODO: separate TimeoutException handling
                //result = new ErrorResponse()
                //{
                //    ErrorStatus = ErrorStatus.Timeout
                //};
                throw new NezaboodkaException(ex.Message);
            }
            finally
            {
                con.Close();
            }

            return result;
        }

        // Internal

        private void RegisterRequests()
        {
            _requestExec.Add(typeof(GetDatabaseListRequest), GetDatabaseListExec);
            _requestExec.Add(typeof(AlterDatabaseListRequest), AlterDatabaseListExec);
            _requestExec.Add(typeof(GetDatabaseAccessModeRequest), GetDatabaseAccessModeExec);
        }

        private DatabaseResponse GetDatabaseListExec(DatabaseRequest request, MySqlCommand cmd)
        {
            cmd.CommandText = RequestConsts.GetDatabaseListQuery;
            MySqlDataReader reader = cmd.ExecuteReader();
            
            var result = ReadDatabaseNamesList(reader);
            return new GetDatabaseListResponse(result);
        }
        
        private DatabaseResponse AlterDatabaseListExec(DatabaseRequest request, MySqlCommand cmd)
        {
            AlterDatabaseListRequest realRequest = request as AlterDatabaseListRequest;
            if (realRequest == null)
            {
                return new DatabaseResponse();
            }

            cmd.CommandType = CommandType.Text;
            cmd.CommandText = string.Empty;

            if (realRequest.DatabaseNamesToRemove != null)
            {
                cmd.CommandText += string.Format(RequestConsts.RemoveDatabaseListPrepareQuery,
                    FormatDatabaseList(realRequest.DatabaseNamesToRemove));
            }

            if (realRequest.DatabaseNamesToAdd != null) {
                cmd.CommandText += string.Format(RequestConsts.AddDatabaseListPrepareQuery,
                    FormatDatabaseList(realRequest.DatabaseNamesToAdd));
            }

            cmd.CommandText += RequestConsts.AlterDatabaseListQuery;
            MySqlDataReader reader = cmd.ExecuteReader();

            var result = ReadDatabaseNamesList(reader);
            return new AlterDatabaseListResponse(result);
        }

        private DatabaseResponse GetDatabaseAccessModeExec(DatabaseRequest request, MySqlCommand cmd)
        {
            DatabaseResponse result = null;
            cmd.CommandText = string.Format(RequestConsts.GetDatabaseAccessModeQuery, DatabaseName);
            MySqlDataReader reader = cmd.ExecuteReader();

            int accessModeNumber = (int)DatabaseAccessMode.NoAccess;
            bool hasRows = reader.HasRows;
            if (hasRows)
            {
                reader.Read();
                accessModeNumber = reader.GetInt32(Consts.AccessFieldName);
            }
            reader.Close();

            if (hasRows)
            {
                var accessMode = (DatabaseAccessMode)accessModeNumber;
                result = new GetDatabaseAccessModeResponse(accessMode);
            }
            else
            {
                result = new ErrorResponse()
                {
                    ErrorStatus = ErrorStatus.AvailabilityError,
                    ErrorMessage = string.Format(ErrorMessageConsts.DatabaseNotFoundName, DatabaseName)
                };
            }

            return result;
        }
        
        private List<string> ReadDatabaseNamesList(MySqlDataReader reader)
        {
            List<string> result = new List<string>();
            while (reader.Read())
            {
                string row = reader.GetString(0);
                result.Add(row);
            }
            reader.Close();
            return result;
        }

        private static string FormatDatabaseList(IEnumerable<string> names)
        {
            return string.Join(",", names.Select(s => $"('{s}')"));
        }

    }

    internal static class Consts   // TODO: move credentials to Protected Configuration
    {
        public static string MySqlUserId => "nz_admin";
        public static string MySqlPass => "nezaboodka";

        public static string AdministrationDatabaseName => "nz_admin_db";
        public static string AccessFieldName => "access";
    }

    internal static class ErrorMessageConsts
    {
        public static string DatabaseNotFoundName => "Database {0} not found";
    }

    internal static class RequestConsts
    {
        public static string GetDatabaseListQuery => "SELECT `name` FROM `db_list`;";
        public static string GetDatabaseAccessModeQuery => "SELECT `access` FROM `db_list` WHERE `name` = '{0}';";

        public static string RemoveDatabaseListPrepareQuery => "INSERT INTO `db_rem_list` (`name`) VALUES {0};";
        public static string AddDatabaseListPrepareQuery => "INSERT INTO `db_add_list` (`name`) VALUES {0};";
        public static string AlterDatabaseListQuery => "CALL alter_database_list();";
    }
}
