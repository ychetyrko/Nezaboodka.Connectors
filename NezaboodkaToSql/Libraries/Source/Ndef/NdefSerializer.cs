namespace Nezaboodka.Ndef
{
    public class NdefSerializer
    {
        public static void WriteDataSets(INdefReader reader, INdefWriter writer)
        {
            while (reader.MoveToNextDataSet())
            {
                NdefDataSet ds = reader.CurrentDataSet;
                writer.WriteDataSetStart(ds.Header, !ds.IsStartOfDataSet, ds.IsImplicit);
                WriteObjects(reader, writer);
                writer.WriteDataSetEnd(ds.IsImplicit);
            }
        }

        private static void WriteObjects(INdefReader reader, INdefWriter writer)
        {
            while (reader.MoveToNextObject())
            {
                NdefObject o = reader.CurrentObject;
                bool noBraces = o.Header.Kind == NdefObjectKind.List &&
                    o.Header.TypeName == NdefConst.ListTypeBraces;
                if (o.IsStartOfObject)
                    writer.WriteObjectStart(o.Header.TypeName, o.Header.Number, o.Header.Key, noBraces);
                while (reader.MoveToNextElement())
                {
                    if (o.CurrentElement.Field.Name != null)
                    {
                        if (o.CurrentElement.Value.Kind == NdefValueKind.Object)
                        {
                            if (!o.CurrentElement.Value.AsNestedObjectToDeserialize.IsEndOfObject)
                                writer.WriteFieldName(o.CurrentElement.Field.Name);
                        }
                        else
                        {
                            writer.WriteFieldName(o.CurrentElement.Field.Name);
                            if (o.CurrentElement.Value.AsObjectKey != null || o.CurrentElement.Value.AsObjectNumber != null)
                                writer.WriteReference(o.CurrentElement.Value.AsObjectKey, o.CurrentElement.Value.AsObjectNumber);
                            else
                                writer.WriteValue(o.CurrentElement.Value.ActualSerializableTypeName,
                                    o.CurrentElement.Value.AsScalar, o.CurrentElement.Value.HasNoLineFeeds);
                        }
                    }
                    else
                    {
                        if (o.CurrentElement.Value.Kind == NdefValueKind.Object)
                        {
                            if (!o.CurrentElement.Value.AsNestedObjectToDeserialize.IsEndOfObject)
                                writer.WriteListItem(o.CurrentElement.Field.Kind == NdefFieldKind.Remove);
                        }
                        else
                        {
                            writer.WriteListItem(o.CurrentElement.Field.Kind == NdefFieldKind.Remove);
                            if (o.CurrentElement.Value.AsObjectKey != null || o.CurrentElement.Value.AsObjectNumber != null)
                                writer.WriteReference(o.CurrentElement.Value.AsObjectKey, o.CurrentElement.Value.AsObjectNumber);
                            else
                                writer.WriteValue(o.CurrentElement.Value.ActualSerializableTypeName,
                                    o.CurrentElement.Value.AsScalar, o.CurrentElement.Value.HasNoLineFeeds);
                        }
                    }
                }
                if (o.IsEndOfObject)
                    writer.WriteObjectEnd(noBraces);
            }
        }
    }
}
