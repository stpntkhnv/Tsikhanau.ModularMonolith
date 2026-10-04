using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsikhanau.Railway;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Persistence;

public static class OptionalPropertyExtensions
{
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
                .HasColumnName(BuildColumnName(propertyName));
        }
    }

    private static String GetPropertyName(LambdaExpression property) =>
        property.Body is MemberExpression memberExpression
            ? memberExpression.Member.Name
            : throw new ArgumentException($"{property} must point at a property", nameof(property));

    private static String BuildFieldName(String propertyName) =>
        $"_{JsonNamingPolicy.CamelCase.ConvertName(propertyName)}";

    private static String BuildColumnName(String propertyName) =>
        JsonNamingPolicy.SnakeCaseLower.ConvertName(propertyName);
}
