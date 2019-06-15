using System;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using EnvDTE;
using EnvDTE80;
using Microsoft;
using Microsoft.VisualStudio.Shell;

namespace BetterUserSecrets.Commands
{
    internal sealed class BetterUserSecretsCommand
    {
        private readonly Package _package;
        
        public static BetterUserSecretsCommand Instance { get; private set; }
        private IServiceProvider ServiceProvider => this._package;

        private BetterUserSecretsCommand(Package package)
        {
            this._package = package;

            var commandService = (OleMenuCommandService)ServiceProvider.GetService(typeof(IMenuCommandService));

            if (commandService == null)
            {
                return;
            }

            var menuCommandId = new CommandID(PackageGuids.guidBUSCmdSet, PackageIds.BUSId);
            var menuItem = new MenuCommand(GetOrCreateSecretsFile, menuCommandId);
            commandService.AddCommand(menuItem);
        }

        public static void Initialize(Package package)
        {
            Instance = new BetterUserSecretsCommand(package);
        }

        private void GetOrCreateSecretsFile(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                var service = (DTE2)this.ServiceProvider.GetService(typeof(DTE));
                Assumes.Present(service);

                var userSecretsId = GetOrUpdateProject(service);

                var secretsFile = GetOrCreateSecretsFile(userSecretsId, service);
                
                service.ItemOperations.OpenFile(secretsFile);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private static string GetOrUpdateProject(DTE2 service)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var selectedItem = (Array)service.ToolWindows.SolutionExplorer.SelectedItems;
            var uiItem = selectedItem.GetValue(0) as UIHierarchyItem;

            if (!(uiItem?.Object is Project project))
            {
                throw new ArgumentException("Selected Item is not Project File");
            }

            //let's see if we can find a pre-existing node for UserSecretsId
            XDocument projectXml;
            using (var xmlReader = XmlReader.Create(project.FullName))
            {
                projectXml = XDocument.Load(xmlReader, LoadOptions.SetLineInfo);
            }

            var userSecretsNode = projectXml.XPathSelectElement("//UserSecretsId");
                
            if (userSecretsNode != null)
            {
                return userSecretsNode.Value;
            }

            var targetFrameworkNode = projectXml.XPathSelectElement("//TargetFramework");

            if (targetFrameworkNode == null)
            {
                throw new ApplicationException("Unable to locate the <TargetFramework> node in the project file!");
            }

            var linePosition = ((IXmlLineInfo) targetFrameworkNode).LinePosition;

            var userSecretsId = Guid.NewGuid().ToString();
            
            userSecretsNode = new XElement("UserSecretsId", userSecretsId);
            targetFrameworkNode.AddAfterSelf(userSecretsNode);
            targetFrameworkNode.AddAfterSelf(new XText(new string(' ', linePosition - 2)));
            targetFrameworkNode.AddAfterSelf(Environment.NewLine);
            
            var xmlSettings = new XmlWriterSettings
            {
                OmitXmlDeclaration = true
            };

            using (var xmlWriter = XmlWriter.Create(project.FullName, xmlSettings))
            {
                projectXml.Save(xmlWriter);
            }
            
            return userSecretsId;
        }

        private static string GetOrCreateSecretsFile(string userSecretsId, DTE2 service)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.GetFullPath($"{appData}\\Microsoft\\UserSecrets\\{userSecretsId}");

            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var secretsFile = Path.Combine(dir, "secrets.json");

            if (!File.Exists(secretsFile))
            {
                File.WriteAllText(secretsFile, "{\r\n\r\n}");
            }

            return secretsFile;
        }
    }
}