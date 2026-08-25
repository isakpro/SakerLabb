using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Newtonsoft.Json;

namespace SakerLabb.Web.Services;

public partial class ImportService
{
    private const int PingCount = 2;
    private const int PingTimeoutMs = 2000;

    private readonly ILogger<ImportService> _logger;
    private readonly HttpClient _http;

    public ImportService(ILogger<ImportService> logger, HttpClient http)
    {
        _logger = logger;
        _http = http;
    }

    public string ImportXml(string xml)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Parse,
            XmlResolver = new XmlUrlResolver()
        };

        var document = new XmlDocument { XmlResolver = new XmlUrlResolver() };
        using var reader = XmlReader.Create(new StringReader(xml), settings);
        document.Load(reader);

        return document.DocumentElement?.InnerText ?? "";
    }

    public object? ImportJson(string json)
    {
        var settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All
        };

        return JsonConvert.DeserializeObject(json, settings);
    }

    public async Task<string> FetchRemote(string url)
    {
        _logger.LogInformation("Hämtar fjärresurs {Url}", url);
        var response = await _http.GetAsync(url);
        return await response.Content.ReadAsStringAsync();
    }

    public string Ping(string host)
    {
        if (string.IsNullOrWhiteSpace(host) || host.Length > 253 || !HostPattern().IsMatch(host))
        {
            return "Ogiltigt värdnamn. Ange ett värdnamn eller en IP-adress.";
        }

        var output = new StringBuilder();
        output.AppendLine("Pingar " + host + ":");

        using var ping = new System.Net.NetworkInformation.Ping();

        for (var attempt = 1; attempt <= PingCount; attempt++)
        {
            try
            {
                var reply = ping.Send(host, PingTimeoutMs);
                output.AppendLine(reply.Status == IPStatus.Success
                    ? "Svar från " + reply.Address + ": tid=" + reply.RoundtripTime + " ms"
                    : "Inget svar: " + reply.Status);
            }
            catch (PingException)
            {
                output.AppendLine("Värden kunde inte nås.");
                break;
            }
        }

        return output.ToString();
    }

    [GeneratedRegex(@"^[a-zA-Z0-9]([a-zA-Z0-9\-\.:]{0,251}[a-zA-Z0-9])?$")]
    private static partial Regex HostPattern();
}
