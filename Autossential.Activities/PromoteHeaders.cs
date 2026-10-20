using Autossential.Activities.Properties;
using System.Activities;
using System.Data;
using System.Xml.Linq;

namespace Autossential.Activities
{
    public sealed class PromoteHeaders : CodeActivity
    {
        [RequiredArgument]
        public InOutArgument<DataTable> DataTable { get; set; }

        public InArgument<bool> AutoRename { get; set; } = true;

        protected override void Execute(CodeActivityContext context)
        {
            var autoRename = AutoRename.Get(context);

            var dt = DataTable.Get(context) ?? throw new InvalidOperationException(ResourcesFn.Common_ErrorMsg_ValueNotSuppliedFormat(Resources.PromoteHeaders_DataTable_DisplayName));
            if (dt.Rows.Count == 0)
                throw new InvalidOperationException(Resources.PromoteHeaders_ErrorMsg_NoData);

            var colNames = dt.Rows[0].ItemArray.Select(v => v.ToString()).ToArray();

            var cache = new Dictionary<string, int>();
            if (autoRename)
            {
                for (int i = 0; i < colNames.Length; i++)
                {
                    var name = colNames[i];
                    var colName = string.IsNullOrWhiteSpace(name) ? "Col" : name;
                    if (cache.TryGetValue(colName, out int value))
                    {
                        cache[colName] = ++value;
                        colName = $"{colName}{value}";
                    }
                    else
                    {
                        cache.Add(colName, 0);
                    }

                    dt.Columns[i].ColumnName = colName;
                }
            }
            else
            {
                for (int i = 0; i < colNames.Length; i++)
                {
                    var name = colNames[i];
                    var colName = string.IsNullOrWhiteSpace(name) ? "Col" : name;
                    dt.Columns[i].ColumnName = colName;
                }
            }
            

            dt.Rows.RemoveAt(0);
            DataTable.Set(context, dt);
        }
    }
}