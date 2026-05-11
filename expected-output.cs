// Focused emission of AuiGrid.razor.cs
// Focus method(s): 1 overload(s) included with full body
// Other members: 6 symbols referenced, signatures only
// Containing type: AuiGrid

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevExpress.Blazor;
using HawkApp.Data;
using HawkApp.Services;
using Microsoft.AspNetCore.Components;

namespace HawkApp.Components.Grid;

public partial class AuiGrid : ComponentBase, IDisposable
{
    [Inject] private IDataService DataService { get; set; }
    [Inject] private ITranslationCache Translations { get; set; }
    [Inject] private ILogger<AuiGrid> Logger { get; set; }
    [Parameter] public string DatasetName { get; set; }
    [Parameter] public GridFilter? Filter { get; set; }
    private List<Row> _rows;
    private bool _isLoading;
    private CancellationTokenSource? _cts;

    protected override async Task OnInitializedAsync()
    {
        // Cancel any previous load — important for fast tab switches
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        _isLoading = true;
        try
        {
            var localizedDataset = Translations.Translate(DatasetName);
            Logger.LogInformation("Loading dataset {Dataset}", localizedDataset);

            _rows = await DataService.GetRowsAsync(
                localizedDataset, 
                Filter, 
                _cts.Token);
        }
        catch (OperationCanceledException)
        {
            Logger.LogInformation("Load cancelled for {Dataset}", DatasetName);
        }
        finally
        {
            _isLoading = false;
        }
    }
}
