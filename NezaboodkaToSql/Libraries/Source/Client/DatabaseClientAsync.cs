using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Nezaboodka.Ndef;

namespace Nezaboodka
{
    public partial class DatabaseClient
    {
        // Asynchronous Administration

        public async Task<EnvironmentConfiguration> GetEnvironmentConfigurationAsync()
        {
            var request = new GetEnvironmentConfigurationRequest();
            var response = (GetEnvironmentConfigurationResponse)await ExecuteRequestAsync(request).ConfigureAwait(false);
            return response.EnvironmentConfiguration;
        }

        public async Task<IList<string>> GetDatabaseListAsync()
        {
            var request = new GetDatabaseListRequest();
            DatabaseResponse response = await ExecuteRequestAsync(request).ConfigureAwait(false);
            IList<string> result = ((GetDatabaseListResponse)response).DatabaseNames;
            if (result == null)
                result = new List<string>();
            return result;
        }

        public async Task<IList<string>> AlterDatabaseListAsync(IList<string> databaseNamesToAdd,
            IList<string> databaseNamesToRemove)
        {
            var request = new AlterDatabaseListRequest();
            request.DatabaseNamesToAdd = databaseNamesToAdd;
            request.DatabaseNamesToRemove = databaseNamesToRemove;
            DatabaseResponse response = await ExecuteRequestAsync(request).ConfigureAwait(false);
            IList<string> result = ((AlterDatabaseListResponse)response).DatabaseNames;
            if (result == null)
                result = new List<string>();
            return result;
        }

        public async Task<DatabaseConfiguration> GetDatabaseConfigurationAsync()
        {
            var request = new GetDatabaseConfigurationRequest();
            DatabaseResponse response = await ExecuteRequestAsync(request).ConfigureAwait(false);
            DatabaseConfiguration result = ((GetDatabaseConfigurationResponse)response).DatabaseConfiguration;
            return result;
        }

        public async Task<DatabaseConfiguration> AlterDatabaseConfigurationAsync(DatabaseConfiguration databaseConfiguration)
        {
            var request = new AlterDatabaseConfigurationRequest(databaseConfiguration);
            DatabaseResponse response = await ExecuteRequestAsync(request).ConfigureAwait(false);
            DatabaseConfiguration result = ((AlterDatabaseConfigurationResponse)response).DatabaseConfiguration;
            return result;
        }

        public async Task<DatabaseAccessMode> GetDatabaseAccessModeAsync()
        {
            var request = new GetDatabaseAccessModeRequest();
            DatabaseResponse response = await ExecuteRequestAsync(request).ConfigureAwait(false);
            DatabaseAccessMode result = ((GetDatabaseAccessModeResponse)response).DatabaseAccessMode;
            return result;
        }

        public async Task<DatabaseAccessMode> SetDatabaseAccessModeAsync(DatabaseAccessMode databaseAccessMode,
            bool createDatabaseSnapshot)
        {
            var request = new SetDatabaseAccessModeRequest(databaseAccessMode, createDatabaseSnapshot);
            DatabaseResponse response = await ExecuteRequestAsync(request).ConfigureAwait(false);
            DatabaseAccessMode result = ((SetDatabaseAccessModeResponse)response).PreviousDatabaseAccessMode;
            return result;
        }

        public async Task RefreshDatabaseCryptoKeyAsync(int timeoutForPreviousKeyInMilliseconds)
        {
            var request = new RefreshDatabaseCryptoKeyRequest(timeoutForPreviousKeyInMilliseconds);
            await ExecuteRequestAsync(request).ConfigureAwait(false);
        }

        public async Task RefreshEnvironmentCryptoKeyAsync(int timeoutForPreviousKeyInMilliseconds)
        {
            var request = new RefreshEnvironmentCryptoKeyRequest(timeoutForPreviousKeyInMilliseconds);
            await ExecuteRequestAsync(request).ConfigureAwait(false);
        }

        public async Task CleanupRemovedDatabasesAsync()
        {
            var request = new CleanupRemovedDatabasesRequest();
            await ExecuteRequestAsync(request).ConfigureAwait(false);
        }

        // Asynchronous Database

        public async Task<DbObject> SaveObjectAsync(DbObject anObject)
        {
            IList result = await SaveObjectsAsync(new SaveQuery(new DbObject[] { anObject }, null, null, false))
                .ConfigureAwait(false);
            return (DbObject)result?[0];
        }

        public async Task<DbObject> SaveObjectAsync(DbObject anObject, bool errorOnRevisionMismatch)
        {
            IList result = await SaveObjectsAsync(new SaveQuery(new DbObject[] { anObject }, null, null, 
                errorOnRevisionMismatch)).ConfigureAwait(false);
            return (DbObject)result?[0];
        }

        public async Task<DbObject> SaveObjectAsync(DbObject anObject, TypeAndFields typeAndFieldsToSave)
        {
            IList result = await SaveObjectsAsync(new SaveQuery(new DbObject[] { anObject }, 
                new TypeAndFields[] { typeAndFieldsToSave }, null, false)).ConfigureAwait(false);
            return (DbObject)result?[0];
        }

        public async Task<DbObject> SaveObjectAsync(DbObject anObject, TypeAndFields typeAndFieldsToSave,
            bool errorOnRevisionMismatch)
        {
            IList result = await SaveObjectsAsync(new SaveQuery(new DbObject[] { anObject }, 
                new TypeAndFields[] { typeAndFieldsToSave }, null, errorOnRevisionMismatch)).ConfigureAwait(false);
            return (DbObject)result?[0];
        }

        public async Task<DbObject> SaveObjectAsync(DbObject anObject, TypeAndFields typeAndFieldsToSave,
            TypeAndFields typeAndFieldsToReturn)
        {
            IList result = await SaveObjectsAsync(new SaveQuery(new DbObject[] { anObject }, 
                new TypeAndFields[] { typeAndFieldsToSave }, new TypeAndFields[] { typeAndFieldsToReturn }, false))
                .ConfigureAwait(false);
            return (DbObject)result?[0];
        }

        public async Task<DbObject> SaveObjectAsync(DbObject anObject, TypeAndFields typeAndFieldsToSave,
            TypeAndFields typeAndFieldsToReturn, bool errorOnRevisionMismatch)
        {
            IList result = await SaveObjectsAsync(new SaveQuery(new DbObject[] { anObject }, 
                new TypeAndFields[] { typeAndFieldsToSave }, new TypeAndFields[] { typeAndFieldsToReturn }, 
                errorOnRevisionMismatch)).ConfigureAwait(false);
            return (DbObject)result?[0];
        }

        public async Task<IList> SaveObjectsAsync(IList objects)
        {
            IList result = await SaveObjectsAsync(new SaveQuery(objects, null, null, false)).ConfigureAwait(false);
            return result;
        }

        public async Task<IList> SaveObjectsAsync(IList objects, bool errorOnRevisionMismatch)
        {
            IList result = await SaveObjectsAsync(new SaveQuery(objects, null, null, errorOnRevisionMismatch))
                .ConfigureAwait(false);
            return result;
        }

        public async Task<IList> SaveObjectsAsync(TypeAndFields typeAndFieldsToSave, IList objects)
        {
            IList result = await SaveObjectsAsync(new SaveQuery(objects, new TypeAndFields[] { typeAndFieldsToSave },
                null, false)).ConfigureAwait(false);
            return result;
        }

        public async Task<IList> SaveObjectsAsync(TypeAndFields typeAndFieldsToSave, IList objects,
            bool errorOnRevisionMismatch)
        {
            IList result = await SaveObjectsAsync(new SaveQuery(objects, new TypeAndFields[] { typeAndFieldsToSave },
                null, errorOnRevisionMismatch)).ConfigureAwait(false);
            return result;
        }

        public async Task<IList> SaveObjectsAsync(TypeAndFields typeAndFieldsToSave, TypeAndFields typeAndFieldsToReturn,
            IList objects)
        {
            IList result = await SaveObjectsAsync(new SaveQuery(objects, new TypeAndFields[] { typeAndFieldsToSave },
                new TypeAndFields[] { typeAndFieldsToReturn }, false)).ConfigureAwait(false);
            return result;
        }

        public async Task<IList> SaveObjectsAsync(TypeAndFields typeAndFieldsToSave, TypeAndFields typeAndFieldsToReturn, 
            IList objects, bool errorOnRevisionMismatch)
        {
            IList result = await SaveObjectsAsync(new SaveQuery(objects, new TypeAndFields[] { typeAndFieldsToSave },
                new TypeAndFields[] { typeAndFieldsToReturn }, errorOnRevisionMismatch)).ConfigureAwait(false);
            return result;
        }

        public async Task<IList> SaveObjectsAsync(IList<TypeAndFields> typesAndFieldsToSave, IList objects)
        {
            IList result = await SaveObjectsAsync(new SaveQuery(objects, typesAndFieldsToSave, null, false))
                .ConfigureAwait(false);
            return result;
        }

        public async Task<IList> SaveObjectsAsync(IList<TypeAndFields> typesAndFieldsToSave, IList objects,
            bool errorOnRevisionMismatch)
        {
            IList result = await SaveObjectsAsync(new SaveQuery(objects, typesAndFieldsToSave, null, 
                errorOnRevisionMismatch)).ConfigureAwait(false);
            return result;
        }

        public async Task<IList> SaveObjectsAsync(IList<TypeAndFields> typesAndFieldsToSave, 
            IList<TypeAndFields> typesAndFieldsToReturn, IList objects)
        {
            IList result = await SaveObjectsAsync(new SaveQuery(objects, typesAndFieldsToSave, typesAndFieldsToReturn,
                false)).ConfigureAwait(false);
            return result;
        }

        public async Task<IList> SaveObjectsAsync(IList<TypeAndFields> typesAndFieldsToSave,
            IList<TypeAndFields> typesAndFieldsToReturn, IList objects, bool errorOnRevisionMismatch)
        {
            IList result = await SaveObjectsAsync(new SaveQuery(objects, typesAndFieldsToSave, typesAndFieldsToReturn,
                errorOnRevisionMismatch)).ConfigureAwait(false);
            return result;
        }

        public async Task<IList> SaveObjectsAsync(SaveQuery query)
        {
            IList<QueryResult> result = await SaveObjectsInQueriesAsync(new SaveQuery[] {query}).ConfigureAwait(false);
            return result[0].Objects;
        }

        public async Task<IList<QueryResult>> SaveObjectsInQueriesAsync(IList<SaveQuery> queries)
        {
            var request = new SaveObjectsRequest(queries);
            var response = await ExecuteRequestAsync(request).ConfigureAwait(false);
            return ((SaveObjectsResponse)response).Results;
        }

        public async Task<long> DeleteObjectAsync(DbKey objectKey)
        {
            return await DeleteObjectsAsync(new DeleteQuery(new DbKey[] { objectKey }, false))
                .ConfigureAwait(false);
        }

        public async Task<long> DeleteObjectAsync(DbKey objectKey, bool errorOnObjectNotFound)
        {
            return await DeleteObjectsAsync(new DeleteQuery(new DbKey[] { objectKey }, 
                errorOnObjectNotFound)).ConfigureAwait(false);
        }

        public async Task<long> DeleteObjectAsync(DbKey objectKey, TypeAndFields typeAndFieldsWithObjectsToDelete,
            bool errorOnObjectNotFound)
        {
            return await DeleteObjectsAsync(new DeleteQuery(new DbKey[] { objectKey }, 
                new List<TypeAndFields>() { typeAndFieldsWithObjectsToDelete }, 
                errorOnObjectNotFound)).ConfigureAwait(false);
        }

        public async Task<long> DeleteObjectsAsync(List<DbKey> objectKeys)
        {
            return await DeleteObjectsAsync(new DeleteQuery(objectKeys, false)).ConfigureAwait(false);
        }

        public async Task<long> DeleteObjectsAsync(DbKey[] objectKeys)
        {
            return await DeleteObjectsAsync(new DeleteQuery(objectKeys, false)).ConfigureAwait(false);
        }

        public async Task<long> DeleteObjectsAsync(IList<TypeAndFields> typesAndFieldsWithDetailObjectsToDelete,
            List<DbKey> objectKeys)
        {
            return await DeleteObjectsAsync(new DeleteQuery(objectKeys, typesAndFieldsWithDetailObjectsToDelete, 
                false)).ConfigureAwait(false);
        }

        public async Task<long> DeleteObjectsAsync(IList<TypeAndFields> typesAndFieldsWithDetailObjectsToDelete,
            DbKey[] objectKeys)
        {
            return await DeleteObjectsAsync(new DeleteQuery(objectKeys, typesAndFieldsWithDetailObjectsToDelete,
                false)).ConfigureAwait(false);
        }

        public async Task<long> DeleteObjectsAsync(IList<TypeAndFields> typesAndFieldsWithDetailObjectsToDelete,
            List<DbKey> objectKeys, bool errorOnObjectNotFound)
        {
            return await DeleteObjectsAsync(new DeleteQuery(objectKeys, typesAndFieldsWithDetailObjectsToDelete,
                errorOnObjectNotFound)).ConfigureAwait(false); ;
        }

        public async Task<long> DeleteObjectsAsync(IList<TypeAndFields> typesAndFieldsWithDetailObjectsToDelete,
            DbKey[] objectKeys, bool errorOnObjectNotFound)
        {
            return await DeleteObjectsAsync(new DeleteQuery(objectKeys, typesAndFieldsWithDetailObjectsToDelete,
                errorOnObjectNotFound)).ConfigureAwait(false); ;
        }

        public async Task<long> DeleteObjectsAsync(DeleteQuery query)
        {
            return await DeleteObjectsInQueriesAsync(new DeleteQuery[] { query }).ConfigureAwait(false);
        }

        public async Task<long> DeleteObjectsInQueriesAsync(IList<DeleteQuery> queries)
        {
            var request = new DeleteObjectsRequest(queries);
            DatabaseResponse response = await ExecuteRequestAsync(request).ConfigureAwait(false);
            return ((DeleteObjectsResponse)response).DeletedObjectCount;
        }

        public async Task<object> GetObjectAsync(DbKey objectKey)
        {
            IList result = await GetObjectsAsync(new GetQuery(null, new DbKey[] { objectKey },
                null, null, false, null)).ConfigureAwait(false);
            return result?[0];
        }

        public async Task<object> GetObjectAsync(DbKey objectKey, bool errorOnObjectNotFound)
        {
            IList result = await GetObjectsAsync(new GetQuery(null, new DbKey[] { objectKey },
                null, null, errorOnObjectNotFound, null)).ConfigureAwait(false);
            return result?[0];
        }

        public async Task<object> GetObjectAsync(DbKey objectKey, TypeAndFields typeAndFieldsToReturn)
        {
            IList result = await GetObjectsAsync(new GetQuery(null, new DbKey[] { objectKey },
                new TypeAndFields[] { typeAndFieldsToReturn }, null, false, null)).ConfigureAwait(false);
            return result?[0];
        }

        public async Task<object> GetObjectAsync(DbKey objectKey, TypeAndFields typeAndFieldsToReturn,
            bool errorOnObjectNotFound)
        {
            IList result = await GetObjectsAsync(new GetQuery(null, new DbKey[] { objectKey }, 
                new TypeAndFields[] { typeAndFieldsToReturn }, null, errorOnObjectNotFound, null)).ConfigureAwait(false);
            return result?[0];
        }

        public async Task<IList> GetObjectsAsync(IList<DbKey> objectKeys)
        {
            IList result = await GetObjectsAsync(new GetQuery(null, objectKeys, null, null, false, null))
                .ConfigureAwait(false);
            return result;
        }

        public async Task<IList> GetObjectsAsync(IList<DbKey> objectKeys, bool errorOnObjectNotFound)
        {
            IList result = await GetObjectsAsync(new GetQuery(null, objectKeys, null, null,
                errorOnObjectNotFound, null)).ConfigureAwait(false);
            return result;
        }

        public async Task<IList> GetObjectsAsync(IList<DbKey> objectKeys, IList<TypeAndFields> typesAndFieldsToReturn)
        {
            IList result = await GetObjectsAsync(new GetQuery(null, objectKeys, typesAndFieldsToReturn, null,
                false, null)).ConfigureAwait(false);
            return result;
        }

        public async Task<IList> GetObjectsAsync(IList<DbKey> objectKeys, IList<TypeAndFields> typesAndFieldsToReturn, 
            bool errorOnObjectNotFound)
        {
            IList result = await GetObjectsAsync(new GetQuery(null, objectKeys, typesAndFieldsToReturn, null, 
                errorOnObjectNotFound, null)).ConfigureAwait(false);
            return result;
        }

        public async Task<IList> GetObjectsAsync(GetQuery query)
        {
            IList<QueryResult> result = await GetObjectsInQueriesAsync(new GetQuery[] { query }).ConfigureAwait(false);
            return result[0].Objects;
        }

        public async Task<IList<QueryResult>> GetObjectsInQueriesAsync(IList<GetQuery> queries)
        {
            var request = new GetObjectsRequest(queries);
            DatabaseResponse response = await ExecuteRequestAsync(request).ConfigureAwait(false);
            return ((GetObjectsResponse)response).Results;
        }

        public async Task<DbObject> LookupObjectAsync(DbObject anObject, string indexToUse)
        {
            IList result = await LookupObjectsAsync(new LookupQuery(new DbObject[] { anObject }, indexToUse, null, 
                false)).ConfigureAwait(false);
            return (DbObject)result?[0];
        }

        public async Task<DbObject> LookupObjectAsync(DbObject anObject, string indexToUse, bool errorOnObjectNotFound)
        {
            IList result = await LookupObjectsAsync(new LookupQuery(new DbObject[] { anObject }, indexToUse, null, 
                errorOnObjectNotFound)).ConfigureAwait(false);
            return (DbObject)result?[0];
        }

        public async Task<DbObject> LookupObjectAsync(DbObject anObject, string indexToUse, 
            TypeAndFields typeAndFieldsToReturn)
        {
            IList result = await LookupObjectsAsync(new LookupQuery(new DbObject[] { anObject }, indexToUse,
                new TypeAndFields[] { typeAndFieldsToReturn }, false)).ConfigureAwait(false);
            return (DbObject)result?[0];
        }

        public async Task<DbObject> LookupObjectAsync(DbObject anObject, string indexToUse, 
            TypeAndFields typeAndFieldsToReturn, bool errorOnObjectNotFound)
        {
            IList result = await LookupObjectsAsync(new LookupQuery(new DbObject[] { anObject }, indexToUse,
                new TypeAndFields[] { typeAndFieldsToReturn }, errorOnObjectNotFound)).ConfigureAwait(false);
            return (DbObject)result?[0];
        }

        public async Task<IList> LookupObjectsAsync(IList objectsToLookup, string indexToUse)
        {
            IList result = await LookupObjectsAsync(new LookupQuery(objectsToLookup, indexToUse, null, 
                false)).ConfigureAwait(false);
            return result;
        }

        public async Task<IList> LookupObjectsAsync(IList objectsToLookup, string indexToUse, bool errorOnObjectNotFound)
        {
            IList result = await LookupObjectsAsync(new LookupQuery(objectsToLookup, indexToUse, null, 
                errorOnObjectNotFound)).ConfigureAwait(false);
            return result;
        }

        public async Task<IList> LookupObjectsAsync(IList objectsToLookup, string indexToUse, 
            IList<TypeAndFields> typesAndFieldsToReturn)
        {
            IList result = await LookupObjectsAsync(new LookupQuery(objectsToLookup, indexToUse, typesAndFieldsToReturn,
                false)).ConfigureAwait(false);
            return result;
        }

        public async Task<IList> LookupObjectsAsync(IList objectsToLookup, string indexToUse, 
            IList<TypeAndFields> typesAndFieldsToReturn, bool errorOnObjectNotFound)
        {
            IList result = await LookupObjectsAsync(new LookupQuery(objectsToLookup, indexToUse, typesAndFieldsToReturn,
                errorOnObjectNotFound)).ConfigureAwait(false);
            return result;
        }

        public async Task<IList> LookupObjectsAsync(LookupQuery query)
        {
            IList<QueryResult> result = await LookupObjectsInQueriesAsync(new LookupQuery[] {query}).ConfigureAwait(false);
            return result[0].Objects;
        }

        public async Task<IList<QueryResult>> LookupObjectsInQueriesAsync(IList<LookupQuery> queries)
        {
            var request = new LookupObjectsRequest(queries);
            DatabaseResponse response = await ExecuteRequestAsync(request).ConfigureAwait(false);
            return ((LookupObjectsResponse)response).Results;
        }

        public async Task<IList> SearchObjectsAsync(SearchQuery query)
        {
            IList<QueryResult> result = await SearchObjectsAsync(new SearchQuery[] {query}).ConfigureAwait(false);
            return result[0].Objects;
        }

        public async Task<IList<QueryResult>> SearchObjectsAsync(IList<SearchQuery> queries)
        {
            var request = new SearchObjectsRequest(queries);
            DatabaseResponse response = await ExecuteRequestAsync(request).ConfigureAwait(false);
            return ((SearchObjectsResponse)response).Results;
        }

        // Asynchronous Files

        public async Task<FileObject> GetFileAsync(string fileName)
        {
            FileObject result = null;
            IList results = await SearchFilesAsync(fileName, null, 1, null, null, null, null, null, null).ConfigureAwait(false);
            if (results != null && results.Count == 1)
                result = results[0] as FileObject;
            return result;
        }

        public Task<IList> SearchFilesAsync(string fileMaskToMatch, int searchLimit, FileObject startAfter,
            TypeAndFields typeAndFieldsToReturn)
        {
            return SearchFilesAsync(fileMaskToMatch, null, searchLimit, null, startAfter, null, null, null,
                new TypeAndFields[] { typeAndFieldsToReturn });
        }

        public async Task<IList> SearchFilesAsync(string fileMaskToMatch, string fileMaskToNotMatch, int searchLimit,
            string forEachVar, FileObject after, string where, string having,
            IList<Parameter> parameters, IList<TypeAndFields> typesAndFieldsToReturn)
        {
            var fileRange = new FileRange();
            SearchQuery query = CreateSearchFilesQuery(fileMaskToMatch, fileMaskToNotMatch, searchLimit,
                forEachVar, after, where, having, parameters, typesAndFieldsToReturn, fileRange);
            var queries = new SearchQuery[] { query };
            var request = new SearchObjectsRequest(queries);
            var response = (SearchObjectsResponse)await ExecuteRequestAsync(request).ConfigureAwait(false);
            return response.Results[0].Objects;
        }

        // Asynchronous ExecuteRequest

        private async Task<DatabaseResponse> ExecuteRequestAsync(DatabaseRequest request)
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
                result = await ExecuteRequestNoRetryAsync(serverAddress, request).ConfigureAwait(false);
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

        // Asynchronous Internal

        private async Task<DatabaseResponse> ExecuteRequestNoRetryAsync(string serverAddress, DatabaseRequest request)
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
                    await FileContentHandler.WriteFilesAsync(saveObjectsRequest, objectsReader.VisitedObjects, ndefWriter)
                        .ConfigureAwait(false);
            }
            DatabaseClientContext newContext = new DatabaseClientContext(fContext);
            DatabaseResponse response = null;
            using (HttpWebResponse webResponse = await GetWebResponseAsync(webRequest, newContext,
                useEnvironmentChangeNumbers).ConfigureAwait(false))
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
                            await FileContentHandler.ReadFilesAsync(deserializer).ConfigureAwait(false);
                        else
                            throw new NezaboodkaException("invalid response");
                    }
                    else
                        throw new WebException(new StreamReader(responseStream).ReadToEnd());
                }
            }
            return response;
        }

        private static async Task<HttpWebResponse> GetWebResponseAsync(WebRequest webRequest, DatabaseClientContext context,
            bool useEnvironmentChangeNumbers)
        {
            HttpWebResponse result = null;
            try
            {
                result = (HttpWebResponse)await webRequest.GetResponseAsync().ConfigureAwait(false);
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
    }
}
