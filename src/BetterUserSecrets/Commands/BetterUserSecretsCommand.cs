using System;
using System.ComponentModel.Design;
using System.IO;
using System.Windows.Forms;
using System.Xml;
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

            var projectXml = new XmlDocument();
            projectXml.Load(project.FullName);

            var frameworkNode = projectXml.SelectSingleNode("/Project/PropertyGroup/TargetFramework");
            var userSecretsId = projectXml.SelectSingleNode("/Project/PropertyGroup/UserSecretsId/text()")?.Value;
            var propGroupNode = frameworkNode?.ParentNode;

            //couldn't find the TargetFramework node which we want to put the UserSecretsId in.
            if (propGroupNode == null)
            {
                throw new ArgumentException("Unable to locate the TargetFramework element and/or its parent node.");
            }

            if (string.IsNullOrWhiteSpace(userSecretsId))
            {
                userSecretsId = CreateUserSecretsId(projectXml, propGroupNode, project);
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

        private static string CreateUserSecretsId(XmlDocument projectXml, XmlNode propGroupNode, Project project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var userSecretsId = Guid.NewGuid().ToString();

            var userSecretsElement = projectXml.CreateElement("UserSecretsId");
            userSecretsElement.InnerText = userSecretsId;
            propGroupNode.AppendChild(userSecretsElement);

            var settings = new XmlWriterSettings()
            {
                Indent = true,
                IndentChars = "  ",
                NewLineHandling = NewLineHandling.None,
                NewLineChars = Environment.NewLine,
                OmitXmlDeclaration = true
            };

            using (var writer = XmlWriter.Create(project.FullName, settings))
            {
                projectXml.Save(writer);
            }

            return userSecretsId;
        }
    }
}