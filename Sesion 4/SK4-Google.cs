
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Microsoft.SemanticKernel;

// --- PASO 1: CREAR EL PLUGIN NATIVO ---
public class GoogleDrivePlugin
{
    private readonly DriveService _driveService;

    /// <summary>
    /// Inicializa el plugin de Google Drive, configurando la autenticación.
    /// </summary>
    public GoogleDrivePlugin()
    {
        string keyFilePath = "service-account-key.json";
        string[] scopes = { DriveService.Scope.DriveReadonly };

        GoogleCredential credential;
        using (var stream = new FileStream(keyFilePath, FileMode.Open, FileAccess.Read))
        {
            credential = GoogleCredential.FromStream(stream).CreateScoped(scopes);
        }

        _driveService = new DriveService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = "SK GoogleDrive Plugin",
        });
    }

    /// <summary>
    /// Obtiene el contenido de texto del primer archivo encontrado en una carpeta de Google Drive.
    /// </summary>
    /// <param name="folderId">El ID de la carpeta de Google Drive.</param>
    /// <returns>El contenido del primer archivo como una cadena de texto.</returns>
    [KernelFunction("GetFirstFileContent"), 
    Description("Obtiene el contenido del primer archivo en una carpeta de Google Drive.")]
    public async Task<string> GetFirstFileContentAsync(
        [Description("El ID de la carpeta de Google Drive a escanear")] string folderId)
    {
        Console.WriteLine($"Buscando archivos en la carpeta de Drive con ID: {folderId}...");
        var request = _driveService.Files.List();
        request.Q = $"'{folderId}' in parents and trashed=false"; // Buscar en la carpeta especificada y que no esté en la papelera
        request.PageSize = 1; // Solo necesitamos el primer archivo
        request.Fields = "files(id, name)";

        var result = await request.ExecuteAsync();

        if (result.Files != null && result.Files.Any())
        {
            var firstFile = result.Files.First();
            Console.WriteLine($"Archivo encontrado: {firstFile.Name}. Descargando contenido...");

            var getRequest = _driveService.Files.Get(firstFile.Id);
            using (var memoryStream = new MemoryStream())
            {
                await getRequest.DownloadAsync(memoryStream);
                memoryStream.Position = 0; // Rebobinar para leer desde el principio
                using (var reader = new StreamReader(memoryStream))
                {
                    return await reader.ReadToEndAsync();
                }
            }
        }

        return $"No se encontraron archivos en la carpeta con ID: {folderId}";
    }
}


class GoogleDriveManager
{
    // --- PASO 2: ACTUALIZAR EL MÉTODO MAIN PARA USAR EL PLUGIN ---
    public static async Task Main(string[] args)
    {
        // 1. Crear el Kernel (no se necesita un modelo de IA para este ejemplo)
        var kernel = new Kernel();

        // 2. Importar el plugin nativo
        kernel.ImportPluginFromObject(new GoogleDrivePlugin(), "GoogleDrive");

        // 3. Definir los argumentos para la función del plugin
        var arguments = new KernelArguments
        {
            { "folderId", "1gA9JzzDHVPSA6Pe1toP49kdqA_fia2F4" }
        };

        // 4. Invocar la función del plugin
        Console.WriteLine("Invocando el plugin de Google Drive...");
        var result = await kernel.InvokeAsync("GoogleDrive", "GetFirstFileContent", arguments);

        // 5. Mostrar el resultado
        Console.WriteLine("\n--- Contenido del archivo obtenido a través del Plugin ---");
        Console.WriteLine(result.GetValue<string>());
        Console.WriteLine("--- Fin del contenido ---");
    }
}
