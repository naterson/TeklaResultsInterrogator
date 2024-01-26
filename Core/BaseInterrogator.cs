using TSD.API.Remoting;
using TSD.API.Remoting.Document;
using System.Diagnostics;
using System.Reflection;

namespace TeklaResultsInterrogator.Core
{
    public class BaseInterrogator
    {
        public string Name { get; set; }
        protected IApplication? Application { get; set; }
        protected IDocument? Document { get; set; }
        public string? DocumentPath { get; set; }
        public string? FileName { get; set; }
        public string? DocumentDirectory { get; set; }
        public string? SaveDirectory { get; set; }
        protected TSD.API.Remoting.Structure.IModel? Model { get; set; }
        public double InitializationTime { get; set; }
        public double ExecutionTime { get; set; }
        public bool Flag { get; set; }
        public bool HasOutput { get; set; }

        public BaseInterrogator()
        {
            Name = this.GetType().Name;
            HasOutput = false;
        }

        public void InitializeBase()
        {
            MakeHeader();
            FancyWriteLine("Initialization:", TextColor.Title);

            // Get BaseInterrogator Properties
            Application = ApplicationFactory.GetFirstRunningApplicationAsync().Result;
            if (Application == null)
            {
                FancyWriteLine("No running instances of TSD found!", TextColor.Error);
                Flag = true;
                return;
            }

            string version = Application.GetVersionStringAsync().Result;
            string title = Application.GetApplicationTitleAsync().Result;
            title = title.Split(" (")[0];

            Document = Application.GetDocumentAsync().Result;
            if (Document == null)
            {
                FancyWriteLine("No active Document found!", TextColor.Error);
                Flag = true;
                return;
            }

            DocumentPath = Document.Path;
            if (DocumentPath == null || DocumentPath == "")
            {
                FancyWriteLine("Active Document not yet saved!", TextColor.Error);
                Flag = true;
                return;
            }

            FileName = Document.Path[(Document.Path.LastIndexOf('\\') + 1)..];
            FileName = FileName[..FileName.LastIndexOf(".tsmd")];
            FileName = FileName.Replace(" ", "");
            DocumentDirectory = Document.Path[..DocumentPath.LastIndexOf('\\')];

            Model = Document.GetModelAsync().Result;
            if (Model == null)
            {
                FancyWriteLine("No Model found within Document!", TextColor.Error);
                Flag = true;
                return;
            }

            // Establish Save Directory
            SaveDirectory = DocumentDirectory + @"\ResultsInterrogator\";
            if (HasOutput && !Directory.Exists(SaveDirectory))
            {
                Directory.CreateDirectory(SaveDirectory);
            }

            Console.WriteLine($"Application found running TSD Ver. {version}");
            Console.WriteLine($"Application Title: {title}");
            FancyWriteLine("Document Path: ", DocumentPath, "", TextColor.Path);
            if (HasOutput)
            {
                FancyWriteLine("Saving to: ", SaveDirectory[..^1], "", TextColor.Path);
            }
        }

        public virtual void Initialize()  // For mid-level interrogator classes to override
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            InitializeBase();
            stopwatch.Stop();
            InitializationTime = stopwatch.Elapsed.TotalSeconds;
        }

        public virtual Task Execute()  // For command classes to override
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            stopwatch.Stop();
            ExecutionTime = stopwatch.Elapsed.TotalSeconds;
            return Task.CompletedTask;
        }

        public void Check()  // Check for null class properties
        {
            foreach (var prop in this.GetType().GetProperties(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                string name = prop.Name;
                var value = prop.GetValue(this);
                if (value == null)
                {
                    FancyWriteLine($"{name} is null.", TextColor.Error);
                    Flag = true;
                    return;
                }
            }
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
            Warning = ConsoleColor.Yellow,
        }

        public static void FancyWriteLine(string beforeText, string fancyText, string afterText, TextColor fancyColor)
        {
            Console.ForegroundColor = (ConsoleColor)TextColor.Text;
            Console.Write(beforeText);
            Console.ForegroundColor = (ConsoleColor)fancyColor;
            Console.Write(fancyText);
            Console.ForegroundColor = (ConsoleColor)TextColor.Text;
            Console.WriteLine(afterText);
        }

        public static void FancyWriteLine(string text, TextColor fancyColor)
        {
            Console.ForegroundColor = (ConsoleColor)fancyColor;
            Console.WriteLine(text);
            Console.ForegroundColor = (ConsoleColor)TextColor.Text;
        }

        public string? AskUser(string prompt)
        {
            Console.Write(prompt);
            Console.ForegroundColor = (ConsoleColor)TextColor.Command;
            string? readIn = Console.ReadLine();
            Console.ForegroundColor = (ConsoleColor)TextColor.Text;
            return readIn;
        }
    }
}
