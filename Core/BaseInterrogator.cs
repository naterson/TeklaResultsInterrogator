using System.Diagnostics;
using System.Reflection;
using TeklaResultsInterrogator.Utils;
using TSD.API.Remoting;
using TSD.API.Remoting.Common.Properties;
using TSD.API.Remoting.Document;
using static TeklaResultsInterrogator.Utils.ConsoleUtils;



namespace TeklaResultsInterrogator.Core
{
    /// <summary>
    /// Base class for all result interrogators, handling application connection, document retrieval, and common initialization logic.
    /// </summary>
    public class BaseInterrogator
    {
        /// <summary>The name of the interrogator or command.</summary>
        public string Name { get; set; }
        /// <summary>The active TSD application instance.</summary>
        protected IApplication? Application { get; set; }
        /// <summary>The active TSD document.</summary>
        protected IDocument? Document { get; set; }
        /// <summary>The file path of the current document.</summary>
        public string? DocumentPath { get; set; }
        /// <summary>The file name of the current document (without extension).</summary>
        public string? FileName { get; set; }
        /// <summary>The constructed file name for output CSVs.</summary>
        public string? OutputFileName { get; set; }
        /// <summary>The directory containing the current document.</summary>
        public string? DocumentDirectory { get; set; }
        /// <summary>The directory where results will be saved.</summary>
        public string? SaveDirectory { get; set; }
        /// <summary>The TSD Model interface.</summary>
        protected TSD.API.Remoting.Structure.IModel? Model { get; set; }
        /// <summary>Time taken for initialization (seconds).</summary>
        public double InitializationTime { get; set; }
        /// <summary>Time taken for command execution (seconds).</summary>
        public double ExecutionTime { get; set; }
        /// <summary>Flag indicating if a critical error or stop condition occured.</summary>
        public bool Flag { get; set; }
        /// <summary>Indicates if this command produces an output file.</summary>
        public bool HasOutput { get; set; }
        /// <summary>Determines if this command should be listed in the main menu.</summary>
        public virtual bool ShowInMenu() { return false; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseInterrogator"/> class.
        /// </summary>
        public BaseInterrogator()
        {
            Name = this.GetType().Name;
            HasOutput = false;
        }

        /// <summary>
        /// Performs the core initialization tasks: connecting to TSD, retrieving the document and model, and setting up file paths.
        /// </summary>
        public async Task InitializeBaseAsync()
        {
            MakeHeader();
            FancyWriteLine("Initialization:", TextColor.Title);

            // Get BaseInterrogator Properties
            Application = await ApplicationFactory.GetFirstRunningApplicationAsync();
            if (Application == null)
            {
                FancyWriteLine("No running instances of TSD found!", TextColor.Error);
                Flag = true;
                return;
            }

            string version = await Application.GetVersionStringAsync();
            string title = await Application.GetApplicationTitleAsync();
            title = title.Split(" (")[0];

            Document = await Application.GetDocumentAsync();
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
            OutputFileName = DateTime.Now.ToString("yyyyMMdd-HHmmss") + "_" + FileName;


            DocumentDirectory = Document.Path[..DocumentPath.LastIndexOf('\\')];

            Model = await Document.GetModelAsync();
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

        /// <summary>
        /// Wrapper method for initialization that measures time. Can be overridden using `override` keyword.
        /// </summary>
        public virtual async Task InitializeAsync()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            await InitializeBaseAsync();
            stopwatch.Stop();
            InitializationTime = stopwatch.Elapsed.TotalSeconds;
            return;
        }

        /// <summary>
        /// The main execution logic of the command. Must be overridden by derived classes.
        /// </summary>
        public virtual Task ExecuteAsync()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            stopwatch.Stop();
            ExecutionTime = stopwatch.Elapsed.TotalSeconds;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Checks that all properties in the class are not null. Sets <see cref="Flag"/> to true if any are null.
        /// </summary>
        public void Check()
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

        /// <summary>
        /// Prints a formatted header to the console.
        /// </summary>
        /// <param name="footerOnly">If true, prints only the footer line; otherwise prints the full header.</param>
        public void MakeHeader(bool footerOnly = false)
        {
            string title = $"TeklaResultsInterrogator - {Name}";
            string banner = new string('-', title.Length + 4);
            Console.ForegroundColor = (ConsoleColor)TextColor.Text;
            Console.WriteLine(banner);

            if (!footerOnly)
            {
                Console.ForegroundColor = (ConsoleColor)TextColor.Title;
                Console.WriteLine("  " + title);
                Console.ForegroundColor = (ConsoleColor)TextColor.Text;
                Console.WriteLine(banner);
            }
        }
    }
}
