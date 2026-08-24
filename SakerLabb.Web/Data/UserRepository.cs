using Microsoft.Data.Sqlite;
using SakerLabb.Web.Services;

namespace SakerLabb.Web.Data;

public class UserRepository
{
    private const string SelectColumns = "SELECT Id, Username, PasswordHash, Role, Email, Personnummer, SecurityAnswer, ResetToken FROM Users ";

    private static readonly string[] AllowedRoles = { "User", "Agent", "Admin" };

    private readonly Db _db;
    private readonly ILogger<UserRepository> _logger;

    public UserRepository(Db db, ILogger<UserRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public User? Authenticate(string username, string password)
    {
        using var connection = _db.Open();
        var command = connection.CreateCommand();
        command.CommandText = SelectColumns + "WHERE Username = $username AND PasswordHash = $hash";
        command.Parameters.AddWithValue("$username", username);
        command.Parameters.AddWithValue("$hash", CryptoService.HashPassword(password));

        var user = Read(command).FirstOrDefault();

        if (user is not null)
        {
            _logger.LogInformation("Inloggning lyckades för användare {UserId}", user.Id);
        }

        return user;
    }

    public User? GetByUsername(string username)
    {
        using var connection = _db.Open();
        var command = connection.CreateCommand();
        command.CommandText = SelectColumns + "WHERE Username = $username";
        command.Parameters.AddWithValue("$username", username);
        return Read(command).FirstOrDefault();
    }

    public User? GetById(string id)
    {
        if (!long.TryParse(id, out var userId))
        {
            return null;
        }

        using var connection = _db.Open();
        var command = connection.CreateCommand();
        command.CommandText = SelectColumns + "WHERE Id = $id";
        command.Parameters.AddWithValue("$id", userId);
        return Read(command).FirstOrDefault();
    }

    public List<User> All()
    {
        using var connection = _db.Open();
        var command = connection.CreateCommand();
        command.CommandText = SelectColumns + "ORDER BY Id";
        return Read(command);
    }

    public string StartPasswordReset(string username, string securityAnswer)
    {
        using var connection = _db.Open();
        var lookup = connection.CreateCommand();
        lookup.CommandText = "SELECT SecurityAnswer FROM Users WHERE Username = $username";
        lookup.Parameters.AddWithValue("$username", username);
        var stored = lookup.ExecuteScalar() as string;

        if (stored is null || !stored.Equals(securityAnswer, StringComparison.OrdinalIgnoreCase))
        {
            return "";
        }

        var token = CryptoService.GenerateResetToken();
        var update = connection.CreateCommand();
        update.CommandText = "UPDATE Users SET ResetToken = $token WHERE Username = $username";
        update.Parameters.AddWithValue("$token", token);
        update.Parameters.AddWithValue("$username", username);
        update.ExecuteNonQuery();

        return token;
    }

    public bool CompletePasswordReset(string token, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        using var connection = _db.Open();
        var command = connection.CreateCommand();
        command.CommandText = "UPDATE Users SET PasswordHash = $hash, ResetToken = NULL WHERE ResetToken = $token";
        command.Parameters.AddWithValue("$hash", CryptoService.HashPassword(newPassword));
        command.Parameters.AddWithValue("$token", token);
        return command.ExecuteNonQuery() > 0;
    }

    public bool SetRole(string userId, string role)
    {
        if (!long.TryParse(userId, out var id) || !AllowedRoles.Contains(role, StringComparer.Ordinal))
        {
            return false;
        }

        using var connection = _db.Open();
        var command = connection.CreateCommand();
        command.CommandText = "UPDATE Users SET Role = $role WHERE Id = $id";
        command.Parameters.AddWithValue("$role", role);
        command.Parameters.AddWithValue("$id", id);
        return command.ExecuteNonQuery() > 0;
    }

    public bool Delete(string userId)
    {
        if (!long.TryParse(userId, out var id))
        {
            return false;
        }

        using var connection = _db.Open();
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Users WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id);
        return command.ExecuteNonQuery() > 0;
    }

    private static List<User> Read(SqliteCommand command)
    {
        var users = new List<User>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            users.Add(new User
            {
                Id = reader.GetInt32(0),
                Username = reader.GetString(1),
                PasswordHash = reader.GetString(2),
                Role = reader.GetString(3),
                Email = reader.GetString(4),
                Personnummer = reader.GetString(5),
                SecurityAnswer = reader.GetString(6),
                ResetToken = reader.IsDBNull(7) ? null : reader.GetString(7)
            });
        }

        return users;
    }
}
