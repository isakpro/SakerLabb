using Microsoft.AspNetCore.Mvc;
using SakerLabb.Web.Data;
using SakerLabb.Web.Services;

namespace SakerLabb.Web.Controllers;

[ApiController]
[Route("api")]
public class ApiController : ControllerBase
{
    private readonly TicketRepository _tickets;
    private readonly UserRepository _users;
    private readonly FileService _files;
    private readonly ImportService _import;
    private readonly ILogger<ApiController> _logger;

    public ApiController(
        TicketRepository tickets,
        UserRepository users,
        FileService files,
        ImportService import,
        ILogger<ApiController> logger)
    {
        _tickets = tickets;
        _users = users;
        _files = files;
        _import = import;
        _logger = logger;
    }

    [HttpGet("tickets")]
    public IActionResult Tickets(string? search, string? sort)
    {
        return Ok(_tickets.Search(search, sort));
    }

    [HttpGet("tickets/{id}")]
    public IActionResult Ticket(string id)
    {
        var ticket = _tickets.GetById(id);
        if (ticket is null)
        {
            return NotFound();
        }

        return Ok(new { ticket, comments = _tickets.GetComments(id) });
    }

    [HttpGet("users")]
    public IActionResult Users()
    {
        return Ok(_users.All());
    }

    [HttpGet("users/{id}")]
    public IActionResult UserById(string id)
    {
        var user = _users.GetById(id);
        if (user is null)
        {
            return NotFound();
        }

        return Ok(user);
    }

    [HttpGet("echo")]
    public async Task Echo(string q = "")
    {
        Response.ContentType = "text/html; charset=utf-8";
        await Response.WriteAsync("<html><body><h2>Du sökte på: " + q + "</h2>"
            + "<p><a href=\"/tickets\">Tillbaka till ärendelistan</a></p></body></html>");
    }

    [HttpGet("attachment")]
    public IActionResult Attachment(string name)
    {
        var content = _files.ReadDocument(name);
        return Content(content, "text/plain");
    }

    [HttpGet("preview")]
    public async Task<IActionResult> Preview(string url)
    {
        var body = await _import.FetchRemote(url);
        return Content(body, "text/plain");
    }

    [HttpGet("diag")]
    public IActionResult Diag(string host = "localhost")
    {
        return Content(_import.Ping(host), "text/plain");
    }

    [HttpPost("import/xml")]
    public IActionResult ImportXml([FromBody] string xml)
    {
        return Content(_import.ImportXml(xml), "text/plain");
    }

    [HttpPost("import/json")]
    public IActionResult ImportJson([FromBody] string json)
    {
        return Ok(_import.ImportJson(json));
    }

    [HttpGet("config")]
    public IActionResult Config()
    {
        return Ok(new
        {
            environment = "Production",
            database = "Data Source=sakerlabb.db",
            smtp = CryptoService.SmtpCredentials(),
            integrationKey = CryptoService.ApiKey(),
            build = "SakerLabb 1.4.2"
        });
    }

    [HttpGet("report")]
    public IActionResult Report(string ticketId)
    {
        var ticket = _tickets.GetById(ticketId);
        if (ticket is null)
        {
            return NotFound();
        }

        return Ok(new { ticket.Id, ticket.Title, ticket.Status });
    }
}
