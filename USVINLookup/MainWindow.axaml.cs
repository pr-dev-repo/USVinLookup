using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Net.Http;
using System.Net.Http.Json;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using System.Text.Json.Serialization;

namespace USVINLookup;

/// <summary>
/// Author: Gabriel A. Rodriguez
/// Date: 6/23/2025
/// Purpose: Simple tool to decode US VIN numbers
/// </summary>
public partial class MainWindow : Window
{

    #region CTOR

    private TextBox vinInput;
    private Button searchButton;
    private Button imageSearchButton;  // New button for opening Google Images
    private TextBlock resultBlock;
    private string? decodedMake;
    private string? decodedModel;
    private string? decodedYear;
    private readonly string apiUrlTemplate;

    public MainWindow()
    {
        InitializeComponent();

        // Load config
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        apiUrlTemplate = config["VinDecoder:ApiUrlTemplate"] ?? string.Empty;

        // Usual control lookups
        vinInput = this.FindControl<TextBox>("VinInput");
        searchButton = this.FindControl<Button>("SearchButton");
        imageSearchButton = this.FindControl<Button>("ImageSearchButton");
        resultBlock = this.FindControl<TextBlock>("ResultBlock");

        searchButton.Click += SearchButton_Click;
        imageSearchButton.Click += ImageSearchButton_Click;
        ImageSearchButton.IsVisible = false;
    }
    #endregion

    /// <summary>
    /// Event launched on search button click
    /// </summary>
    /// <param name="sender">button metadata</param>
    /// <param name="e">Provides state information and data specified to routed event.</param>
    private async void SearchButton_Click(object? sender, RoutedEventArgs e)
    {
        resultBlock.Text = string.Empty;
        ImageSearchButton.IsVisible = false;

        var vin = vinInput.Text?.Trim().ToUpper();
        if (string.IsNullOrEmpty(vin) || vin.Length != 17)
        {
            resultBlock.Text = "Invalid VIN. Must be 17 characters.";
            ImageSearchButton.IsVisible = false; // Hide when invalid
            return;
        }

        string url = apiUrlTemplate.Replace("{vin}", vin);

        try
        {
            using var http = new HttpClient();
            var response = await http.GetFromJsonAsync(url, NhtsaResponseJsonContext.Default.NhtsaResponse);
            var data = response?.Results?.FirstOrDefault();
            if (data is null || string.IsNullOrEmpty(data.Make))
            {
                resultBlock.Text = "No results found.";
                ImageSearchButton.IsVisible = false; // Hide if no results
                return;
            }

            // Clear previous inlines
            resultBlock?.Inlines?.Clear();

            void AddLine(string label, string? value)
            {
                resultBlock.Inlines.Add(new Run(label) { FontWeight = FontWeight.Normal });
                resultBlock.Inlines.Add(new Run(value ?? "N/A") { FontWeight = FontWeight.Bold });
                resultBlock.Inlines.Add(new Run("\n"));
            }

            AddLine("Make: ", data.Make);
            AddLine("Model: ", data.Model);
            AddLine("Year: ", data.ModelYear);
            AddLine("Body: ", data.BodyClass);
            AddLine("Fuel: ", data.FuelTypePrimary);
            AddLine("Drive: ", data.DriveType);
            AddLine("Engine: ", $"{data.EngineModel} ({data.EngineCylinders} cyl, {data.EngineHP} HP)");

            // Handle DisplacementCC safely
            string displacementFormatted = "N/A";
            if (int.TryParse(data.DisplacementCC, out int displacementCc))
            {
                displacementFormatted = $"{Math.Round(displacementCc / 1000.0, 1)}L";
            }
            AddLine("Displacement: ", displacementFormatted);

            AddLine("Transmission: ", data.TransmissionStyle);
            AddLine("Suggested VIN: ", data.SuggestedVIN);

            decodedMake = data.Make;
            decodedModel = data.Model;
            decodedYear = data.ModelYear;

            ImageSearchButton.IsVisible = true; // Show button when results exist
        }
        catch (Exception ex)
        {
            resultBlock.Text = $"Error: {ex.Message}";
            ImageSearchButton.IsVisible = false; // Hide on error
        }
    }

    /// <summary>
    /// Event launched on image search button
    /// </summary>
    /// <param name="sender">button metadata</param>
    /// <param name="e">Provides state information and data specified to routed event.</param>
    private void ImageSearchButton_Click(object? sender, RoutedEventArgs e)
    {
        OpenGoogleImageSearch();
    }

    /// <summary>
    /// Opens browser a browser instance/tab with vehicle from results; if any
    /// </summary>
    private void OpenGoogleImageSearch()
    {
        if (string.IsNullOrWhiteSpace(decodedMake) ||
            string.IsNullOrWhiteSpace(decodedModel) ||
            string.IsNullOrWhiteSpace(decodedYear))
        {
            resultBlock.Text += "\n⚠️ Missing vehicle data for image search.";
            return;
        }

        string query = $"{decodedYear} {decodedMake} {decodedModel}";
        string searchUrl = "https://www.google.com/search?tbm=isch&q=" + Uri.EscapeDataString(query);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = searchUrl,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            resultBlock.Text += $"\n⚠️ Failed to open browser: {ex.Message}";
        }
    }

}

#region DTOs

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(NhtsaResponse))]
public partial class NhtsaResponseJsonContext : JsonSerializerContext
{
}

// DTO classes unchanged
public class NhtsaResponse
{
    public int Count { get; set; }
    public string? Message { get; set; }
    public string? SearchCriteria { get; set; }
    public List<NhtsaResult>? Results { get; set; }
}

public class NhtsaResult
{
    public string? VIN { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public string? ModelYear { get; set; }
    public string? BodyClass { get; set; }
    public string? FuelTypePrimary { get; set; }
    public string? DriveType { get; set; }
    public string? EngineModel { get; set; }
    public string? EngineCylinders { get; set; }
    public string? EngineHP { get; set; }
    public string? DisplacementCC { get; set; }
    public string? SuggestedVIN { get; set; }
    public string? TransmissionStyle { get; set; }
}
#endregion
