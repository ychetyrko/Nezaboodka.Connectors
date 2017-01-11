using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Nezaboodka
{
    public class DatabaseRequest
    {
    }

    public class DatabaseResponse
    {
    }

    public class ErrorResponse : DatabaseResponse
    {
        public int ErrorStatus;
        public string ErrorMessage;
    }

    // Administration

    public class AdministrationRequest : DatabaseRequest
    {
    }

    public class AdministrationResponse : DatabaseResponse
    {
    }

    // GetServerInfo

    public class GetServerInfoRequest : AdministrationRequest
    {
    }

    public class GetServerInfoResponse : AdministrationResponse
    {
        public string UpTime;
        public string Version;
        public string Build;
    }

    // GetEnvironment

    public class GetEnvironmentConfigurationRequest : AdministrationRequest
    {
    }

    public class GetEnvironmentConfigurationResponse : AdministrationResponse
    {
        public EnvironmentConfiguration EnvironmentConfiguration;

        public GetEnvironmentConfigurationResponse()
        {
        }

        public GetEnvironmentConfigurationResponse(EnvironmentConfiguration environmentConfiguration)
        {
            EnvironmentConfiguration = environmentConfiguration;
        }
    }

    // GetDatabaseList

    public class GetDatabaseListRequest : AdministrationRequest
    {
    }

    public class GetDatabaseListResponse : AdministrationResponse
    {
        public IList<string> DatabaseNames;

        public GetDatabaseListResponse()
        {
        }

        public GetDatabaseListResponse(IList<string> databaseNames)
        {
            DatabaseNames = databaseNames;
        }
    }

    // AlterDatabaseList

    public class AlterDatabaseListRequest : AdministrationRequest
    {
        public IList<string> DatabaseNamesToAdd;
        public IList<string> DatabaseNamesToRemove;
    }

    public class AlterDatabaseListResponse : AdministrationResponse
    {
        public IList<string> DatabaseNames;

        public AlterDatabaseListResponse()
        {
        }

        public AlterDatabaseListResponse(IList<string> databaseNames)
        {
            DatabaseNames = databaseNames;
        }
    }

    // GetDatabaseConfiguration

    public class GetDatabaseConfigurationRequest : AdministrationRequest
    {
    }

    public class GetDatabaseConfigurationResponse : AdministrationResponse
    {
        public DatabaseConfiguration DatabaseConfiguration;

        public GetDatabaseConfigurationResponse()
        {
        }

        public GetDatabaseConfigurationResponse(DatabaseConfiguration databaseConfiguration)
        {
            DatabaseConfiguration = databaseConfiguration;
        }
    }

    // AlterDatabaseConfiguration

    public class AlterDatabaseConfigurationRequest : AdministrationRequest
    {
        public DatabaseConfiguration DatabaseConfiguration;

        public AlterDatabaseConfigurationRequest()
        {
        }

        public AlterDatabaseConfigurationRequest(DatabaseConfiguration databaseConfiguration)
        {
            DatabaseConfiguration = databaseConfiguration;
        }
    }

    public class AlterDatabaseConfigurationResponse : AdministrationResponse
    {
        public DatabaseConfiguration DatabaseConfiguration;
    }

    // GetDatabaseAccessMode

    public enum DatabaseAccessMode
    {
        ReadWrite = 0,
        ReadOnly = 1,
        NoAccess = 2
    }

    public class GetDatabaseAccessModeRequest : AdministrationRequest
    {
    }

    public class GetDatabaseAccessModeResponse : AdministrationResponse
    {
        public DatabaseAccessMode DatabaseAccessMode;

        public GetDatabaseAccessModeResponse()
        {
        }

        public GetDatabaseAccessModeResponse(DatabaseAccessMode databaseAccessMode)
        {
            DatabaseAccessMode = databaseAccessMode;
        }
    }

    // SetDatabaseAccessMode

    public class SetDatabaseAccessModeRequest : AdministrationRequest
    {
        public DatabaseAccessMode DatabaseAccessMode;
        public bool CreateDatabaseSnapshot;

        public SetDatabaseAccessModeRequest()
        {
        }

        public SetDatabaseAccessModeRequest(DatabaseAccessMode databaseAccessMode, bool createDatabaseSnapshot)
        {
            DatabaseAccessMode = databaseAccessMode;
            CreateDatabaseSnapshot = createDatabaseSnapshot;
        }
    }

    public class SetDatabaseAccessModeResponse : AdministrationResponse
    {
        public DatabaseAccessMode PreviousDatabaseAccessMode;

        public SetDatabaseAccessModeResponse()
        {
        }

        public SetDatabaseAccessModeResponse(DatabaseAccessMode previousDatabaseAccessMode)
        {
            PreviousDatabaseAccessMode = previousDatabaseAccessMode;
        }
    }

    // UnloadDatabase

    public class UnloadDatabaseRequest : AdministrationRequest
    {
        public UnloadDatabaseRequest()
        {
        }
    }

    public class UnloadDatabaseResponse : AdministrationResponse
    {
        public UnloadDatabaseResponse()
        {
        }
    }

    // LoadDatabase

    public class LoadDatabaseRequest : AdministrationRequest
    {
        public LoadDatabaseRequest()
        {
        }
    }

    public class LoadDatabaseResponse : AdministrationResponse
    {
        public LoadDatabaseResponse()
        {
        }
    }

    // RefreshDatabaseCryptoKey

    public class RefreshDatabaseCryptoKeyRequest : AdministrationRequest
    {
        public int TimeoutForPreviousKeyInMilliseconds;

        public RefreshDatabaseCryptoKeyRequest()
        {
        }

        public RefreshDatabaseCryptoKeyRequest(int timeoutForPreviousKeyInMilliseconds)
        {
            TimeoutForPreviousKeyInMilliseconds = timeoutForPreviousKeyInMilliseconds;
        }
    }

    public class RefreshDatabaseCryptoKeyResponse : AdministrationResponse
    {
    }

    // RefreshEnvironmentCryptoKey

    public class RefreshEnvironmentCryptoKeyRequest : AdministrationRequest
    {
        public int TimeoutForPreviousKeyInMilliseconds;

        public RefreshEnvironmentCryptoKeyRequest()
        {
        }

        public RefreshEnvironmentCryptoKeyRequest(int timeoutForPreviousKeyInMilliseconds)
        {
            TimeoutForPreviousKeyInMilliseconds = timeoutForPreviousKeyInMilliseconds;
        }
    }

    public class RefreshEnvironmentCryptoKeyResponse : AdministrationResponse
    {
    }

    // CleanupRemovedDatabases

    public class CleanupRemovedDatabasesRequest : AdministrationRequest
    {
    }

    public class CleanupRemovedDatabasesResponse : AdministrationResponse
    {
    }

    // SaveObjects

    public class SaveObjectsRequest : DatabaseRequest
    {
        public IList<SaveQuery> Queries;

        public SaveObjectsRequest()
        {
        }

        public SaveObjectsRequest(IList<SaveQuery> queries)
        {
            Queries = queries;
        }
    }

    public class SaveObjectsResponse : DatabaseResponse
    {
        public IList<QueryResult> Results;

        public SaveObjectsResponse()
        {
        }

        public SaveObjectsResponse(IList<QueryResult> results)
        {
            Results = results;
        }
    }

    public class SaveQuery
    {
        public static readonly TypeAndFields[] AllTypesAndFields = new TypeAndFields[0];

        public string Name;
        public IList<Parameter> Parameters;       // new List<Parameter>() { new Parameter() { Name = "T", Value = "Dev" } }
        public string ForEachVar;                 // "G: Group"
        public IList ForEachIn;                   // new List<DbObject>() { group1, group2 }
        public string LookupVar;                  // "X: Group"
        public string LookupIn;                   // "Group[+Title]"
        public string Where;                      // "X.Title.StartsWith(T)"
        public string Having;                     // "X.Participants.Count > 0"
        public IList<TypeAndFields> SaveFields;   // new List<TypeAndFields> { TypeAndFields.Parse("Group{Title, Participants}") }
        public IList<TypeAndFields> ReturnFields; // new List<TypeAndFields> { TypeAndFields.Parse("Group{Id, Title, Participants}") }
        public bool ErrorOnRevisionMismatch;

        public SaveQuery()
        {
        }

        public SaveQuery(IList objects, IList<TypeAndFields> save, IList<TypeAndFields> returnFields, bool errorOnRevisionMismatch)
        {
            SaveFields = save;
            ReturnFields = returnFields;
            ForEachIn = objects;
            ErrorOnRevisionMismatch = errorOnRevisionMismatch;
        }

        public SaveQuery(string name, IList<Parameter> parameters, string forEachVar, IList forEachIn, string lookupVar, 
            string lookupIn, string where, string having, IList<TypeAndFields> saveFields, IList<TypeAndFields> returnFields, 
            bool errorOnRevisionMismatch)
        {
            Name = name;
            Parameters = parameters;
            ForEachVar = forEachVar;
            ForEachIn = forEachIn;
            LookupVar = lookupVar;
            LookupIn = lookupIn;
            Where = where;
            Having = having;
            SaveFields = saveFields;
            ReturnFields = returnFields;
            ErrorOnRevisionMismatch = errorOnRevisionMismatch;
        }
    }

    public struct TypeAndFields
    {
        public string TypeName;
        public IList<string> FieldNames;
        public bool Inversion;
        public bool AllFields { get { return FieldNames == null || FieldNames.Count == 0; } }

        public TypeAndFields(string typeName, IList<string> fieldNames, bool inversion)
        {
            TypeName = typeName;
            FieldNames = fieldNames;
            Inversion = inversion;
        }

        public TypeAndFields(string typeName, IList<string> fieldNames)
            : this(typeName, fieldNames, false)
        {
        }

        public TypeAndFields(string str)
        {
            this = QueryParser.ParseTypeAndFields(str);
        }

        public static TypeAndFields Parse(string str)
        {
            return QueryParser.ParseTypeAndFields(str);
        }

        public override string ToString()
        {
            var result = new StringBuilder(TypeName);
            if (Inversion)
                result.Append("~");
            if (FieldNames != null && FieldNames.Count > 0)
            {
                result.Append("{");
                result.Append(FieldNames[0]);
                for (int i = 1; i < FieldNames.Count; i++)
                {
                    result.Append(",");
                    result.Append(FieldNames[i]);
                }
                result.Append("}");
            }
            return result.ToString();
        }
    }

    // DeleteObjects

    public class DeleteObjectsRequest : DatabaseRequest
    {
        public IList<DeleteQuery> Queries;

        public DeleteObjectsRequest()
        {
        }

        public DeleteObjectsRequest(IList<DeleteQuery> queries)
        {
            Queries = queries;
        }
    }

    public class DeleteObjectsResponse : DatabaseResponse
    {
        public long DeletedObjectCount;

        public DeleteObjectsResponse()
        {
        }

        public DeleteObjectsResponse(long deletedObjectCount)
        {
            DeletedObjectCount = deletedObjectCount;
        }
    }

    public class DeleteQuery
    {
        public string Name;
        public IList<Parameter> Parameters;      // new List<Parameter>() { new Parameter() { Name = "A", Value = "Dev" }, new Parameter() { Name = "B", Value = "Eng" } }
        public string ForEachVar;                // "G: Group"
        public IList ForEachIn;                  // new List<DbKey>() { key1.AsId, key2.AsId }
        public string LookupVar;                 // "X: Group"
        public string LookupIn;                  // "Group[+Title]"
        public object AfterObject;               // new Group() { Title = A }
        public object UntilObject;               // new Group() { Title = B }
        public string Where;                     // "X.IsDeleted"
        public IList<SearchQuery> DetailQueries; // new SearchQuery() { LookupVar = "U: User", LookupIn = "User.Friends[]", Limit = int.MaxValue }
        public bool ErrorOnObjectNotFound;

        public DeleteQuery()
        {
        }

        public DeleteQuery(IList objects, bool errorOnObjectNotFound)
        {
            ForEachIn = objects;
            ErrorOnObjectNotFound = errorOnObjectNotFound;
        }

        public DeleteQuery(IList objects, IList<TypeAndFields> typesAndFieldsWithDetailObjectsToDelete, 
            bool errorOnObjectNotFound)
        {
            ForEachIn = objects;
            ErrorOnObjectNotFound = errorOnObjectNotFound;
        }
    }

    // GetObjects

    public class GetObjectsRequest : DatabaseRequest
    {
        public IList<GetQuery> Queries;

        public GetObjectsRequest()
        {
        }

        public GetObjectsRequest(IList<GetQuery> queries)
        {
            Queries = queries;
        }
    }

    public class GetObjectsResponse : DatabaseResponse
    {
        public IList<QueryResult> Results;

        public GetObjectsResponse()
        {
        }

        public GetObjectsResponse(IList<QueryResult> results)
        {
            Results = results;
        }
    }

    public class GetQuery
    {
        public string Name;
        public IList<DbKey> InKeys;
        public IList<TypeAndFields> ReturnFields;
        public IList<FileRange> FileRanges; // Length is a maximum range, the actual range may be less
        public bool ErrorOnObjectNotFound;
        public IList<SearchQuery> DetailQueries;

        public GetQuery()
        {
        }

        public GetQuery(string name, IList<DbKey> inKeys, IList<TypeAndFields> returnFields,
            IList<FileRange> fileRanges, bool errorOnObjectNotFound, IList<SearchQuery> detailQueries)
        {
            Name = name;
            InKeys = inKeys;
            ReturnFields = returnFields;
            FileRanges = fileRanges;
            ErrorOnObjectNotFound = errorOnObjectNotFound;
            DetailQueries = detailQueries;
        }
    }

    // LookupObjects

    public class LookupObjectsRequest : DatabaseRequest
    {
        public IList<LookupQuery> Queries;

        public LookupObjectsRequest()
        {
        }

        public LookupObjectsRequest(IList<LookupQuery> queries)
        {
            Queries = queries;
        }
    }

    public class LookupObjectsResponse : DatabaseResponse
    {
        public IList<QueryResult> Results;

        public LookupObjectsResponse()
        {
        }

        public LookupObjectsResponse(IList<QueryResult> results)
        {
            Results = results;
        }
    }

    public class LookupQuery
    {
        public string Name;
        public IList<Parameter> Parameters;       // new List<Parameter>() { new Parameter() { Name = "R", Value = 50.0 } }
        public string ForEachVar;                 // "U: User"
        public IList ForEachIn;                   // new List<DbObject>() { obj1, obj2 }
        public string LookupVar;                  // "X: User"
        public string LookupIn;                   // "User[+Id]"
        public string Where;                      // "U.Rating >= R && U.Timestamp == X.Timestamp"
        public string Having;                     // "X.Participants.Count > 0"
        public IList<FileRange> FileRanges;       // Length is a maximum range, the actual range may be less
        public IList<TypeAndFields> ReturnFields; // new List<TypeAndFields> { TypeAndFields.Parse("User{Key}") }
        public bool ErrorOnObjectNotFound;
        public IList<SearchQuery> DetailQueries;

        public LookupQuery()
        {
        }

        public LookupQuery(IList forEachIn, string lookupIn, IList<TypeAndFields> returnFields, bool errorOnObjectNotFound)
        {
            ForEachIn = forEachIn;
            LookupIn = lookupIn;
            ReturnFields = returnFields;
            ErrorOnObjectNotFound = errorOnObjectNotFound;
        }

        public LookupQuery(string name, IList<Parameter> parameters, string forEachVar, IList forEachIn,
            string lookupVar, string lookupIn, string where, string having, IList<FileRange> fileRanges,
            IList<TypeAndFields> returnFields, bool errorOnObjectNotFound, IList<SearchQuery> detailQueries)
        {
            Name = name;
            Parameters = parameters;
            ForEachVar = forEachVar;
            ForEachIn = forEachIn;
            LookupVar = lookupVar;
            LookupIn = lookupIn;
            Where = where;
            Having = having;
            FileRanges = fileRanges;
            ReturnFields = returnFields;
            ErrorOnObjectNotFound = errorOnObjectNotFound;
            DetailQueries = detailQueries;
        }
    }

    // SearchObjects

    public class SearchObjectsRequest : DatabaseRequest
    {
        public IList<SearchQuery> Queries;

        public SearchObjectsRequest()
        {
        }

        public SearchObjectsRequest(IList<SearchQuery> queries)
        {
            Queries = queries;
        }
    }

    public class SearchObjectsResponse : DatabaseResponse
    {
        public IList<QueryResult> Results;

        public SearchObjectsResponse()
        {
        }

        public SearchObjectsResponse(IList<QueryResult> results)
        {
            Results = results;
        }
    }

    public class SearchQuery
    {
        public string Name;
        public int Limit;
        public IList<Parameter> Parameters;       // new List<Parameter>() { new Parameter() { Name = "T", Value = "Dev" } }
        public string LookupVar;                  // "X: Group"
        public string LookupIn;                   // "Group[+Title]"
        public object AfterObject;                // new Group() { Title = "Dev" }
        public int SkipCount;                     // Not supported in detail queries!
        public IList<TextFilter> TextLike;        // Or(X1, ..., Xn)
        public string Where;                      // "X.Title.StartsWith(T)"
        public string Having;                     // "X.Participants.Count > 0"
        public FileRange FileRange;               // Length is a maximum range, the actual range may be less
        public IList<TypeAndFields> ReturnFields; // new List<TypeAndFields> { TypeAndFields.Parse("Group{Id, Title, Participants}") }
        public IList<SearchQuery> DetailQueries;

        public SearchQuery()
        {
        }

        public SearchQuery(string name, IList<Parameter> parameters, string lookupVar, string lookupIn, object afterObject,
            int skipCount, IList<TextFilter> textLike, string where, string having, FileRange fileRange, 
            IList<TypeAndFields> returnFields, int limit, IList<SearchQuery> detailQueries)
        {
            Name = name;
            Parameters = parameters;
            LookupVar = lookupVar;
            LookupIn = lookupIn;
            AfterObject = afterObject;
            SkipCount = skipCount;
            Where = where;
            TextLike = textLike;
            Having = having;
            FileRange = fileRange;
            ReturnFields = returnFields;
            Limit = limit;
            DetailQueries = detailQueries;
        }
    }

    public class Parameter
    {
        public string Name;
        public object Value;

        public Parameter()
        {
        }

        public Parameter(string name, object value)
        {
            Name = name;
            Value = value;
        }
    }

    public class TextFilter
    {
        public IList<TextCondition> TextConditions; // And(X1, ..., Xn)

        public TextFilter()
        {
        }

        public TextFilter(IList<TextCondition> textConditions)
        {
            TextConditions = textConditions;
        }
    }

    public class TextCondition
    {
        public string TextPattern; // "Software Engineer; Scientist Physics/Biology ~Mathematics"
        public TypeAndFields InTypeAndFields;

        public TextCondition()
        {
        }

        public TextCondition(string textPattern, TypeAndFields inTypeAndFields)
        {
            TextPattern = textPattern;
            InTypeAndFields = inTypeAndFields;
        }
    }

    public class QueryResult
    {
        public int QueryNumber;
        public IList Objects;

        public QueryResult()
        {
        }

        public QueryResult(int queryNumber, IList objects)
        {
            QueryNumber = queryNumber;
            Objects = objects;
        }
    }

    //public class AggregateQuery : SearchQuery
    //{
    //    public AggregationRule AggregationRule;
    //}

    //public class AggregationRule
    //{
    //    public int MaxAggregateCount;
    //    public string MasterAggregateDesignator; // "B: GroupAggregate"
    //    public string MasterAggregateField; // "B.Users"
    //    public string CurrentAggregateDesignator; // "A: UserAggregate"
    //    public string CurrentObjectDesignator; // "U: User"
    //    public IList<string> GroupStatements;
    //    public IList<string> AggregationStatements;
    //    public IList<string> MergeStatements;
    //    public IList<Parameter> Parameters;
    //}
}
