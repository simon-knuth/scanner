using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Scanner.Messages;
using Scanner.Models;
using Scanner.Services.Interfaces;
using System;
using System.Threading.Tasks;

namespace Scanner.ViewModels;

partial class TemplatesViewModel : ObservableRecipient, IDisposable
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    #region Services
    public readonly ITemplatesService TemplateService = Ioc.Default.GetRequiredService<ITemplatesService>();
    #endregion

    #region Commands
    public RelayCommand<TemplateEntry> TryApplyTemplateCommand => new(TryApplyTemplate);
    public AsyncRelayCommand CreateTemplateAsyncCommand;
    public AsyncRelayCommand<TemplateEntry> RemoveTemplateAsyncCommand;
    public AsyncRelayCommand ClearListAsyncCommand;
    public RelayCommand<TemplateEntry> StartRenamingCommand => new(StartRenaming);
    public RelayCommand DisposeCommand => new RelayCommand(Dispose);
    #endregion

    [ObservableProperty]
    private bool isScannerSelected;


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public TemplatesViewModel()
    {
        CreateTemplateAsyncCommand = new(CreateTemplateAsync);
        RemoveTemplateAsyncCommand = new(RemoveTemplateAsync);
        ClearListAsyncCommand = new(ClearListAsync);

        Messenger.Register<SelectedScannerChangedMessage>(this, (r, m) =>
        {
            IsScannerSelected = m.SelectedScanner != null;
        });
        IsScannerSelected = Messenger.Send(new SelectedScannerRequestMessage()).Response != null;
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public void Dispose()
    {
        Messenger.UnregisterAll(this);
    }

    public void TryApplyTemplate(TemplateEntry template)
    {
        Messenger.Send(new ApplyTemplateMessage(template));
    }

    public async Task CreateTemplateAsync()
    {
        await TemplateService.AddTemplateAsync(Resources.Strings.Resources.Template, Messenger.Send(new ScanOptionsRequestMessage()).Response);
    }

    public async Task RemoveTemplateAsync(TemplateEntry template)
    {
        await TemplateService.RemoveTemplateAsync(template);
    }

    public async Task ClearListAsync()
    {
        await TemplateService.ClearTemplatesAsync();
    }

    public void StartRenaming(TemplateEntry template)
    {
        template.IsRenaming = true;
    }

    public async Task StopRenamingAsync(TemplateEntry template, string name)
    {
        template.IsRenaming = false;
        template.Name = name;
        await TemplateService.RenameTemplateAsync(template, name);
    }
}
