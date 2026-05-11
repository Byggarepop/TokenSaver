using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevExpress.Blazor;
using HawkApp.Data;
using HawkApp.Services;
using Microsoft.AspNetCore.Components;

namespace HawkApp.Components.Grid;

/// <summary>
/// The main data grid component used across HawkApp dashboards.
/// Supports virtualization, multi-column sort, and DevExpress integration.
/// </summary>
/// <remarks>
/// Memory-leak hotspot: see #4421. Tab switches under OnDemand rendering
/// previously caused dxbl-* leaks. Fixed in v25.1.4.
/// </remarks>
public partial class AuiGrid : ComponentBase, IDisposable
{
    /// <summary>The data service used to load grid rows.</summary>
    [Inject] private IDataService DataService { get; set; } = null!;

    /// <summary>Translation cache used for column header localization.</summary>
    [Inject] private ITranslationCache Translations { get; set; } = null!;

    /// <summary>Logger.</summary>
    [Inject] private ILogger<AuiGrid> Logger { get; set; } = null!;

    /// <summary>The dataset name to fetch.</summary>
    [Parameter] public string DatasetName { get; set; } = "";

    /// <summary>Optional filter applied server-side.</summary>
    [Parameter] public GridFilter? Filter { get; set; }

    /// <summary>Fired when the user selects a row.</summary>
    [Parameter] public EventCallback<Row> OnRowSelected { get; set; }

    private List<Row> _rows = new();
    private Row? _selectedRow;
    private bool _isLoading;
    private CancellationTokenSource? _cts;

    /// <summary>
    /// Loads the grid data when the component initializes.
    /// This is the method that's been causing perf issues in production.
    /// </summary>
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

    /// <summary>Refreshes the grid by clearing and reloading.</summary>
    public async Task RefreshAsync()
    {
        _rows.Clear();
        await OnInitializedAsync();
    }

    /// <summary>Handles row click and notifies parent.</summary>
    private async Task HandleRowClick(Row row)
    {
        _selectedRow = row;
        await OnRowSelected.InvokeAsync(row);
    }

    /// <summary>Returns the currently selected row, if any.</summary>
    public Row? GetSelected() => _selectedRow;

    /// <summary>Counts the rows matching a predicate. Used by the bottom bar.</summary>
    public int CountWhere(Func<Row, bool> predicate)
    {
        return _rows.Count(predicate);
    }

    /// <summary>Disposes the cancellation token source.</summary>
    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
