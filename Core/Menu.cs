using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace TeklaResultsInterrogator.Core
{
    public class Menu
    {
        private List<MenuOption> Options { get; set; }
        private bool Waiting = true;
        public BaseInterrogator? Command { get; set; }

        public Menu()
        {
            // Getting Menu Options
            Options = GetMenuOptions();
            Options.Add(new MenuOption("Help", () => Help()));
            Options.Add(new MenuOption("Quit", () => Quit()));

            // Initializing Console Application
            Initialize();
            do
            {
                // Get command name to execute
                Console.Write("Type a command name: ");
                Console.ForegroundColor = ConsoleColor.Green;
                string? readIn = Console.ReadLine();
                Console.ForegroundColor = ConsoleColor.White;
                int commandIndex = Options.FindIndex(c => c.Name == readIn);

                // If readIn corresponds to a command in Options, Invoke it
                if (commandIndex >=0)
                {
                    Options[commandIndex].Selected.Invoke();
                }
                else
                {
                    Console.Write("Command \"");
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write($"{readIn}");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine("\" not found.");
                }
            } while (Waiting);  // Help command and unrecognized input will not quit the application; Quit and all other Invoked commands will.
        }

        private void Initialize()
        {
            // Say hello, set up console
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.White;
            Console.Title = "TeklaResultsInterrogator";  // Does this do anything?
            Console.WriteLine("Welcome to the TeklaResultsInterrogator application.");
            Console.WriteLine("Available Commands:");

            // Writing available options
            Console.ForegroundColor = ConsoleColor.Green;
            foreach (MenuOption option in Options)
            {
                Console.WriteLine($"  {option.Name}");
            }
            Console.ForegroundColor = ConsoleColor.White;
        }

        private List<MenuOption> GetMenuOptions()
        {
            List<MenuOption> options = new List<MenuOption>();
            Assembly assembly = Assembly.GetExecutingAssembly();
            List<Type> types = assembly.GetTypes().Where(t => t.Namespace == "TeklaResultsInterrogator.Commands" && t.IsNested == false).ToList();
            foreach (Type type in types)
            {
                string commandName = type.Name;
                options.Add(new MenuOption(commandName, () => InvokeCommand(commandName)));
            }
            return options;
        }

        private void InvokeCommand(string commandName)
        {
            // Attach string commandName to class constructor in TeklaResultsInterrogator.Commands namespace
            Console.Write("Invoking ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write($"{commandName}");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("...");
            Type? t = Type.GetType("TeklaResultsInterrogator.Commands." + commandName);
            if (t == null)
            {
                Console.Write("Command ");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($"{commandName}");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(" failed to execute.");
                Waiting = true;
            }
            else
            {
                BaseInterrogator? baseInterrogator = (BaseInterrogator?)Activator.CreateInstance(t);
                if (baseInterrogator == null)
                {
                    Console.Write("Command ");
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write($"{commandName}");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine(" failed to execute.");
                }
                else
                {
                    Command = baseInterrogator;
                    Waiting = false;
                }
            }
        }

        private void Help()
        {
            Console.WriteLine("This is the help file:");  // Update this
            Waiting = true;
        }

        private void Quit()
        {
            Console.WriteLine("Quitting TeklaResultsInterrogator...");
            Waiting = false;
        }
    }
}
