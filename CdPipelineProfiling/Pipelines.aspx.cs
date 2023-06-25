using Sitecore;
using Sitecore.Configuration;
using Sitecore.Diagnostics;
using Sitecore.Diagnostics.Profiling;
using Sitecore.Globalization;
using Sitecore.sitecore.admin;
using Sitecore.Web;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;

namespace CdPipelineProfiling
{
  /// <summary>The cache page.</summary>
  public class Pipelines : AdminPage
  {
    /// <summary>The number format.</summary>
    private const string NumberFormatPattern = "{0:#,##0.00}";
    /// <summary>The extented number format pattern.</summary>
    private const string ExtentedNumberFormatPattern = "{0:#,######0.000000}";
    private readonly string[] WorstRankingHintImage = new string[3]
    {
      "font_char49_red_16.png",
      "font_char50_orange_16.png",
      "font_char51_yellow_16.png"
    };
    /// <summary>The btn reset.</summary>
    protected Button reset;
    /// <summary>phRenderings</summary>
    protected PlaceHolder resultTable;
    /// <summary>The ph actions.</summary>
    protected PlaceHolder actions;
    /// <summary>The ph legend.</summary>
    protected PlaceHolder legend;
    /// <summary>The ph profiler disabled.</summary>
    protected PlaceHolder profilerDisabledMessage;
    /// <summary>The ph profiler no data.</summary>
    protected PlaceHolder noDataMessage;
    /// <summary>Placeholder hosting template for qtip table.</summary>
    protected PlaceHolder qtip;
    /// <summary>The ref culture info.</summary>
    private readonly CultureInfo refCultureInfo = new CultureInfo("en-US");
    /// <summary>The dataSource.</summary>
    protected KeyValuePair<string, PipelinePerformanceInformation>[] DataSource;
    /// <summary>"(empty)" string</summary>
    protected string Empty = Translate.Text("(empty)");
    /// <summary>Whether CPU time should be displayed</summary>
    private bool displayCpuTime = Settings.Pipelines.Profiling.MeasureCpuTime;

    /// <summary>Current Processor used when rendering a qtip.</summary>
    protected Pipelines.ProcessorViewModel CurrentProcessor { get; set; }

    /// <summary>Current Pipeline used when rendering a qtip.</summary>
    protected Pipelines.PipelineViewModel CurrentPipeline { get; set; }

    /// <summary>Whether CPU information should be displayed.</summary>
    protected bool DisplayCpuTime => this.displayCpuTime;

    /// <summary>The dataSource bind.</summary>
    public override void DataBind()
    {
      this.DataSource = ((IEnumerable<KeyValuePair<string, PipelinePerformanceInformation>>)ProfilerApi.GetPipelineProfilingSnapshot()).OrderBy<KeyValuePair<string, PipelinePerformanceInformation>, double>((Func<KeyValuePair<string, PipelinePerformanceInformation>, double>)(pair => -pair.Value.PerformanceData.WallTime)).ToArray<KeyValuePair<string, PipelinePerformanceInformation>>();
      base.DataBind();
    }

    /// <summary>
    /// Raises the <see cref="E:System.Web.UI.Control.Init" /> event to initialize the page.
    /// </summary>
    /// <param name="arguments">
    /// An <see cref="T:System.EventArgs" /> that contains the event dataSource.
    /// </param>
    protected override void OnInit(EventArgs arguments)
    {
      Assert.ArgumentNotNull((object)arguments, nameof(arguments));
      base.OnInit(arguments);
      // Comment out this method to allow run when anonymous
      //this.CheckSecurity(true);
      this.reset.Click += (EventHandler)((e, s) =>
      {
        ProfilerApi.ResetCounters();
        this.DataBind();
      });
      this.DataBind();
    }

    /// <summary>OnPreRender</summary>
    /// <param name="e">The e.</param>
    protected override void OnPreRender(EventArgs e)
    {
      this.noDataMessage.Visible = false;
      this.resultTable.Visible = true;
      if (!ProfilerApi.IsProfilingEnabled)
      {
        this.actions.Visible = false;
        this.legend.Visible = false;
        this.profilerDisabledMessage.Visible = true;
        this.resultTable.Visible = false;
      }
      else if (this.DataSource == null || this.DataSource.Length == 0)
      {
        this.noDataMessage.Visible = true;
        this.resultTable.Visible = false;
      }
      else
        base.OnPreRender(e);
    }

    /// <summary>Formats a floating point number</summary>
    /// <param name="value"></param>
    /// <returns></returns>
    protected string FormatNumber(double value) => value < 0.01 ? "< 0.01" : string.Format((IFormatProvider)this.refCultureInfo, "{0:#,##0.00}", (object)value);

    /// <summary>Formats an integer.</summary>
    /// <param name="value"></param>
    /// <returns></returns>
    protected string FormatNumber(long value) => value.ToString((IFormatProvider)this.refCultureInfo);

    /// <summary>
    /// Shortens processor name to fit the width of the table.
    /// </summary>
    /// <param name="processorName"></param>
    /// <param name="width"></param>
    /// <returns></returns>
    protected string ShortenProcessorName(string processorName, int width) => processorName.Length <= width ? processorName : processorName.Substring(0, width - 3) + "...";

    /// <summary>Formats a processor name</summary>
    /// <param name="processorName"></param>
    /// <returns></returns>
    protected string FormatProcessorName(string processorName)
    {
      if (processorName.Length > 70)
        processorName = string.Join("<br/>", this.WrapText(processorName, 90, '.'));
      return processorName;
    }

    /// <summary>
    /// Formats a floating point number using extended format.
    /// </summary>
    /// <param name="maxTime"></param>
    /// <returns></returns>
    protected string FormatNumberExtended(double maxTime) => maxTime <= 0.0001 ? "< 0.0001" : string.Format((IFormatProvider)this.refCultureInfo, "{0:#,######0.000000}", (object)maxTime);

    /// <summary>Renders summary table</summary>
    /// <param name="header"></param>
    /// <param name="headerRows"></param>
    /// <param name="contentRows"></param>
    /// <returns></returns>
    protected string RenderSummaryTable(string header, string[] headerRows, string[] contentRows)
    {
      if (headerRows.Length != contentRows.Length)
        throw new ArgumentOutOfRangeException("headerRows and contentRows should be of equal length.");
      StringBuilder sb = new StringBuilder();
      sb.AppendFormat("<strong>{0}</strong><br/>", (object)header);
      HtmlTable table = HtmlUtil.CreateTable(0, 0);
      table.Attributes.Add("class", "processorSummaryTable");
      for (int index = 0; index < headerRows.Length; ++index)
        HtmlUtil.AddRow(table, new string[2]
        {
          headerRows[index],
          contentRows[index]
        }).Cells[1].Style.Add(HtmlTextWriterStyle.TextAlign, "right");
      using (StringWriter writer1 = new StringWriter(sb))
      {
        using (HtmlTextWriter writer2 = new HtmlTextWriter((TextWriter)writer1))
          table.RenderControl(writer2);
      }
      return sb.ToString().Replace("\"", "'");
    }

    /// <summary>Formats percentage.</summary>
    /// <param name="value">Fractional value to format.</param>
    /// <returns>Formatted string in configured culture.</returns>
    protected string FormatPercentage(double value)
    {
      value *= 100.0;
      if (value < 0.01)
        return "< 0.01";
      return value > 99.99 ? "> 99.99" : string.Format((IFormatProvider)this.refCultureInfo, "{0:#,##0.00}", (object)value);
    }

    /// <summary>Formats CPU cycle count.</summary>
    /// <param name="cpuCycles"></param>
    /// <returns></returns>
    protected string CpuCyclesToString(double cpuCycles)
    {
      string[] strArray = new string[8]
      {
        "k",
        "M",
        "G",
        "T",
        "P",
        "E",
        "Z",
        "Y"
      };
      double num = cpuCycles;
      int index;
      for (index = 0; num >= 1000.0 && index + 1 < strArray.Length; num /= 1000.0)
        ++index;
      return string.Format((IFormatProvider)this.refCultureInfo, "{0:#,##0.00} {1}", (object)num, (object)strArray[index]);
    }

    /// <summary>Wraps text at margin by splitting at word boundary.</summary>
    /// <param name="text"></param>
    /// <param name="maxLength"></param>
    /// <param name="separator"></param>
    /// <returns></returns>
    private string[] WrapText(string text, int maxLength, char separator)
    {
      if (text.Length == 0)
        return (string[])null;
      string[] strArray = text.Split(separator);
      List<string> stringList = new List<string>();
      string str1 = "";
      foreach (string str2 in strArray)
      {
        if (str1.Length > maxLength || str1.Length + str2.Length > maxLength)
        {
          stringList.Add(str1 + (object)separator);
          str1 = "";
        }
        str1 = str1.Length <= 0 ? str1 + str2 : str1 + (object)separator + str2;
      }
      if (str1.Length > 0)
        stringList.Add(str1);
      return stringList.ToArray();
    }

    /// <summary>Returns a data model for the view</summary>
    /// <returns></returns>
    protected IEnumerable<Pipelines.PipelineViewModel> GetPipelines()
    {
      Pipelines pipelines = this;
      foreach (KeyValuePair<string, PipelinePerformanceInformation> keyValuePair in new List<KeyValuePair<string, PipelinePerformanceInformation>>((IEnumerable<KeyValuePair<string, PipelinePerformanceInformation>>)pipelines.DataSource))
      {
        string key = keyValuePair.Key;
        PipelinePerformanceInformation performanceInformation = keyValuePair.Value;
        OperationPerformanceData performanceData = performanceInformation.PerformanceData;
        ProcessorPerformanceInformation[] processors = ((IEnumerable<ProcessorPerformanceInformation>)performanceInformation.GetAllProcessors()).ToArray<ProcessorPerformanceInformation>();
        var processorRanks = processors.OrderByDescending(processor => processor.PerformanceData.WallTime)
    .Select((processor, index) => new { Rank = index, Processor = processor })
    .ToArray();

        // ISSUE: reference to a compiler-generated method
        Pipelines.PipelineViewModel pipeline = new Pipelines.PipelineViewModel()
        {
          Name = key,
          ExecutionCount = performanceData.ExecutionCount,
          WallTime = performanceData.WallTime,
          MaxWallTime = performanceData.MaxWallTime,
          CpuTime = performanceData.CpuTime,
          Processors = processors.Select(_param1 => new Pipelines.ProcessorViewModel()
          {
            Name = _param1.ProcessorName,
            ExecutionCount = _param1.PerformanceData.ExecutionCount,
            WallTime = _param1.PerformanceData.WallTime,
            MaxWallTime = _param1.PerformanceData.MaxWallTime,
            CpuTime = _param1.PerformanceData.CpuTime,
            Rank = processors.Length == 1 ? 0 : processorRanks.First(r => r.Processor == _param1).Rank + 1
          }).ToArray()
      };
        foreach (Pipelines.ProcessorViewModel processor in pipeline.Processors)
          processor.Details = pipelines.BuildDetails2(processor, pipeline);
        yield return pipeline;
      }
    }

    private string BuildDetails2(
      Pipelines.ProcessorViewModel processor,
      Pipelines.PipelineViewModel pipeline)
    {
      this.CurrentProcessor = processor;
      this.CurrentPipeline = pipeline;
      this.qtip.Visible = true;
      StringBuilder sb = new StringBuilder();
      using (StringWriter writer1 = new StringWriter(sb))
      {
        using (HtmlTextWriter writer2 = new HtmlTextWriter((TextWriter)writer1))
          this.qtip.RenderControl(writer2);
      }
      this.qtip.Visible = false;
      this.CurrentProcessor = (Pipelines.ProcessorViewModel)null;
      this.CurrentPipeline = (Pipelines.PipelineViewModel)null;
      return sb.Replace('"', '\'').ToString();
    }

    /// <summary>Builds processor name</summary>
    /// <param name="processorName"></param>
    /// <param name="methodName"></param>
    /// <returns></returns>
    private string BuildProcessorName(string processorName, string methodName)
    {
      string text = processorName;
      if (!string.IsNullOrEmpty(text))
      {
        int length = text.IndexOf(',');
        if (length > 0)
          text = StringUtil.Left(text, length).Trim();
      }
      if (string.IsNullOrEmpty(text))
        text = string.Empty;
      if (!string.IsNullOrEmpty(methodName))
        text = text + "." + methodName;
      return text;
    }

    /// <summary>Describes a single entry of a legend.</summary>
    protected class ColumnDescription
    {
      /// <summary>Header of the column</summary>
      public string Header { get; set; }

      /// <summary>Description of the column</summary>
      public string Description { get; set; }
    }

    /// <summary>View data model for a pipeline.</summary>
    protected class PipelineViewModel
    {
      /// <summary>Name of the pipeline</summary>
      public string Name { get; set; }

      /// <summary>Execution count</summary>
      public long ExecutionCount { get; set; }

      /// <summary>Total Wall time</summary>
      public double WallTime { get; set; }

      /// <summary>Max wall time in call</summary>
      public double MaxWallTime { get; set; }

      /// <summary>Total CPU usage</summary>
      public double CpuTime { get; set; }

      /// <summary>Processors in the pipeline</summary>
      public IEnumerable<Pipelines.ProcessorViewModel> Processors { get; set; }
    }

    /// <summary>View model for a processor</summary>
    protected class ProcessorViewModel
    {
      /// <summary>Name of the processor</summary>
      public string Name { get; set; }

      /// <summary>Rank of the processor</summary>
      public int Rank { get; set; }

      /// <summary>Execution count</summary>
      public long ExecutionCount { get; set; }

      /// <summary>total wall time</summary>
      public double WallTime { get; set; }

      /// <summary>Max wall time in call</summary>
      public double MaxWallTime { get; set; }

      /// <summary>Total CPU usage.</summary>
      public double CpuTime { get; set; }

      /// <summary>Details</summary>
      public string Details { get; set; }
    }

    /// <summary>Describes table columns</summary>
    protected static class Columns
    {
      /// <summary>Name column</summary>
      public static Pipelines.ColumnDescription Name => new Pipelines.ColumnDescription()
      {
        Header = "Pipeline / Processor Name",
        Description = "Name of the pipeline or logical name of the processor (or name of the implementing type and method)"
      };

      /// <summary>Execution Count</summary>
      public static Pipelines.ColumnDescription ExecutionCount => new Pipelines.ColumnDescription()
      {
        Header = "#Executions",
        Description = "The number of executions of the pipeline or processor"
      };

      /// <summary>% Wall Time</summary>
      public static Pipelines.ColumnDescription WallTimePercent => new Pipelines.ColumnDescription()
      {
        Header = "% Wall Time",
        Description = "For a processor, percentage of total pipeline execution time that is spent executing this processor"
      };

      /// <summary>Wall Time</summary>
      public static Pipelines.ColumnDescription WallTime => new Pipelines.ColumnDescription()
      {
        Header = "Wall Time",
        Description = "Total wall time spent in all executions of the pipeline or processor"
      };

      /// <summary>Max Wall Time in Call</summary>
      public static Pipelines.ColumnDescription MaxWallTime => new Pipelines.ColumnDescription()
      {
        Header = "Max Wall Time",
        Description = "Wall Time of the longest execution of a pipeline or processor"
      };

      /// <summary>% Cpu Time</summary>
      public static Pipelines.ColumnDescription CpuPercent => new Pipelines.ColumnDescription()
      {
        Header = "% CPU",
        Description = "For a processor, percentage of total pipeline CPU usage that is spent executing this processor"
      };

      /// <summary>Wall Time per Call</summary>
      public static Pipelines.ColumnDescription TimePerCall => new Pipelines.ColumnDescription()
      {
        Header = "Time / Execution",
        Description = "Average time taken by a single execution of the pipeline or processor (wall time)"
      };

      /// <summary>All columns</summary>
      public static IEnumerable<Pipelines.ColumnDescription> AllColumns
      {
        get
        {
          yield return Pipelines.Columns.ExecutionCount;
          yield return Pipelines.Columns.WallTime;
          yield return Pipelines.Columns.WallTimePercent;
          yield return Pipelines.Columns.MaxWallTime;
          yield return Pipelines.Columns.CpuPercent;
          yield return Pipelines.Columns.TimePerCall;
        }
      }
    }
  }
}
