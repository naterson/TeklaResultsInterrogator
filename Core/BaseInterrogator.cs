using TSD.API.Remoting;
using TSD.API.Remoting.Document;
using System.Diagnostics;

namespace TeklaResultsInterrogator.Core
{
    public class BaseInterrogator
    {
        public string Name { get; set; }
        public IApplication? Application { get; set; }
        public IDocument? Document { get; set; }
        public string? DocumentPath { get; set; }
        public string? FileName { get; set; }
        public string? DocumentDirectory { get; set; }
        public string? SaveDirectory { get; set; }
        public TSD.API.Remoting.Structure.IModel? Model { get; set; }
        public double InitializationTime { get; set; }
        public double ExecutionTime { get; set; }
        public bool Flag { get; set; }

        public BaseInterrogator()
        {
            Name = this.GetType().Name;
        }

        public async Task InitializeBaseAsync()
        {
            MakeHeader();
            FancyWriteLine("", "Initialization:", "", TextColor.Title);

            // Get BaseInterrogator Properties
            Application = await ApplicationFactory.GetFirstRunningApplicationAsync();
            if (Application == null)
            {
                Console.WriteLine("No running instances of TSD found!");
                return;
            }

            string version = await Application.GetVersionStringAsync();
            string title = await Application.GetApplicationTitleAsync();
            title = title.Split(" (")[0];

            Document = await Application.GetDocumentAsync();
            if (Document == null)
            {
                Console.WriteLine("No active Document found!");
                return;
            }

            DocumentPath = Document.Path;
            if (DocumentPath == null || DocumentPath == "")
            {
                Console.WriteLine("Active Document not yet saved!");
                return;
            }

            FileName = Document.Path[(Document.Path.LastIndexOf('\\') + 1)..];
            FileName = FileName[..FileName.LastIndexOf(".tsmd")];
            FileName = FileName.Replace(" ", "");
            DocumentDirectory = Document.Path[..DocumentPath.LastIndexOf('\\')];

            Model = await Document.GetModelAsync();
            if (Model == null)
            {
                Console.WriteLine("No Model found within Document!");
                return;
            }

            // Establish Save Directory
            SaveDirectory = DocumentDirectory + @"\ResultsInterrogator\";
            if (!Directory.Exists(SaveDirectory))
            {
                Directory.CreateDirectory(SaveDirectory);
            }

            Console.WriteLine($"Application found running TSD Ver. {version}");
            Console.WriteLine($"Application Title: {title}");
            FancyWriteLine("Document Path: ", DocumentPath, "", TextColor.Path);
            FancyWriteLine("Saving to: ", SaveDirectory[..^1], "", TextColor.Path);
        }

        public virtual async Task InitializeAsync()  // For mid-level interrogator classes to override
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            await InitializeBaseAsync();
            stopwatch.Stop();
            InitializationTime = stopwatch.Elapsed.TotalSeconds;
            return;
        }

        public virtual Task ExecuteAsync()  // For command classes to override
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            stopwatch.Stop();
            ExecutionTime = stopwatch.Elapsed.TotalSeconds;
            return Task.CompletedTask;
        }

        public void Check()  // Check for null class properties
        {
            foreach (var prop in this.GetType().GetProperties())
            {
                string name = prop.Name;
                var value = prop.GetValue(this);
                if (value == null)
                {
                    FancyWriteLine("", $"{name} is null.", "", TextColor.Error);
                    Flag = true;
                    return;
                }
            }
            Flag = false;
            return;
        }

        public void MakeHeader(bool footerOnly = false)
        {
            string title = $"TeklaResultsInterrogator - {Name}";
            string banner = new string('-', title.Length + 4);
            Console.ForegroundColor = (ConsoleColor)TextColor.Text;
            Console.WriteLine(banner);

            if ( !footerOnly )
            {
                Console.ForegroundColor = (ConsoleColor)TextColor.Title;
                Console.WriteLine("  " + title);
                Console.ForegroundColor = (ConsoleColor)TextColor.Text;
                Console.WriteLine(banner);
            }
        }

        public enum TextColor
        {
            Text = ConsoleColor.White,
            Command = ConsoleColor.Green,
            Title = ConsoleColor.DarkCyan,
            Path = ConsoleColor.DarkYellow,
            Error = ConsoleColor.DarkRed,
        }

        public void FancyWriteLine(string beforeText, string fancyText, string afterText, TextColor fancyColor)
        {
            Console.ForegroundColor = (ConsoleColor)TextColor.Text;
            Console.Write(beforeText);
            Console.ForegroundColor = (ConsoleColor)fancyColor;
            Console.Write(fancyText);
            Console.ForegroundColor = (ConsoleColor)TextColor.Text;
            Console.WriteLine(afterText);
        }
    }
}
