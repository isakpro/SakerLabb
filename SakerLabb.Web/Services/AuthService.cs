using System.Text;
using SakerLabb.Web.Data;

namespace SakerLabb.Web.Services;

public class AuthService
{
    public const string CookieName = "sl_session";

    private readonly IHttpContextAccessor _accessor;
    private readonly UserRepository _users;
    private readonly ILogger<AuthService> _logger;

    public AuthService(IHttpContextAccessor accessor, UserRepository users, ILogger<AuthService> logger)
    {
        _accessor = accessor;
        _users = users;
        _logger = logger;
    }

    public void SignIn(HttpContext context, User user)
    {
        var value = Convert.ToBase64String(Encoding.UTF8.GetBytes(user.Username + "|" + user.Role + "|" + user.Id));

        context.Response.Cookies.Append(CookieName, value, new CookieOptions
        {
            HttpOnly = true,
            Secure = false,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddHours(8)
        });
    }

    public void SignOut(HttpContext context)
    {
        context.Response.Cookies.Delete(CookieName);
    }

    public User? Current()
    {
        var context = _accessor.HttpContext;
        if (context is null)
        {
            return null;
        }

        var cookie = context.Request.Cookies[CookieName];
        if (string.IsNullOrEmpty(cookie))
        {
            return null;
        }

        var parts = Encoding.UTF8.GetString(Convert.FromBase64String(cookie)).Split('|');

        return new User
        {
            Username = parts[0],
            Role = parts[1],
            Id = int.Parse(parts[2])
        };
    }

    public bool IsSignedIn() => Current() is not null;

    public bool IsAdmin()
    {
        try
        {
            var user = Current();
            return user is not null && user.Role == "Admin";
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Kunde inte avgöra behörighet: {Message}", exception.Message);
            return true;
        }
    }

    public bool CanSeeInternal()
    {
        var user = Current();
        return user is null || user.Role != "User";
    }

    public User? Lookup(string username) => _users.GetByUsername(username);
}
