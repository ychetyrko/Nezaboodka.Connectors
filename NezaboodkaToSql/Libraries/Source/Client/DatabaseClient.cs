using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using Nezaboodka.Ndef;

namespace Nezaboodka
{
    public partial class DatabaseClient
    {
        private static readonly Random RandomAddressGenerator = new Random();
        private static readonly Random RandomDelayGenerator = new Random();
        private static readonly char[] DelimiterBetweenIdentifierAndTypeName = new char[] { ':' };

        // Fields

        private DatabaseClientContext fContext;

        // Public

        public FileContentHandler FileContentHandler { get; set; }
        public ReadOnlyCollection<string> ServerAddresses { get; set; }
        public int CurrentServerAddressNumber { get; set; }
        public string CurrentServerAddress { get { return ServerAddresses[CurrentServerAddressNumber]; } }
        public ServerAddressSelectionMode ServerAddressSelectionMode { get; set; }
        public string DatabaseName { get; set; }
        public ClientTypeBinder TypeBinder { get; private set; }
        public int TimeoutInMilliseconds { get; set; } // Timeout.Infinite (-1) is set by default
        public int RetryLimit { get; set; } // number of attempts, 0 is unlimited by default
        public int MaxDelayBeforeRetryInMilliseconds { get; set; }

        public DatabaseClient(string serverAddress, string databaseName, ClientTypeBinder typeBinder)
            : this(new string[] { serverAddress }, ServerAddressSelectionMode.FirstAvailable, databaseName, typeBinder)
        {
        }

        public DatabaseClient(IList<string> serverAddresses, ServerAddressSelectionMode serverAddressSelectionMode,
            string databaseName, ClientTypeBinder typeBinder)
            : this(serverAddresses, serverAddressSelectionMode, databaseName, typeBinder, Timeout.Infinite, 0, 5000)
        {
        }

        public DatabaseClient(IList<string> serverAddresses, ServerAddressSelectionMode serverAddressSelectionMode,
            string databaseName, ClientTypeBinder typeBinder, int timeoutInMilliseconds, int retryLimit,
            int maxDelayBeforeRetryInMilliseconds)
        {
            FileContentHandler = new FileContentHandler();
            ServerAddresses = new ReadOnlyCollection<string>(serverAddresses);
            ServerAddressSelectionMode = serverAddressSelectionMode;
            if (serverAddressSelectionMode != ServerAddressSelectionMode.FirstAvailable)
                lock (RandomAddressGenerator)
                    CurrentServerAddressNumber = RandomAddressGenerator.Next(serverAddresses.Count);
            DatabaseName = databaseName;
            TypeBinder = typeBinder;
            TimeoutInMilliseconds = timeoutInMilliseconds;
            RetryLimit = retryLimit;
            MaxDelayBeforeRetryInMilliseconds = maxDelayBeforeRetryInMilliseconds;
            fContext = new DatabaseClientContext();
        }

        public DatabaseClient(DatabaseClient existing)
            : this(existing, existing.TypeBinder)
        {
        }

        public DatabaseClient(DatabaseClient existing, ClientTypeBinder typeBinder)
        {
            FileContentHandler = existing.FileContentHandler;
            ServerAddresses = existing.ServerAddresses;
            ServerAddressSelectionMode = existing.ServerAddressSelectionMode;
            CurrentServerAddressNumber = existing.CurrentServerAddressNumber;
            if (ServerAddressSelectionMode != ServerAddressSelectionMode.FirstAvailable)
                lock (RandomAddressGenerator)
                    CurrentServerAddressNumber = RandomAddressGenerator.Next(ServerAddresses.Count);
            DatabaseName = existing.DatabaseName;
            TypeBinder = typeBinder;
            TimeoutInMilliseconds = existing.TimeoutInMilliseconds;
            RetryLimit = existing.RetryLimit;
            MaxDelayBeforeRetryInMilliseconds = existing.MaxDelayBeforeRetryInMilliseconds;
            fContext = new DatabaseClientContext(existing.fContext);
        }

        // Administration

        public EnvironmentConfiguration GetEnvironmentConfiguration()
        {
            var request = new GetEnvironmentConfigurationRequest();
            var response = (GetEnvironmentConfigurationResponse)ExecuteRequest(request);
            return response.EnvironmentConfiguration;
        }

        public IList<string> GetDatabaseList()
        {
            var request = new GetDatabaseListRequest();
            var response = (GetDatabaseListResponse)ExecuteRequest(request);
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
            var response = (AlterDatabaseListResponse)ExecuteRequest(request);
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
            var request = new SetDatabaseAccessModeRequest(databaseAccessMode, createDatabaseSnapshot);
            var response = (SetDatabaseAccessModeResponse)ExecuteRequest(request);
            return response.PreviousDatabaseAccessMode;
        }

        public void UnloadDatabase()
        {
            var request = new UnloadDatabaseRequest();
            var response = (UnloadDatabaseResponse)ExecuteRequest(request);
        }

        public void LoadDatabase()
        {
            var request = new LoadDatabaseRequest();
            var response = (LoadDatabaseResponse)ExecuteRequest(request);
        }

        public void RefreshDatabaseCryptoKey(int timeoutForPreviousKeyInMilliseconds)
        {
            var request = new RefreshDatabaseCryptoKeyRequest(timeoutForPreviousKeyInMilliseconds);
            var response = (RefreshDatabaseCryptoKeyResponse)ExecuteRequest(request);
        }

        public void RefreshEnvironmentCryptoKey(int timeoutForPreviousKeyInMilliseconds)
        {
            var request = new RefreshEnvironmentCryptoKeyRequest(timeoutForPreviousKeyInMilliseconds);
            var response = (RefreshEnvironmentCryptoKeyResponse)ExecuteRequest(request);
        }

        public void CleanupRemovedDatabases()
        {
            var request = new CleanupRemovedDatabasesRequest();
            var response = (CleanupRemovedDatabasesResponse)ExecuteRequest(request);
        }

        // Database

        public DbObject SaveObject(DbObject anObject)
        {
            return (DbObject)SaveObjects(new SaveQuery(new DbObject[] { anObject }, null, null, false))?[0];
        }

        public DbObject SaveObject(DbObject anObject, bool errorOnRevisionMismatch)
        {
            return (DbObject)SaveObjects(new SaveQuery(new DbObject[] { anObject }, null, null, 
                errorOnRevisionMismatch))?[0];
        }

        public DbObject SaveObject(DbObject anObject, TypeAndFields typeAndFieldsToSave)
        {
            return (DbObject)SaveObjects(new SaveQuery(new DbObject[] { anObject },
                new TypeAndFields[] { typeAndFieldsToSave }, null, false))?[0];
        }

        public DbObject SaveObject(DbObject anObject, TypeAndFields typeAndFieldsToSave, bool errorOnRevisionMismatch)
        {
            return (DbObject)SaveObjects(new SaveQuery(new DbObject[] { anObject },
                new TypeAndFields[] { typeAndFieldsToSave }, null, errorOnRevisionMismatch))?[0];
        }

        public DbObject SaveObject(DbObject anObject, TypeAndFields typeAndFieldsToSave,
            TypeAndFields typeAndFieldsToReturn)
        {
            return (DbObject)SaveObjects(new SaveQuery(new DbObject[] { anObject },
                new TypeAndFields[] { typeAndFieldsToSave }, new TypeAndFields[] { typeAndFieldsToReturn }, 
                false))?[0];
        }

        public DbObject SaveObject(DbObject anObject, TypeAndFields typeAndFieldsToSave,
            TypeAndFields typeAndFieldsToReturn, bool errorOnRevisionMismatch)
        {
            return (DbObject)SaveObjects(new SaveQuery(new DbObject[] { anObject }, 
                new TypeAndFields[] { typeAndFieldsToSave }, new TypeAndFields[] { typeAndFieldsToReturn }, 
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
            return SaveObjects(new SaveQuery(objects, new TypeAndFields[] { typeAndFieldsToSave }, null, false));
        }

        public IList SaveObjects(TypeAndFields typeAndFieldsToSave, IList objects, bool errorOnRevisionMismatch)
        {
            return SaveObjects(new SaveQuery(objects, new TypeAndFields[] { typeAndFieldsToSave },
                null, errorOnRevisionMismatch));
        }

        public IList SaveObjects(TypeAndFields typeAndFieldsToSave, TypeAndFields typeAndFieldsToReturn, IList objects)
        {
            return SaveObjects(new SaveQuery(objects, new TypeAndFields[] { typeAndFieldsToSave },
                new TypeAndFields[] { typeAndFieldsToReturn }, false));
        }

        public IList SaveObjects(TypeAndFields typeAndFieldsToSave, TypeAndFields typeAndFieldsToReturn, IList objects, 
            bool errorOnRevisionMismatch)
        {
            return SaveObjects(new SaveQuery(objects, new TypeAndFields[] { typeAndFieldsToSave },
                new TypeAndFields[] { typeAndFieldsToReturn }, errorOnRevisionMismatch));
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
            return SaveObjectsInQueries(new SaveQuery[] { query })[0].Objects;
        }

        public IList<QueryResult> SaveObjectsInQueries(IList<SaveQuery> queries)
        {
            var request = new SaveObjectsRequest(queries);
            var response = (SaveObjectsResponse)ExecuteRequest(request);
            return response.Results;
        }

        public long DeleteObject(DbKey objectKey)
        {
            return DeleteObjects(new DeleteQuery(new DbKey[] { objectKey }, false));
        }

        public long DeleteObject(DbKey objectKey, bool errorOnObjectNotFound)
        {
            return DeleteObjects(new DeleteQuery(new DbKey[] { objectKey }, errorOnObjectNotFound));
        }

        public long DeleteObject(DbKey objectKey, TypeAndFields typeAndFieldsWithObjectsToDelete,
            bool errorOnObjectNotFound)
        {
            return DeleteObjects(new DeleteQuery(new DbKey[] { objectKey }, 
                new List<TypeAndFields>() { typeAndFieldsWithObjectsToDelete }, errorOnObjectNotFound));
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
            var request = new DeleteObjectsRequest(queries);
            var response = (DeleteObjectsResponse)ExecuteRequest(request);
            return response.DeletedObjectCount;
        }

        public object GetObject(DbKey objectKey)
        {
            return GetObjects(new GetQuery(null, new DbKey[] { objectKey }, null, null, false, null))[0];
        }

        public object GetObject(DbKey objectKey, bool errorOnObjectNotFound)
        {
            return GetObjects(new GetQuery(null, new DbKey[] { objectKey }, null, null, errorOnObjectNotFound, null))[0];
        }

        public object GetObject(DbKey objectKey, TypeAndFields typeAndFieldsToReturn)
        {
            return GetObjects(new GetQuery(null, new DbKey[] { objectKey }, new TypeAndFields[] { typeAndFieldsToReturn },
                null, false, null))[0];
        }

        public object GetObject(DbKey objectKey, TypeAndFields typeAndFieldsToReturn, bool errorOnObjectNotFound)
        {
            return GetObjects(new GetQuery(null, new DbKey[] { objectKey }, new TypeAndFields[] { typeAndFieldsToReturn }, 
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
            return GetObjectsInQueries(new GetQuery[] { query })[0].Objects;
        }

        public IList<QueryResult> GetObjectsInQueries(IList<GetQuery> queries)
        {
            var request = new GetObjectsRequest(queries);
            var response = (GetObjectsResponse)ExecuteRequest(request);
            return response.Results;
        }

        public DbObject LookupObject(DbObject anObject, string indexToUse)
        {
            return (DbObject)LookupObjects(new LookupQuery(new DbObject[] { anObject }, indexToUse, null, false))[0];
        }

        public DbObject LookupObject(DbObject anObject, string indexToUse, bool errorOnObjectNotFound)
        {
            return (DbObject)LookupObjects(new LookupQuery(new DbObject[] { anObject }, indexToUse, null, 
                errorOnObjectNotFound))[0];
        }

        public DbObject LookupObject(DbObject anObject, string indexToUse, TypeAndFields typeAndFieldsToReturn)
        {
            return (DbObject)LookupObjects(new LookupQuery(new DbObject[] { anObject }, indexToUse, 
                new TypeAndFields[] { typeAndFieldsToReturn }, false))[0];
        }

        public DbObject LookupObject(DbObject anObject, string indexToUse, TypeAndFields typeAndFieldsToReturn, 
            bool errorOnObjectNotFound)
        {
            return (DbObject)LookupObjects(new LookupQuery(new DbObject[] { anObject }, indexToUse,
                new TypeAndFields[] { typeAndFieldsToReturn }, errorOnObjectNotFound))[0];
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
            return LookupObjectsInQueries(new LookupQuery[] { query })[0].Objects;
        }

        public IList<QueryResult> LookupObjectsInQueries(IList<LookupQuery> queries)
        {
            var request = new LookupObjectsRequest(queries);
            var response = (LookupObjectsResponse)ExecuteRequest(request);
            return response.Results;
        }

        public QueryResult SearchObjects(SearchQuery query)
        {
            return SearchObjects(new SearchQuery[] { query })[0];
        }

        public IList<QueryResult> SearchObjects(IList<SearchQuery> queries)
        {
            var request = new SearchObjectsRequest(queries);
            var response = (SearchObjectsResponse)ExecuteRequest(request);
            return response.Results;
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
                new TypeAndFields[] { typeAndFieldsToReturn });
        }

        public IList SearchFiles(string fileMaskToMatch, string fileMaskToNotMatch, int searchLimit, 
            string forEachVar, FileObject after, string where, string having,
            IList<Parameter> parameters, IList<TypeAndFields> typesAndFieldsToReturn)
        {
            var fileRange = new FileRange();
            SearchQuery query = CreateSearchFilesQuery(fileMaskToMatch, fileMaskToNotMatch, searchLimit,
                forEachVar, after, where, having, parameters, typesAndFieldsToReturn, fileRange);
            var queries = new SearchQuery[] { query };
            var request = new SearchObjectsRequest(queries);
            var response = (SearchObjectsResponse)ExecuteRequest(request);
            return response.Results[0].Objects;
        }

        public SearchQuery CreateSearchFilesQuery(string fileMaskToMatch, string fileMaskToNotMatch,
            int searchLimit, string forEachVar, FileObject after, string where, string having,
            IList<Parameter> parameters, IList<TypeAndFields> typesAndFieldsToReturn, FileRange fileRange)
        {
            if (string.IsNullOrEmpty(forEachVar))
                forEachVar = "X: FileObject";
            if (!string.IsNullOrEmpty(fileMaskToMatch) || !string.IsNullOrEmpty(fileMaskToNotMatch))
            {
                Parameter[] tempParameters;
                if (parameters != null)
                {
                    tempParameters = new Parameter[parameters.Count + 2];
                    parameters.CopyTo(tempParameters, 0);
                }
                else
                    tempParameters = new Parameter[2];
                tempParameters[tempParameters.Length - 2] = new Parameter("FileMaskToMatch", fileMaskToMatch);
                tempParameters[tempParameters.Length - 1] = new Parameter("FileMaskToNotMatch", fileMaskToNotMatch);
                parameters = tempParameters;
                string currentVarName = GetObjectVariableName(forEachVar);
                if (!string.IsNullOrEmpty(where))
                    where = string.Format("IsFileNameMatch({0}.FileName, {1}, {2}) and ({3})",
                        currentVarName, "{FileMaskToMatch}", "{FileMaskToNotMatch}", where);
                else
                    where = string.Format("IsFileNameMatch({0}.FileName, {1}, {2})",
                        currentVarName, "{FileMaskToMatch}", "{FileMaskToNotMatch}");
            }
            var result = new SearchQuery()
            {
                Parameters = parameters,
                LookupVar = forEachVar,
                LookupIn = "FileObject[+FileName]!",
                AfterObject = after,
                Where = where,
                Having = having,
                ReturnFields = typesAndFieldsToReturn,
                FileRange = fileRange,
                Limit = searchLimit,
            };
            return result;
        }

        // ExecuteRequest

        public DatabaseResponse ExecuteRequest(DatabaseRequest request)
        {
            DatabaseResponse result = null;
            string serverAddress = CurrentServerAddress;
            if (ServerAddressSelectionMode == ServerAddressSelectionMode.RandomPerCall)
                CurrentServerAddressNumber = (CurrentServerAddressNumber + 1) % ServerAddresses.Count;
            var stopwatch = new Stopwatch();
            int totalElapsedTimeInMilliseconds = 0;
            int retryCount = 0;
            int basePartOfDelayInMilliseconds = 0;
            bool success = false;
            while (!success)
            {
                stopwatch.Restart();
                result = ExecuteRequestNoRetry(serverAddress, request);
                int elapsedMilliseconds = (int)stopwatch.ElapsedMilliseconds;
                if (result is ErrorResponse)
                {
                    ErrorResponse error = result as ErrorResponse;
                    totalElapsedTimeInMilliseconds += elapsedMilliseconds;
                    if ((error.ErrorStatus == ErrorStatus.Retry) &&
                        (RetryLimit <= 0 || retryCount < RetryLimit) &&
                        (TimeoutInMilliseconds == Timeout.Infinite || totalElapsedTimeInMilliseconds < TimeoutInMilliseconds))
                    {
                        if (basePartOfDelayInMilliseconds < elapsedMilliseconds)
                            basePartOfDelayInMilliseconds = elapsedMilliseconds;
                        if (basePartOfDelayInMilliseconds < MaxDelayBeforeRetryInMilliseconds / 2)
                            basePartOfDelayInMilliseconds = basePartOfDelayInMilliseconds * 2;
                        else
                            basePartOfDelayInMilliseconds = MaxDelayBeforeRetryInMilliseconds / 2;
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
                            var newContext = new DatabaseClientContext(fContext);
                            newContext.DatabaseChangeNumbers = null;
                            Interlocked.Exchange(ref fContext, null);
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
            string requestUriString;
            bool useEnvironmentChangeNumbers = false;
            if (!(request is AdministrationRequest) || request is RefreshDatabaseCryptoKeyRequest ||
                request is GetDatabaseConfigurationRequest || request is AlterDatabaseConfigurationRequest ||
                request is GetDatabaseAccessModeRequest || request is SetDatabaseAccessModeRequest ||
                request is UnloadDatabaseRequest || request is LoadDatabaseRequest)
            {
                requestUriString = serverAddress.TrimEnd('/') + '/' + DatabaseName.TrimStart('/');
            }
            else
            {
                requestUriString = serverAddress.TrimEnd('/') + '/';
                useEnvironmentChangeNumbers = true;
            }
            //Temporary!!! Debug!!!
            //using (var fileStream = new FileStream(Path.Combine(Path.GetDirectoryName(
            //    Assembly.GetEntryAssembly().Location), "Log.ndef"), FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            //using (var ndefWriter = new NdefWriter(fileStream))
            //{
            //    var objectsReader = new ObjectsReader(TypeBinder, !(request is SaveObjectsRequest), new object[] { request });
            //    new NdefSerializer(ndefWriter).WriteDataSet(objectsReader);
            //}
            //Temporary!!! Debug!!!
            HttpWebRequest webRequest = CreateWebRequest(requestUriString, TimeoutInMilliseconds, fContext,
                useEnvironmentChangeNumbers);
            using (Stream requestStream = webRequest.GetRequestStream())
            using (var ndefWriter = new NdefWriter(requestStream))
            {
                ndefWriter.WriteDataSetStart(false, null);
                var saveObjectsRequest = request as SaveObjectsRequest;
                var objectsReader = new ObjectsReader(TypeBinder, saveObjectsRequest == null, new object[] { request });
                NdefSerializer.WriteObjects(objectsReader, ndefWriter);
                ndefWriter.WriteDataSetEnd();
                if (saveObjectsRequest != null)
                    FileContentHandler.WriteFiles(saveObjectsRequest, objectsReader.VisitedObjects, ndefWriter);
            }
            DatabaseClientContext newContext = new DatabaseClientContext(fContext);
            DatabaseResponse response = null;
            using (HttpWebResponse webResponse = GetWebResponse(webRequest, newContext, useEnvironmentChangeNumbers))
            {
                Interlocked.Exchange(ref fContext, newContext);
                using (Stream responseStream = webResponse.GetResponseStream())
                {
                    if (webResponse.StatusCode == HttpStatusCode.OK)
                    {
                        var deserializer = new NdefDeserializer(responseStream, NdefLinkingMode.OneWayLinkingAndOriginalOrder);
                        deserializer.SetTypeBinder(TypeBinder);
                        if (deserializer.MoveToNextDataSet())
                            response = deserializer.ReadObjects().FirstOrDefault() as DatabaseResponse;
                        if (response != null)
                            FileContentHandler.ReadFiles(deserializer);
                        else
                            throw new NezaboodkaException("invalid response");
                    }
                    else
                        throw new WebException(new StreamReader(responseStream).ReadToEnd());
                }
            }
            return response;
        }

        private static HttpWebRequest CreateWebRequest(string requestUriString, int timeout, DatabaseClientContext context,
            bool useEnvironmentChangeNumbers)
        {
            HttpWebRequest result = (HttpWebRequest)WebRequest.Create(requestUriString);
            result.Method = "POST";
            result.ContentType = "text/plain; charset=utf-8";
            result.Pipelined = false;
            result.Timeout = timeout;
            if (context != null)
            {
                //if (!string.IsNullOrEmpty(context.TrackingCode))
                //    result.Headers[Const.TrackingCodeHttpHeader] = context.TrackingCode;
                if (!string.IsNullOrEmpty(context.DatabaseListId))
                    result.Headers[Const.DatabaseListIdHttpHeader] = context.DatabaseListId;
                if (!string.IsNullOrEmpty(context.DatabaseConfigurationId))
                    result.Headers[Const.DatabaseConfigurationIdHttpHeader] = context.DatabaseConfigurationId;
                if (useEnvironmentChangeNumbers && !string.IsNullOrEmpty(context.EnvironmentChangeNumbers))
                    result.Headers[Const.ChangeNumbers] = context.EnvironmentChangeNumbers;
                else if (!useEnvironmentChangeNumbers && !string.IsNullOrEmpty(context.DatabaseChangeNumbers))
                    result.Headers[Const.ChangeNumbers] = context.DatabaseChangeNumbers;

            }
            return result;
        }

        private static HttpWebResponse GetWebResponse(WebRequest webRequest, DatabaseClientContext context,
            bool useEnvironmentChangeNumbers)
        {
            HttpWebResponse result = null;
            try
            {
                result = (HttpWebResponse)webRequest.GetResponse();
                context.TrackingCode = result.Headers[Const.TrackingCodeHttpHeader];
                context.DatabaseListId = result.Headers[Const.DatabaseListIdHttpHeader];
                context.DatabaseConfigurationId =
                    result.Headers[Const.DatabaseConfigurationIdHttpHeader] ?? context.DatabaseConfigurationId;
                if (useEnvironmentChangeNumbers)
                    context.EnvironmentChangeNumbers = result.Headers[Const.ChangeNumbers];
                else
                    context.DatabaseChangeNumbers = result.Headers[Const.ChangeNumbers];

            }
            catch (WebException e)
            {
                if (e.Status == WebExceptionStatus.ProtocolError)
                {
                    var response = (HttpWebResponse)e.Response;
                    if (response.StatusCode == HttpStatusCode.InternalServerError)
                    {
                        using (var stream = response.GetResponseStream())
                        using (var reader = new StreamReader(stream))
                            throw new NezaboodkaException(reader.ReadToEnd());
                    }
                    else
                        throw;
                }
                else
                    throw;
            }
            return result;
        }

        private static string GetObjectVariableName(string objectDefinition)
        {
            string result = null;
            string[] s = objectDefinition.Split(DelimiterBetweenIdentifierAndTypeName, StringSplitOptions.RemoveEmptyEntries);
            if (s.Length == 2)
                result = s[0].Trim();
            else
                throw new NezaboodkaException(string.Format("object identifier definition '{0}' is wrong, " +
                    "the valid format is 'identifier : type-name'", objectDefinition));
            return result;
        }
    }

    internal class DatabaseClientContext
    {
        public string TrackingCode;
        public string DatabaseListId;
        public string DatabaseConfigurationId;
        public string DatabaseChangeNumbers;
        public string EnvironmentChangeNumbers;


        public DatabaseClientContext()
        {
            TrackingCode = string.Empty;
            DatabaseListId = string.Empty;
            DatabaseConfigurationId = string.Empty;
            DatabaseChangeNumbers = string.Empty;
            EnvironmentChangeNumbers = string.Empty;
        }

        public DatabaseClientContext(DatabaseClientContext existing)
        {
            TrackingCode = existing.TrackingCode;
            DatabaseListId = existing.DatabaseListId;
            DatabaseConfigurationId = existing.DatabaseConfigurationId;
            DatabaseChangeNumbers = existing.DatabaseChangeNumbers;
            EnvironmentChangeNumbers = existing.EnvironmentChangeNumbers;
        }
    }

    public enum ServerAddressSelectionMode
    {
        FirstAvailable,
        RandomPerSession,
        RandomPerCall
    }

    // Constants

    public static partial class Const
    {
        public static string TrackingCodeHttpHeader = "X-Nezaboodka-TrackingCode";
        public static string DatabaseListIdHttpHeader = "X-Nezaboodka-DatabaseListId";
        public static string DatabaseConfigurationIdHttpHeader = "X-Nezaboodka-DatabaseConfigurationId";
        public static string ChangeNumbers = "X-Nezaboodka-ChangeNumbers";
        public static string AccessTokenHttpRequestHeader = "X-Nezaboodka-Access-Token";
        public static string WriteModeHttpRequestHeader = "X-Nezaboodka-Write-Mode";
        public static string DeleteHttpRequestHeader = "X-Nezaboodka-Delete";
        public const int DefaultFileBlockSize = 4096;
    }
}
