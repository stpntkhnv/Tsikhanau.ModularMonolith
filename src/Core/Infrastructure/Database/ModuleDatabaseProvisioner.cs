using Dapper;
using Npgsql;

namespace Tsikhanau.Monolith.Infrastructure.Database;

internal sealed class ModuleDatabaseProvisioner
{
    private const String LoginAttributes = "LOGIN NOSUPERUSER NOCREATEROLE NOCREATEDB NOREPLICATION NOBYPASSRLS";
    private const String GroupAttributes = "NOLOGIN NOSUPERUSER NOCREATEROLE NOCREATEDB NOREPLICATION NOBYPASSRLS";
    private const String DropAdvice = "Drop the old development database (the Aspire data volume) and start again.";

    private const String OwnershipAdvice =
        "Module roles are named after the database: if it was renamed, give it back the name it was prepared under; "
        + "if this is a development database (the Aspire data volume), you can also drop it and start again.";

    private const String ConnectionStringSetting = $"ConnectionStrings:{DatabaseConnectionStrings.Name}";

    private const String MaxConnectionsSetting =
        $"{DatabaseSettings.Section}:{nameof(DatabaseSettings.MaxConnections)}";

    private const String CannotCreateRoles =
        $"The login in {ConnectionStringSetting} must be a superuser or have CREATEROLE to create module logins";

    private const String CannotCreateSchemas =
        $"The login in {ConnectionStringSetting} needs CREATE on the database to create module schemas";

    private const String ServerVersionNumberSql = "SELECT current_setting('server_version_num')::int";

    private const String CanCreateRolesSql =
        "SELECT rolsuper OR rolcreaterole FROM pg_roles WHERE rolname = current_user";

    private const String CanCreateSchemasSql =
        "SELECT has_database_privilege(current_user, current_database(), 'CREATE')";

    private const String AvailableConnectionsSql = """
        SELECT current_setting('max_connections')::int
             - current_setting('superuser_reserved_connections')::int
             - current_setting('reserved_connections')::int
        """;

    private const String RoleExistsSql = "SELECT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = @Name)";

    private const String ExcessLoginRightsSql = """
        SELECT rolsuper OR rolcreaterole OR rolcreatedb OR rolreplication OR rolbypassrls OR NOT rolcanlogin
        FROM pg_roles WHERE rolname = @Name
        """;

    private const String ExcessGroupRightsSql = """
        SELECT rolsuper OR rolcreaterole OR rolcreatedb OR rolreplication OR rolbypassrls OR rolcanlogin
        FROM pg_roles WHERE rolname = @Name
        """;

    private const String PlainGroupMembershipSql = """
        SELECT COALESCE(bool_or(m.inherit_option) AND NOT bool_or(m.admin_option OR m.set_option), false)
        FROM pg_auth_members m
        WHERE m.roleid = to_regrole(@Group) AND m.member = to_regrole(@Name)
        """;

    private const String ParentRolesSql = """
        SELECT r.rolname FROM pg_auth_members m JOIN pg_roles r ON r.oid = m.roleid
        WHERE m.member = to_regrole(@Name) AND r.rolname <> @Allowed
        ORDER BY r.rolname
        """;

    private const String SchemaOwnerSql =
        "SELECT pg_get_userbyid(nspowner) FROM pg_namespace WHERE nspname = @Schema";

    private const String ObjectsOwnedByOthersSql = """
        SELECT c.relname FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = @Schema AND c.relkind IN ('r', 'p', 'v', 'm', 'S', 'f')
          AND pg_get_userbyid(c.relowner) <> @Name
        ORDER BY c.relname
        """;

    private static readonly TimeSpan[] PausesBetweenAttempts =
        [TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(300)];

    private readonly NpgsqlConnection _connection;
    private readonly NpgsqlTransaction _transaction;
    private readonly String _group;
    private readonly CancellationToken _cancellationToken;

    private ModuleDatabaseProvisioner(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        String group,
        CancellationToken cancellationToken)
    {
        _connection = connection;
        _transaction = transaction;
        _group = group;
        _cancellationToken = cancellationToken;
    }

    public static Task ProvisionAsync(
        NpgsqlDataSource adminNpgsqlDataSource,
        String group,
        IReadOnlyList<ModuleLogin> logins,
        Int32 maxConnections,
        CancellationToken cancellationToken) =>
        RetryParallelStartCollisionsAsync(
            () => ProvisionOnceAsync(adminNpgsqlDataSource, group, logins, maxConnections, cancellationToken),
            cancellationToken);

    private static async Task RetryParallelStartCollisionsAsync(
        Func<Task> provision,
        CancellationToken cancellationToken)
    {
        foreach (var pause in PausesBetweenAttempts)
        {
            try
            {
                await provision();
                return;
            }
            catch (PostgresException exception) when (IsParallelStartCollision(exception))
            {
                await Task.Delay(pause, cancellationToken);
            }
        }

        await provision();
    }

    private static Boolean IsParallelStartCollision(PostgresException exception) =>
        exception.SqlState is PostgresErrorCodes.UniqueViolation
            or PostgresErrorCodes.InternalError
            or PostgresErrorCodes.DuplicateObject;

    private static async Task ProvisionOnceAsync(
        NpgsqlDataSource adminNpgsqlDataSource,
        String group,
        IReadOnlyList<ModuleLogin> logins,
        Int32 maxConnections,
        CancellationToken cancellationToken)
    {
        await using var connection = await adminNpgsqlDataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var provisioner = new ModuleDatabaseProvisioner(connection, transaction, group, cancellationToken);

        await provisioner.EnsureSupportedServerAsync();
        await provisioner.EnsureAdminCanProvisionAsync();
        await provisioner.EnsureConnectionBudgetAsync(maxConnections, logins.Count);
        await provisioner.EnsureGroupAsync();
        foreach (var login in logins)
        {
            await provisioner.EnsureLoginAsync(login);
            await provisioner.ValidateAsync(login);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task EnsureSupportedServerAsync() =>
        PostgresVersion.EnsureSupported(await ExecuteScalarAsync<Int32>(ServerVersionNumberSql));

    private async Task EnsureAdminCanProvisionAsync()
    {
        if (!await ExecuteScalarAsync<Boolean>(CanCreateRolesSql))
        {
            throw new InvalidOperationException(CannotCreateRoles);
        }

        if (!await ExecuteScalarAsync<Boolean>(CanCreateSchemasSql))
        {
            throw new InvalidOperationException(CannotCreateSchemas);
        }
    }

    private async Task EnsureConnectionBudgetAsync(Int32 maxConnections, Int32 moduleCount)
    {
        var availableConnections = await ExecuteScalarAsync<Int32>(AvailableConnectionsSql);
        var neededConnections = DatabaseSettings.MinConnectionsPerModule * moduleCount;
        if (maxConnections > availableConnections || maxConnections < neededConnections)
        {
            throw new InvalidOperationException(
                $"{MaxConnectionsSetting} is {maxConnections}; it must be at least {neededConnections} "
                + $"({DatabaseSettings.MinConnectionsPerModule} for each of the {moduleCount} modules) "
                + $"and at most the {availableConnections} connections the server leaves to ordinary logins");
        }
    }

    private async Task EnsureGroupAsync()
    {
        await EnsureRoleAsync(_group, GroupAttributes);
        var problems = new List<String>();
        problems.AddRange(await ReadExcessRightsAsync(_group, ExcessGroupRightsSql));
        problems.AddRange(await ReadParentRolesAsync(_group, allowedParentRole: String.Empty));
        ThrowIfAny(problems, DropAdvice);
    }

    private async Task EnsureRoleAsync(String name, String attributes)
    {
        if (!await ExecuteScalarAsync<Boolean>(RoleExistsSql, new { Name = name }))
        {
            await ExecuteAsync($"CREATE ROLE \"{name}\" {attributes}");
        }
    }

    private async Task EnsureLoginAsync(ModuleLogin login)
    {
        await EnsureRoleAsync(login.Name, LoginAttributes);
        try
        {
            await ExecuteAsync(
                $"ALTER ROLE \"{login.Name}\" PASSWORD {QuoteLiteral(ScramPasswordHasher.Hash(login.Password))}");

            await ExecuteAsync(
                $"GRANT \"{_group}\" TO \"{login.Name}\" WITH ADMIN FALSE, INHERIT TRUE, SET FALSE");

            await ExecuteAsync($"GRANT \"{login.Name}\" TO CURRENT_USER WITH SET TRUE, INHERIT FALSE");
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.InsufficientPrivilege)
        {
            throw new InvalidOperationException(
                $"The login in {ConnectionStringSetting} cannot manage {login.Name}: "
                + $"it needs the ADMIN option on {login.Name} and {_group} (it has it on roles it created). "
                + exception.MessageText,
                exception);
        }

        await ExecuteAsync($"CREATE SCHEMA IF NOT EXISTS \"{login.Schema}\" AUTHORIZATION \"{login.Name}\"");
    }

    private async Task ValidateAsync(ModuleLogin login)
    {
        var problems = new List<String>();
        problems.AddRange(await ReadExcessRightsAsync(login.Name, ExcessLoginRightsSql));
        problems.AddRange(await ReadGroupMembershipProblemsAsync(login));
        problems.AddRange(await ReadParentRolesAsync(login.Name, allowedParentRole: _group));
        problems.AddRange(await ReadSchemaOwnerProblemsAsync(login));
        problems.AddRange(await ReadObjectsOwnedByOthersAsync(login));
        ThrowIfAny(problems, OwnershipAdvice);
    }

    private async Task<IEnumerable<String>> ReadExcessRightsAsync(String role, String sql) =>
        await ExecuteScalarAsync<Boolean>(sql, new { Name = role })
            ? [$"role {role} has more rights than it needs"]
            : [];

    private async Task<IEnumerable<String>> ReadGroupMembershipProblemsAsync(ModuleLogin login) =>
        await ExecuteScalarAsync<Boolean>(PlainGroupMembershipSql, new { Group = _group, login.Name })
            ? []
            : [$"login {login.Name} must inherit {_group} without ADMIN or SET"];

    private async Task<IEnumerable<String>> ReadParentRolesAsync(String role, String allowedParentRole)
    {
        var parents = await QueryAsync<String>(ParentRolesSql, new { Name = role, Allowed = allowedParentRole });
        return parents.Select(parent => $"role {role} is a member of {parent}");
    }

    private async Task<IEnumerable<String>> ReadSchemaOwnerProblemsAsync(ModuleLogin login)
    {
        var owner = await ExecuteScalarAsync<String>(SchemaOwnerSql, new { login.Schema });
        return owner == login.Name ? [] : [$"schema {login.Schema} is owned by {owner}, not {login.Name}"];
    }

    private async Task<IEnumerable<String>> ReadObjectsOwnedByOthersAsync(ModuleLogin login)
    {
        var objectsOwnedByOthers = await QueryAsync<String>(ObjectsOwnedByOthersSql, new { login.Schema, login.Name });
        return objectsOwnedByOthers.Select(objectName => $"{login.Schema}.{objectName} is not owned by {login.Name}");
    }

    private static void ThrowIfAny(List<String> problems, String advice)
    {
        if (problems.Count > 0)
        {
            throw new InvalidOperationException($"{String.Join("; ", problems)}. {advice}");
        }
    }

    private Task<T> ExecuteScalarAsync<T>(String sql, Object? parameters = null) =>
        _connection.ExecuteScalarAsync<T>(CreateCommand(sql, parameters))!;

    private Task<IEnumerable<T>> QueryAsync<T>(String sql, Object? parameters = null) =>
        _connection.QueryAsync<T>(CreateCommand(sql, parameters));

    private Task ExecuteAsync(String sql, Object? parameters = null) =>
        _connection.ExecuteAsync(CreateCommand(sql, parameters));

    private CommandDefinition CreateCommand(String sql, Object? parameters) =>
        new(sql, parameters, _transaction, cancellationToken: _cancellationToken);

    private static String QuoteLiteral(String value) => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
}
