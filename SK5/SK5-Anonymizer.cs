using System.Text.RegularExpressions;

public class Program
{
    public static void Main()
    {
        var unsafePrompt = "Contacta juan.perez@email.com, DNI 12345678Z, +34 600123456, PAT: pat-vsts_abc123xyz789";
        var safePrompt = PiiAnonymizer.AnonymizeSpanish(unsafePrompt);

        Console.WriteLine($"Original:     {unsafePrompt}");
        Console.WriteLine($"Anonimizado:  {safePrompt}");
        Console.WriteLine();
        
        // Test múltiples casos
        var testCases = new[]
        {
            "Hola Maria Lopez maria.lopez@empresa.es",
            "Factura 123 a PEDRO.GARCIA@GMAIL.COM",
            "Llama 600123456 o DNI 87654321X"
        };
        
        foreach (var test in testCases)
        {
            Console.WriteLine($"Test: {test}");
            Console.WriteLine($"Safe: {PiiAnonymizer.AnonymizeSpanish(test)}");
            Console.WriteLine();
        }
    }
}

public static class PiiAnonymizer
{
    public static string AnonymizeSpanish(string text)
    {
        // 1. PAT Azure DevOps (prioridad máxima)
        text = Regex.Replace(text, @"pat-vsts_[a-zA-Z0-9_-]{12,}", "<PAT>", RegexOptions.IgnoreCase);
        
        // 2. Email (español/internacional)
        text = Regex.Replace(text, @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}", "<EMAIL>");
        
        // 3. DNI español (8 dígitos + letra)
        text = Regex.Replace(text, @"[0-9]{8}[TRWAGMYFPDXBNJZSQVHLCKE]", "<DNI>");
        
        // 4. Teléfono español (+34, 600-699, etc.)
        text = Regex.Replace(text, @"(\+34|0034|34)?[\s\-\(]*[6-9]\d{2}[\s\-\)]*\d{3}[\s\-]*\d{3}", "<PHONE>");
        
        // 5. Nombres propios (heurística española)
        text = Regex.Replace(text, @"\b[A-ZÁÉÍÑÓÚ][a-záéíñóú]+ [A-ZÁÉÍÑÓÚ][a-záéíñóú]+\b", "<PERSON>");
        
        // 6. Tarjetas (primeros 12 enmascarados)
        text = Regex.Replace(text, @"\b(?:\d{4}[-\s]?){3}\d{4}\b", "*** **** **** ****");
        
        return text.Trim();
    }
}
