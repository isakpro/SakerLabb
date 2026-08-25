using Microsoft.Data.Sqlite;

namespace SakerLabb.Web.Data;

public class TicketRepository
{
    private static readonly Dictionary<string, string> AllowedSorts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["t.Id DESC"] = "t.Id DESC",
        ["t.Priority ASC"] = "t.Priority ASC",
        ["t.Status ASC"] = "t.Status ASC"
    };

    private const string DefaultSort = "t.Id DESC";

    private readonly Db _db;
    private readonly ILogger<TicketRepository> _logger;

    public TicketRepository(Db db, ILogger<TicketRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public List<Ticket> Search(string? search, string? sort)
    {
        using var connection = _db.Open();
        var command = connection.CreateCommand();

        var order = ResolveSort(sort);
        var hasSearch = !string.IsNullOrWhiteSpace(search);
        var where = hasSearch ? "(t.Title LIKE $search OR t.Body LIKE $search)" : "1=1";

        command.CommandText = "SELECT t.Id, t.Title, t.Body, t.Status, t.Priority, t.OwnerId, t.Internal, t.Created, u.Username "
            + "FROM Tickets t JOIN Users u ON u.Id = t.OwnerId WHERE " + where + " ORDER BY " + order;

        if (hasSearch)
        {
            command.Parameters.AddWithValue("$search", "%" + search + "%");
        }

        _logger.LogInformation("Ärendesökning utförd med fritext {Search} och sortering {Sort}", search, order);

        return Read(command);
    }

    public Ticket? GetById(string id)
    {
        if (!TryParseId(id, out var ticketId))
        {
            return null;
        }

        using var connection = _db.Open();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT t.Id, t.Title, t.Body, t.Status, t.Priority, t.OwnerId, t.Internal, t.Created, u.Username "
            + "FROM Tickets t JOIN Users u ON u.Id = t.OwnerId WHERE t.Id = $id";
        command.Parameters.AddWithValue("$id", ticketId);

        return Read(command).FirstOrDefault();
    }

    public List<Comment> GetComments(string ticketId)
    {
        if (!TryParseId(ticketId, out var id))
        {
            return new List<Comment>();
        }

        using var connection = _db.Open();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, TicketId, Author, Text, Created FROM Comments WHERE TicketId = $ticketId";
        command.Parameters.AddWithValue("$ticketId", id);

        var comments = new List<Comment>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            comments.Add(new Comment
            {
                Id = reader.GetInt32(0),
                TicketId = reader.GetInt32(1),
                Author = reader.GetString(2),
                Text = reader.GetString(3),
                Created = reader.GetString(4)
            });
        }

        return comments;
    }

    public bool AddComment(string ticketId, string author, string text)
    {
        if (!TryParseId(ticketId, out var id))
        {
            return false;
        }

        using var connection = _db.Open();
        var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Comments (TicketId, Author, Text, Created) VALUES ($ticketId, $author, $text, $created)";
        command.Parameters.AddWithValue("$ticketId", id);
        command.Parameters.AddWithValue("$author", author);
        command.Parameters.AddWithValue("$text", text);
        command.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm"));
        command.ExecuteNonQuery();

        return true;
    }

    public bool UpdateStatus(string ticketId, string status)
    {
        if (!TryParseId(ticketId, out var id))
        {
            return false;
        }

        using var connection = _db.Open();
        var command = connection.CreateCommand();
        command.CommandText = "UPDATE Tickets SET Status = $status WHERE Id = $id";
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$id", id);

        return command.ExecuteNonQuery() > 0;
    }

    private static string ResolveSort(string? sort)
    {
        if (string.IsNullOrWhiteSpace(sort))
        {
            return DefaultSort;
        }

        return AllowedSorts.TryGetValue(sort.Trim(), out var resolved) ? resolved : DefaultSort;
    }

    private static bool TryParseId(string? value, out long id)
    {
        return long.TryParse(value, out id);
    }

    private static List<Ticket> Read(SqliteCommand command)
    {
        var tickets = new List<Ticket>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            tickets.Add(new Ticket
            {
                Id = reader.GetInt32(0),
                Title = reader.GetString(1),
                Body = reader.GetString(2),
                Status = reader.GetString(3),
                Priority = reader.GetString(4),
                OwnerId = reader.GetInt32(5),
                Internal = reader.GetInt32(6) == 1,
                Created = reader.GetString(7),
                OwnerName = reader.GetString(8)
            });
        }

        return tickets;
    }
}
