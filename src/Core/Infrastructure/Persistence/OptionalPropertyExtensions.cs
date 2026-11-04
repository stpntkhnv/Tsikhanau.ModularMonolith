using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsikhanau.Monolith.Infrastructure.Persistence.Naming;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Infrastructure.Persistence;

public static class OptionalPropertyExtensions
{
    private const String EmptyColumnNameProblem =
        "gets an empty column name, since the naming convention drops letters without upper and lower case: "
        + "give the property a name with Latin letters and keep its old column with HasColumnName after HasOptional";

    extension<TEntity>(EntityTypeBuilder<TEntity> builder)
        where TEntity : class
    {
        public PropertyBuilder<TValue?> HasOptional<TValue>(Expression<Func<TEntity, Optional<TValue>>> property)
            where TValue : struct
        {
            var propertyName = GetPropertyName(property);
            builder.Ignore(propertyName);
            return builder
                .Property<TValue?>(BuildFieldName(propertyName))
                .HasColumnName(BuildColumnName(typeof(TEntity), propertyName));
        }
    }

    private static String GetPropertyName(LambdaExpression property) =>
        property.Body is MemberExpression memberExpression
            ? memberExpression.Member.Name
            : throw new ArgumentException($"{property} must point at a property", nameof(property));

    private static String BuildFieldName(String propertyName) =>
        $"_{JsonNamingPolicy.CamelCase.ConvertName(propertyName)}";

    private static String BuildColumnName(Type entityType, String propertyName)
    {
        var columnName = ColumnNames.Of(propertyName);
        return columnName.Length > 0
            ? columnName
            : throw new InvalidOperationException(
                $"Optional property {entityType.FullName}.{propertyName} {EmptyColumnNameProblem}");
    }
}
