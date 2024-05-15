using System;
using System.Runtime.InteropServices;
using System.Threading;
using CodingWithCalvin.BetterUserSecrets.Commands;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace CodingWithCalvin.BetterUserSecrets
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration(Vsix.Name, Vsix.Description, Vsix.Version)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(PackageGuids.guidPackageString)]
    public sealed class BetterUserSecretsPackage : AsyncPackage
    {
        protected override async Task InitializeAsync(
            CancellationToken cancellationToken,
            IProgress<ServiceProgressData> progress
        )
        {
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            BetterUserSecretsCommand.Initialize(this);
        }
    }
}
