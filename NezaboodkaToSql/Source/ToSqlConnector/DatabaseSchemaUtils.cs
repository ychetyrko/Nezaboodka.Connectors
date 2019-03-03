using System.Collections.Generic;

namespace Nezaboodka.ToSqlConnector
{
    static class DatabaseSchemaUtils
    {
        // Public

        public static DatabaseSchemaDiff GetDiff(DatabaseSchema oldDatabaseSchema, DatabaseSchema newDatabaseSchema)
        {
            var result = new DatabaseSchemaDiff();

            ClientTypeSystem oldTypeSystem = new ClientTypeSystem(oldDatabaseSchema.TypeDefinitions);
            ClientTypeSystem newTypeSystem = new ClientTypeSystem(newDatabaseSchema.TypeDefinitions);

            List<string> typesToAdd, fieldsToAdd;
            List<string> typesToRemove, fieldsToRemove;
            List<string> backRefsToUpdate;

            GetUpdatedTypesFields(oldTypeSystem, newTypeSystem, out typesToAdd, out fieldsToAdd, out typesToRemove,
                out fieldsToRemove, out backRefsToUpdate);
            result.TypesToAdd.AddRange(typesToAdd);
            result.FieldsToAdd.AddRange(fieldsToAdd);
            result.TypesToRemove.AddRange(typesToRemove);
            result.FieldsToRemove.AddRange(fieldsToRemove);
            result.BackRefsToUpdate.AddRange(backRefsToUpdate);

            GetNewTypesFields(oldTypeSystem, newTypeSystem, out typesToAdd, out fieldsToAdd, out backRefsToUpdate);
            result.TypesToAdd.AddRange(typesToAdd);
            result.FieldsToAdd.AddRange(fieldsToAdd);
            result.BackRefsToUpdate.AddRange(backRefsToUpdate);

            return result;
        }

        // Internal

        private static void GetUpdatedTypesFields(ClientTypeSystem oldTypeSystem, ClientTypeSystem newTypeSystem,
            out List<string> typesToAdd, out List<string> fieldsToAdd,
            out List<string> typesToRemove, out List<string> fieldsToRemove,
            out List<string> backRefsToUpdate)
        {
            typesToAdd = new List<string>();
            fieldsToAdd = new List<string>();
            typesToRemove = new List<string>();
            fieldsToRemove = new List<string>();
            backRefsToUpdate = new List<string>();

            for (int oldTypeNumber = 0; oldTypeNumber < oldTypeSystem.GetTypeCount(); ++oldTypeNumber)
            {
                string typeName = oldTypeSystem.GetTypeName(oldTypeNumber);
                int typeNumber = newTypeSystem.GetTypeNumberByName(typeName);
                if (typeNumber >= 0)
                {
                    if (oldTypeSystem.TypeDefinitions[oldTypeNumber].BaseTypeName
                        == newTypeSystem.TypeDefinitions[typeNumber].BaseTypeName)
                    {
                        int fieldCount = oldTypeSystem.GetFieldCount(oldTypeNumber);
                        for (int oldFieldNumber = 0; oldFieldNumber < fieldCount; ++oldFieldNumber)
                        {
                            int fieldNumber = newTypeSystem.GetFieldNumberByName(typeNumber,
                                oldTypeSystem.GetFieldName(oldTypeNumber, oldFieldNumber));
                            bool isInherited = IsInheritedField(oldTypeSystem, oldTypeNumber, oldFieldNumber);
                            if (fieldNumber >= 0 && !isInherited)
                            {
                                if (oldTypeSystem.GetFieldTypeName(oldTypeNumber, oldFieldNumber)
                                    == newTypeSystem.GetFieldTypeName(typeNumber, fieldNumber))
                                {
                                    oldTypeSystem.GetFieldBackReferenceInfo(oldTypeNumber, oldFieldNumber,
                                        out int oldBackReferenceTypeNumber, out int oldBackReferenceFieldNumber);
                                    newTypeSystem.GetFieldBackReferenceInfo(typeNumber, fieldNumber,
                                        out int newBackReferenceTypeNumber, out int newBackReferenceFieldNumber);
                                    if (newBackReferenceTypeNumber == oldBackReferenceTypeNumber
                                        && newBackReferenceFieldNumber != oldBackReferenceFieldNumber)
                                    {
                                        string fieldName = newTypeSystem.GetFieldName(typeNumber, fieldNumber);
                                        if (newBackReferenceFieldNumber != -1)
                                        {
                                            string backRefName = newTypeSystem.GetFieldName(typeNumber,
                                                newBackReferenceFieldNumber);
                                            backRefsToUpdate.Add(QueryFormatter.GetAddBackRefString(typeName,
                                                fieldName, backRefName));
                                        }
                                        else
                                        {
                                            backRefsToUpdate.Add(QueryFormatter.GetRemoveBackRefString(typeName,
                                                fieldName));
                                        }
                                    }
                                }
                                else
                                {
                                    FieldDefinition fieldDefinition = newTypeSystem.GetFieldDefinition(typeNumber,
                                        fieldNumber);
                                    string fieldAddString = QueryFormatter.GetAddFieldString(typeName,
                                        fieldDefinition);
                                    fieldsToAdd.Add(fieldAddString);
                                    fieldNumber = -1;
                                    if (!string.IsNullOrEmpty(fieldDefinition.BackReferenceFieldName))
                                    {
                                        string backRefUpdString = QueryFormatter.GetAddBackRefString(typeName,
                                            fieldDefinition.FieldName, fieldDefinition.BackReferenceFieldName);
                                        backRefsToUpdate.Add(backRefUpdString);
                                    }
                                }
                            }
                            if (fieldNumber == -1 && !isInherited)
                            {
                                FieldDefinition fieldDefinition = oldTypeSystem.GetFieldDefinition(oldTypeNumber,
                                    oldFieldNumber);
                                string fieldRemoveString = QueryFormatter.GetRemoveFieldString(typeName,
                                    fieldDefinition.FieldName);
                                fieldsToRemove.Add(fieldRemoveString); // back reference is removed automatically
                            }
                        }

                        fieldCount = newTypeSystem.GetFieldCount(oldTypeNumber);
                        for (int newFieldNumber = 0; newFieldNumber < fieldCount; ++newFieldNumber)
                        {
                            int fieldNumber = oldTypeSystem.GetFieldNumberByName(oldTypeNumber,
                                newTypeSystem.GetFieldName(typeNumber, newFieldNumber));
                            if (fieldNumber == -1)
                            {
                                FieldDefinition fieldDefinition = newTypeSystem.GetFieldDefinition(typeNumber,
                                    newFieldNumber);
                                string fieldAddString = QueryFormatter.GetAddFieldString(typeName, fieldDefinition);
                                fieldsToAdd.Add(fieldAddString);
                                if (!string.IsNullOrEmpty(fieldDefinition.BackReferenceFieldName))
                                {
                                    string backRefUpdString = QueryFormatter.GetAddBackRefString(typeName,
                                        fieldDefinition.FieldName, fieldDefinition.BackReferenceFieldName);
                                    backRefsToUpdate.Add(backRefUpdString);
                                }
                            }
                        }
                    }
                    else
                    {
                        TypeDefinition typeDefinition = newTypeSystem.TypeDefinitions[typeNumber];
                        string typeAddString = QueryFormatter.GetAddTypeString(typeDefinition);
                        typesToAdd.Add(typeAddString);
                        AddAllTypeFields(typeDefinition, fieldsToAdd, backRefsToUpdate);
                        typeNumber = -1;
                    }
                }
                if (typeNumber == -1)
                {
                    TypeDefinition typeDefinition = oldTypeSystem.TypeDefinitions[oldTypeNumber];
                    string typeRemoveString = QueryFormatter.GetRemoveTypeString(typeDefinition);
                    typesToRemove.Add(typeRemoveString);
                }
            }
        }

        private static bool IsInheritedField(ClientTypeSystem typeSystem, int typeNumber, int fieldNumber)
        {
            bool result = false;
            int baseTypeNumber = typeSystem.GetBaseTypeNumber(typeNumber);
            if (baseTypeNumber >= 0)
            {
                int baseFieldNumber = typeSystem.GetFieldNumberByName(baseTypeNumber,
                    typeSystem.GetFieldName(typeNumber, fieldNumber));
                if (baseFieldNumber >= 0)
                {
                    result = true;
                }
            }
            return result;
        }

        private static void GetNewTypesFields(ClientTypeSystem oldTypeSystem, ClientTypeSystem newTypeSystem,
            out List<string> newTypes, out List<string> newFields, out List<string> newBackRefs)
        {
            newTypes = new List<string>();
            newFields = new List<string>();
            newBackRefs = new List<string>();
            for (int i = 0; i < newTypeSystem.GetTypeCount(); ++i)
            {
                string typeName = newTypeSystem.GetTypeName(i);
                int typeNumber = oldTypeSystem.GetTypeNumberByName(typeName);
                if (typeNumber == -1)
                {
                    TypeDefinition typeDefinition = newTypeSystem.TypeDefinitions[i];
                    string typeAddString = QueryFormatter.GetAddTypeString(typeDefinition);
                    newTypes.Add(typeAddString);
                    AddAllTypeFields(typeDefinition, newFields, newBackRefs);
                }
            }
        }

        private static void AddAllTypeFields(TypeDefinition typeDefinition, List<string> fieldsList,
            List<string> backRefsList)
        {
            foreach (var fieldDefinition in typeDefinition.FieldDefinitions)
            {
                string fieldAddString = QueryFormatter.GetAddFieldString(typeDefinition.TypeName, fieldDefinition);
                fieldsList.Add(fieldAddString);
                if (!string.IsNullOrEmpty(fieldDefinition.BackReferenceFieldName))
                {
                    string backRefUpdString = QueryFormatter.GetAddBackRefString(typeDefinition.TypeName,
                    fieldDefinition.FieldName, fieldDefinition.BackReferenceFieldName);
                    backRefsList.Add(backRefUpdString);
                }
            }
        }
    }

    class DatabaseSchemaDiff
    {
        public List<string> TypesToRemove = new List<string>();
        public List<string> TypesToAdd = new List<string>();
        public List<string> FieldsToRemove = new List<string>();
        public List<string> FieldsToAdd = new List<string>();
        public List<string> BackRefsToUpdate = new List<string>();
    }
}
