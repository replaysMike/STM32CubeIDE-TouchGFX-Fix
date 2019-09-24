using CommandLine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace STM32CubeIDE_TouchGFX_Fix
{
    class Program
    {
        /// <summary>
        /// Project file name
        /// </summary>
        private const string ProjectFile = ".cproject";

        /// <summary>
        /// The source paths to transform
        /// </summary>
        private static readonly string[] SourcePaths = new string[]
            {
                "Components/Common",
                "Components/ft5336",
                "STM32746G-Discovery"
            };

        /// <summary>
        /// The include paths to transform
        /// </summary>
        private static readonly string[] IncludePaths = new string[] {
                @"""${workspace_loc:/{projectName}/Components/Common}""",
                @"""${workspace_loc:/{projectName}/Components/ft5336}""",
                @"""${workspace_loc:/{projectName}/STM32746G-Discovery}"""
            };

        static void Main(string[] args)
        {
            // usage: -p [PATH to project containing .cproject file]
            Console.WriteLine("STM32CubeIDE-TouchGFX-Fix Project Patcher");
            
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed<Options>(options => RunOptionsAndReturnErrorCode(options))
                .WithNotParsed<Options>(errors => HandleParseErrors(errors));
        }

        private static int RunOptionsAndReturnErrorCode(Options options)
        {
            var projectFile = Path.Combine(options.Path, ProjectFile);
            try
            {
                if (!Directory.Exists(options.Path))
                    throw new Exception($"Path does not exist! {options.Path}");
                if (!File.Exists(projectFile))
                    throw new Exception($"Project file does not exist! {projectFile}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return -1;
            }

            var hasModifications = false;
            XElement rootElement = null;
            try
            {
                rootElement = XElement.Load(projectFile);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load XML project file! ${projectFile}");
                Console.WriteLine($"Exception: ${ex.Message}");
                return -2;
            }
            var storageModule = rootElement.Descendants("storageModule");

            // find the project name
            var projectName = storageModule.Descendants("project").Attributes("name").First().Value;

            var debugConfiguration = storageModule.Where(x => x.Attribute("moduleId").Value == "org.eclipse.cdt.core.settings" && x.Attribute("id") == null)
                .First().Descendants("cconfiguration").Descendants("configuration").Where(x => x.Attribute("name").Value == "Debug").First();

            var sourceEntries = debugConfiguration
                .Descendants("sourceEntries").First();

            // add TouchGfx/simulator ignore
            var touchGfxEntries = sourceEntries.Descendants("entry").Where(x => x.Attribute("name").Value == "TouchGFX");
            if (touchGfxEntries.Count() > 1)
            {
                // delete extra entries, the IDE is messed up.
                var entriesToRemove = new List<XElement>();
                foreach (var entry in touchGfxEntries)
                {
                    if (entry.Attributes("excluding").Any())
                        entriesToRemove.Add(entry);
                }
                foreach (var entry in entriesToRemove)
                    entry.Remove();
                hasModifications = true;
            }
            if (!touchGfxEntries.Any())
            {
                sourceEntries.Add(new XElement("entry", new XAttribute("flags", "VALUE_WORKSPACE_PATH|RESOLVED"), new XAttribute("kind", "sourcePath"), new XAttribute("name", "TouchGFX")));
                hasModifications = true;
            }
            if (touchGfxEntries.Any() && !touchGfxEntries.Attributes("excluding").Any())
            {
                touchGfxEntries.First().Add(new XAttribute("excluding", "simulator"));
                hasModifications = true;
            }

            // add Middlewares ignores
            var halSimulatorEntries = sourceEntries.Descendants("entry").Where(x => x.Attribute("name").Value == "Middlewares");
            if (halSimulatorEntries.Count() > 1)
            {
                // delete extra entries, the IDE is messed up.
                var entriesToRemove = new List<XElement>();
                foreach (var entry in halSimulatorEntries)
                {
                    if (entry.Attributes("excluding").Any())
                        entriesToRemove.Add(entry);
                }
                foreach (var entry in entriesToRemove)
                    entry.Remove();
            }
            if (!halSimulatorEntries.Any())
            {
                sourceEntries.Add(new XElement("entry", new XAttribute("flags", "VALUE_WORKSPACE_PATH|RESOLVED"), new XAttribute("kind", "sourcePath"), new XAttribute("name", "Middlewares")));
                hasModifications = true;
            }
            if (halSimulatorEntries.Any() && !halSimulatorEntries.Attributes("excluding").Any())
            {
                halSimulatorEntries.First().Add(new XAttribute("excluding", "ST/TouchGFX/touchgfx/framework/source/platform/driver/touch/SDL2TouchController.cpp|ST/TouchGFX/touchgfx/framework/source/platform/hal/simulator"));
                hasModifications = true;
            }

            // add additional source entries
            foreach (var sourcePath in SourcePaths)
            {
                var fullSourcePath = sourcePath.Replace("{projectName}", projectName);
                if (sourceEntries.Descendants("entry").Count(x => x.Attribute("name").Value == fullSourcePath) == 0)
                {
                    sourceEntries.Add(new XElement("entry", new XAttribute("flags", "VALUE_WORKSPACE_PATH"), new XAttribute("kind", "sourcePath"), new XAttribute("name", fullSourcePath)));
                    hasModifications = true;
                }
            }

            var gppCompiler = debugConfiguration
                .Descendants("folderInfo").Descendants("tool").Where(x => x.Attribute("name").Value == "MCU G++ Compiler").First();
            var includeEntries = gppCompiler
                .Descendants("option")
                .Where(x => x.Attribute("valueType") != null && x.Attribute("valueType").Value == "includePath").First();

            // add additional includes paths
            foreach (var include in IncludePaths)
            {
                var fullIncludeName = include.Replace("{projectName}", projectName);
                if (includeEntries.Descendants("listOptionValue").Count(x => x.Attribute("value").Value == fullIncludeName) == 0)
                {
                    includeEntries.Add(new XElement("listOptionValue", new XAttribute("builtIn", "false"), new XAttribute("value", fullIncludeName)));
                    hasModifications = true;
                }
            }

            // backup project file
            Console.WriteLine($"Patching project '{projectName}':");
            if (hasModifications)
            {
                var backupFile = $"{projectFile}.{DateTime.Now.ToString("yyyyMMdd_hh_mm_ss_tt")}.backup";
                Console.WriteLine($"Backing up project to {backupFile}");
                File.Copy(projectFile, backupFile, false);

                // customize the xml serialization to be closer to java xml behavior.
                // some reordering of attributes and/or elements may happen
                var sb = new StringBuilder();
                var settings = new XmlWriterSettings();
                settings.OmitXmlDeclaration = false;
                settings.ConformanceLevel = ConformanceLevel.Document;
                settings.CheckCharacters = false;
                settings.NamespaceHandling = NamespaceHandling.Default;
                settings.Indent = true;
                settings.NewLineOnAttributes = false;
                settings.NewLineHandling = NewLineHandling.Entitize;
                settings.Encoding = new UTF8Encoding(false);
                using (var writer = XmlWriter.Create(sb, settings))
                {
                    rootElement.Save(writer);
                }

                // restore fileVersion invalid element
                var xml = sb.ToString().Replace("<cproject storage", "<?fileVersion 4.0.0?><cproject storage");
                xml = xml.Replace("<?xml version=\"1.0\" encoding=\"utf-16\"?>", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\"?>");
                File.WriteAllText(projectFile, xml);
                Console.WriteLine($"Successfully patched!");
            }
            else
            {
                Console.WriteLine("No patch required.");
            }

            Console.WriteLine();

            return 0;
        }

        private static void HandleParseErrors(IEnumerable<Error> errors)
        {
            Console.WriteLine();
        }
    }
}
