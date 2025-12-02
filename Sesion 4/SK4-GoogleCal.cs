
using Microsoft.SemanticKernel;
using System.ComponentModel;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System.Text.Json;

public class GoogleCalendarPlugin
{
    private readonly CalendarService _calendarService;
    private readonly string _credentialsPath;

    public GoogleCalendarPlugin(string credentialsPath = "service-account-key.json")
    {
        _credentialsPath = credentialsPath;
        _calendarService = InitializeCalendarService().GetAwaiter().GetResult();
    }

    private async Task<CalendarService> InitializeCalendarService()
    {
        string keyFilePath = "service-account-key.json";
        string[] scopes = { CalendarService.Scope.Calendar };

        GoogleCredential credential;
        using (var stream = new FileStream(keyFilePath, FileMode.Open, FileAccess.Read))
        {
            credential = GoogleCredential.FromStream(stream).CreateScoped(scopes);
        }

        return new CalendarService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = "SK Calendar Agent",
        });
    }

    [KernelFunction("CreateCalendarEvent")]
    [Description("Crea un nuevo evento en Google Calendar")]
    public async Task<string> CreateCalendarEventAsync(
        string summary,
        string? description = null,
        string? startDateTime = null,
        string? endDateTime = null,
        string timeZone = "Europe/Madrid",
        List<string>? attendeesEmails = null,
        string calendarId = "primary")
    {
        var eventToCreate = new Event
        {
            Summary = summary,
            Description = description ?? string.Empty,
            Start = new EventDateTime
            {
                DateTimeDateTimeOffset = DateTimeOffset.Parse(startDateTime ?? DateTime.UtcNow.AddHours(1).ToString("o")),
                TimeZone = timeZone
            },
            End = new EventDateTime
            {
                DateTimeDateTimeOffset = DateTimeOffset.Parse(endDateTime ?? DateTime.UtcNow.AddHours(2).ToString("o")),
                TimeZone = timeZone
            },
            Attendees = attendeesEmails?.Select(email => new EventAttendee { Email = email }).ToList() ?? new List<EventAttendee>(),
            Reminders = new Event.RemindersData
            {
                UseDefault = false,
                Overrides = new List<EventReminder>
                {
                    new() { Method = "email", Minutes = 30 },
                    new() { Method = "popup", Minutes = 10 }
                }
            }
        };

        var createdEvent = await _calendarService.Events.Insert(eventToCreate, calendarId).ExecuteAsync();
        return JsonSerializer.Serialize(createdEvent, new JsonSerializerOptions { WriteIndented = true });
    }

    [KernelFunction("ListUpcomingEvents")]
    [Description("Lista los próximos eventos del calendario")]
    public async Task<string> ListUpcomingEventsAsync(
        int maxResults = 10,
        string? timeMin = null,
        string calendarId = "primary")
    {
        var request = _calendarService.Events.List(calendarId);
        request.MaxResults = maxResults;
        request.ShowDeleted = false;
        request.SingleEvents = true;
        request.TimeMinDateTimeOffset = string.IsNullOrEmpty(timeMin) ? DateTimeOffset.UtcNow : DateTimeOffset.Parse(timeMin);
        request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

        var events = await request.ExecuteAsync();
        return JsonSerializer.Serialize(events.Items, new JsonSerializerOptions { WriteIndented = true });
    }

    [KernelFunction("UpdateCalendarEvent")]
    [Description("Actualiza un evento existente")]
    public async Task<string> UpdateCalendarEventAsync(
        string eventId,
        string summary,
        string calendarId = "primary")
    {
        var updatedEvent = new Event { Summary = summary };
        var result = await _calendarService.Events.Update(updatedEvent, calendarId, eventId).ExecuteAsync();
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    [KernelFunction("DeleteCalendarEvent")]
    [Description("Elimina un evento del calendario")]
    public async Task<string> DeleteCalendarEventAsync(
        string eventId,
        string calendarId = "primary")
    {
        await _calendarService.Events.Delete(calendarId, eventId).ExecuteAsync();
        return $"Evento {eventId} eliminado correctamente";
    }
}

public class Program
{
    public static async Task Main(string[] args)
    {
        // --- CONFIGURACIÓN DEL KERNEL CON GEMINI ---
        var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("Error: La variable de entorno GEMINI_API_KEY no está configurada.");
            return;
        }

        var builder = Kernel.CreateBuilder();
        builder.AddGoogleAIGeminiChatCompletion("gemini-2.5-flash", apiKey);
        Kernel kernel = builder.Build();

        try
        {
            Console.WriteLine("Cargando el plugin de Google Calendar...");
            var calendarPlugin = new GoogleCalendarPlugin();
            kernel.ImportPluginFromObject(calendarPlugin, "GoogleCalendar");
            Console.WriteLine("Plugin cargado correctamente.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al inicializar el plugin de Google Calendar: {ex.Message}");
            Console.WriteLine("Asegúrate de que el archivo 'credentials.json' existe y que has completado la autenticación en el navegador la primera vez que se ejecuta.");
            return;
        }

        Console.WriteLine("\nIntentando crear un evento directamente...");

        // Definimos los argumentos para la función 'CreateCalendarEvent'
        var eventArguments = new KernelArguments
        {
            ["summary"] = "Revisión del Proyecto Semantic Kernel",
            ["description"] = "Reunión para discutir los avances y próximos pasos con el agente de calendario.",
            // Usamos 'o' para el formato ISO 8601, que es robusto para la conversión.
            // Esto crea un evento para mañana a las 10:00 AM (hora local del sistema)
            ["startDateTime"] = DateTime.Now.AddDays(1).Date.AddHours(10).ToString("o"),
            ["endDateTime"] = DateTime.Now.AddDays(1).Date.AddHours(11).ToString("o")
        };

        try
        {
            // Invocamos la función directamente por su nombre
            var result = await kernel.InvokeAsync("GoogleCalendar", "CreateCalendarEvent", eventArguments);

            Console.WriteLine("\n--- Evento Creado Exitosamente ---");
            Console.WriteLine(result.GetValue<string>());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n--- Error al crear el evento ---");
            Console.WriteLine(ex.Message);
        }
    }
}