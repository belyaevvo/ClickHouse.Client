using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using ClickHouse.Client.ADO;
using ClickHouse.Client.ADO.Adapters;
using ClickHouse.Client.ADO.Readers;
using ClickHouse.Client.Types;

namespace ClickHouse.Client.Utility;

internal static class SchemaDescriber
{
    private static readonly string[] Columns =
    {
        "ColumnName",
        "ColumnOrdinal",
        "ColumnSize",
        "NumericPrecision",
        "NumericScale",
        "DataType",
        "ProviderType",
        "IsLong",
        "AllowDBNull",
        "IsReadOnly",
        "IsRowVersion",
        "IsUnique",
        "IsKey",
        "IsAutoIncrement",
        "BaseCatalogName",
        "BaseSchemaName",
        "BaseTableName",
        "BaseColumnName",
        "AutoIncrementSeed",
        "AutoIncrementStep",
        "DefaultValue",
        "Expression",
        "ColumnMapping",
        "BaseTableNamespace",
        "BaseColumnNamespace",
    };

    public static DataTable DescribeSchemaTable(this ClickHouseDataReader reader)
    {
        var result = new DataTable
        {
            Columns =
            {
                "ColumnName",
                { "ColumnOrdinal", typeof(int) },
                { "ColumnSize", typeof(int) },
                { "NumericPrecision", typeof(int) },
                { "NumericScale", typeof(int) },
                { "DataType", typeof(Type) },
                "ProviderType",
                { "IsLong", typeof(bool) },
                { "AllowDBNull", typeof(bool) },
                { "IsReadOnly", typeof(bool) },
                { "IsRowVersion", typeof(bool) },
                { "IsUnique", typeof(bool) },
                { "IsKey", typeof(bool) },
                { "IsAutoIncrement", typeof(bool) },
                "BaseCatalogName",
                "BaseSchemaName",
                "BaseTableName",
                "BaseColumnName",
                { "AutoIncrementSeed", typeof(int) },
                { "AutoIncrementStep", typeof(int) },
                { "DefaultValue", typeof(object) },
                "Expression",
                { "ColumnMapping", typeof(MappingType)},
                "BaseTableNamespace",
                "BaseColumnNamespace",
            },
        };
        /*foreach (var columnName in Columns)
            result.Columns.Add(columnName);*/
        for (int i = 0; i < reader.FieldCount; i++)
            result.Rows.Add(reader.DescribeColumn(i));

        return result;
    }

    private static object[] DescribeColumn(this ClickHouseDataReader reader, int ordinal)
    {
        var chType = reader.GetClickHouseType(ordinal);

        var result = new object[Columns.Length];
        result[0] = reader.GetName(ordinal); // ColumnName
        result[1] = ordinal; // ColumnOrdinal
        result[2] = -1; // ColumnSize
        result[3] = null; // NumericPrecision
        result[4] = null; // NumericScale
        result[5] = reader.GetFieldType(ordinal); // chType.FrameworkType == typeof(DBNull) ? typeof(int) : chType.FrameworkType; // DataType
        result[6] = chType.ToString(); // ProviderType
        result[7] = chType is StringType; // IsLong
        result[8] = chType is NullableType; // AllowDBNull
        result[9] = true; // IsReadOnly
        result[10] = false; // IsRowVersion
        result[11] = false; // IsUnique
        result[12] = false; // IsKey
        result[13] = false; // IsAutoIncrement
        result[14] = null; // BaseCatalogName
        result[15] = null; // BaseSchemaName
        result[16] = null; // BaseTableName
        result[17] = reader.GetName(ordinal); // BaseColumnName
        result[18] = null; // AutoIncrementSeed
        result[19] = null; // AutoIncrementStep
        result[20] = null; // DefaultValue
        result[21] = null; // Expression
        result[22] = MappingType.Element; // ColumnMapping
        result[23] = null; // BaseTableNamespace
        result[24] = null; // BaseColumnNamespace

        if (chType is DecimalType dt)
        {
            result[2] = dt.Size;
            result[3] = dt.Precision;
            result[4] = dt.Scale;            
        }

        return result;
    }

    public static DataTable DescribeSchema(this ClickHouseConnection connection, string type, string[] restrictions) => type switch
    {
        "Columns" => DescribeColumns(connection, restrictions),
        "Tables" => DescribeTables(connection, restrictions),
        "DataSourceInformation" => DescribeDataSource(connection, restrictions),
        _ => throw new NotSupportedException()
    };

    private static DataTable DescribeDataSource(ClickHouseConnection connection, string[] restrictions)
    {
        DataTable result = new DataTable
        {
            Columns =
            {
                "CompositeIdentifierSeparatorPattern",
                "DataSourceProductName",
                "DataSourceProductVersion",
                "DataSourceProductVersionNormalized",
                { "GroupByBehavior", typeof(GroupByBehavior) },
                "IdentifierPattern",
                { "IdentifierCase", typeof(IdentifierCase) },
                { "OrderByColumnsInSelect", typeof(bool) },
                "ParameterMarkerFormat",
                "ParameterMarkerPattern",
                { "ParameterNameMaxLength", typeof(int) },
                "ParameterNamePattern",
                "QuotedIdentifierPattern",
                { "QuotedIdentifierCase", typeof(IdentifierCase) },
                "StatementSeparatorPattern",
                "StringLiteralPattern",
                { "SupportedJoinOperators", typeof(SupportedJoinOperators) },
            },
        };

        result.Rows.Add(
            "\\.",
            "ClickHouse ADO.NET Provider",
            "1.0.0",
            "1.0.0",
            GroupByBehavior.Unrelated,
            string.Empty,
            IdentifierCase.Sensitive,
            false,
            "{0}",
            "\"@[\\p{Lo}\\p{Lu}\\p{Ll}\\p{Lm}_@#][\\p{Lo}\\p{Lu}\\p{Ll}\\p{Lm}\\p{Nd}\\uff3f_@#\\$]*(?=\\s+|$)\"",
            30,
            "\\p{Lu}\\p{Ll}\\p{Lt}\\p{Lm}\\p{Lo}\\p{Nl}\\p{Nd}",
            "(([^\"]|\"\")*)",
            IdentifierCase.Sensitive,
            ";",
            "('([^']|'')*')",
            SupportedJoinOperators.Inner | SupportedJoinOperators.FullOuter | SupportedJoinOperators.LeftOuter | SupportedJoinOperators.RightOuter);
        return result;
    }

    private static DataTable DescribeTables(ClickHouseConnection connection, string[] restrictions)
    {
        var command = connection.CreateCommand();
        var query = new StringBuilder("SELECT database as TABLE_CATALOG, null as TABLE_SCHEMA, name as TABLE_NAME, 'BASE TABLE' as TABLE_TYPE FROM system.tables");
        var database = restrictions != null && restrictions.Length > 0 ? restrictions[0] : connection.Database;

        var table = restrictions != null && restrictions.Length > 2 ? restrictions[2] : null;

        if (database != null)
        {
            query.Append(" WHERE database={database:String}");
            command.AddParameter("database", "String", database);
        }

        if (table != null)
        {
            query.Append(" AND name={table:String}");
            command.AddParameter("table", "String", table);
        }

        command.CommandText = query.ToString();
        using var adapter = new ClickHouseDataAdapter();
        adapter.SelectCommand = command;
        var result = new DataTable();
        adapter.Fill(result);

        return result;
    }

    private static DataTable DescribeColumns(ClickHouseConnection connection, string[] restrictions)
    {
        var command = connection.CreateCommand();
        var query = new StringBuilder("SELECT database as Database, table as Table, name as Name, type as ProviderType, type as DataType FROM system.columns");
        var database = restrictions != null && restrictions.Length > 0 ? restrictions[0] : null;
        var table = restrictions != null && restrictions.Length > 1 ? restrictions[1] : null;

        if (database != null)
        {
            query.Append(" WHERE database={database:String}");
            command.AddParameter("database", "String", database);
        }

        if (table != null)
        {
            query.Append(" AND table={table:String}");
            command.AddParameter("table", "String", table);
        }

        command.CommandText = query.ToString();
        using var adapter = new ClickHouseDataAdapter();
        adapter.SelectCommand = command;
        var result = new DataTable();
        adapter.Fill(result);

        foreach (var row in result.Rows.Cast<DataRow>())
        {
            var clickHouseType = TypeConverter.ParseClickHouseType((string)row["ProviderType"], TypeSettings.Default);
            row["ProviderType"] = clickHouseType.ToString();
            // TODO: this should return actual framework type like other implementations do
            row["DataType"] = clickHouseType.FrameworkType.ToString().Replace("System.", string.Empty);
        }

        return result;
    }
}
