namespace Nezaboodka.Ndef
{
    public class NdefSerializer
    {
        public static void WriteDataSets(INdefReader reader, INdefWriter writer)
        {
            while (reader.MoveToNextDataSet())
            {
                NdefDataSet dataSet = reader.CurrentDataSet;
                if (dataSet.IsStartOfDataSet)
                    writer.WriteDataSetStart(dataSet.IsEndOfDataSet, dataSet.Header);
                WriteObjects(reader, writer);
                if (dataSet.IsEndOfDataSet)
                    writer.WriteDataSetEnd();
            }
            writer.Flush();
        }

        public static void WriteObjects(INdefReader reader, INdefWriter writer)
        {
            while (reader.MoveToNextObject())
            {
                NdefObject o = reader.CurrentObject;
                bool noBraces = o.Header.Kind == NdefObjectKind.List &&
                    o.Header.TypeName == NdefConst.ListTypeBraces;
                if (o.IsStartOfObject)
                    writer.WriteObjectStart(noBraces, o.Header.TypeName, o.Header.Number, o.Header.Key);
                while (reader.MoveToNextElement())
                {
                    NdefElement x = o.CurrentElement;
                    if (x.Field.Name != null)
                    {
                        if (x.Value.Kind == NdefValueKind.Object)
                        {
                            if (!x.Value.AsNestedObjectToDeserialize.IsEndOfObject)
                                writer.WriteFieldName(x.Field.Name);
                        }
                        else
                        {
                            writer.WriteFieldName(x.Field.Name);
                            if (x.Value.AsObjectKey != null || x.Value.AsObjectNumber != null)
                                writer.WriteReference(x.Value.AsObjectKey, x.Value.AsObjectNumber);
                            else
                                writer.WriteValue(x.Value.ActualSerializableTypeName, x.Value.AsScalar,
                                    x.Value.HasNoLineFeeds);
                        }
                    }
                    else
                    {
                        if (x.Value.Kind == NdefValueKind.Object)
                        {
                            if (!x.Value.AsNestedObjectToDeserialize.IsEndOfObject)
                                writer.WriteListItem(x.Field.Kind == NdefFieldKind.Remove);
                        }
                        else
                        {
                            writer.WriteListItem(x.Field.Kind == NdefFieldKind.Remove);
                            if (x.Value.AsObjectKey != null || x.Value.AsObjectNumber != null)
                                writer.WriteReference(x.Value.AsObjectKey, x.Value.AsObjectNumber);
                            else
                                writer.WriteValue(x.Value.ActualSerializableTypeName, x.Value.AsScalar,
                                    x.Value.HasNoLineFeeds);
                        }
                    }
                }
                if (o.IsEndOfObject)
                    writer.WriteObjectEnd(noBraces);
            }
        }
    }
}
