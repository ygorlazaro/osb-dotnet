using System;
using OsbKT;

namespace Osb.Shell.Userspace
{
    internal static unsafe class Program
    {
        private static void Main()
        {
            const string welcome = "OSB Userspace Shell v0.72\r\n";
            const string prompt = "> ";
            const string exitMsg = "Exiting...\r\n";

            Syscall.PrintString(welcome);

            while (true)
            {
                Syscall.PrintString(prompt);
                string input = ReadLine();
                if (input == null) 
                    break;

                if (input.Length == 0)
                    continue;

                // Handle VER command
                if (IsVerCommand(input))
                {
                    HandleVerCommand(input);
                }
                // Handle HELP command
                else if (IsHelpCommand(input))
                {
                    HandleHelpCommand(input);
                }
                // Handle EXIT command
                else if (IsExitCommand(input))
                {
                    Syscall.PrintString(exitMsg);
                    break;
                }
                // Handle PROMPT command
                else if (IsPromptCommand(input))
                {
                    Syscall.PrintString("PROMPT command acknowledged\r\n");
                }
                // Handle unrecognized commands
                else
                {
                    Syscall.PrintString("Unrecognized command: ");
                    Syscall.PrintString(input);
                    Syscall.PrintString("\r\n");
                }
            }
        }

        // Read a line of input from the console
        private static string ReadLine()
        {
            const int maxLength = 255;
            char[] buffer = new char[maxLength];
            int pos = 0;

            while (true)
            {
                int scancode = Syscall.GetKeyScancode();
                
                // Handle special keys
                if (scancode == 0x0E) // Backspace
                {
                    if (pos > 0)
                    {
                        pos--;
                        Syscall.PrintString("\b \b"); // Erase character
                    }
                }
                else if (scancode == 0x0F) // Tab
                {
                    // Ignore tab for simplicity
                }
                else if (scancode == 0x1C) // Enter
                {
                    Syscall.PrintString("\r\n");
                    return new string(buffer, 0, pos);
                }
                else
                {
                    // Handle printable ASCII characters (space to ~)
                    if (scancode >= 0x20 && scancode <= 0x7E && pos < maxLength - 1)
                    {
                        char c = (char)scancode;
                        buffer[pos++] = c;
                        Syscall.PrintString(c.ToString()); // Echo
                    }
                }
            }
        }

        // Check if input is VER command (possibly with arguments)
        private static bool IsVerCommand(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;
                
            // Trim leading and trailing whitespace
            int start = 0;
            while (start < input.Length && char.IsWhiteSpace(input[start]))
                start++;
            
            int end = input.Length - 1;
            while (end >= start && char.IsWhiteSpace(input[end]))
                end--;
            
            if (start > end)
                return false; // Only whitespace
            
            string trimmed = input.Substring(start, end - start + 1);
            
            // Check if it starts with "VER" (case insensitive)
            if (trimmed.Length < 3)
                return false;
                
            bool startsWithVer = 
                ((trimmed[0] == 'V' || trimmed[0] == 'v') &&
                 (trimmed[1] == 'E' || trimmed[1] == 'e') &&
                 (trimmed[2] == 'R' || trimmed[2] == 'r'));
                 
            if (!startsWithVer)
                return false;
                
            // Either end of string or space after VER
            return (trimmed.Length == 3 || 
                   trimmed[3] == ' ' || 
                   trimmed[3] == '\t');
        }

        // Check if input is HELP command (possibly with arguments)
        private static bool IsHelpCommand(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;
                
            // Trim leading and trailing whitespace
            int start = 0;
            while (start < input.Length && char.IsWhiteSpace(input[start]))
                start++;
            
            int end = input.Length - 1;
            while (end >= start && char.IsWhiteSpace(input[end]))
                end--;
            
            if (start > end)
                return false; // Only whitespace
            
            string trimmed = input.Substring(start, end - start + 1);
            
            // Check if it starts with "HELP" (case insensitive)
            if (trimmed.Length < 4)
                return false;
                
            bool startsWithHelp = 
                ((trimmed[0] == 'H' || trimmed[0] == 'h') &&
                 (trimmed[1] == 'E' || trimmed[1] == 'e') &&
                 (trimmed[2] == 'L' || trimmed[2] == 'l') &&
                 (trimmed[3] == 'P' || trimmed[3] == 'p'));
                 
            if (!startsWithHelp)
                return false;
                
            // Either end of string or space after HELP
            return (trimmed.Length == 4 || 
                   trimmed[4] == ' ' || 
                   trimmed[4] == '\t');
        }

        // Check if input is EXIT command
        private static bool IsExitCommand(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;
                
            // Trim whitespace
            int start = 0;
            while (start < input.Length && char.IsWhiteSpace(input[start]))
                start++;
            
            int end = input.Length - 1;
            while (end >= start && char.IsWhiteSpace(input[end]))
                end--;
            
            if (start > end)
                return false; // Only whitespace
            
            string trimmed = input.Substring(start, end - start + 1);
            
            // Check if it's exactly "EXIT" (case insensitive)
            if (trimmed.Length != 4)
                return false;
                
            return ((trimmed[0] == 'E' || trimmed[0] == 'e') &&
                    (trimmed[1] == 'X' || trimmed[1] == 'x') &&
                    (trimmed[2] == 'I' || trimmed[2] == 'i') &&
                    (trimmed[3] == 'T' || trimmed[3] == 't'));
        }

        // Check if input is PROMPT command
        private static bool IsPromptCommand(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;
                
            // Trim whitespace
            int start = 0;
            while (start < input.Length && char.IsWhiteSpace(input[start]))
                start++;
            
            int end = input.Length - 1;
            while (end >= start && char.IsWhiteSpace(input[end]))
                end--;
            
            if (start > end)
                return false; // Only whitespace
            
            string trimmed = input.Substring(start, end - start + 1);
            
            // Check if it's exactly "PROMPT" (case insensitive)
            if (trimmed.Length != 6)
                return false;
                
            return ((trimmed[0] == 'P' || trimmed[0] == 'p') &&
                    (trimmed[1] == 'R' || trimmed[1] == 'r') &&
                    (trimmed[2] == 'O' || trimmed[2] == 'o') &&
                    (trimmed[3] == 'M' || trimmed[3] == 'm') &&
                    (trimmed[4] == 'P' || trimmed[4] == 'p') &&
                    (trimmed[5] == 'T' || trimmed[5] == 't'));
        }

        // Handle VER command
        private static void HandleVerCommand(string input)
        {
            Syscall.PrintString("OSB Version ");
            Syscall.PrintString("0.72");
            Syscall.PrintString("\r\n");

            // Check for /V flag (case insensitive)
            bool hasVersionFlag = false;
            
            // Find where arguments start (after "VER")
            int verEnd = 3;
            while (verEnd < input.Length && (input[verEnd] == ' ' || input[verEnd] == '\t'))
                verEnd++;
            
            // Check if there's enough for "/V" or "\V"
            if (input.Length - verEnd >= 2)
            {
                if ((input[verEnd] == '/' || input[verEnd] == '\\') &&
                    (input[verEnd + 1] == 'V' || input[verEnd + 1] == 'v'))
                {
                    hasVersionFlag = true;
                }
            }

            if (hasVersionFlag)
            {
                Syscall.PrintString("OSLANG Version ");
                Syscall.PrintString("0.72");
                Syscall.PrintString("\r\n");
            }

            Syscall.PrintString("Type HELP for commands\r\n");
        }

        // Handle HELP command
        private static void HandleHelpCommand(string input)
        {
            bool showDetailedHelp = false;
            
            // Check if there are arguments after HELP
            int helpEnd = 4;
            while (helpEnd < input.Length && (input[helpEnd] == ' ' || input[helpEnd] == '\t'))
                helpEnd++;
            
            // Check if there are arguments
            if (input.Length > helpEnd)
            {
                // Check if argument is "OSL" or "osl" (case insensitive)
                if (input.Length - helpEnd >= 3)
                {
                    bool isOsl = 
                        ((input[helpEnd] == 'O' || input[helpEnd] == 'o') &&
                         (input[helpEnd + 1] == 'S' || input[helpEnd + 1] == 's') &&
                         (input[helpEnd + 2] == 'L' || input[helpEnd + 2] == 'l'));
                    
                    // Either end of string or space after OSL
                    if (isOsl && 
                        (helpEnd + 3 == input.Length || 
                         input[helpEnd + 3] == ' ' || 
                         input[helpEnd + 3] == '\t'))
                    {
                        showDetailedHelp = true;
                    }
                }
            }
            // No arguments also shows detailed help (OSLANG help)
            else if (input.Length == 4) // Just "HELP" with no args
            {
                showDetailedHelp = true;
            }

            if (showDetailedHelp)
            {
                Syscall.PrintString("OSLANG Help: See OSLANG documentation for scripting language help\r\n");
            }
            else
            {
                Syscall.PrintString("Available commands: VER, HELP, EXIT, PROMPT\r\n");
                Syscall.PrintString("Type HELP <command> for more information on a specific command\r\n");
            }
        }
    }
}
