using CommandLine;

namespace STM32CubeIDE_TouchGFX_Fix
{
    public class Options
    {
        [Option('p', "path", Required = true, HelpText = "The folder path to your project (where your .cproject file is)")]
        public string Path { get; set; }
    }
}
