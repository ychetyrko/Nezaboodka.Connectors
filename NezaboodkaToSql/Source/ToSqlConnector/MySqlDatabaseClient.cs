using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.Threading;
using MySql.Data.MySqlClient;
using System.Globalization;

namespace Nezaboodka.ToSqlConnector
{
    public class MySqlDatabaseClient
    {
        private static readonly Random RandomAddressGenerator = new Random();
        private static readonly Random RandomDelayGenerator = new Random();

        private readonly Dictionary<System.Type, Func<DatabaseRequest, MySqlCommand, DatabaseResponse>> _requestExec =
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
            var request = new GetDatabaseConfigurationRequest();
            var response = (GetDatabaseConfigurationResponse)ExecuteRequest(request);
            return response.DatabaseConfiguration;
        }

        public DatabaseConfiguration AlterDatabaseConfiguration(DatabaseConfiguration databaseConfiguration)
        {
            var request = new AlterDatabaseConfigurationRequest(databaseConfiguration);
            var response = (AlterDatabaseConfigurationResponse)ExecuteRequest(request);
            return response.DatabaseConfiguration;
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
            throw new NotImplementedException();
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
            //var response = (SaveObjectsResponse)ExecuteRequest(request);
            //return response.Results;
        }

        public long DeleteObject(DbKey objectKey)
        {
            return DeleteObjects(new DeleteQuery(new DbKey[] {objectKey}, false));
        }

        public long DeleteObject(DbKey objectKey, bool errorOnObjectNotFound)
        {
            return DeleteObjects(new DeleteQuery(new DbKey[] {objectKey}, errorOnObjectNotFound));
        }

        public long DeleteObject(DbKey objectKey, TypeAndFields typeAndFieldsWithObjectsToDelete,
            bool errorOnObjectNotFound)
        {
            return DeleteObjects(new DeleteQuery(new DbKey[] {objectKey},
                new List<TypeAndFields>() {typeAndFieldsWithObjectsToDelete}, errorOnObjectNotFound));
        }

        public long DeleteObjects(List<DbKey> objectKeys)
        {
            return DeleteObjects(new DeleteQuery(objectKeys, false));
        }

        public long DeleteObjects(DbKey[] objectKeys)
        {
            return DeleteObjects(new DeleteQuery(objectKeys, false));
        }

        public long DeleteObjects(List<DbKey> objectKeys,
            IList<TypeAndFields> typesAndFieldsWithDetailObjectsToDelete)
        {
            return DeleteObjects(new DeleteQuery(objectKeys, typesAndFieldsWithDetailObjectsToDelete, false));
        }

        public long DeleteObjects(DbKey[] objectKeys,
            IList<TypeAndFields> typesAndFieldsWithDetailObjectsToDelete)
        {
            return DeleteObjects(new DeleteQuery(objectKeys, typesAndFieldsWithDetailObjectsToDelete, false));
        }

        public long DeleteObjects(List<DbKey> objectKeys,
            IList<TypeAndFields> typesAndFieldsWithDetailObjectsToDelete, bool errorOnObjectNotFound)
        {
            return DeleteObjects(new DeleteQuery(objectKeys, typesAndFieldsWithDetailObjectsToDelete,
                errorOnObjectNotFound));
        }

        public long DeleteObjects(DbKey[] objectKeys,
            IList<TypeAndFields> typesAndFieldsWithDetailObjectsToDelete, bool errorOnObjectNotFound)
        {
            return DeleteObjects(new DeleteQuery(objectKeys, typesAndFieldsWithDetailObjectsToDelete,
                errorOnObjectNotFound));
        }

        public long DeleteObjects(DeleteQuery query)
        {
            return DeleteObjectsInQueries(new DeleteQuery[] { query });
        }

        public long DeleteObjectsInQueries(IList<DeleteQuery> queries)
        {
            throw new NotImplementedException();
            //var request = new DeleteObjectsRequest(queries);
            //var response = (DeleteObjectsResponse)ExecuteRequest(request);
            //return response.DeletedObjectCount;
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
            throw new NotImplementedException();

            //FileObject result = null;
            //IList results = SearchFiles(fileName, null, 1, null, null, null, null, null, null);
            //if (results != null && results.Count == 1)
            //    result = results[0] as FileObject;
            //return result;
        }

        public IList SearchFiles(string fileMaskToMatch, int searchLimit, FileObject startAfter,
            TypeAndFields typeAndFieldsToReturn)
        {
            return SearchFiles(fileMaskToMatch, null, searchLimit, null, startAfter, null, null, null,
                new TypeAndFields[] {typeAndFieldsToReturn});
        }

        public IList SearchFiles(string fileMaskToMatch, string fileMaskToNotMatch, int searchLimit,
            string forEachVar, FileObject after, string where, string having,
            IList<Parameter> parameters, IList<TypeAndFields> typesAndFieldsToReturn)
        {
            throw new NotImplementedException();

            //var fileRange = new FileRange();
            //SearchQuery query = CreateSearchFilesQuery(fileMaskToMatch, fileMaskToNotMatch, searchLimit,
            //    forEachVar, after, where, having, parameters, typesAndFieldsToReturn, fileRange);
            //var queries = new SearchQuery[] { query };
            //var request = new SearchObjectsRequest(queries);
            //var response = (SearchObjectsResponse)ExecuteRequest(request);
            //return response.Results[0].Objects;
        }

        public SearchQuery CreateSearchFilesQuery(string fileMaskToMatch, string fileMaskToNotMatch,
            int searchLimit, string forEachVar, FileObject after, string where, string having,
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
            var stopwatch = new Stopwatch();
            int totalElapsedTimeInMilliseconds = 0;
            int retryCount = 0;
            int basePartOfDelayInMilliseconds = 0;
            bool success = false;
            while (!success)
            {
                stopwatch.Restart();
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
            var dbName = AdminDatabaseConst.AdminDbName;
            if (!(request is AdministrationRequest) || request is RefreshDatabaseCryptoKeyRequest ||
                request is GetDatabaseConfigurationRequest || // request is AlterDatabaseConfigurationRequest || // <-- !! request for AdministrationDatabase
                //request is GetDatabaseAccessModeRequest || request is SetDatabaseAccessModeRequest || // <-- !! requests for AdministrationDatabase
                request is UnloadDatabaseRequest || request is LoadDatabaseRequest)
            {
                dbName = DatabaseName;

                if (string.IsNullOrEmpty(dbName))
                    return new ErrorResponse()
                    {
                        ErrorStatus = ErrorStatus.AvailabilityError,
                        ErrorMessage = ErrorMessagesFormatter.DatabaseNotFoundName(string.Empty)
                    };
            }

            Uri serverUri = new Uri(serverAddress);

            MySqlConnectionStringBuilder conStringBuilder = new MySqlConnectionStringBuilder
            {
                Server = serverUri.Host,
                Port = (uint)serverUri.Port,
                UserID = SqlAuthData.UserId,
                Password = SqlAuthData.Pass,
                Database = dbName,
                PersistSecurityInfo = false
            };

            MySqlConnection con =
                new MySqlConnection(conStringBuilder.GetConnectionString(true));

            MySqlCommand cmd = con.CreateCommand();

            // TODO: make test for TimeOut
            cmd.CommandTimeout = (TimeoutInMilliseconds == Timeout.Infinite) ? 0 : TimeoutInMilliseconds;

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
            _requestExec.Add(typeof(GetDatabaseConfigurationRequest), GetDatabaseConfigurationExec);
            _requestExec.Add(typeof(AlterDatabaseConfigurationRequest), AlterDatabaseConfigurationExec);
        }

        private DatabaseResponse GetDatabaseListExec(DatabaseRequest request, MySqlCommand cmd)
        {
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = DbQueryBuilder.GetDatabaseListQuery;

            MySqlDataReader reader = cmd.ExecuteReader();
            var result = ReadDatabaseNamesList(reader);
            reader.Close();

            return new GetDatabaseListResponse(result);
        }
        
        private DatabaseResponse AlterDatabaseListExec(DatabaseRequest request, MySqlCommand cmd)
        {
            AlterDatabaseListRequest realRequest = request as AlterDatabaseListRequest;

            cmd.CommandType = CommandType.Text;
            cmd.CommandText = DbQueryBuilder.AlterDatabaseListQuery(realRequest.DatabaseNamesToRemove, realRequest.DatabaseNamesToAdd);

            MySqlDataReader reader = cmd.ExecuteReader();
            var result = ReadDatabaseNamesList(reader);
            reader.Close();

            return new AlterDatabaseListResponse(result);
        }

        private DatabaseResponse GetDatabaseAccessModeExec(DatabaseRequest request, MySqlCommand cmd)
        {
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = DbQueryBuilder.GetDatabaseAccessModeQuery(DatabaseName);

            MySqlDataReader reader = cmd.ExecuteReader();
            int? accessModeNumber = ReadDatabaseAccessMode(reader);
            reader.Close();

            DatabaseResponse result = null;
            if (accessModeNumber != null)
            {
                var accessMode = (DatabaseAccessMode)accessModeNumber;
                result = new GetDatabaseAccessModeResponse(accessMode);
            }
            else
            {
                result = new ErrorResponse()
                {
                    ErrorStatus = ErrorStatus.AvailabilityError,
                    ErrorMessage = ErrorMessagesFormatter.DatabaseNotFoundName(DatabaseName)
                };
            }

            return result;
        }

        private DatabaseResponse GetDatabaseConfigurationExec(DatabaseRequest request, MySqlCommand cmd)
        {
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = DbQueryBuilder.GetDatabaseConfigurationQuery(DatabaseName);

            MySqlDataReader reader = cmd.ExecuteReader();
            DatabaseConfiguration dbConfig = ReadDatabaseConfiguration(reader);
            reader.Close();

            GetDatabaseConfigurationResponse result = new GetDatabaseConfigurationResponse(dbConfig);
            return result;
        }

        private DatabaseResponse AlterDatabaseConfigurationExec(DatabaseRequest request, MySqlCommand cmd)
        {
            AlterDatabaseConfigurationRequest realRequest = request as AlterDatabaseConfigurationRequest;

            cmd.CommandType = CommandType.Text;
            cmd.CommandText = string.Empty;

            var newConfig = realRequest.DatabaseConfiguration;

            // !!! only writes schema --> NO MERGE
            // TODO: merge existing schema with new
            cmd.CommandText = DbQueryBuilder.AlterDatabaseConfigurationQuery(DatabaseName, newConfig);
            cmd.CommandText += DbQueryBuilder.GetDatabaseConfigurationQuery(DatabaseName);

            MySqlDataReader reader = cmd.ExecuteReader();
            newConfig = ReadDatabaseConfiguration(reader);
            reader.Close();

            var result = new AlterDatabaseConfigurationResponse()
            {
                DatabaseConfiguration = newConfig
            };
            
            return result;
        }

        // Readers

        private List<string> ReadDatabaseNamesList(MySqlDataReader reader)
        {
            List<string> result = new List<string>();
            while (reader.Read())
            {
                string row = reader.GetString(0);
                result.Add(row);
            }
            return result;
        }

        private static int? ReadDatabaseAccessMode(MySqlDataReader reader)
        {
            int? result = null;
            if (reader.HasRows)
            {
                reader.Read();
                result = reader.GetInt32(AdminDatabaseConst.AccessField);
            }
            return result;
        }

        private DatabaseConfiguration ReadDatabaseConfiguration(MySqlDataReader reader)
        {
            DatabaseSchema dbSchema = ReadDatabaseSchema(reader);

            // TODO: Read Secondary and Referencial indexes

            DatabaseConfiguration result = new DatabaseConfiguration()
            {
                DatabaseSchema = dbSchema
            };

            return result;
        }

        private static DatabaseSchema ReadDatabaseSchema(MySqlDataReader reader)
        {
            var dbSchema = new DatabaseSchema();
            var typeNameMap = new Dictionary<string, TypeDefinition>();

            if (reader.HasRows)
            {
                // Types list
                while (reader.Read())
                {
                    var typeDef = new TypeDefinition
                    {
                        TypeName = reader.GetString(SchemaFieldConst.TypeName),
                        BaseTypeName = reader.GetString(SchemaFieldConst.BaseTypeName)
                    };

                    dbSchema.TypeDefinitions.Add(typeDef);
                    typeNameMap.Add(typeDef.TypeName, typeDef);
                }

                // Fields list
                if (reader.NextResult())
                {
                    while (reader.Read())
                    {
                        var fieldDef = new FieldDefinition()
                        {
                            FieldName = reader.GetString(SchemaFieldConst.FieldName),
                            FieldTypeName = reader.GetString(SchemaFieldConst.FieldTypeName),
                            IsList = bool.Parse(reader.GetString(SchemaFieldConst.FieldIsList)),
                            CompareOptions = (CompareOptions)Enum.Parse(typeof(CompareOptions), reader.GetString(SchemaFieldConst.FieldCompareOptions)),
                            BackReferenceFieldName = reader.GetString(SchemaFieldConst.FieldBackRefName)
                        };

                        string ownerType = reader.GetString(SchemaFieldConst.FieldOwnerTypeName);
                        typeNameMap[ownerType].FieldDefinitions.Add(fieldDef);
                        // TODO: Catch KeyNotFoundException
                    }
                }
            }

            return dbSchema;
        }
    }

    internal static class ErrorMessagesFormatter
    {
        public static string DatabaseNotFoundName(string dbName)
        {
            return "Database " + dbName + " not found";
        }
    }
}
