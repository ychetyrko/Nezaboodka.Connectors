using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

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

    // ReadWriteObjects

    public class WriteObjectsRequest : DatabaseRequest
    {
        public IList FileObjects;
    }

    public class ReadObjectsResponse : DatabaseResponse
    {
        public IList FileObjects;
    }

    public struct FileRange
    {
        private static readonly char[] DelimiterBetweenPositionAndLength = new char[] { '+' };

        public long Position; // Запись: 0 - Save, не 0 - Append. Чтение: отрицательные значения превращаются в 0.
        public long Length; // 0 - IsNull

        public bool IsNull()
        {
            return Length == 0;
        }

        public override string ToString()
        {
            string result = null;
            if (!IsNull())
            {
                if (Position >= 0)
                    result = string.Format("{0:X}{1}{2:X}", Position, DelimiterBetweenPositionAndLength[0], Length);
                else
                    result = string.Format("{0}{1:X}", DelimiterBetweenPositionAndLength[0], Length);
            }
            return result;
        }

        public static FileRange Parse(string value)
        {
            var result = new FileRange();
            if (!string.IsNullOrEmpty(value))
            {
                string[] t = value.Split(DelimiterBetweenPositionAndLength, 2);
                if (t.Length == 2)
                {
                    string p = t[0].Trim();
                    if (!string.IsNullOrEmpty(p))
                        result.Position = long.Parse(p, NumberStyles.AllowHexSpecifier);
                    result.Length = long.Parse(t[1].Trim(), NumberStyles.AllowHexSpecifier);
                }
                else
                    throw new NezaboodkaException(string.Format("invalid file range format: '{0}'", value));
            }
            return result;
        }
    }

    // SaveObjects

    public class SaveObjectsRequest : WriteObjectsRequest
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
        public string ForVar;                     // "G: Group"
        public IList InObjects;                   // new List<DbObject>() { group1, group2 }
        public string GetVar;                     // "X: Group"
        public string FromIndex;                  // "Group[+Title]"
        public string Where;                      // "X.Title.StartsWith(T)"
        public string Having;                     // "X.Participants.Count > 0"
        public IList<TypeAndFields> SaveFields;   // new List<TypeAndFields> { TypeAndFields.Parse("Group: Title, Participants") }
        public IList<TypeAndFields> ReturnFields; // new List<TypeAndFields> { TypeAndFields.Parse("Group: Id, Title, Participants") }
        public bool ErrorOnRevisionMismatch;

        public SaveQuery()
        {
        }

        public SaveQuery(IList inObjects, IList<TypeAndFields> save, IList<TypeAndFields> returnFields, bool errorOnRevisionMismatch)
        {
            SaveFields = save;
            ReturnFields = returnFields;
            InObjects = inObjects;
            ErrorOnRevisionMismatch = errorOnRevisionMismatch;
        }

        public SaveQuery(string name, IList<Parameter> parameters, string forVar, IList inObjects, string getVar, 
            string fromIndex, string where, string having, IList<TypeAndFields> saveFields, IList<TypeAndFields> returnFields, 
            bool errorOnRevisionMismatch)
        {
            Name = name;
            Parameters = parameters;
            ForVar = forVar;
            InObjects = inObjects;
            GetVar = getVar;
            FromIndex = fromIndex;
            Where = where;
            Having = having;
            SaveFields = saveFields;
            ReturnFields = returnFields;
            ErrorOnRevisionMismatch = errorOnRevisionMismatch;
        }
    }

    public struct TypeAndFields
    {
        private static readonly char[] DelimiterBetweenTypeAndFields = new char[] { ':' };
        private static readonly char[] DelimitersBetweenFieldNames = new char[] { ' ', ',' };

        public const string InversionTag = "~";

        public string TypeName;
        public IList<string> FieldNames;

        public TypeAndFields(string typeName, IList<string> fieldNames)
        {
            TypeName = typeName;
            FieldNames = fieldNames;
        }

        public TypeAndFields(string str)
        {
            TypeName = null;
            FieldNames = null;
            if (!string.IsNullOrEmpty(str))
            {
                string[] t = str.Split(DelimiterBetweenTypeAndFields);
                if (t.Length == 1 || t.Length == 2)
                {
                    if (!string.IsNullOrWhiteSpace(t[0]))
                        TypeName = t[0];
                    if (t.Length == 2 && !string.IsNullOrWhiteSpace(t[1]))
                        FieldNames = t[1].Split(DelimitersBetweenFieldNames, StringSplitOptions.RemoveEmptyEntries);
                }
                else
                    throw new NezaboodkaException(string.Format("wrong format of type-and-fields string: {0}", str));
            }
        }

        public static TypeAndFields Parse(string str)
        {
            return new TypeAndFields(str);
        }

        public override string ToString()
        {
            string result;
            if (FieldNames != null)
                result = string.Format("{0}{1} {2}", TypeName, DelimiterBetweenTypeAndFields[0], string.Join(", ", FieldNames));
            else
                result = TypeName;
            return result;
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
        public IList<DeleteResult> Results;

        public DeleteObjectsResponse()
        {
        }

        public DeleteObjectsResponse(IList<DeleteResult> results)
        {
            Results = results;
        }
    }

    public class DeleteQuery
    {
        public string Name;
        public IList<Parameter> Parameters;      // new List<Parameter>() { new Parameter() { Name = "A", Value = "Dev" }, new Parameter() { Name = "B", Value = "Eng" } }
        public string ForVar;                    // "G: Group"
        public IList InObjects;                  // new List<DbKey>() { key1.AsId, key2.AsId }
        public string GetVar;                    // "X: Group"
        public string FromIndex;                 // "Group[+Title]"
        public object AfterObject;               // new Group() { Title = A }
        public object UntilObject;               // new Group() { Title = B }
        public string Where;                     // "X.IsDeleted"
        public IList<SearchQuery> DetailQueries; //TODO
        public string DeleteVar;                 //TODO: "X"
        public bool ErrorOnObjectNotFound;

        public IList<TypeAndFields> TypesAndFieldsWithDetailObjectsToDelete; // Deprecated

        public DeleteQuery()
        {
        }

        public DeleteQuery(IList inObjects, bool errorOnObjectNotFound)
        {
            InObjects = inObjects;
            ErrorOnObjectNotFound = errorOnObjectNotFound;
        }

        public DeleteQuery(IList inObjects, IList<TypeAndFields> typesAndFieldsWithDetailObjectsToDelete, 
            bool errorOnObjectNotFound)
        {
            InObjects = inObjects;
            ErrorOnObjectNotFound = errorOnObjectNotFound;
            TypesAndFieldsWithDetailObjectsToDelete = typesAndFieldsWithDetailObjectsToDelete;
        }
    }

    public class DeleteResult
    {
        public int QueryNumber;
        public int AffectedObjectCount;
        public int AffectedDetailObjectCount;

        public DeleteResult()
        {
        }

        public DeleteResult(int queryNumber)
        {
            QueryNumber = queryNumber;
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

    public class GetObjectsResponse : ReadObjectsResponse
    {
        public IList<QueryResult> Results;

        public GetObjectsResponse()
        {
        }

        public GetObjectsResponse(IList<QueryResult> results, IList fileObjects)
        {
            FileObjects = fileObjects;
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

    public class LookupObjectsResponse : ReadObjectsResponse
    {
        public IList<QueryResult> Results;

        public LookupObjectsResponse()
        {
        }

        public LookupObjectsResponse(IList<QueryResult> results, IList fileObjects)
        {
            FileObjects = fileObjects;
            Results = results;
        }
    }

    public class LookupQuery
    {
        public string Name;
        public IList<Parameter> Parameters;       // new List<Parameter>() { new Parameter() { Name = "R", Value = 50.0 } }
        public string ForVar;                     // "U: User"
        public IList InObjects;                   // new List<DbObject>() { obj1, obj2 }
        public string GetVar;                     // "X: User"
        public string FromIndex;                  // "User[+Id]"
        public string Where;                      // "U.Rating >= R && U.Timestamp == X.Timestamp"
        public string Having;                     // "X.Participants.Count > 0"
        public IList<FileRange> FileRanges;       // Length is a maximum range, the actual range may be less
        public IList<TypeAndFields> ReturnFields; // new List<TypeAndFields> { TypeAndFields.Parse("User: Key") }
        public bool ErrorOnObjectNotFound;
        public IList<SearchQuery> DetailQueries;

        public LookupQuery()
        {
        }

        public LookupQuery(IList inObjects, string fromIndex, IList<TypeAndFields> returnFields, bool errorOnObjectNotFound)
        {
            InObjects = inObjects;
            FromIndex = fromIndex;
            ReturnFields = returnFields;
            ErrorOnObjectNotFound = errorOnObjectNotFound;
        }

        public LookupQuery(string name, IList<Parameter> parameters, string forVar, IList inObjects,
            string getVar, string fromIndex, string where, string having, IList<FileRange> fileRanges,
            IList<TypeAndFields> returnFields, bool errorOnObjectNotFound, IList<SearchQuery> detailQueries)
        {
            Name = name;
            Parameters = parameters;
            ForVar = forVar;
            InObjects = inObjects;
            GetVar = getVar;
            FromIndex = fromIndex;
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

    public class SearchObjectsResponse : ReadObjectsResponse
    {
        public IList<QueryResult> Results;

        public SearchObjectsResponse()
        {
        }

        public SearchObjectsResponse(IList<QueryResult> results, IList fileObjects)
        {
            FileObjects = fileObjects;
            Results = results;
        }
    }

    public class SearchQuery
    {
        public string Name;
        public int Limit;
        public IList<Parameter> Parameters;       // new List<Parameter>() { new Parameter() { Name = "T", Value = "Dev" } }
        public string GetVar;                     // "X: Group"
        public string FromIndex;                  // "Group[+Title]"
        public object AfterObject;                // new Group() { Title = "Dev" }
        public int SkipCount;                     // Not supported in detail queries!
        public IList<TextFilter> TextLike;        // Or(X1, ..., Xn)
        public string Where;                      // "X.Title.StartsWith(T)"
        public string Having;                     // "X.Participants.Count > 0"
        public FileRange FileRange;               // Length is a maximum range, the actual range may be less
        public IList<TypeAndFields> ReturnFields; // new List<TypeAndFields> { TypeAndFields.Parse("Group: Id, Title, Participants") }
        public IList<SearchQuery> DetailQueries;

        public SearchQuery()
        {
        }

        public SearchQuery(string name, IList<Parameter> parameters, string getVar, string fromIndex, object afterObject,
            int skipCount, IList<TextFilter> textLike, string where, string having, FileRange fileRange, 
            IList<TypeAndFields> returnFields, int limit, IList<SearchQuery> detailQueries)
        {
            Name = name;
            Parameters = parameters;
            GetVar = getVar;
            FromIndex = fromIndex;
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
