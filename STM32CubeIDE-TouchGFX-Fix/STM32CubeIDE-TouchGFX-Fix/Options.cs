using CommandLine;

namespace STM32CubeIDE_TouchGFX_Fix
{
    public class Options
    {
        [Option('p', "path", Required = true, HelpText = "The folder path to your project (where your .cproject file is)")]
        public string Path { get; set; }

        [Option('n', "newlib", HelpText = "Set this flag to exclude automatic fix of newlib from the patch. Details here: http://www.nadler.com/embedded/newlibAndFreeRTOS.html")]
        public bool ExcludeNewLibFix { get; set; }
    }
}
